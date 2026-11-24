namespace Leaderboard.Domain.Entities;

/// <summary>One persisted catalogue statistic for a hero.</summary>
public sealed class HeroStatisticValue
{
    public const string DevelopmentSeedOrigin = "development-seed";

    public string HeroId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public long Value { get; set; }
    public string Origin { get; set; } = string.Empty;
}
