using System.Globalization;
using System.Threading.Channels;
using Combat.Contracts.Events.V1;
using Google.Protobuf;
using Leaderboard.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace Leaderboard.Infrastructure.Messaging;

/// <summary>A message as received from the broker, stamped on receipt.</summary>
public sealed record RawEventDelivery(
    ulong Tag,
    byte[] Body,
    string? TransactionId,
    DateTimeOffset ReceivedAt
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
/// the write is confirmed, reject (dead-letter) when it cannot be read or stored.
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
        List<(RawEventDelivery Delivery, RawEvent Event)> valid = [];
        foreach (RawEventDelivery delivery in batch)
        {
            if (TryMap(delivery, out RawEvent? rawEvent))
            {
                valid.Add((delivery, rawEvent));
                continue;
            }

            logger.LogError("Unreadable message {DeliveryTag}, dead-lettering", delivery.Tag);
            await acknowledger.RejectAsync(delivery.Tag, cancellationToken);
        }

        if (valid.Count == 0)
        {
            return;
        }

        bool stored = await writer.TryWriteAsync(
            valid.Select(item => item.Event).ToList(),
            cancellationToken
        );

        foreach ((RawEventDelivery delivery, RawEvent rawEvent) in valid)
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

    private static bool TryMap(RawEventDelivery delivery, out RawEvent rawEvent)
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
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind
                ),
                ReceivedAt = delivery.ReceivedAt,
                Producer = envelope.Producer,
                Payload = envelope.Payload.ToByteArray(),
            };
            return true;
        }
        catch (Exception exception)
            when (exception is FormatException or InvalidProtocolBufferException)
        {
            return false;
        }
    }
}
