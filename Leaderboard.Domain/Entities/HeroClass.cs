namespace Leaderboard.Domain.Entities;

public sealed class HeroClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Hero> Heroes { get; } = [];
}
