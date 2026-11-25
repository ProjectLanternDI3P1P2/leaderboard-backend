namespace Leaderboard.Domain.Entities;

/// <summary>A hero ranking row. Development data is seeded until hero events feed this projection.</summary>
public sealed class HeroRanking
{
    public string HeroId { get; set; } = string.Empty;
    public string HeroName { get; set; } = string.Empty;
    public string HeroClass { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public long AllTimeXp { get; set; }
    public long? SeasonXp { get; set; }
    public long? WeekXp { get; set; }
    public int Level { get; set; }
    public int Runs { get; set; }
    public double WinRate { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset SeededAt { get; set; }
    public bool IsPending { get; set; }
    public int? RankDelta { get; set; }
}
