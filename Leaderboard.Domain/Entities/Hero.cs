namespace Leaderboard.Domain.Entities;

public sealed class Hero
{
    public Guid Id { get; set; }
    public Guid? PlayerId { get; set; }
    public int ClassId { get; set; }
    public string Pseudo { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Score { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public HeroClass? Class { get; set; }
    public ICollection<HeroStatistic> HeroStatistics { get; } = [];
    public ICollection<HeroAchievement> HeroAchievements { get; } = [];
}
