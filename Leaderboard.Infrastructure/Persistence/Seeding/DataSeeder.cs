using Bogus;
using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Infrastructure.Persistence.Seeding;

public static class DataSeeder
{
    public static async Task SeedAsync(
        LeaderboardDbContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (await context.Players.AnyAsync(cancellationToken))
        {
            return;
        }

        var faker = new Faker<Player>()
            .RuleFor(player => player.Id, _ => Guid.NewGuid())
            .RuleFor(player => player.Name, faker => faker.Name.FirstName())
            .RuleFor(player => player.MaxHealth, faker => faker.Random.Int(100, 200))
            .RuleFor(player => player.Health, (_, player) => player.MaxHealth)
            .RuleFor(player => player.Attack, faker => faker.Random.Int(1, 20));

        await context.Players.AddRangeAsync(faker.Generate(10), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
