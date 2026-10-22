using System.Diagnostics;
using System.Text.Json;
using Google.Protobuf;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Infrastructure.Collection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;

if (args.Length > 0 && args[0] == "--probe-before-insert")
    return await RunBeforeInsertProbeAsync(args[1..]);

// Deliberately fixed acceptance duration/rate. No short mode can report an acceptance pass.
const int rate = 50;
const int durationSeconds = 600;
const int expectedCount = rate * durationSeconds;
const double maximumPublishLatenessMs = 200.0;
var maximumSlopeMsPerMinute = ReadThreshold(args, "--max-p95-slope-ms-per-minute", 0);
var maximumEndWindowRegressionMs = ReadThreshold(args, "--max-p95-regression-ms", 0);
var runId = Guid.NewGuid().ToString("D");
var resourceName = "collection_load_" + runId.Replace("-", "");
var rabbit = new RabbitCollectionOptions
{
    Enabled = true,
    QueueName = resourceName,
    DeadLetterQueueName = resourceName + ".dead",
    DeadLetterExchangeName = resourceName + ".dlx",
    UserName =
        Environment.GetEnvironmentVariable("LEADERBOARD_TEST_RABBITMQ_USER") ?? "leaderboard",
    Password =
        Environment.GetEnvironmentVariable("LEADERBOARD_TEST_RABBITMQ_PASSWORD") ?? "leaderboard",
};
var mongoConnection =
    Environment.GetEnvironmentVariable("LEADERBOARD_TEST_MONGO_CONNECTION")
    ?? "mongodb://localhost:27017";
var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration.AddInMemoryCollection(
    new Dictionary<string, string?>
    {
        ["RabbitMq:Enabled"] = "true",
        ["RabbitMq:QueueName"] = rabbit.QueueName,
        ["RabbitMq:DeadLetterQueueName"] = rabbit.DeadLetterQueueName,
        ["RabbitMq:DeadLetterExchangeName"] = rabbit.DeadLetterExchangeName,
        ["RabbitMq:UserName"] = rabbit.UserName,
        ["RabbitMq:Password"] = rabbit.Password,
        ["MongoArchive:ConnectionString"] = mongoConnection,
        ["MongoArchive:Database"] = resourceName,
    }
);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddEventCollection(builder.Configuration);
builder.Services.RemoveAll<Leaderboard.Domain.Repositories.IRawEventRepository>();
builder.Services.AddSingleton<ConfirmedInsertLatencyRepository>(sp =>
    new(sp.GetRequiredService<MongoRawEventRepository>())
);
builder.Services.AddSingleton<Leaderboard.Domain.Repositories.IRawEventRepository>(sp =>
    sp.GetRequiredService<ConfirmedInsertLatencyRepository>()
);
using var host = builder.Build();
await host.StartAsync();
await using var connection = await rabbit.CreateConnectionFactory().CreateConnectionAsync();
await using var publisher = await connection.CreateChannelAsync(
    new CreateChannelOptions(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: false
    )
);
var confirms = new PublisherConfirmTracker();
publisher.BasicAcksAsync += (_, args) =>
{
    confirms.Acknowledge(args.DeliveryTag, args.Multiple);
    return Task.CompletedTask;
};
publisher.BasicNacksAsync += (_, args) =>
{
    confirms.NegativeAcknowledge(args.DeliveryTag, args.Multiple);
    return Task.CompletedTask;
};
publisher.BasicReturnAsync += (_, args) =>
{
    confirms.Returned(args.BasicProperties.MessageId, args.ReplyCode, args.ReplyText);
    return Task.CompletedTask;
};
publisher.ChannelShutdownAsync += (_, args) =>
{
    confirms.Fail(
        new IOException($"Publisher channel closed: {args.Exception?.Message ?? args.ReplyText}")
    );
    return Task.CompletedTask;
};
await RabbitEventCollector.DeclareTopologyAsync(publisher, rabbit, CancellationToken.None);
var readyDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
while ((await publisher.QueueDeclarePassiveAsync(rabbit.QueueName)).ConsumerCount == 0)
{
    if (DateTimeOffset.UtcNow >= readyDeadline)
        throw new TimeoutException("Collector did not become ready.");
    await Task.Delay(100);
}

Console.WriteLine(
    $"Run {runId}: scheduling exactly {expectedCount} persistent messages at {rate}/s for {durationSeconds}s."
);
Console.WriteLine(
    $"Acceptance thresholds: p95 slope <= {maximumSlopeMsPerMinute} ms/min; final p95 <= first p95 + {maximumEndWindowRegressionMs} ms; publisher schedule lateness <= {maximumPublishLatenessMs} ms. Defaults are strict (zero slope and zero regression); explicit options can set a different agreed threshold. Any loss, byte mismatch, duplicate receipt, backlog, quarantine, or pacing failure fails."
);
var expected = new Dictionary<
    string,
    (byte[] Body, DateTimeOffset PublishedAtUtc, long PublishedAtTicks, int Minute)
>(expectedCount);
var prepared = new (string MessageId, byte[] Body, BasicProperties Properties, int Minute)[
    expectedCount
];
var idBytes = Guid.Parse(runId).ToByteArray();
for (var i = 0; i < expectedCount; i++)
{
    BitConverter.GetBytes(i + 1).CopyTo(idBytes, 0);
    var messageId = new Guid(idBytes).ToString("D");
    var message = new GameEventEnvelope
    {
        EnvelopeVersion = 1,
        EventVersion = 1,
        EventType = "player.hero-created",
        ProducerSquad = ProducerSquad.Player,
        MessageId = messageId,
        TransactionId = runId,
        CorrelationId = runId,
        OccurredAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
            new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        ),
        HeroCreated = new HeroCreated
        {
            Actor = new Actor
            {
                PlayerId = "00000000-0000-4000-8000-000000000001",
                HeroId = "00000000-0000-4000-8000-000000000002",
            },
            HeroClassId = "00000000-0000-4000-8000-000000000080",
        },
    };
    if (GameEventContract.Validate(message).Count != 0)
        throw new InvalidOperationException("Invalid load fixture.");
    var body = message.ToByteArray().Concat(new byte[] { 8, 1, 160, 6, 42 }).ToArray();
    prepared[i] = (
        messageId,
        body,
        new BasicProperties
        {
            Persistent = true,
            MessageId = messageId,
            CorrelationId = runId,
            ContentType = "application/x-protobuf",
        },
        i / (rate * 60)
    );
}
var maxLateness = 0.0;
var maxLatenessIndex = -1;
var stopwatch = Stopwatch.StartNew();
var startedAt = DateTimeOffset.UtcNow;
var publisherFatal = (Exception?)null;
for (var i = 0; i < expectedCount; i++)
{
    var due = TimeSpan.FromSeconds((double)i / rate);
    var wait = due - stopwatch.Elapsed;
    if (wait > TimeSpan.Zero)
    {
        var delay = Task.Delay(wait);
        if (await Task.WhenAny(delay, confirms.FailureTask) != delay)
            publisherFatal = await confirms.FailureTask;
        else
            await delay;
    }
    if (publisherFatal is not null)
        break;
    try
    {
        await confirms.WaitForCapacityAsync(256, TimeSpan.FromSeconds(30));
        var item = prepared[i];
        var sequence = await publisher.GetNextPublishSequenceNumberAsync();
        confirms.Register(sequence, item.MessageId);
        var now = stopwatch.Elapsed;
        var lateness = Math.Max(0, (now - due).TotalMilliseconds);
        if (lateness > maxLateness)
        {
            maxLateness = lateness;
            maxLatenessIndex = i;
        }
        var publishedAtUtc = DateTimeOffset.UtcNow;
        var publishedAtTicks = Stopwatch.GetTimestamp();
        expected.Add(item.MessageId, (item.Body, publishedAtUtc, publishedAtTicks, item.Minute));
        await publisher.BasicPublishAsync(
            "player.events",
            "player.hero-created.v1",
            true,
            item.Properties,
            item.Body
        );
        confirms.MarkSent();
    }
    catch (Exception exception)
    {
        confirms.Fail(exception);
        publisherFatal = exception;
        break;
    }
    if ((i + 1) % (rate * 60) == 0)
    {
        var snapshot = confirms.Snapshot;
        Console.WriteLine(
            $"Minute {(i + 1) / (rate * 60)}: sent={snapshot.SentCount}, confirmed={snapshot.ConfirmedCount}, pending={snapshot.PendingCount}."
        );
    }
}
var remainder = TimeSpan.FromSeconds(durationSeconds) - stopwatch.Elapsed;
if (publisherFatal is null && remainder > TimeSpan.Zero)
    await Task.Delay(remainder);
if (publisherFatal is null)
{
    try
    {
        await confirms.WaitForAllConfirmedAsync(expectedCount, TimeSpan.FromSeconds(30));
    }
    catch (Exception exception)
    {
        confirms.Fail(exception);
        publisherFatal = exception;
    }
}
else
    confirms.Fail(publisherFatal);
var confirmSnapshot = confirms.Snapshot;
var actualDurationSeconds = stopwatch.Elapsed.TotalSeconds;
var repository = host.Services.GetRequiredService<MongoRawEventRepository>();
var latencyRepository = host.Services.GetRequiredService<ConfirmedInsertLatencyRepository>();
await using var inspector = await connection.CreateChannelAsync();
IReadOnlyList<Leaderboard.Domain.Collection.RawEventReceipt> receipts = [];
var drainDeadline = DateTimeOffset.UtcNow.AddSeconds(30);
while (DateTimeOffset.UtcNow < drainDeadline)
{
    receipts = await repository.ReadPeriodAsync(
        startedAt.AddSeconds(-1),
        DateTimeOffset.UtcNow.AddSeconds(1),
        CancellationToken.None
    );
    try
    {
        if (
            receipts.Count >= confirmSnapshot.SentCount
            && (await inspector.QueueDeclarePassiveAsync(rabbit.QueueName)).MessageCount == 0
        )
            break;
    }
    catch (Exception exception)
    {
        publisherFatal ??= exception;
        break;
    }
    await Task.Delay(200);
}
await host.StopAsync(); // Returns any unfinished deliveries before inspecting final backlog.
var queueState = await inspector.QueueDeclarePassiveAsync(rabbit.QueueName);
var backlog = queueState.MessageCount;
var activeConsumers = queueState.ConsumerCount;
var quarantined = (
    await inspector.QueueDeclarePassiveAsync(rabbit.DeadLetterQueueName)
).MessageCount;
var matched = receipts.Where(r => r.CorrelationId == runId).ToArray();
var uniqueReceiptIds = matched.Select(x => x.ReceiptId).Distinct().Count();
var uniqueMessageIds = matched.Select(x => x.BrokerMessageId).Distinct().Count();
var byteMismatches = matched.Count(r =>
    r.BrokerMessageId is null
    || !expected.TryGetValue(r.BrokerMessageId, out var e)
    || !r.Body.SequenceEqual(e.Body)
);
var metadataMismatches = matched.Count(r =>
    r.TransactionId != runId || r.ProducerSquad != "Player" || r.ParseStatus != "Valid"
);
var windows = Enumerable
    .Range(0, 10)
    .Select(minute =>
    {
        var minuteReceipts = matched
            .Where(r =>
                r.BrokerMessageId is not null
                && expected.TryGetValue(r.BrokerMessageId, out var e)
                && e.Minute == minute
            )
            .ToArray();
        var receptionLatencies = minuteReceipts
            .Select(r =>
                (r.ReceivedAt - expected[r.BrokerMessageId!].PublishedAtUtc).TotalMilliseconds
            )
            .Order()
            .ToArray();
        var confirmedInsertLatencies = minuteReceipts
            .Where(r => latencyRepository.ConfirmedInsertTicks.ContainsKey(r.BrokerMessageId!))
            .Select(r =>
                Stopwatch
                    .GetElapsedTime(
                        expected[r.BrokerMessageId!].PublishedAtTicks,
                        latencyRepository.ConfirmedInsertTicks[r.BrokerMessageId!]
                    )
                    .TotalMilliseconds
            )
            .Order()
            .ToArray();
        return new
        {
            Minute = minute + 1,
            Count = minuteReceipts.Length,
            ReceptionP95Milliseconds = receptionLatencies.Length == 0
                ? double.NaN
                : receptionLatencies[(int)Math.Ceiling(receptionLatencies.Length * .95) - 1],
            ConfirmedInsertCount = confirmedInsertLatencies.Length,
            ConfirmedInsertP95Milliseconds = confirmedInsertLatencies.Length == 0
                ? double.NaN
                : confirmedInsertLatencies[
                    (int)Math.Ceiling(confirmedInsertLatencies.Length * .95) - 1
                ],
        };
    })
    .ToArray();
var meanY = windows.Average(x => x.ConfirmedInsertP95Milliseconds);
var slope =
    windows.Sum(x => (x.Minute - 5.5) * (x.ConfirmedInsertP95Milliseconds - meanY))
    / windows.Sum(x => Math.Pow(x.Minute - 5.5, 2));
var regression =
    windows[^1].ConfirmedInsertP95Milliseconds - windows[0].ConfirmedInsertP95Milliseconds;
var passed =
    actualDurationSeconds >= durationSeconds
    && publisherFatal is null
    && confirmSnapshot.SentCount == expectedCount
    && confirmSnapshot.ConfirmedCount == expectedCount
    && confirmSnapshot.PendingCount == 0
    && confirmSnapshot.NackEventCount == 0
    && confirmSnapshot.NackedMessageCount == 0
    && confirmSnapshot.ReturnedCount == 0
    && confirmSnapshot.Failure is null
    && matched.Length == expectedCount
    && uniqueReceiptIds == expectedCount
    && uniqueMessageIds == expectedCount
    && latencyRepository.ConfirmedInsertTicks.Count == expectedCount
    && byteMismatches == 0
    && metadataMismatches == 0
    && backlog == 0
    && activeConsumers == 0
    && quarantined == 0
    && windows.All(x => x.Count == rate * 60)
    && windows.All(x => x.ConfirmedInsertCount == rate * 60)
    && slope <= maximumSlopeMsPerMinute
    && regression <= maximumEndWindowRegressionMs
    && maxLateness <= maximumPublishLatenessMs;
var report = new
{
    RunId = runId,
    Database = resourceName,
    Queue = rabbit.QueueName,
    StartedAt = startedAt,
    ScheduledRate = rate,
    ScheduledDurationSeconds = durationSeconds,
    ActualDurationSeconds = actualDurationSeconds,
    PublishAttempts = expected.Count,
    Published = confirmSnapshot.SentCount,
    Confirmed = confirmSnapshot.ConfirmedCount,
    PendingConfirms = confirmSnapshot.PendingCount,
    NackEvents = confirmSnapshot.NackEventCount,
    NackedMessages = confirmSnapshot.NackedMessageCount,
    ReturnedMessages = confirmSnapshot.ReturnedCount,
    PublisherError = (publisherFatal ?? confirmSnapshot.Failure)?.ToString(),
    Stored = matched.Length,
    PublishedBytes = expected.Values.Sum(x => (long)x.Body.Length),
    StoredBytes = matched.Sum(x => (long)x.Body.Length),
    ConfirmedInserts = latencyRepository.ConfirmedInsertTicks.Count,
    ActualPublishRatePerSecond = confirmSnapshot.SentCount / actualDurationSeconds,
    UniqueReceiptIds = uniqueReceiptIds,
    UniqueMessageIds = uniqueMessageIds,
    ByteMismatches = byteMismatches,
    MetadataMismatches = metadataMismatches,
    Backlog = backlog,
    ActiveConsumersAfterStop = activeConsumers,
    Quarantined = quarantined,
    MinuteWindows = windows,
    ConfirmedInsertP95SlopeMillisecondsPerMinute = slope,
    ConfirmedInsertLastMinusFirstP95Milliseconds = regression,
    MaxPublishLatenessMilliseconds = maxLateness,
    MaxPublishLatenessIndex = maxLatenessIndex,
    Tolerance = new
    {
        MaximumSlopeMsPerMinute = maximumSlopeMsPerMinute,
        MaximumEndWindowRegressionMs = maximumEndWindowRegressionMs,
        MaximumPublishLatenessMs = maximumPublishLatenessMs,
    },
    Result = passed ? "PASS" : "FAIL",
};
var outputPath = Path.Combine("scripts", "collection", "results", runId + ".json");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var json = JsonSerializer.Serialize(
    report,
    new JsonSerializerOptions
    {
        WriteIndented = true,
        NumberHandling = System
            .Text
            .Json
            .Serialization
            .JsonNumberHandling
            .AllowNamedFloatingPointLiterals,
    }
);
await File.WriteAllTextAsync(outputPath, json);
Console.WriteLine(json);
Console.WriteLine(
    $"Report: {outputPath}. Mongo receipts retained for inspection; test queues removed only after PASS."
);
if (passed)
{
    await inspector.QueueDeleteAsync(rabbit.QueueName);
    await inspector.QueueDeleteAsync(rabbit.DeadLetterQueueName);
    await inspector.ExchangeDeleteAsync(rabbit.DeadLetterExchangeName);
}
else if (inspector.IsOpen)
{
    foreach (var subscription in rabbit.Subscriptions)
    {
        try
        {
            await inspector.QueueUnbindAsync(
                rabbit.QueueName,
                subscription.ExchangeName,
                subscription.RoutingKey,
                cancellationToken: CancellationToken.None
            );
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Could not detach failed run binding {subscription.ExchangeName}/{subscription.RoutingKey}: {exception.Message}"
            );
        }
    }
}
return passed ? 0 : 1;

static double ReadThreshold(string[] arguments, string name, double defaultValue)
{
    var index = Array.IndexOf(arguments, name);
    if (index < 0)
        return defaultValue;
    if (
        index + 1 >= arguments.Length
        || !double.TryParse(arguments[index + 1], out var value)
        || value < 0
    )
        throw new ArgumentException($"{name} requires a nonnegative number.");
    return value;
}

static async Task<int> RunBeforeInsertProbeAsync(string[] arguments)
{
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < arguments.Length; i += 2)
    {
        if (i + 1 >= arguments.Length)
            throw new ArgumentException($"Missing value for {arguments[i]}.");
        values.Add(arguments[i], arguments[i + 1]);
    }
    string Required(string key) =>
        values.TryGetValue(key, out var value)
            ? value
            : throw new ArgumentException($"Missing required option {key}.");

    var options = new RabbitCollectionOptions
    {
        Enabled = true,
        QueueName = Required("--queue"),
        DeadLetterQueueName = Required("--dead-letter-queue"),
        DeadLetterExchangeName = Required("--dead-letter-exchange"),
        MaxDeliveryAttempts = int.Parse(
            Required("--max-delivery-attempts"),
            System.Globalization.CultureInfo.InvariantCulture
        ),
        UserName =
            Environment.GetEnvironmentVariable("LEADERBOARD_TEST_RABBITMQ_USER") ?? "leaderboard",
        Password =
            Environment.GetEnvironmentVariable("LEADERBOARD_TEST_RABBITMQ_PASSWORD")
            ?? "leaderboard",
    };
    var configuration = new Dictionary<string, string?>
    {
        ["RabbitMq:Enabled"] = "true",
        ["RabbitMq:QueueName"] = options.QueueName,
        ["RabbitMq:DeadLetterQueueName"] = options.DeadLetterQueueName,
        ["RabbitMq:DeadLetterExchangeName"] = options.DeadLetterExchangeName,
        ["RabbitMq:MaxDeliveryAttempts"] = options.MaxDeliveryAttempts.ToString(
            System.Globalization.CultureInfo.InvariantCulture
        ),
        ["RabbitMq:UserName"] = options.UserName,
        ["RabbitMq:Password"] = options.Password,
        ["MongoArchive:Database"] = Required("--database"),
    };
    var builder = Host.CreateApplicationBuilder();
    builder.Configuration.Sources.Clear();
    builder.Configuration.AddInMemoryCollection(configuration);
    builder.Services.AddEventCollection(builder.Configuration);
    builder.Services.RemoveAll<Leaderboard.Domain.Repositories.IRawEventRepository>();
    builder.Services.AddSingleton<Leaderboard.Domain.Repositories.IRawEventRepository>(
        _ => new BeforeInsertBlockingRepository(Required("--started-marker"))
    );
    using var host = builder.Build();
    await host.StartAsync();
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}

internal sealed class BeforeInsertBlockingRepository(string markerPath)
    : Leaderboard.Domain.Repositories.IRawEventRepository
{
    public async Task AddAsync(
        Leaderboard.Domain.Collection.RawEventReceipt receipt,
        CancellationToken cancellationToken
    )
    {
        await File.WriteAllTextAsync(markerPath, receipt.ReceiptId, cancellationToken);
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public Task<IReadOnlyList<Leaderboard.Domain.Collection.RawEventReceipt>> ReadPeriodAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();
}
