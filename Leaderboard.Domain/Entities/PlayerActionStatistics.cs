namespace Leaderboard.Domain.Entities;

/// <summary>Warehouse totals of one player for one action type, in one generation.</summary>
public class PlayerActionStatistics
{
    public Guid GenerationId { get; init; }
    public string PlayerId { get; init; } = string.Empty;
    public string ActionType { get; init; } = string.Empty;
    public long ActionCount { get; init; }
    public decimal ValueTotal { get; init; }
}
