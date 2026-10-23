using Leaderboard.Domain.Entities;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Application.Ports;

/// <summary>Reads a stored raw event as a player action, without exposing its wire format.</summary>
public interface IPlayerActionDecoder
{
    /// <returns><c>null</c> when the payload cannot be decoded or names no player.</returns>
    PlayerAction? Decode(RawEvent rawEvent);
}
