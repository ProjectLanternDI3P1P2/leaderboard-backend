namespace Leaderboard.Domain.Entities;

/// <summary>Warehouse totals of one player in one generation. The league table ranks on <see cref="TotalActions"/>.</summary>
public class PlayerStatistics
{
    public Guid GenerationId { get; init; }
    public string PlayerId { get; init; } = string.Empty;
    public long TotalActions { get; init; }
    public decimal ValueTotal { get; init; }
    public DateTimeOffset LastActionAt { get; init; }
}
