using System.Diagnostics;

internal sealed class PublisherConfirmTracker
{
    private readonly object sync = new();
    private readonly SortedDictionary<ulong, string> pending = [];
    private readonly Dictionary<string, ulong> sequenceByMessageId = new(StringComparer.Ordinal);
    private TaskCompletionSource changed = NewSignal();
    private readonly TaskCompletionSource<Exception> failed = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private Exception? failure;
    private int sentCount;
    private int confirmedCount;
    private int nackEventCount;
    private int nackedMessageCount;
    private int returnedCount;

    public Task<Exception> FailureTask => failed.Task;

    public PublisherConfirmSnapshot Snapshot
    {
        get
        {
            lock (sync)
                return new(
                    sentCount,
                    confirmedCount,
                    pending.Count,
                    nackEventCount,
                    nackedMessageCount,
                    returnedCount,
                    failure
                );
        }
    }

    public void Register(ulong sequenceNumber, string messageId)
    {
        lock (sync)
        {
            if (failure is not null)
                throw new InvalidOperationException(
                    "Publisher failed before the next send.",
                    failure
                );
            if (pending.ContainsKey(sequenceNumber) || sequenceByMessageId.ContainsKey(messageId))
            {
                FailLocked(
                    new InvalidOperationException(
                        "Duplicate publisher sequence number or message ID."
                    )
                );
                throw new InvalidOperationException(
                    "Duplicate publisher sequence number or message ID."
                );
            }

            pending.Add(sequenceNumber, messageId);
            sequenceByMessageId.Add(messageId, sequenceNumber);
            SignalLocked();
        }
    }

    public void MarkSent()
    {
        lock (sync)
        {
            sentCount++;
            SignalLocked();
        }
    }

    public void Acknowledge(ulong deliveryTag, bool multiple) =>
        Complete(deliveryTag, multiple, false);

    public void NegativeAcknowledge(ulong deliveryTag, bool multiple) =>
        Complete(deliveryTag, multiple, true);

    public void Returned(string? messageId, ushort replyCode, string replyText)
    {
        lock (sync)
        {
            returnedCount++;
            var identified = messageId is not null && sequenceByMessageId.ContainsKey(messageId);
            FailLocked(
                new IOException(
                    identified
                        ? $"Broker returned message {messageId}: {replyCode} {replyText}"
                        : $"Broker returned an uncorrelated message: {replyCode} {replyText}"
                )
            );
        }
    }

    public void Fail(Exception exception)
    {
        lock (sync)
            FailLocked(exception);
    }

    public async Task WaitForCapacityAsync(int maximumPending, TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            Task changedTask;
            lock (sync)
            {
                ThrowIfFailedLocked();
                if (pending.Count < maximumPending)
                    return;
                changedTask = changed.Task;
            }

            var remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException(
                    $"Publisher confirm window remained full at {maximumPending} messages."
                );
            await changedTask.WaitAsync(remaining);
        }
    }

    public async Task WaitForAllConfirmedAsync(int expectedCount, TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            Task changedTask;
            lock (sync)
            {
                ThrowIfFailedLocked();
                if (
                    sentCount == expectedCount
                    && confirmedCount == expectedCount
                    && pending.Count == 0
                )
                    return;
                changedTask = changed.Task;
            }

            var remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException(
                    $"Publisher confirms timed out: sent={Snapshot.SentCount}, confirmed={Snapshot.ConfirmedCount}, pending={Snapshot.PendingCount}."
                );
            await changedTask.WaitAsync(remaining);
        }
    }

    private void Complete(ulong deliveryTag, bool multiple, bool negative)
    {
        lock (sync)
        {
            var completed = pending
                .Keys.Where(sequence =>
                    multiple ? deliveryTag == 0 || sequence <= deliveryTag : sequence == deliveryTag
                )
                .ToArray();
            if (completed.Length == 0)
            {
                if (negative)
                    nackEventCount++;
                FailLocked(
                    new InvalidOperationException(
                        $"Received an unexpected publisher {(negative ? "nack" : "ack")} for sequence {deliveryTag}."
                    )
                );
                return;
            }

            foreach (var sequence in completed)
            {
                sequenceByMessageId.Remove(pending[sequence]);
                pending.Remove(sequence);
            }

            if (negative)
            {
                nackEventCount++;
                nackedMessageCount += completed.Length;
                FailLocked(
                    new IOException(
                        $"Broker negatively acknowledged {completed.Length} message(s) through sequence {deliveryTag}."
                    )
                );
            }
            else
            {
                confirmedCount += completed.Length;
                SignalLocked();
            }
        }
    }

    private void ThrowIfFailedLocked()
    {
        if (failure is not null)
            throw new IOException("Publisher confirmation failed.", failure);
    }

    private void FailLocked(Exception exception)
    {
        failure ??= exception;
        failed.TrySetResult(failure);
        SignalLocked();
    }

    private void SignalLocked()
    {
        var previous = changed;
        changed = NewSignal();
        previous.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed record PublisherConfirmSnapshot(
    int SentCount,
    int ConfirmedCount,
    int PendingCount,
    int NackEventCount,
    int NackedMessageCount,
    int ReturnedCount,
    Exception? Failure
);
