using MediatR;

namespace Leaderboard.Application.Features.WarehouseRebuildUseCase.GetWarehouseRebuild;

public sealed record GetWarehouseRebuildQuery(Guid RebuildId) : IRequest<WarehouseRebuildReport>;
