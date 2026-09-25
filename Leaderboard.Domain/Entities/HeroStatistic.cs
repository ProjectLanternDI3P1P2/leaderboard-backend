namespace Leaderboard.Domain.Entities;

public sealed class HeroStatistic
{
    public Guid HeroId { get; set; }
    public Guid StatisticId { get; set; }
    public int Value { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Hero? Hero { get; set; }
    public Statistic? Statistic { get; set; }
}
