namespace Leaderboard.Domain.Enums;

// Example of a finite domain state. Use enums for known business states
// instead of repeating magic strings such as "pending" or "completed".
public enum CombatStatus
{
    Pending,
    Active,
    Completed,
    Failed
}
