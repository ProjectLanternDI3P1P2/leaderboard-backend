using FluentAssertions;
using Leaderboard.Application.Messaging;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using Leaderboard.Infrastructure.Messaging;
using Leaderboard.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leaderboard.Test.Persistence;

public sealed class ServiceRegistrationTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void AddMongoEventStore_UsesTheConnectionStringsEntry()
    {
        var services = new ServiceCollection()
            .AddOptions()
            .AddSingleton(NullLogger<MongoRawEventStore>.Instance);

        services.AddMongoEventStore(
            Configuration(
                ("ConnectionStrings:Mongo", "mongodb://mongo-host:27018/?directConnection=true")
            )
        );
        using ServiceProvider provider = services.BuildServiceProvider();

        provider
            .GetRequiredService<IOptions<MongoOptions>>()
            .Value.ConnectionString.Should()
            .Be("mongodb://mongo-host:27018/?directConnection=true");
        provider.GetRequiredService<IRawEventStore>().Should().BeOfType<MongoRawEventStore>();
    }

    [Fact]
    public void AddMongoEventStore_KeepsTheDefaultsWithoutConfiguration()
    {
        var services = new ServiceCollection().AddMongoEventStore(Configuration());
        using ServiceProvider provider = services.BuildServiceProvider();

        MongoOptions options = provider.GetRequiredService<IOptions<MongoOptions>>().Value;

        options.DatabaseName.Should().Be("progression_lake");
        options.CollectionName.Should().Be("raw_events");
    }

    [Fact]
    public void AddMongoEventStore_RejectsAnEmptyCollectionName()
    {
        Action register = () =>
            new ServiceCollection().AddMongoEventStore(
                Configuration(("Mongo:CollectionName", " "))
            );

        register
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Mongo:ConnectionString*");
    }

    [Fact]
    public void AddMessaging_RegistersTheConsumerOnlyWhenEnabled()
    {
        var enabled = new ServiceCollection().AddMessaging(
            Configuration(("RabbitMq:Enabled", "true"), ("RawEventConsumer:Enabled", "true"))
        );
        var disabled = new ServiceCollection().AddMessaging(Configuration());

        enabled.Should().Contain(d => d.ImplementationType == typeof(RabbitMqRawEventConsumer));
        enabled.Should().Contain(d => d.ServiceType == typeof(IMessagePublisher));
        disabled.Should().NotContain(d => d.ServiceType == typeof(IHostedService));
        disabled
            .Single(d => d.ServiceType == typeof(IMessagePublisher))
            .ImplementationType.Should()
            .Be(typeof(NoOpMessagePublisher));
    }

    [Fact]
    public void AddMessaging_RejectsTheConsumerWithoutTheBroker()
    {
        Action register = () =>
            new ServiceCollection().AddMessaging(
                Configuration(("RawEventConsumer:Enabled", "true"))
            );

        register
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*requires RabbitMq:Enabled*");
    }

    [Theory]
    [InlineData("RawEventConsumer:BatchSize", "0")]
    [InlineData("RawEventConsumer:MaxAttempts", "0")]
    [InlineData("RawEventConsumer:QueueName", " ")]
    public void AddMessaging_RejectsInvalidConsumerSettings(string key, string value)
    {
        Action register = () =>
            new ServiceCollection().AddMessaging(
                Configuration(
                    ("RabbitMq:Enabled", "true"),
                    ("RawEventConsumer:Enabled", "true"),
                    (key, value)
                )
            );

        register.Should().Throw<InvalidOperationException>().WithMessage("*RawEventConsumer*");
    }
}
