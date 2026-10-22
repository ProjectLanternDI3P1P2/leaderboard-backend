using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Channels;
using Leaderboard.Domain.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Leaderboard.Infrastructure.Messaging;

/// <summary>
/// Broker plumbing only: the logic lives in <see cref="RawEventBatchProcessor"/>, which is tested.
/// Stores every received message in the raw event store, then acknowledges it.
/// A message is acknowledged only once the write is confirmed, so a crash can
/// redeliver (at least once) but never lose an event. Unreadable messages and
/// batches that exhaust their retries are rejected to the dead-letter queue.
/// </summary>
[ExcludeFromCodeCoverage(
    Justification = "Broker plumbing; the logic is covered through RawEventBatchProcessor."
)]
public sealed class RabbitMqRawEventConsumer(
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<RawEventConsumerOptions> consumerOptions,
    IClock clock,
    RawEventBatchProcessor processor,
    ILogger<RabbitMqRawEventConsumer> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Raw event consumer stopped, reconnecting");
                await Task.Delay(consumerOptions.Value.ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        RabbitMqOptions broker = rabbitMqOptions.Value;
        RawEventConsumerOptions settings = consumerOptions.Value;
        ConnectionFactory factory = new()
        {
            HostName = broker.HostName,
            Port = broker.Port,
            VirtualHost = broker.VirtualHost,
            UserName = broker.UserName,
            Password = broker.Password,
        };

        await using IConnection connection = await factory.CreateConnectionAsync(cancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken
        );

        await channel.QueueDeclareAsync(
            settings.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken
        );
        await channel.QueueDeclareAsync(
            settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = settings.DeadLetterQueueName,
            },
            cancellationToken: cancellationToken
        );
        IEnumerable<string> sourceExchanges =
            settings.SourceExchanges.Count > 0 ? settings.SourceExchanges : [broker.ExchangeName];
        foreach (string exchange in sourceExchanges)
        {
            await channel.ExchangeDeclareAsync(
                exchange,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken
            );
            await channel.QueueBindAsync(
                settings.QueueName,
                exchange,
                settings.BindingKey,
                cancellationToken: cancellationToken
            );
        }
        await channel.BasicQosAsync(0, (ushort)settings.BatchSize, false, cancellationToken);

        Channel<RawEventDelivery> deliveries = Channel.CreateUnbounded<RawEventDelivery>();
        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            // receivedAt is set here, on receipt, before any batching.
            await deliveries.Writer.WriteAsync(
                new RawEventDelivery(
                    args.DeliveryTag,
                    args.Body.ToArray(),
                    ReadTransactionId(args.BasicProperties),
                    clock.UtcNow
                ),
                cancellationToken
            );
        };
        await channel.BasicConsumeAsync(
            settings.QueueName,
            autoAck: false,
            consumer,
            cancellationToken
        );

        var acknowledger = new ChannelAcknowledger(channel);
        while (await deliveries.Reader.WaitToReadAsync(cancellationToken))
        {
            List<RawEventDelivery> batch = await processor.ReadBatchAsync(
                deliveries.Reader,
                cancellationToken
            );
            await processor.ProcessAsync(batch, acknowledger, cancellationToken);
        }
    }

    private sealed class ChannelAcknowledger(IChannel channel) : IDeliveryAcknowledger
    {
        public async Task AckAsync(ulong tag, CancellationToken cancellationToken) =>
            await channel.BasicAckAsync(tag, false, cancellationToken);

        public async Task RejectAsync(ulong tag, CancellationToken cancellationToken) =>
            await channel.BasicNackAsync(tag, false, false, cancellationToken);
    }

    private static string? ReadTransactionId(IReadOnlyBasicProperties properties) =>
        properties.Headers?.TryGetValue("transaction-id", out object? value) == true
            ? value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string text => text,
                _ => null,
            }
            : null;
}
