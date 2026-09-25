using Bogus;
using Leaderboard.Application.Features.PlayerUseCase.CreatePlayer;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Leaderboard.Application.Messaging;
using FluentAssertions;
using Moq;

namespace Leaderboard.Test.Features.PlayerUseCase.CreatePlayer;

public class CreatePlayerCommandHandlerTests
{
    private readonly Mock<IPlayerRepository> _playerRepositoryMock = new();
    private readonly Mock<IMessagePublisher> _messagePublisherMock = new();
    private readonly CreatePlayerCommandHandler _handler;
    private readonly Faker _faker = new();

    public CreatePlayerCommandHandlerTests()
    {
        _handler = new CreatePlayerCommandHandler(_playerRepositoryMock.Object, _messagePublisherMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsMappedPlayer()
    {
        // Arrange
        var command = new CreatePlayerCommand(
            Guid.NewGuid(),
            _faker.Name.FirstName(),
            _faker.Random.Int(1, 20),
            _faker.Random.Int(1, 100),
            _faker.Random.Int(100, 200));
        Player? capturedPlayer = null;

        _playerRepositoryMock
            .Setup(repository => repository.AddPlayerAsync(It.IsAny<Player>(), It.IsAny<CancellationToken>()))
            .Callback<Player, CancellationToken>((player, _) => capturedPlayer = player)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _playerRepositoryMock.Verify(
            repository => repository.AddPlayerAsync(It.IsAny<Player>(), It.IsAny<CancellationToken>()),
            Times.Once);
        capturedPlayer.Should().NotBeNull();
        capturedPlayer!.Id.Should().Be(command.Id);
        capturedPlayer.Name.Should().Be(command.Name);
        capturedPlayer.Attack.Should().Be(command.Attack);
        capturedPlayer.Health.Should().Be(command.Health);
        capturedPlayer.MaxHealth.Should().Be(command.MaxHealth);
        _messagePublisherMock.Verify(
            publisher => publisher.PublishAsync(
                It.Is<MessageEnvelope>(message =>
                    message.Type == PlayerCreatedMessageFactory.MessageType &&
                    message.Version == PlayerCreatedMessageFactory.Version),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
