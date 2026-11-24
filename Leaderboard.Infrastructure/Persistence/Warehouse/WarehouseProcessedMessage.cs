namespace Leaderboard.Infrastructure.Persistence.Warehouse;

/// <summary>A message ID already counted into a generation: the rebuild deduplication set.</summary>
public sealed class WarehouseProcessedMessage
{
    public Guid GenerationId { get; init; }
    public Guid MessageId { get; init; }
}
