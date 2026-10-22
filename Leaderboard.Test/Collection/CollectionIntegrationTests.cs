using System.Diagnostics;
using FluentAssertions;
using Google.Protobuf;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Collection;
using Leaderboard.Domain.Repositories;
using Leaderboard.Infrastructure.Collection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace Leaderboard.Test.Collection;

/// <summary>Real engines; start `docker compose up -d rabbitmq mongo` first. No silent skips.</summary>
public sealed class CollectionIntegrationTests
{
    [Fact]
    public async Task BrokerToMongoPreservesEveryBodyAndDuplicateProducerIds()
    {
        await using var stack = new TestStack();
        using var host = stack.BuildHost();
        await host.StartAsync(TestContext.Current.CancellationToken);
        await stack.WaitForQueueAsync();
        var message = ArchiveEventTests.Fixture();
        var body = message.ToByteArray().Concat(new byte[] { 8, 1, 160, 6, 42 }).ToArray();
        await stack.PublishAsync(body, "same-broker-id");
        await stack.PublishAsync(body, "same-broker-id");
        await stack.PublishAsync([255], "unreadable");
        message.EnvelopeVersion = 99;
        var unknown = message.ToByteArray();
        await stack.PublishAsync(unknown, "unknown");
        var receipts = await stack.WaitForReceiptsAsync(4);
        await host.StopAsync(TestContext.Current.CancellationToken);

        receipts.Select(x => x.ReceiptId).Distinct().Should().HaveCount(4);
        var duplicates = receipts.Where(x => x.BrokerMessageId == "same-broker-id").ToArray();
        duplicates.Should().HaveCount(2);
        foreach (var receipt in duplicates)
        {
            receipt.Body.Should().Equal(body);
            receipt.TransactionId.Should().Be(message.TransactionId);
            receipt.ProducerSquad.Should().Be("Player");
            receipt.ReceivedAt.Offset.Should().Be(TimeSpan.Zero);
            receipt.ParseStatus.Should().Be("Valid");
            receipt.Exchange.Should().Be("player.events");
            receipt.RoutingKey.Should().Be("player.hero-created.v1");
        }
        receipts.Single(x => x.BrokerMessageId == "unreadable").Body.Should().Equal([255]);
        receipts.Single(x => x.BrokerMessageId == "unknown").Body.Should().Equal(unknown);
        (
            await stack.Channel.QueueDeclarePassiveAsync(
                stack.Options.QueueName,
                TestContext.Current.CancellationToken
            )
        )
            .MessageCount.Should()
            .Be(0);
    }

    [Fact]
    public async Task EveryCatalogueRouteAndDistinctEventsSharingATransactionAreArchived()
    {
        await using var stack = new TestStack();
        using var host = stack.BuildHost();
        await host.StartAsync(TestContext.Current.CancellationToken);
        await stack.WaitForQueueAsync();

        var messages = CatalogueFixtures();
        messages
            .Select(x => x.Definition.RoutingKey)
            .Distinct()
            .Should()
            .HaveCount(GameEventContract.Events.Count);
        foreach (var (definition, message, body) in messages)
        {
            await stack.PublishAsync(
                body,
                message.MessageId,
                definition.EventType.Split('.')[0] + ".events",
                definition.RoutingKey
            );
        }

        var receipts = await stack.WaitForReceiptsAsync(messages.Count);
        await host.StopAsync(TestContext.Current.CancellationToken);

        receipts.Should().HaveCount(messages.Count);
        foreach (var (definition, message, body) in messages)
        {
            var receipt = receipts.Single(x => x.BrokerMessageId == message.MessageId);
            receipt.Body.Should().Equal(body);
            receipt.TransactionId.Should().Be(message.TransactionId);
            receipt.ProducerSquad.Should().Be(message.ProducerSquad.ToString());
            receipt.RoutingKey.Should().Be(definition.RoutingKey);
            receipt.ParseStatus.Should().Be("Valid");
            receipt.ReceivedAt.Offset.Should().Be(TimeSpan.Zero);
        }

        var sharedTransaction = messages
            .Where(x =>
                x.Message.MessageId
                    is "00000000-0000-4000-8000-000000001016"
                        or "00000000-0000-4000-8000-000000001017"
            )
            .ToArray();
        sharedTransaction.Should().HaveCount(2);
        sharedTransaction.Select(x => x.Message.TransactionId).Distinct().Should().ContainSingle();
        sharedTransaction[0].Body.Should().NotEqual(sharedTransaction[1].Body);
        receipts.Select(x => x.ReceiptId).Distinct().Should().HaveCount(messages.Count);
    }

    [Fact]
    public async Task InsertConfirmedBeforeAckCrashWindowRetainsBothDeliveryReceipts()
    {
        await using var stack = new TestStack();
        var insertThenBlock = new InsertThenBlockRepository(stack.Repository);
        var body = ArchiveEventTests.Fixture().ToByteArray();
        using (var host = stack.BuildHost(insertThenBlock))
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
            await stack.WaitForQueueAsync();
            await stack.PublishAsync(body, "insert-before-ack");
            await insertThenBlock.InsertConfirmed.Task.WaitAsync(
                TimeSpan.FromSeconds(20),
                TestContext.Current.CancellationToken
            );
            await host.StopAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        }

        var pending = await stack.Channel.BasicGetAsync(
            stack.Options.QueueName,
            false,
            TestContext.Current.CancellationToken
        );
        pending.Should().NotBeNull();
        pending!.Redelivered.Should().BeTrue();
        await stack.Channel.BasicNackAsync(
            pending.DeliveryTag,
            false,
            true,
            TestContext.Current.CancellationToken
        );

        using (var restarted = stack.BuildHost())
        {
            await restarted.StartAsync(TestContext.Current.CancellationToken);
            var receipts = await stack.WaitForReceiptsAsync(2);
            receipts.Should().HaveCount(2);
            receipts.Select(x => x.Body).Should().OnlyContain(x => x.SequenceEqual(body));
            receipts.Select(x => x.ReceiptId).Distinct().Should().HaveCount(2);
            receipts.Should().ContainSingle(x => x.Redelivered);
            await restarted.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task StopBeforeWriteReturnsUnackedMessageAndRestartArchivesIt()
    {
        await using var stack = new TestStack();
        var blocked = new BlockingRepository();
        using (var host = stack.BuildHost(blocked))
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
            await stack.WaitForQueueAsync();
            await stack.PublishAsync(ArchiveEventTests.Fixture().ToByteArray(), "restart");
            await blocked.Entered.Task.WaitAsync(
                TimeSpan.FromSeconds(20),
                TestContext.Current.CancellationToken
            );
            (
                await stack.Repository.ReadPeriodAsync(
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MaxValue,
                    TestContext.Current.CancellationToken
                )
            )
                .Should()
                .BeEmpty();
            // Graceful stop cancels the unfinished insert and closes the channel without ACK.
            await host.StopAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        }
        var pending = await stack.Channel.BasicGetAsync(
            stack.Options.QueueName,
            false,
            TestContext.Current.CancellationToken
        );
        pending.Should().NotBeNull();
        pending!.Redelivered.Should().BeTrue();
        await stack.Channel.BasicNackAsync(
            pending.DeliveryTag,
            false,
            true,
            TestContext.Current.CancellationToken
        );

        using (var restarted = stack.BuildHost())
        {
            await restarted.StartAsync(TestContext.Current.CancellationToken);
            var receipts = await stack.WaitForReceiptsAsync(1);
            receipts.Single().Redelivered.Should().BeTrue();
            receipts.Single().Body.Should().Equal(ArchiveEventTests.Fixture().ToByteArray());
            await restarted.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ChildProcessCrashBeforeInsertRedeliversAfterRestart()
    {
        await using var stack = new TestStack();
        var repoRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var childAssembly = Path.Combine(
            repoRoot,
            "scripts",
            "collection",
            "bin",
            configuration,
            "net10.0",
            "Collection.Load.dll"
        );
        File.Exists(childAssembly).Should().BeTrue("the test project builds the probe helper");
        var marker = Path.Combine(
            Path.GetTempPath(),
            "collection-probe-" + Guid.NewGuid().ToString("N")
        );
        using var child = Process.Start(
            CreateProbeStartInfo(
                childAssembly,
                stack.Options.QueueName,
                stack.Options.DeadLetterQueueName,
                stack.Options.DeadLetterExchangeName,
                stack.Options.MaxDeliveryAttempts,
                stack.DatabaseName,
                marker
            )
        );
        child.Should().NotBeNull();

        try
        {
            await stack.WaitForQueueAsync();
            var body = ArchiveEventTests.Fixture().ToByteArray();
            await stack.PublishAsync(body, "child-crash-before-insert");
            var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
            while (!File.Exists(marker) && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(50, TestContext.Current.CancellationToken);
            File.Exists(marker)
                .Should()
                .BeTrue("the production consumer reached its pre-insert repository barrier");

            child!.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(TestContext.Current.CancellationToken);
            var pending = await WaitForReadyDeliveryAsync(stack, TimeSpan.FromSeconds(20));
            pending.Should().NotBeNull();
            pending!.Redelivered.Should().BeTrue();
            await stack.Channel.BasicNackAsync(
                pending.DeliveryTag,
                false,
                true,
                TestContext.Current.CancellationToken
            );

            using var restarted = stack.BuildHost();
            await restarted.StartAsync(TestContext.Current.CancellationToken);
            var receipts = await stack.WaitForReceiptsAsync(1);
            receipts.Should().ContainSingle();
            receipts.Single().Body.Should().Equal(body);
            receipts.Single().Redelivered.Should().BeTrue();
            await restarted.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            if (child is { HasExited: false })
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(CancellationToken.None);
            }
            if (File.Exists(marker))
                File.Delete(marker);
        }
    }

    [Fact]
    public async Task WriteFailureRetriesWithDelayThenRetainsBodyInDurableQuarantine()
    {
        await using var stack = new TestStack();
        var failing = new FailingRepository();
        using var host = stack.BuildHost(failing);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await stack.WaitForQueueAsync();
        byte[] body = [255];
        await stack.PublishAsync(body, "failed");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        BasicGetResult? quarantined = null;
        while (DateTimeOffset.UtcNow < deadline && quarantined is null)
        {
            quarantined = await stack.Channel.BasicGetAsync(
                stack.Options.DeadLetterQueueName,
                true,
                TestContext.Current.CancellationToken
            );
            if (quarantined is null)
                await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        await host.StopAsync(TestContext.Current.CancellationToken);
        quarantined.Should().NotBeNull();
        quarantined!.Body.ToArray().Should().Equal(body);
        failing.Attempts.Should().BeInRange(2, 5);
        failing
            .Times.Zip(failing.Times.Skip(1), (a, b) => b - a)
            .Should()
            .OnlyContain(x => x >= TimeSpan.FromMilliseconds(900));
    }

    private sealed class BlockingRepository : IRawEventRepository
    {
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task AddAsync(RawEventReceipt receipt, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public Task<IReadOnlyList<RawEventReceipt>> ReadPeriodAsync(
            DateTimeOffset a,
            DateTimeOffset b,
            CancellationToken ct
        ) => throw new NotSupportedException();
    }

    private sealed class InsertThenBlockRepository(IRawEventRepository inner) : IRawEventRepository
    {
        public TaskCompletionSource InsertConfirmed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task AddAsync(RawEventReceipt receipt, CancellationToken cancellationToken)
        {
            await inner.AddAsync(receipt, cancellationToken);
            InsertConfirmed.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public Task<IReadOnlyList<RawEventReceipt>> ReadPeriodAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken
        ) => inner.ReadPeriodAsync(from, to, cancellationToken);
    }

    private sealed class FailingRepository : IRawEventRepository
    {
        public int Attempts { get; private set; }
        public List<DateTimeOffset> Times { get; } = [];

        public Task AddAsync(RawEventReceipt r, CancellationToken ct)
        {
            Attempts++;
            Times.Add(DateTimeOffset.UtcNow);
            throw new IOException("storage unavailable");
        }

        public Task<IReadOnlyList<RawEventReceipt>> ReadPeriodAsync(
            DateTimeOffset a,
            DateTimeOffset b,
            CancellationToken ct
        ) => throw new NotSupportedException();
    }

    private sealed class TestStack : IAsyncDisposable
    {
        private readonly string id = "collection_test_" + Guid.NewGuid().ToString("N");
        private readonly Dictionary<string, string?> settings;
        private IConnection? connection;
        private IChannel? channel;
        public IChannel Channel => channel!;
        public RabbitCollectionOptions Options { get; }
        public MongoRawEventRepository Repository { get; }
        public string DatabaseName => id;

        public TestStack()
        {
            Options = new RabbitCollectionOptions
            {
                Enabled = true,
                QueueName = id,
                DeadLetterQueueName = id + ".dead",
                DeadLetterExchangeName = id + ".dlx",
                UserName =
                    Environment.GetEnvironmentVariable("LEADERBOARD_TEST_RABBITMQ_USER")
                    ?? "leaderboard",
                Password =
                    Environment.GetEnvironmentVariable("LEADERBOARD_TEST_RABBITMQ_PASSWORD")
                    ?? "leaderboard",
                MaxDeliveryAttempts = 2,
                RetryDelaySeconds = 1,
                ReconnectDelaySeconds = 1,
            };
            settings = new()
            {
                ["RabbitMq:Enabled"] = "true",
                ["RabbitMq:QueueName"] = Options.QueueName,
                ["RabbitMq:DeadLetterQueueName"] = Options.DeadLetterQueueName,
                ["RabbitMq:DeadLetterExchangeName"] = Options.DeadLetterExchangeName,
                ["RabbitMq:UserName"] = Options.UserName,
                ["RabbitMq:Password"] = Options.Password,
                ["RabbitMq:RetryDelaySeconds"] = "1",
                ["RabbitMq:ReconnectDelaySeconds"] = "1",
                ["RabbitMq:MaxDeliveryAttempts"] = "2",
                ["MongoArchive:Database"] = id,
            };
            Repository = new MongoRawEventRepository(
                Microsoft.Extensions.Options.Options.Create(
                    new MongoArchiveOptions { Database = id }
                )
            );
        }

        public IHost BuildHost(IRawEventRepository? repository = null)
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(settings);
            builder.Services.AddEventCollection(builder.Configuration);
            if (repository is not null)
            {
                builder.Services.RemoveAll<IRawEventRepository>();
                builder.Services.AddSingleton(repository);
            }
            return builder.Build();
        }

        public async Task WaitForQueueAsync()
        {
            connection = await Options.CreateConnectionFactory().CreateConnectionAsync();
            channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true
                )
            );
            // Idempotent declaration also makes publisher readiness deterministic.
            await RabbitEventCollector.DeclareTopologyAsync(
                channel,
                Options,
                TestContext.Current.CancellationToken
            );
            var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
            while ((await channel.QueueDeclarePassiveAsync(Options.QueueName)).ConsumerCount == 0)
            {
                if (DateTimeOffset.UtcNow >= deadline)
                    throw new TimeoutException("Collector did not start.");
                await Task.Delay(100);
            }
        }

        public async Task PublishAsync(
            byte[] bytes,
            string messageId,
            string exchange = "player.events",
            string routingKey = "player.hero-created.v1"
        ) =>
            await Channel.BasicPublishAsync(
                exchange,
                routingKey,
                true,
                new BasicProperties
                {
                    Persistent = true,
                    MessageId = messageId,
                    CorrelationId = id,
                    ContentType = "application/x-protobuf",
                },
                bytes
            );

        public async Task<IReadOnlyList<RawEventReceipt>> WaitForReceiptsAsync(int count)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
            while (DateTimeOffset.UtcNow < deadline)
            {
                var receipts = await Repository.ReadPeriodAsync(
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MaxValue,
                    TestContext.Current.CancellationToken
                );
                if (receipts.Count >= count)
                    return receipts;
                await Task.Delay(100);
            }
            throw new TimeoutException($"Expected {count} Mongo receipts.");
        }

        public async ValueTask DisposeAsync()
        {
            if (channel is { IsOpen: true })
            {
                await channel.QueueDeleteAsync(Options.QueueName);
                await channel.QueueDeleteAsync(Options.DeadLetterQueueName);
                await channel.ExchangeDeleteAsync(Options.DeadLetterExchangeName);
                await channel.DisposeAsync();
            }
            if (connection is not null)
                await connection.DisposeAsync();
            // Isolated test database only; shared local volumes and other databases are preserved.
            await new MongoClient("mongodb://localhost:27017").DropDatabaseAsync(id);
        }
    }

    private static List<(
        EventDefinition Definition,
        GameEventEnvelope Message,
        byte[] Body
    )> CatalogueFixtures()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "CollectionFixtures");
        using var document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(directory, "manifest.json"))
        );
        var fixtures = document
            .RootElement.GetProperty("fixtures")
            .EnumerateArray()
            .Where(x => x.GetProperty("expected").GetString() == "valid")
            .ToArray();
        var result = new List<(EventDefinition, GameEventEnvelope, byte[])>();
        foreach (var definition in GameEventContract.Events)
        {
            var fixture = fixtures.First(x =>
                x.GetProperty("routingKey").GetString() == definition.RoutingKey
            );
            var message = GameEventEnvelope.Parser.ParseJson(
                File.ReadAllText(Path.Combine(directory, fixture.GetProperty("file").GetString()!))
            );
            result.Add((definition, message, message.ToByteArray()));
        }

        var secondParticipant = fixtures.First(x =>
            x.GetProperty("file").GetString() == "participant-second.json"
        );
        var participantDefinition = GameEventContract.Events.Single(x =>
            x.EventType == "player.game-session-participant-joined"
        );
        var secondMessage = GameEventEnvelope.Parser.ParseJson(
            File.ReadAllText(
                Path.Combine(directory, secondParticipant.GetProperty("file").GetString()!)
            )
        );
        result.Add((participantDefinition, secondMessage, secondMessage.ToByteArray()));
        return result;
    }

    private static ProcessStartInfo CreateProbeStartInfo(
        string childAssembly,
        string queue,
        string deadLetterQueue,
        string deadLetterExchange,
        int maxDeliveryAttempts,
        string database,
        string marker
    )
    {
        var startInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        startInfo.ArgumentList.Add(childAssembly);
        startInfo.ArgumentList.Add("--probe-before-insert");
        startInfo.ArgumentList.Add("--queue");
        startInfo.ArgumentList.Add(queue);
        startInfo.ArgumentList.Add("--dead-letter-queue");
        startInfo.ArgumentList.Add(deadLetterQueue);
        startInfo.ArgumentList.Add("--dead-letter-exchange");
        startInfo.ArgumentList.Add(deadLetterExchange);
        startInfo.ArgumentList.Add("--max-delivery-attempts");
        startInfo.ArgumentList.Add(
            maxDeliveryAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture)
        );
        startInfo.ArgumentList.Add("--database");
        startInfo.ArgumentList.Add(database);
        startInfo.ArgumentList.Add("--started-marker");
        startInfo.ArgumentList.Add(marker);
        startInfo.WorkingDirectory = FindRepositoryRoot();
        return startInfo;
    }

    private static string FindRepositoryRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "Leaderboard.Presentation.slnx")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Cannot locate the repository root.");
    }

    private static async Task<BasicGetResult?> WaitForReadyDeliveryAsync(
        TestStack stack,
        TimeSpan timeout
    )
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var pending = await stack.Channel.BasicGetAsync(
                stack.Options.QueueName,
                false,
                TestContext.Current.CancellationToken
            );
            if (pending is not null)
                return pending;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        return null;
    }
}
