namespace Leaderboard.Domain.Entities;

/// <summary>Hero data built from real events in one warehouse generation.</summary>
public sealed class HeroRankingSnapshot
{
    public Guid GenerationId { get; set; }
    public string HeroId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string? HeroClassId { get; set; }
    public long TotalXp { get; set; }
    public int Level { get; set; } = 1;
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
}
