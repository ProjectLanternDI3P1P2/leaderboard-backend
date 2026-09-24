using Bogus;
using Leaderboard.Domain.Entities;
using Leaderboard.Infrastructure.Persistence;
using Leaderboard.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Test.Persistence.Repositories;

public class PlayerRepositoryTests
{
    private readonly Faker _faker = new();

    private static LeaderboardDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<LeaderboardDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LeaderboardDbContext(options);
    }

    [Fact]
    public async Task AddPlayerAsync_ValidPlayer_PersistsPlayer()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var repository = new PlayerRepository(dbContext);
        var player = CreatePlayer();

        // Act
        await repository.AddPlayerAsync(player, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var storedPlayer = await dbContext.Players.FindAsync([player.Id], TestContext.Current.CancellationToken);
        storedPlayer.Should().NotBeNull();
        storedPlayer!.Name.Should().Be(player.Name);
    }

    [Fact]
    public async Task GetPlayerByIdAsync_PlayerExists_ReturnsPlayer()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var player = CreatePlayer();
        dbContext.Players.Add(player);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerRepository(dbContext);

        // Act
        var result = await repository.GetPlayerByIdAsync(player.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(player.Id);
        result.Name.Should().Be(player.Name);
    }

    [Fact]
    public async Task GetPlayerByIdAsync_PlayerDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var repository = new PlayerRepository(dbContext);

        // Act
        var result = await repository.GetPlayerByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    private Player CreatePlayer()
    {
        return new Player
        {
            Id = Guid.NewGuid(),
            Name = _faker.Name.FirstName(),
            Health = _faker.Random.Int(1, 100),
            MaxHealth = _faker.Random.Int(100, 200),
            Attack = _faker.Random.Int(1, 20)
        };
    }
}
