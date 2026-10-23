using System.Threading.Channels;
using Google.Protobuf;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace Leaderboard.Infrastructure.Messaging;

/// <summary>A message as received from the broker, stamped on receipt.</summary>
public sealed record RawEventDelivery(
    ulong Tag,
    byte[] Body,
    DateTimeOffset ReceivedAt,
    string? Exchange = null,
    string? RoutingKey = null,
    string? BrokerMessageId = null,
    bool Redelivered = false
);

/// <summary>The broker operations needed to settle a delivery.</summary>
public interface IDeliveryAcknowledger
{
    Task AckAsync(ulong tag, CancellationToken cancellationToken);

    /// <summary>Rejects without requeue, so the broker dead-letters the message.</summary>
    Task RejectAsync(ulong tag, CancellationToken cancellationToken);
}

/// <summary>
/// Turns a batch of deliveries into stored events and settles each one: ack once
/// the write is confirmed, reject (dead-letter) when storage retries are exhausted.
/// </summary>
public sealed class RawEventBatchProcessor(
    RawEventBatchWriter writer,
    IOptions<RawEventConsumerOptions> options,
    ILogger<RawEventBatchProcessor> logger
)
{
    public async Task<List<RawEventDelivery>> ReadBatchAsync(
        ChannelReader<RawEventDelivery> reader,
        CancellationToken cancellationToken
    )
    {
        RawEventConsumerOptions settings = options.Value;
        List<RawEventDelivery> batch = [];
        using CancellationTokenSource window = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        window.CancelAfter(settings.BatchTimeout);

        try
        {
            while (batch.Count < settings.BatchSize && await reader.WaitToReadAsync(window.Token))
            {
                while (
                    batch.Count < settings.BatchSize
                    && reader.TryRead(out RawEventDelivery? delivery)
                )
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

    public async Task ProcessAsync(
        IReadOnlyCollection<RawEventDelivery> batch,
        IDeliveryAcknowledger acknowledger,
        CancellationToken cancellationToken
    )
    {
        var events = batch.Select(delivery => (Delivery: delivery, Event: Map(delivery))).ToList();
        if (events.Count == 0)
            return;

        bool stored = await writer.TryWriteAsync(
            events.Select(item => item.Event).ToList(),
            cancellationToken
        );

        foreach ((RawEventDelivery delivery, RawEvent rawEvent) in events)
        {
            using IDisposable correlation = LogContext.PushProperty(
                "CorrelationId",
                rawEvent.CorrelationId
            );
            using IDisposable message = LogContext.PushProperty("MessageId", rawEvent.MessageId);

            if (stored)
            {
                await acknowledger.AckAsync(delivery.Tag, cancellationToken);
                logger.LogDebug("Stored raw event");
            }
            else
            {
                logger.LogError("Retries exhausted for raw event, dead-lettering");
                await acknowledger.RejectAsync(delivery.Tag, cancellationToken);
            }
        }
    }

    private static RawEvent Map(RawEventDelivery delivery)
    {
        // Own the buffer and archive it unchanged, including unknown/duplicate wire fields.
        byte[] original = delivery.Body.ToArray();
        GameEventEnvelope? envelope = null;
        string status;
        string[] errors;
        DateTimeOffset? occurredAt = null;
        try
        {
            envelope = GameEventEnvelope.Parser.ParseFrom(original);
            errors = GameEventContract.Validate(envelope).ToArray();
            if (
                delivery.RoutingKey is { } routingKey
                && routingKey != $"{envelope.EventType}.v{envelope.EventVersion}"
            )
                errors = [.. errors, "AMQP routing key does not match the envelope."];
            if (delivery.BrokerMessageId is { } brokerId && brokerId != envelope.MessageId)
                errors = [.. errors, "AMQP MessageId does not match the envelope."];
            status = errors.Length == 0 ? "Valid" : "InvalidOrUnknown";
            if (
                envelope.OccurredAt is
                {
                    Seconds: >= -62135596800L and <= 253402300799L,
                    Nanos: >= 0 and <= 999999999
                } timestamp
            )
                occurredAt = timestamp.ToDateTimeOffset();
        }
        catch (InvalidProtocolBufferException)
        {
            status = "Unreadable";
            errors = ["Body cannot be parsed as GameEventEnvelope."];
        }

        return new RawEvent
        {
            Id = Guid.NewGuid(),
            MessageId =
                Guid.TryParse(envelope?.MessageId, out var id) && id != Guid.Empty ? id : null,
            TransactionId = envelope?.TransactionId,
            Type = envelope?.EventType,
            Version = envelope is { EventVersion: <= int.MaxValue }
                ? (int)envelope.EventVersion
                : null,
            CorrelationId = envelope?.HasCorrelationId == true ? envelope.CorrelationId : null,
            CausationId = envelope?.HasCausationId == true ? envelope.CausationId : null,
            OccurredAtUtc = occurredAt,
            ReceivedAt = delivery.ReceivedAt,
            Producer = envelope?.ProducerSquad.ToString(),
            Payload = original,
            ParseStatus = status,
            ValidationErrors = errors,
            Exchange = delivery.Exchange,
            RoutingKey = delivery.RoutingKey,
            BrokerMessageId = delivery.BrokerMessageId,
            Redelivered = delivery.Redelivered,
        };
    }
}
