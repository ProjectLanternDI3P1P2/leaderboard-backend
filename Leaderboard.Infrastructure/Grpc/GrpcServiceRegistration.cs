using Combat.Contracts.V1;
using Leaderboard.Infrastructure.Grpc.Clients;
using Leaderboard.Infrastructure.Grpc.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leaderboard.Infrastructure.Grpc;

public static class GrpcServiceRegistration
{
    public static IServiceCollection AddGrpcConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        PlayerGrpcClientOptions playerGrpcClientOptions = configuration
            .GetSection(PlayerGrpcClientOptions.SectionName)
            .Get<PlayerGrpcClientOptions>() ?? new PlayerGrpcClientOptions();

        ValidatePlayerGrpcClientOptions(playerGrpcClientOptions);

        return services
            .AddSingleton(Options.Create(playerGrpcClientOptions))
            .AddGrpcClient<CombatPlayerService.CombatPlayerServiceClient>(options =>
                options.Address = new Uri(playerGrpcClientOptions.Address))
            .Services
            .Scan(scan => scan
                .FromAssembliesOf(typeof(GrpcServiceRegistration))
                .AddClasses(classes => classes.Where(c => c.Name.EndsWith("GrpcClient")))
                .AsImplementedInterfaces()
                .WithScopedLifetime());
    }

    private static void ValidatePlayerGrpcClientOptions(PlayerGrpcClientOptions options)
    {
        if (!Uri.TryCreate(options.Address, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Grpc:Player:Address must be an absolute URI.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Grpc:Player:TimeoutSeconds must be greater than zero.");
        }
    }
}
