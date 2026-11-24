using Leaderboard.Domain.Entities;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Application.Ports;

/// <summary>Reads a stored raw event as player contributions, without exposing its wire format.</summary>
public interface IPlayerActionDecoder
{
    /// <returns>One contribution per player, or an empty list for invalid/unknown events.</returns>
    IReadOnlyList<PlayerAction> Decode(RawEvent rawEvent);
}
