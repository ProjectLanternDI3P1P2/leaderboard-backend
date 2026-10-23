namespace Leaderboard.Domain.Exceptions;

/// <summary>The request contradicts the current state of a resource.</summary>
public sealed class ConflictException(string message) : Exception(message);
