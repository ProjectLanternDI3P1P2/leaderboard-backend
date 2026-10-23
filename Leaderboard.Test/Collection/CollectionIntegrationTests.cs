using FluentAssertions;
using Google.Protobuf;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Enums;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using Leaderboard.Infrastructure.Messaging;
using Leaderboard.Infrastructure.Services;
using Leaderboard.Test.Integration.Warehouse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using RabbitMQ.Client;

namespace Leaderboard.Test.Collection;

public sealed class CollectionIntegrationTests
{
    [Fact]
    public async Task Catalogue_RabbitMqToMongoToPostgres_PreservesBytesAndProjectsEveryParticipantOnce()
    {
        await using var warehouse = await WarehouseTestHarness.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var stack = await CollectorStack.CreateAsync(warehouse.RawEventStore);
        using var host = stack.BuildHost(warehouse.RawEventStore);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await stack.WaitForConsumerAsync();
        var messages = EventFixtures.Catalogue().ToArray();
        List<byte[]> bodies = [];
        foreach (var message in messages)
        {
            // These legal extra wire bytes must not disappear through reserialization.
            byte[] body = [.. message.ToByteArray(), 8, 1, 160, 6, 42];
            bodies.Add(body);
            await stack.PublishAsync(message, body);
        }
        await stack.PublishAsync(messages[0], bodies[0]);
        await stack.PublishAsync(messages[0], [255]);
        var invalid = EventFixtures.Load("hero-created.json");
        invalid.HeroCreated.Actor.PlayerId = "";
        await stack.PublishAsync(invalid, invalid.ToByteArray());
        var unknown = EventFixtures.Load("hero-created.json");
        unknown.EnvelopeVersion = 99;
        await stack.PublishAsync(unknown, unknown.ToByteArray());
        var receipts = await stack.WaitForReceiptsAsync(20);
        receipts.Should().HaveCount(20);
        receipts.Count(r => r.ParseStatus == "Valid").Should().Be(17);
        receipts.Count(r => r.ParseStatus == "Unreadable").Should().Be(1);
        receipts.Count(r => r.ParseStatus == "InvalidOrUnknown").Should().Be(2);
        foreach (var body in bodies)
            receipts.Should().Contain(r => r.Payload.SequenceEqual(body));
        receipts
            .Where(r => r.ParseStatus == "Valid")
            .Should()
            .OnlyContain(r =>
                r.TransactionId != null
                && r.MessageId != null
                && r.Producer != null
                && r.ReceivedAt > DateTimeOffset.UtcNow.AddMinutes(-1)
            );
        receipts.Select(r => r.Id).Should().OnlyHaveUniqueItems();

        var started = await warehouse.StartAsync(TestContext.Current.CancellationToken);
        await warehouse.RunAsync(3, TestContext.Current.CancellationToken);
        var report = await warehouse.GetRebuildAsync(started.Id);
        report.Status.Should().Be(WarehouseRebuildStatus.Completed);
        report.EventsProcessed.Should().Be(20);
        report.EventsApplied.Should().Be(16);
        report.DuplicatesIgnored.Should().Be(1);
        report.EventsIgnored.Should().Be(3);
        var rows = await warehouse.GetGenerationAsync(report.GenerationId);
        rows.Players.Should().HaveCount(2);
        rows.Players.Single(p => p.PlayerId == "00000000-0000-4000-8000-000000000001")
            .TotalActions.Should()
            .Be(15);
        rows.Players.Single(p => p.PlayerId == "00000000-0000-4000-8000-000000000004")
            .TotalActions.Should()
            .Be(7);
        rows.Actions.Single(a => a.ActionType == "rewards.gold-earned")
            .ValueTotal.Should()
            .Be(messages.Single(m => m.GoldEarned != null).GoldEarned.Amount);
        rows.Actions.Single(a => a.ActionType == "progression.xp-awarded")
            .ValueTotal.Should()
            .Be(messages.Single(m => m.XpAwarded != null).XpAwarded.Amount);
        rows.Actions.Select(a => a.ActionType).Distinct().Should().HaveCount(16);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopBeforeAck_RedeliversAndPreservesAnyAlreadyInsertedCopy(bool insertFirst)
    {
        await using var warehouse = await WarehouseTestHarness.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var stack = await CollectorStack.CreateAsync(warehouse.RawEventStore);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = new Mock<IRawEventStore>();
        blocked
            .Setup(s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (IReadOnlyCollection<RawEvent> events, CancellationToken ct) =>
                {
                    if (insertFirst)
                        await warehouse.RawEventStore.AppendAsync(events, ct);
                    entered.TrySetResult();
                    await Task.Delay(Timeout.Infinite, ct);
                }
            );
        var message = EventFixtures.Load("hero-created.json");
        using (var host = stack.BuildHost(blocked.Object))
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
            await stack.WaitForConsumerAsync();
            await stack.PublishAsync(message, message.ToByteArray());
            await entered.Task.WaitAsync(
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken
            );
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
        using var restarted = stack.BuildHost(warehouse.RawEventStore);
        await restarted.StartAsync(TestContext.Current.CancellationToken);
        var receipts = await stack.WaitForReceiptsAsync(insertFirst ? 2 : 1);
        receipts.Should().OnlyContain(r => r.Payload.SequenceEqual(message.ToByteArray()));
        receipts.Should().Contain(r => r.Redelivered);
        var started = await warehouse.StartAsync(TestContext.Current.CancellationToken);
        await warehouse.RunAsync(1, TestContext.Current.CancellationToken);
        var report = await warehouse.GetRebuildAsync(started.Id);
        report.EventsApplied.Should().Be(1);
        report.DuplicatesIgnored.Should().Be(insertFirst ? 1 : 0);
        await restarted.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StoreFailure_ExhaustsBatchRetriesThenRetainsOriginalBodyInQuarantine()
    {
        await using var warehouse = await WarehouseTestHarness.CreateAsync(
            TestContext.Current.CancellationToken
        );
        await using var stack = await CollectorStack.CreateAsync(warehouse.RawEventStore);
        var failed = new Mock<IRawEventStore>();
        failed
            .Setup(s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new IOException("Unavailable"));
        using var host = stack.BuildHost(failed.Object);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await stack.WaitForConsumerAsync();
        var message = EventFixtures.Load("hero-created.json");
        await stack.PublishAsync(message, message.ToByteArray());
        BasicGetResult? quarantined = null;
        await CollectorStack.WaitAsync(async () =>
        {
            quarantined = await stack.Channel.BasicGetAsync(
                stack.Options.DeadLetterQueueName,
                true,
                TestContext.Current.CancellationToken
            );
            return quarantined is not null;
        });
        quarantined!.Body.ToArray().Should().Equal(message.ToByteArray());
        failed.Verify(
            s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class CollectorStack(
        IRawEventStore store,
        IConnection connection,
        IChannel channel
    ) : IAsyncDisposable
    {
        public IChannel Channel => channel;
        public RawEventConsumerOptions Options { get; } =
            new()
            {
                Enabled = true,
                QueueName = "merge_test_" + Guid.NewGuid().ToString("N"),
                DeadLetterQueueName = "merge_dead_" + Guid.NewGuid().ToString("N"),
            };
        private static string User =>
            Environment.GetEnvironmentVariable("LEADERBOARD_TEST_RABBITMQ_USER") ?? "leaderboard";
        private static string Password =>
            Environment.GetEnvironmentVariable("LEADERBOARD_TEST_RABBITMQ_PASSWORD")
            ?? "leaderboard";

        public static async Task<CollectorStack> CreateAsync(IRawEventStore store)
        {
            var connection = await new ConnectionFactory
            {
                HostName = "localhost",
                UserName = User,
                Password = Password,
            }.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true
                )
            );
            var result = new CollectorStack(store, connection, channel);
            await channel.QueueDeclareAsync(
                result.Options.DeadLetterQueueName,
                true,
                false,
                false,
                new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }
            );
            await channel.QueueDeclareAsync(
                result.Options.QueueName,
                true,
                false,
                false,
                new Dictionary<string, object?>
                {
                    ["x-queue-type"] = "quorum",
                    ["x-overflow"] = "reject-publish",
                    ["x-dead-letter-strategy"] = "at-least-once",
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = result.Options.DeadLetterQueueName,
                }
            );
            return result;
        }

        public IHost BuildHost(IRawEventStore target)
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RabbitMq:Enabled"] = "true",
                    ["RabbitMq:UserName"] = User,
                    ["RabbitMq:Password"] = Password,
                    ["RawEventConsumer:Enabled"] = "true",
                    ["RawEventConsumer:QueueName"] = Options.QueueName,
                    ["RawEventConsumer:DeadLetterQueueName"] = Options.DeadLetterQueueName,
                    ["RawEventConsumer:BatchTimeout"] = "00:00:00.050",
                    ["RawEventConsumer:MaxAttempts"] = "2",
                    ["RawEventConsumer:InitialBackoff"] = "00:00:00.050",
                    ["RawEventConsumer:ReconnectDelay"] = "00:00:00.100",
                }
            );
            builder
                .Services.AddSingleton<IClock, SystemClock>()
                .AddSingleton(target)
                .AddMessaging(builder.Configuration);
            return builder.Build();
        }

        public Task WaitForConsumerAsync() =>
            WaitAsync(async () =>
                (await channel.QueueDeclarePassiveAsync(Options.QueueName)).ConsumerCount > 0
            );

        public async Task PublishAsync(GameEventEnvelope message, byte[] body) =>
            await channel.BasicPublishAsync(
                message.EventType.Split('.')[0] + ".events",
                $"{message.EventType}.v{message.EventVersion}",
                true,
                new BasicProperties
                {
                    Persistent = true,
                    MessageId = message.MessageId,
                    ContentType = "application/x-protobuf",
                },
                body
            );

        public async Task<List<RawEvent>> WaitForReceiptsAsync(int count)
        {
            List<RawEvent> receipts = [];
            await WaitAsync(async () =>
            {
                receipts.Clear();
                await foreach (
                    var item in store.GetByPeriodAsync(
                        DateTimeOffset.MinValue,
                        DateTimeOffset.MaxValue,
                        TestContext.Current.CancellationToken
                    )
                )
                    receipts.Add(item);
                return receipts.Count >= count;
            });
            return receipts;
        }

        public static async Task WaitAsync(Func<Task<bool>> condition)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (!await condition())
            {
                if (DateTimeOffset.UtcNow > deadline)
                    throw new TimeoutException("Collector integration check timed out.");
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (channel.IsOpen)
            {
                await channel.QueueDeleteAsync(Options.QueueName);
                await channel.QueueDeleteAsync(Options.DeadLetterQueueName);
            }
            await channel.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
