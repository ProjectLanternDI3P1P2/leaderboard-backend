using System.Text;
using System.Threading.Channels;
using Combat.Contracts.Events.V1;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace Leaderboard.Infrastructure.Messaging;

/// <summary>
/// Stores every received message in the raw event store, then acknowledges it.
/// A message is acknowledged only once the write is confirmed, so a crash can
/// redeliver (at least once) but never lose an event. Unreadable messages and
/// batches that exhaust their retries are rejected to the dead-letter queue.
/// </summary>
public sealed class RabbitMqRawEventConsumer(
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<RawEventConsumerOptions> consumerOptions,
    IClock clock,
    RawEventBatchWriter writer,
    ILogger<RabbitMqRawEventConsumer> logger
) : BackgroundService
{
    private sealed record Delivery(
        ulong Tag,
        byte[] Body,
        string? TransactionId,
        DateTimeOffset ReceivedAt
    );

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

        await channel.ExchangeDeclareAsync(
            broker.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
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
        await channel.QueueBindAsync(
            settings.QueueName,
            broker.ExchangeName,
            settings.BindingKey,
            cancellationToken: cancellationToken
        );
        await channel.BasicQosAsync(0, (ushort)settings.BatchSize, false, cancellationToken);

        Channel<Delivery> deliveries = Channel.CreateUnbounded<Delivery>();
        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            // receivedAt is set here, on receipt, before any batching.
            await deliveries.Writer.WriteAsync(
                new Delivery(
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

        while (await deliveries.Reader.WaitToReadAsync(cancellationToken))
        {
            List<Delivery> batch = await ReadBatchAsync(
                deliveries.Reader,
                settings,
                cancellationToken
            );
            await ProcessBatchAsync(channel, batch, cancellationToken);
        }
    }

    private static async Task<List<Delivery>> ReadBatchAsync(
        ChannelReader<Delivery> reader,
        RawEventConsumerOptions settings,
        CancellationToken cancellationToken
    )
    {
        List<Delivery> batch = [];
        using CancellationTokenSource window = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        window.CancelAfter(settings.BatchTimeout);

        try
        {
            while (batch.Count < settings.BatchSize && await reader.WaitToReadAsync(window.Token))
            {
                while (batch.Count < settings.BatchSize && reader.TryRead(out Delivery? delivery))
                {
                    batch.Add(delivery);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The batch window elapsed: write what we have.
        }

        return batch;
    }

    private async Task ProcessBatchAsync(
        IChannel channel,
        List<Delivery> batch,
        CancellationToken cancellationToken
    )
    {
        List<(Delivery Delivery, RawEvent Event)> valid = [];
        foreach (Delivery delivery in batch)
        {
            if (TryMap(delivery, out RawEvent? rawEvent))
            {
                valid.Add((delivery, rawEvent));
                continue;
            }

            logger.LogError("Unreadable message {DeliveryTag}, dead-lettering", delivery.Tag);
            await channel.BasicNackAsync(delivery.Tag, false, false, cancellationToken);
        }

        if (valid.Count == 0)
        {
            return;
        }

        bool stored = await writer.TryWriteAsync(
            valid.Select(item => item.Event).ToList(),
            cancellationToken
        );

        foreach ((Delivery delivery, RawEvent rawEvent) in valid)
        {
            using IDisposable correlation = LogContext.PushProperty(
                "CorrelationId",
                rawEvent.CorrelationId
            );
            using IDisposable message = LogContext.PushProperty("MessageId", rawEvent.MessageId);

            if (stored)
            {
                await channel.BasicAckAsync(delivery.Tag, false, cancellationToken);
                logger.LogDebug("Stored raw event {Type}", rawEvent.Type);
            }
            else
            {
                logger.LogError(
                    "Retries exhausted for raw event {Type}, dead-lettering",
                    rawEvent.Type
                );
                await channel.BasicNackAsync(delivery.Tag, false, false, cancellationToken);
            }
        }
    }

    private static bool TryMap(Delivery delivery, out RawEvent rawEvent)
    {
        rawEvent = null!;
        try
        {
            MessageEnvelopeMessage envelope = MessageEnvelopeMessage.Parser.ParseFrom(
                delivery.Body
            );
            rawEvent = new RawEvent
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.Parse(envelope.MessageId),
                TransactionId = delivery.TransactionId,
                Type = envelope.Type,
                Version = envelope.Version,
                CorrelationId = envelope.CorrelationId,
                CausationId = string.IsNullOrEmpty(envelope.CausationId)
                    ? null
                    : envelope.CausationId,
                OccurredAtUtc = DateTimeOffset.Parse(
                    envelope.OccurredAtUtc,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind
                ),
                ReceivedAt = delivery.ReceivedAt,
                Producer = envelope.Producer,
                Payload = envelope.Payload.ToByteArray(),
            };
            return true;
        }
        catch (Exception exception)
            when (exception is FormatException or Google.Protobuf.InvalidProtocolBufferException)
        {
            return false;
        }
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
