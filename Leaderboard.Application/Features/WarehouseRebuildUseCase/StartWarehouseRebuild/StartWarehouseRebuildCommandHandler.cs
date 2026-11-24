using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Exceptions;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using MediatR;

namespace Leaderboard.Application.Features.WarehouseRebuildUseCase.StartWarehouseRebuild;

public sealed class StartWarehouseRebuildCommandHandler(
    IWarehouseRebuildRepository rebuildRepository,
    IClock clock
) : IRequestHandler<StartWarehouseRebuildCommand, WarehouseRebuildReport>
{
    public async Task<WarehouseRebuildReport> Handle(
        StartWarehouseRebuildCommand request,
        CancellationToken cancellationToken
    )
    {
        if (await rebuildRepository.HasActiveAsync(cancellationToken))
        {
            throw new ConflictException("A warehouse rebuild is already pending or running.");
        }

        // The worker picks it up once this command commits.
        WarehouseRebuild rebuild = WarehouseRebuild.Request(clock.UtcNow);
        await rebuildRepository.AddAsync(rebuild, cancellationToken);

        return WarehouseRebuildReport.From(rebuild);
    }
}
