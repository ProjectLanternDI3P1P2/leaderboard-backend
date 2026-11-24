namespace Leaderboard.Domain.Enums;

public enum WarehouseRebuildStatus
{
    /// <summary>Requested or resumed, waiting for a worker.</summary>
    Pending,
    Running,

    /// <summary>Stopped before the end; resumable from its checkpoint.</summary>
    Interrupted,

    /// <summary>Stopped by an error; resumable from its checkpoint.</summary>
    Failed,

    /// <summary>Finished and activated: reads now use its generation.</summary>
    Completed,
}
