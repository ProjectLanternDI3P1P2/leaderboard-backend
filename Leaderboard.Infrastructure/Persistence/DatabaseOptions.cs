namespace Leaderboard.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public string? DefaultConnection { get; init; }
    public string? PasswordFile { get; init; }
}
