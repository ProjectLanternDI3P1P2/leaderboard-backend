using Leaderboard.Application.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leaderboard.Infrastructure.Messaging;

public static class MessagingServiceRegistration
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        RabbitMqOptions options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        Validate(options);
        services.AddSingleton(Options.Create(options));

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

        if (string.IsNullOrWhiteSpace(options.HostName) || string.IsNullOrWhiteSpace(options.ExchangeName))
        {
            throw new InvalidOperationException("RabbitMq:HostName and RabbitMq:ExchangeName are required.");
        }
    }
}
