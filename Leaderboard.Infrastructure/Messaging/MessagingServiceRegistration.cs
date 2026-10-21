using Leaderboard.Application.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leaderboard.Infrastructure.Messaging;

public static class MessagingServiceRegistration
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        RabbitMqOptions options =
            configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();
        Validate(options);
        services.AddSingleton(Options.Create(options));

        RawEventConsumerOptions consumerOptions =
            configuration
                .GetSection(RawEventConsumerOptions.SectionName)
                .Get<RawEventConsumerOptions>()
            ?? new RawEventConsumerOptions();
        ValidateConsumer(options, consumerOptions);
        services.AddSingleton(Options.Create(consumerOptions));
        if (consumerOptions.Enabled)
        {
            services
                .AddSingleton<RawEventBatchWriter>()
                .AddSingleton<RawEventBatchProcessor>()
                .AddHostedService<RabbitMqRawEventConsumer>();
        }

        return options.Enabled
            ? services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>()
            : services.AddSingleton<IMessagePublisher, NoOpMessagePublisher>();
    }

    private static void Validate(RabbitMqOptions options)
    {
        if (options.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("RabbitMq:Port must be between 1 and 65535.");
        }

        if (
            string.IsNullOrWhiteSpace(options.HostName)
            || string.IsNullOrWhiteSpace(options.ExchangeName)
        )
        {
            throw new InvalidOperationException(
                "RabbitMq:HostName and RabbitMq:ExchangeName are required."
            );
        }
    }

    private static void ValidateConsumer(RabbitMqOptions rabbitMq, RawEventConsumerOptions options)
    {
        if (!options.Enabled)
        {
            return;
        }

        if (!rabbitMq.Enabled)
        {
            throw new InvalidOperationException(
                "RawEventConsumer:Enabled requires RabbitMq:Enabled."
            );
        }

        if (
            options.BatchSize is < 1 or > ushort.MaxValue
            || options.MaxAttempts < 1
            || string.IsNullOrWhiteSpace(options.QueueName)
            || string.IsNullOrWhiteSpace(options.DeadLetterQueueName)
            || options.SourceExchanges.Any(string.IsNullOrWhiteSpace)
        )
        {
            throw new InvalidOperationException(
                "RawEventConsumer: BatchSize, MaxAttempts and the queue names must be valid."
            );
        }
    }
}
