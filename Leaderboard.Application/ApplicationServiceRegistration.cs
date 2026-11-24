using System.Reflection;
using FluentValidation;
using Leaderboard.Application.Features.WarehouseRebuildUseCase.RunWarehouseRebuild;
using Leaderboard.Application.PipelineBehavior;
using Microsoft.Extensions.DependencyInjection;

namespace Leaderboard.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        return services
            .ConfigureMediatR()
            .ConfigureFluentValidation()
            .AddScoped<WarehouseRebuildRunner>();
    }

    private static IServiceCollection ConfigureMediatR(this IServiceCollection services)
    {
        services.AddMediatR(cf =>
        {
            cf.RegisterServicesFromAssemblies(Assembly.GetExecutingAssembly());
            cf.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cf.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        return services;
    }

    private static IServiceCollection ConfigureFluentValidation(this IServiceCollection services)
    {
        return services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
