using Leaderboard.Application.Abstractions;

namespace Leaderboard.Application.Features.WarehouseRebuildUseCase.StartWarehouseRebuild;

public sealed record StartWarehouseRebuildCommand : ICommand<WarehouseRebuildReport>;
