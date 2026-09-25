namespace Leaderboard.Domain.Entities;

public sealed class Statistic
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool CountsInScore { get; set; }
    public int Multiplier { get; set; } = 1;
    public bool IsSortable { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<HeroStatistic> HeroStatistics { get; } = [];
    public ICollection<Achievement> Achievements { get; } = [];
}
