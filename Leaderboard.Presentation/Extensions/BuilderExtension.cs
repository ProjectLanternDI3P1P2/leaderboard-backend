using System.Text.Json.Serialization;
using Leaderboard.Presentation.Extensions.LogExtension;
using Leaderboard.Presentation.Middleware;
using Serilog;
using Serilog.Templates;

namespace Leaderboard.Presentation.Extensions;

public static class BuilderExtension
{
    private const string ConsoleJsonTemplate =
        """
        { {
            timestamp: @t,
            level: level,
            service: 'leaderboard-backend',
            origin: 'backend',
            message: @m,
            properties: Rest(),
            exception: if @x is not null then {
                type: TypeOf(@x),
                message: Inspect(@x).Message,
                stack: Inspect(@x).StackTrace
            } else undefined()
        } }
        """ + "\n";

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
                    .Enrich.With<SensitiveDataEnricher>()
                    .Destructure.With<IgnoreLoggingDestructuringPolicy>()
                    .WriteTo.Console(new ExpressionTemplate(ConsoleJsonTemplate));
            },
            preserveStaticLogger: true
        );
    }
}
