namespace Leaderboard.Application.Features.WarehouseRebuildUseCase;

public sealed class WarehouseRebuildOptions
{
    public const string SectionName = "Warehouse:Rebuild";

    /// <summary>Raw events written per transaction; each batch is one checkpoint.</summary>
    public int BatchSize { get; init; } = 5000;

    public bool IsWorkerEnabled { get; init; } = true;

    /// <summary>How often an idle worker looks for a pending rebuild.</summary>
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>A running rebuild without a heartbeat for this long lost its worker.</summary>
    public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromMinutes(2);
}
