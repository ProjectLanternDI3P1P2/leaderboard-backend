using Google.Protobuf;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Collection;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;

namespace Leaderboard.Application.Collection;

public sealed record DeliveryMetadata(
    string Exchange,
    string RoutingKey,
    string? MessageId,
    string? CorrelationId,
    bool Redelivered
);

public sealed class ArchiveEvent(IRawEventRepository repository, IClock clock)
{
    public async Task HandleAsync(
        ReadOnlyMemory<byte> body,
        DeliveryMetadata metadata,
        Func<CancellationToken, Task> acknowledge,
        CancellationToken cancellationToken
    )
    {
        // RabbitMQ owns the callback buffer. Copy before the first await and never reserialize it.
        byte[] original = body.ToArray();
        var receivedAt = clock.UtcNow.ToUniversalTime();
        GameEventEnvelope? envelope = null;
        string status;
        string[] errors;
        try
        {
            envelope = GameEventEnvelope.Parser.ParseFrom(original);
            errors = GameEventContract.Validate(envelope).ToArray();
            if (metadata.RoutingKey != $"{envelope.EventType}.v{envelope.EventVersion}")
                errors = [.. errors, "AMQP routing key does not match the envelope."];
            status = errors.Length == 0 ? "Valid" : "InvalidOrUnknown";
        }
        catch (InvalidProtocolBufferException)
        {
            status = "Unreadable";
            errors = ["Body cannot be parsed as GameEventEnvelope."];
        }

        var receipt = new RawEventReceipt(
            Guid.NewGuid().ToString("D"),
            receivedAt,
            original,
            envelope?.TransactionId,
            envelope?.ProducerSquad.ToString(),
            envelope?.EventType,
            status,
            errors,
            metadata.Exchange,
            metadata.RoutingKey,
            metadata.MessageId,
            metadata.CorrelationId,
            metadata.Redelivered
        );
        await repository.AddAsync(receipt, cancellationToken);
        await acknowledge(cancellationToken);
    }
}
