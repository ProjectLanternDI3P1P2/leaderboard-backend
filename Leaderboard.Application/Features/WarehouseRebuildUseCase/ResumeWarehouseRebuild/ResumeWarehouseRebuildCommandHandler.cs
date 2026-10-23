using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Exceptions;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using MediatR;

namespace Leaderboard.Application.Features.WarehouseRebuildUseCase.ResumeWarehouseRebuild;

public sealed class ResumeWarehouseRebuildCommandHandler(
    IWarehouseRebuildRepository rebuildRepository,
    IClock clock
) : IRequestHandler<ResumeWarehouseRebuildCommand, WarehouseRebuildReport>
{
    public async Task<WarehouseRebuildReport> Handle(
        ResumeWarehouseRebuildCommand request,
        CancellationToken cancellationToken
    )
    {
        WarehouseRebuild rebuild =
            await rebuildRepository.GetByIdAsync(request.RebuildId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Warehouse rebuild not found with RebuildId '{request.RebuildId}'."
            );

        if (await rebuildRepository.HasActiveAsync(cancellationToken))
        {
            throw new ConflictException("A warehouse rebuild is already pending or running.");
        }

        rebuild.Resume(clock.UtcNow);

        return WarehouseRebuildReport.From(rebuild);
    }
}
