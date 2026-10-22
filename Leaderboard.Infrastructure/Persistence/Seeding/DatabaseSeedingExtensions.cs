using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Leaderboard.Infrastructure.Persistence.Seeding;

public static class DatabaseSeedingExtensions
{
    public static async Task MigrateAndSeedDevelopmentDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        LeaderboardDbContext context =
            scope.ServiceProvider.GetRequiredService<LeaderboardDbContext>();

        await context.Database.MigrateAsync(cancellationToken);
        await DataSeeder.SeedAsync(context, cancellationToken);
    }
}
