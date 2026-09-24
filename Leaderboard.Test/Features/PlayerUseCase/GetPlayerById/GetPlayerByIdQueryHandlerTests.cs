using Bogus;
using Leaderboard.Application.Features.PlayerUseCase.GetPlayerById;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace Leaderboard.Test.Features.PlayerUseCase.GetPlayerById;

public class GetPlayerByIdQueryHandlerTests
{
    private readonly Mock<IPlayerRepository> _playerRepositoryMock = new();
    private readonly GetPlayerByIdQueryHandler _handler;
    private readonly Faker _faker = new();

    public GetPlayerByIdQueryHandlerTests()
    {
        _handler = new GetPlayerByIdQueryHandler(_playerRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_PlayerExists_ReturnsMappedResult()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player
        {
            Id = playerId,
            Name = _faker.Name.FirstName(),
            Health = _faker.Random.Int(1, 100),
            MaxHealth = _faker.Random.Int(100, 200),
            Attack = _faker.Random.Int(1, 20)
        };

        _playerRepositoryMock
            .Setup(repository => repository.GetPlayerByIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);

        // Act
        var result = await _handler.Handle(new GetPlayerByIdQuery(playerId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(player.Id);
        result.Name.Should().Be(player.Name);
        result.Health.Should().Be(player.Health);
        result.MaxHealth.Should().Be(player.MaxHealth);
        result.Attack.Should().Be(player.Attack);
    }

    [Fact]
    public async Task Handle_PlayerDoesNotExist_ThrowsKeyNotFoundException()
    {
        // Arrange
        var playerId = Guid.NewGuid();

        _playerRepositoryMock
            .Setup(repository => repository.GetPlayerByIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Player?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(new GetPlayerByIdQuery(playerId), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{playerId}*");
    }
}
