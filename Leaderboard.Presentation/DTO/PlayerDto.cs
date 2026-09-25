namespace Leaderboard.Presentation.DTO;

public class PlayerDto
{
    public required Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public required int Health { get; set; }
    public required int MaxHealth { get; set; }
    public required int Attack { get; set; }
}
