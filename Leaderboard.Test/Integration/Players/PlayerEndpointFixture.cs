namespace Leaderboard.Test.Integration.Players;

public sealed class PlayerEndpointFixture : IAsyncLifetime
{
    private TestDatabase? database;
    private LeaderboardWebApplicationFactory? factory;

    public HttpClient HttpClient =>
        factory?.CreateClient() ?? throw new InvalidOperationException("Fixture not initialized.");

    public async ValueTask InitializeAsync()
    {
        database = await TestDatabase.CreateAsync($"leaderboard_test_players_{Guid.NewGuid():N}");
        factory = new LeaderboardWebApplicationFactory(database.ConnectionString);
        await database.ResetAsync();
    }

    public async ValueTask DisposeAsync()
    {
        factory?.Dispose();
        if (database is not null)
            await database.DisposeAsync();
    }
}
