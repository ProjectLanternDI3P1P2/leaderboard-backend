using FluentAssertions;
using Leaderboard.Application.Features.WarehouseRebuildUseCase;
using Leaderboard.Application.Features.WarehouseRebuildUseCase.StartWarehouseRebuild;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Enums;
using Leaderboard.Domain.Exceptions;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using Moq;

namespace Leaderboard.Test.Warehouse;

public class StartWarehouseRebuildCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IWarehouseRebuildRepository> _repositoryMock = new();
    private readonly StartWarehouseRebuildCommandHandler _handler;

    public StartWarehouseRebuildCommandHandlerTests()
    {
        Mock<IClock> clock = new();
        clock.Setup(c => c.UtcNow).Returns(Now);
        _handler = new StartWarehouseRebuildCommandHandler(_repositoryMock.Object, clock.Object);
    }

    [Fact]
    public async Task Handle_NoActiveRebuild_AddsAPendingRebuild()
    {
        // Arrange
        WarehouseRebuild? added = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<WarehouseRebuild>(), It.IsAny<CancellationToken>()))
            .Callback<WarehouseRebuild, CancellationToken>((rebuild, _) => added = rebuild)
            .Returns(Task.CompletedTask);

        // Act
        WarehouseRebuildReport report = await _handler.Handle(
            new StartWarehouseRebuildCommand(),
            TestContext.Current.CancellationToken
        );

        // Assert
        added.Should().NotBeNull();
        report.Id.Should().Be(added!.Id);
        report.Status.Should().Be(WarehouseRebuildStatus.Pending);
        report.ConsolidatedUntil.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_RebuildAlreadyActive_ThrowsConflictException()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.HasActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Func<Task> start = () =>
            _handler.Handle(
                new StartWarehouseRebuildCommand(),
                TestContext.Current.CancellationToken
            );

        // Assert
        await start.Should().ThrowAsync<ConflictException>();
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<WarehouseRebuild>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
