using Leaderboard.Application.Features.WarehouseRebuildUseCase;
using Leaderboard.Domain.Enums;

namespace Leaderboard.Presentation.DTO;

public sealed record WarehouseRebuildDto(
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
    public static WarehouseRebuildDto From(WarehouseRebuildReport report) =>
        new(
            report.Id,
            report.Status,
            report.RequestedAt,
            report.CompletedAt,
            report.ConsolidatedUntil,
            report.EventsProcessed,
            report.EventsApplied,
            report.DuplicatesIgnored,
            report.EventsIgnored,
            report.LatestEventReceivedAt,
            report.AttemptCount,
            report.LastError
        );
}
