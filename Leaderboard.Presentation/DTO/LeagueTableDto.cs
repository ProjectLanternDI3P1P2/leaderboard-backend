using Leaderboard.Application.Features.StatisticsUseCase.GetLeagueTable;

namespace Leaderboard.Presentation.DTO;

public sealed record LeagueTableDto(
    int Page,
    int PageSize,
    long TotalPlayers,
    DateTimeOffset? ConsolidatedUntil,
    IReadOnlyList<LeagueTableEntryDto> Entries
)
{
    public static LeagueTableDto From(GetLeagueTableResult result) =>
        new(
            result.Page,
            result.PageSize,
            result.TotalPlayers,
            result.ConsolidatedUntil,
            [
                .. result.Entries.Select(entry => new LeagueTableEntryDto(
                    entry.Rank,
                    entry.PlayerId,
                    entry.TotalActions,
                    entry.LastActionAt
                )),
            ]
        );
}

public sealed record LeagueTableEntryDto(
    long Rank,
    string PlayerId,
    long TotalActions,
    DateTimeOffset LastActionAt
);
