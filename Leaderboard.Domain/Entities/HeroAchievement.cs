namespace Leaderboard.Domain.Entities;

public sealed class HeroAchievement
{
    public Guid AchievementId { get; set; }
    public Guid HeroId { get; set; }
    public DateTimeOffset ObtainedAt { get; set; }

    public Achievement? Achievement { get; set; }
    public Hero? Hero { get; set; }
}
