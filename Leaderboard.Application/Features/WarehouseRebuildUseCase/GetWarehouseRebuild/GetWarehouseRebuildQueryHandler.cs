using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using MediatR;

namespace Leaderboard.Application.Features.WarehouseRebuildUseCase.GetWarehouseRebuild;

public sealed class GetWarehouseRebuildQueryHandler(IWarehouseRebuildRepository rebuildRepository)
    : IRequestHandler<GetWarehouseRebuildQuery, WarehouseRebuildReport>
{
    public async Task<WarehouseRebuildReport> Handle(
        GetWarehouseRebuildQuery request,
        CancellationToken cancellationToken
    )
    {
        WarehouseRebuild rebuild =
            await rebuildRepository.GetByIdAsync(request.RebuildId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Warehouse rebuild not found with RebuildId '{request.RebuildId}'."
            );

        return WarehouseRebuildReport.From(rebuild);
    }
}
