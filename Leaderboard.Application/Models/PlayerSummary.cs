namespace Leaderboard.Application.Models;

/// <summary>Player data required by a consuming use case, independent of transport contracts.</summary>
public sealed class PlayerSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Health { get; init; }
    public int MaxHealth { get; init; }
    public int Attack { get; init; }
}
