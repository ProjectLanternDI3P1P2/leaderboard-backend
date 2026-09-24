using Leaderboard.Application.Messaging;
using Combat.Contracts.Events.V1;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Leaderboard.Infrastructure.Messaging;

/// <summary>RabbitMQ adapter for the broker-independent message publisher seam.</summary>
public sealed class RabbitMqMessagePublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqMessagePublisher> logger) : IMessagePublisher
{
    public async Task PublishAsync(MessageEnvelope message, CancellationToken cancellationToken)
    {
        RabbitMqOptions settings = options.Value;
        ConnectionFactory factory = new()
        {
            HostName = settings.HostName,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
            UserName = settings.UserName,
            Password = settings.Password
        };

        await using IConnection connection = await factory.CreateConnectionAsync(cancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(settings.ExchangeName, ExchangeType.Topic, durable: true,
            autoDelete: false, cancellationToken: cancellationToken);

        MessageEnvelopeMessage envelope = new()
        {
            MessageId = message.MessageId.ToString(),
            CorrelationId = message.CorrelationId,
            CausationId = message.CausationId ?? string.Empty,
            Type = message.Type,
            Version = message.Version,
            OccurredAtUtc = message.OccurredAtUtc.ToString("O"),
            Producer = message.Producer,
            Payload = ByteString.CopyFrom(message.Payload.Span)
        };

        await channel.BasicPublishAsync(settings.ExchangeName, $"{message.Type}.v{message.Version}", mandatory: true,
            body: envelope.ToByteArray(), cancellationToken: cancellationToken);

        logger.LogInformation("Published integration message {MessageType} with id {MessageId}", message.Type, message.MessageId);
    }
}
