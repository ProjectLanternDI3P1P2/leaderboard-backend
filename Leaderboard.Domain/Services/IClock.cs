namespace Leaderboard.Domain.Services;

// The abstraction the comment on SystemClock promises: Application and Domain
// depend on this, never on DateTimeOffset.UtcNow, so tests can move time.
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
