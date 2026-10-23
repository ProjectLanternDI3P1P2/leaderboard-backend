using System.Runtime.CompilerServices;
using Leaderboard.Application.Features.StatisticsUseCase.GetLeagueTable;
using Leaderboard.Application.Features.StatisticsUseCase.GetPlayerStatistics;
using Leaderboard.Application.Features.WarehouseRebuildUseCase;
using Leaderboard.Application.Features.WarehouseRebuildUseCase.ResumeWarehouseRebuild;
using Leaderboard.Application.Features.WarehouseRebuildUseCase.RunWarehouseRebuild;
using Leaderboard.Application.Features.WarehouseRebuildUseCase.StartWarehouseRebuild;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using Leaderboard.Infrastructure.Persistence;
using Leaderboard.Infrastructure.Persistence.Mongo;
using Leaderboard.Infrastructure.Persistence.Repositories;
using Leaderboard.Infrastructure.Persistence.Warehouse;
using Leaderboard.Infrastructure.Services;
using Leaderboard.Infrastructure.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leaderboard.Test.Integration.Warehouse;

/// <summary>
/// Real PostgreSQL and MongoDB, wired by hand: every operation gets its own DbContext,
/// like a request or a worker iteration gets its own scope.
/// </summary>
public sealed class WarehouseTestHarness : IAsyncDisposable
{
    private readonly TestDatabase postgres;
    private readonly MongoTestDatabase mongo;
    private readonly IClock clock = new SystemClock();

    private WarehouseTestHarness(TestDatabase postgres, MongoTestDatabase mongo)
    {
        this.postgres = postgres;
        this.mongo = mongo;
        RawEventStore = new MongoRawEventStore(
            mongo.Client,
            Options.Create(
                new MongoOptions
                {
                    ConnectionString = MongoTestDatabase.ConnectionString,
                    DatabaseName = mongo.DatabaseName,
                    CollectionName = MongoTestDatabase.CollectionName,
                }
            )
        );
    }

    public MongoRawEventStore RawEventStore { get; }

    public static async Task<WarehouseTestHarness> CreateAsync(CancellationToken cancellationToken)
    {
        TestDatabase postgres = await TestDatabase.CreateAsync(
            $"leaderboard_test_warehouse_{Guid.NewGuid():N}"
        );
        MongoTestDatabase mongo = await MongoTestDatabase.CreateAsync(cancellationToken);
        return new WarehouseTestHarness(postgres, mongo);
    }

    public LeaderboardDbContext CreateDbContext() =>
        new(
            new DbContextOptionsBuilder<LeaderboardDbContext>()
                .UseNpgsql(postgres.ConnectionString)
                .Options
        );

    public async Task<WarehouseRebuildReport> StartAsync(CancellationToken cancellationToken)
    {
        await using LeaderboardDbContext dbContext = CreateDbContext();
        WarehouseRebuildReport report = await new StartWarehouseRebuildCommandHandler(
            new WarehouseRebuildRepository(dbContext),
            clock
        ).Handle(new StartWarehouseRebuildCommand(), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return report;
    }

    public async Task<WarehouseRebuildReport> ResumeAsync(
        Guid rebuildId,
        CancellationToken cancellationToken
    )
    {
        await using LeaderboardDbContext dbContext = CreateDbContext();
        WarehouseRebuildReport report = await new ResumeWarehouseRebuildCommandHandler(
            new WarehouseRebuildRepository(dbContext),
            clock
        ).Handle(new ResumeWarehouseRebuildCommand(rebuildId), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return report;
    }

    /// <summary>Runs the pending rebuild, as one worker iteration does.</summary>
    public async Task RunAsync(
        int batchSize,
        CancellationToken cancellationToken,
        IRawEventStore? rawEventStore = null
    )
    {
        await using LeaderboardDbContext dbContext = CreateDbContext();
        await new WarehouseRebuildRunner(
            new PostgresWarehouseStore(dbContext),
            rawEventStore ?? RawEventStore,
            new GameEventPlayerActionDecoder(),
            clock,
            Options.Create(new WarehouseRebuildOptions { BatchSize = batchSize }),
            NullLogger<WarehouseRebuildRunner>.Instance
        ).RunNextAsync(cancellationToken);
    }

    public async Task<WarehouseRebuild> GetRebuildAsync(Guid rebuildId)
    {
        await using LeaderboardDbContext dbContext = CreateDbContext();
        return await dbContext.WarehouseRebuilds.AsNoTracking().SingleAsync(r => r.Id == rebuildId);
    }

    public async Task<GetLeagueTableResult> GetLeagueTableAsync(int pageSize = 100)
    {
        await using LeaderboardDbContext dbContext = CreateDbContext();
        return await new GetLeagueTableQueryHandler(new WarehouseReadRepository(dbContext)).Handle(
            new GetLeagueTableQuery(1, pageSize),
            CancellationToken.None
        );
    }

    public async Task<GetPlayerStatisticsResult> GetPlayerStatisticsAsync(string playerId)
    {
        await using LeaderboardDbContext dbContext = CreateDbContext();
        return await new GetPlayerStatisticsQueryHandler(
            new WarehouseReadRepository(dbContext)
        ).Handle(new GetPlayerStatisticsQuery(playerId), CancellationToken.None);
    }

    /// <summary>Every warehouse row of a generation, without its generation ID, for comparison.</summary>
    public async Task<WarehouseContent> GetGenerationAsync(Guid generationId)
    {
        await using LeaderboardDbContext dbContext = CreateDbContext();
        return new WarehouseContent(
            [
                .. await dbContext
                    .PlayerStatistics.AsNoTracking()
                    .Where(s => s.GenerationId == generationId)
                    .OrderBy(s => s.PlayerId)
                    .Select(s => new PlayerRow(
                        s.PlayerId,
                        s.TotalActions,
                        s.ValueTotal,
                        s.LastActionAt
                    ))
                    .ToListAsync(),
            ],
            [
                .. await dbContext
                    .PlayerActionStatistics.AsNoTracking()
                    .Where(s => s.GenerationId == generationId)
                    .OrderBy(s => s.PlayerId)
                    .ThenBy(s => s.ActionType)
                    .Select(s => new ActionRow(
                        s.PlayerId,
                        s.ActionType,
                        s.ActionCount,
                        s.ValueTotal
                    ))
                    .ToListAsync(),
            ]
        );
    }

    public async ValueTask DisposeAsync()
    {
        await mongo.DisposeAsync();
        await postgres.DisposeAsync();
    }
}

public sealed record WarehouseContent(
    IReadOnlyList<PlayerRow> Players,
    IReadOnlyList<ActionRow> Actions
);

public sealed record PlayerRow(
    string PlayerId,
    long TotalActions,
    decimal ValueTotal,
    DateTimeOffset LastActionAt
);

public sealed record ActionRow(
    string PlayerId,
    string ActionType,
    long ActionCount,
    decimal ValueTotal
);

/// <summary>Cancels the rebuild after a given number of events, like a pod being stopped.</summary>
public sealed class InterruptingRawEventStore(
    IRawEventStore inner,
    int interruptAfter,
    CancellationTokenSource stop
) : IRawEventStore
{
    public Task AppendAsync(
        IReadOnlyCollection<RawEvent> events,
        CancellationToken cancellationToken
    ) => inner.AppendAsync(events, cancellationToken);

    public async IAsyncEnumerable<RawEvent> GetByPeriodAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        int read = 0;
        await foreach (RawEvent rawEvent in inner.GetByPeriodAsync(from, to, cancellationToken))
        {
            yield return rawEvent;
            if (++read == interruptAfter)
            {
                await stop.CancelAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
