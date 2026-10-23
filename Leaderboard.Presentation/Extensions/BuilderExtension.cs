using System.Text.Json.Serialization;
using Leaderboard.Presentation.Extensions.LogExtension;
using Leaderboard.Presentation.Middleware;
using Serilog;

namespace Leaderboard.Presentation.Extensions;

public static class BuilderExtension
{
    public static WebApplicationBuilder ConfigureApi(this WebApplicationBuilder builder)
    {
        // String enums in HTTP JSON (ADR-0035).
        builder
            .Services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
            );
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();
        builder.Services.AddHealthChecks();

        ConfigureLogger(builder);

        builder.Services.AddTransient<ExceptionHandlingMiddleware>();
        builder.Services.AddHttpClient();

        return builder;
    }

    private static void ConfigureLogger(WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog(
            (context, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.With<LowercaseLevelEnricher>()
                    .Destructure.With<IgnoreLoggingDestructuringPolicy>();
            },
            preserveStaticLogger: true
        );
    }
}
