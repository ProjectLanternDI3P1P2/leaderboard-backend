using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Enums;

namespace Leaderboard.Application.Features.WarehouseRebuildUseCase;

public sealed record WarehouseRebuildReport(
    Guid Id,
    WarehouseRebuildStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset ConsolidatedUntil,
    long EventsProcessed,
    long EventsApplied,
    long DuplicatesIgnored,
    long EventsIgnored,
    DateTimeOffset? LatestEventReceivedAt,
    int AttemptCount,
    string? LastError
)
{
    public static WarehouseRebuildReport From(WarehouseRebuild rebuild) =>
        new(
            rebuild.Id,
            rebuild.Status,
            rebuild.RequestedAt,
            rebuild.CompletedAt,
            rebuild.ConsolidatedUntil,
            rebuild.EventsProcessed,
            rebuild.EventsApplied,
            rebuild.DuplicatesIgnored,
            rebuild.EventsIgnored,
            rebuild.LatestEventReceivedAt,
            rebuild.AttemptCount,
            rebuild.LastError
        );
}
