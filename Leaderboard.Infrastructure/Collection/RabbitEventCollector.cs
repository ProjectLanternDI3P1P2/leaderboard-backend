using Leaderboard.Application.Collection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Leaderboard.Infrastructure.Collection;

public sealed class RabbitEventCollector(
    IOptions<RabbitCollectionOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitEventCollector> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope
                    .ServiceProvider.GetRequiredService<MongoRawEventRepository>()
                    .InitializeAsync(stoppingToken);
                await using var connection = await config
                    .CreateConnectionFactory()
                    .CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken
                );
                await DeclareTopologyAsync(channel, config, stoppingToken);
                await channel.BasicQosAsync(0, config.PrefetchCount, false, stoppingToken);

                var disconnected = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                channel.ChannelShutdownAsync += (_, _) =>
                {
                    disconnected.TrySetResult();
                    return Task.CompletedTask;
                };
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.UnregisteredAsync += (_, _) =>
                {
                    disconnected.TrySetResult();
                    return Task.CompletedTask;
                };
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try
                    {
                        using var deliveryScope = scopeFactory.CreateScope();
                        await deliveryScope
                            .ServiceProvider.GetRequiredService<ArchiveEvent>()
                            .HandleAsync(
                                delivery.Body,
                                new DeliveryMetadata(
                                    delivery.Exchange,
                                    delivery.RoutingKey,
                                    delivery.BasicProperties.MessageId,
                                    delivery.BasicProperties.CorrelationId,
                                    delivery.Redelivered
                                ),
                                async ct =>
                                    await channel.BasicAckAsync(delivery.DeliveryTag, false, ct),
                                stoppingToken
                            );
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Closing the channel returns all unacknowledged messages to the broker.
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(
                            exception,
                            "Archival/ACK failed for {MessageId}; retaining broker delivery. After {Limit} redeliveries the quorum queue transfers it to durable quarantine {Queue}.",
                            delivery.BasicProperties.MessageId,
                            config.MaxDeliveryAttempts,
                            config.DeadLetterQueueName
                        );
                        try
                        {
                            // Spaced retries prevent a poison message or storage outage from spinning.
                            await Task.Delay(
                                TimeSpan.FromSeconds(config.RetryDelaySeconds),
                                stoppingToken
                            );
                            // basic.reject counts this processing failure toward quorum delivery-limit;
                            // basic.nack requeue would permit poison messages to retry forever on RabbitMQ 4.3+.
                            await channel.BasicRejectAsync(
                                delivery.DeliveryTag,
                                true,
                                stoppingToken
                            );
                        }
                        catch (Exception retryException)
                        {
                            logger.LogWarning(
                                retryException,
                                "Requeue unconfirmed; reconnecting the collector channel."
                            );
                            disconnected.TrySetResult();
                        }
                    }
                };
                var consumerTag = await channel.BasicConsumeAsync(
                    config.QueueName,
                    false,
                    consumer,
                    stoppingToken
                );
                logger.LogInformation(
                    "Collecting {Queue} with {Prefetch} pending deliveries; manual ACK follows journaled MongoDB insert.",
                    config.QueueName,
                    config.PrefetchCount
                );
                try
                {
                    await disconnected.Task.WaitAsync(stoppingToken);
                }
                finally
                {
                    if (channel.IsOpen)
                        await channel.CloseAsync(CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Collector disconnected; reconnecting in {Seconds}s.",
                    config.ReconnectDelaySeconds
                );
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(config.ReconnectDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public static async Task DeclareTopologyAsync(
        IChannel channel,
        RabbitCollectionOptions config,
        CancellationToken ct
    )
    {
        await channel.ExchangeDeclareAsync(
            config.DeadLetterExchangeName,
            ExchangeType.Direct,
            true,
            false,
            cancellationToken: ct
        );
        await channel.QueueDeclareAsync(
            config.DeadLetterQueueName,
            true,
            false,
            false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: ct
        );
        await channel.QueueBindAsync(
            config.DeadLetterQueueName,
            config.DeadLetterExchangeName,
            "quarantine",
            cancellationToken: ct
        );
        await channel.QueueDeclareAsync(
            config.QueueName,
            true,
            false,
            false,
            new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = config.MaxDeliveryAttempts,
                ["x-dead-letter-exchange"] = config.DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = "quarantine",
                // At-least-once dead lettering retains the original until the DLQ confirms it.
                ["x-dead-letter-strategy"] = "at-least-once",
                ["x-overflow"] = "reject-publish",
            },
            cancellationToken: ct
        );
        foreach (var subscription in config.Subscriptions)
        {
            await channel.ExchangeDeclareAsync(
                subscription.ExchangeName,
                ExchangeType.Topic,
                true,
                false,
                cancellationToken: ct
            );
            await channel.QueueBindAsync(
                config.QueueName,
                subscription.ExchangeName,
                subscription.RoutingKey,
                cancellationToken: ct
            );
        }
    }
}
