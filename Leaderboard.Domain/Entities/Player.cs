namespace Leaderboard.Domain.Entities;

public class Player
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int Attack { get; set; }
}
