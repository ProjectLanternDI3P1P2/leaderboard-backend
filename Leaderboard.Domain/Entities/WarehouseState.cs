namespace Leaderboard.Domain.Entities;

/// <summary>
/// Which generation reads use. A rebuild writes a new generation and switches this
/// single row once it is complete, so reads never see a partial warehouse.
/// </summary>
public class WarehouseState
{
    public const int SingletonId = 1;

    public int Id { get; init; } = SingletonId;
    public Guid? ActiveGenerationId { get; set; }

    /// <summary>Kept until the next switch, so a read started before the switch still finds its rows.</summary>
    public Guid? PreviousGenerationId { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>The active generation contains every event received before this instant.</summary>
    public DateTimeOffset? ConsolidatedUntil { get; set; }
}
