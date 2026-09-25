namespace Leaderboard.Domain.Entities;

public sealed class Achievement
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid StatisticId { get; set; }
    public string Operator { get; set; } = string.Empty;
    public int Threshold { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Statistic? Statistic { get; set; }
    public ICollection<HeroAchievement> HeroAchievements { get; } = [];
}
