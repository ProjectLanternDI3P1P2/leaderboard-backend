using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leaderboard.Infrastructure.Messaging;

/// <summary>
/// Writes a batch to the store with a bounded retry and exponential backoff.
/// A failed attempt may have inserted part of the batch, so a retry can store
/// some events twice. That is accepted: the store keeps duplicates and replays
/// deduplicate on <c>MessageId</c>.
/// </summary>
public sealed class RawEventBatchWriter(
    IRawEventStore store,
    IOptions<RawEventConsumerOptions> options,
    ILogger<RawEventBatchWriter> logger
)
{
    /// <returns>True once the batch is durably stored, false when retries are exhausted.</returns>
    public async Task<bool> TryWriteAsync(
        IReadOnlyCollection<RawEvent> events,
        CancellationToken cancellationToken
    )
    {
        RawEventConsumerOptions settings = options.Value;
        TimeSpan backoff = settings.InitialBackoff;

        for (int attempt = 1; attempt <= settings.MaxAttempts; attempt++)
        {
            try
            {
                // A prior attempt may have inserted a prefix; new receipt IDs let the full retry proceed.
                var attemptEvents =
                    attempt == 1
                        ? events
                        : events.Select(e => e with { Id = Guid.NewGuid() }).ToArray();
                await store.AppendAsync(attemptEvents, cancellationToken);
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Writing {Count} raw events failed (attempt {Attempt}/{MaxAttempts})",
                    events.Count,
                    attempt,
                    settings.MaxAttempts
                );

                if (attempt < settings.MaxAttempts)
                {
                    await Task.Delay(backoff, cancellationToken);
                    backoff *= 2;
                }
            }
        }

        return false;
    }
}
