using Leaderboard.Presentation.Middleware;
using Scalar.AspNetCore;

namespace Leaderboard.Presentation.Extensions;

public static class ApplicationExtension
{
    public static WebApplication ConfigureStart(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.MapControllers();
        app.MapGrpcServices();
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(opt =>
            {
                opt.Title = "Leaderboard API";
                opt.Theme = ScalarTheme.DeepSpace;
            });
        }

        return app;
    }
}
