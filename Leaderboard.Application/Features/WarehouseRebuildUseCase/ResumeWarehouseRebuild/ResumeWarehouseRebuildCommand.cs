using Leaderboard.Application.Abstractions;

namespace Leaderboard.Application.Features.WarehouseRebuildUseCase.ResumeWarehouseRebuild;

public sealed record ResumeWarehouseRebuildCommand(Guid RebuildId)
    : ICommand<WarehouseRebuildReport>;
