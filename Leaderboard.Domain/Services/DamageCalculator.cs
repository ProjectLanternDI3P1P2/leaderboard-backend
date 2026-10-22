namespace Leaderboard.Domain.Services;

// Example of a domain service. Put calculation rules here when the rule
// belongs to the business model but does not naturally fit on one entity.
public sealed class DamageCalculator
{
    public static int CalculateDamage(int attack, int defense)
    {
        return Math.Max(1, attack - defense);
    }
}
