using Leaderboard.Application.Features.WarehouseRebuildUseCase;
using Leaderboard.Application.Features.WarehouseRebuildUseCase.GetWarehouseRebuild;
using Leaderboard.Application.Features.WarehouseRebuildUseCase.ResumeWarehouseRebuild;
using Leaderboard.Application.Features.WarehouseRebuildUseCase.StartWarehouseRebuild;
using Leaderboard.Presentation.Authorization;
using Leaderboard.Presentation.DTO;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ILogger = Serilog.ILogger;

namespace Leaderboard.Presentation.Controllers;

[ApiController]
[Route("api/v1/warehouse/rebuilds")]
[RequireRole(OperatorRoleKey, DefaultOperatorRole)]
public sealed class WarehouseRebuildController(IMediator mediator, ILogger logger) : ControllerBase
{
    public const string OperatorRoleKey = "Warehouse:OperatorRole";
    public const string DefaultOperatorRole = "operator";

    /// <summary>Requests a full rebuild; it runs in the background.</summary>
    [HttpPost]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        WarehouseRebuildReport report = await mediator.Send(
            new StartWarehouseRebuildCommand(),
            cancellationToken
        );

        logger.Information("Warehouse rebuild {RebuildId} requested.", report.Id);
        return AcceptedAtAction(
            nameof(GetById),
            new { rebuildId = report.Id },
            WarehouseRebuildDto.From(report)
        );
    }

    /// <summary>Resumes an interrupted or failed rebuild from its last checkpoint.</summary>
    [HttpPost("{rebuildId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid rebuildId, CancellationToken cancellationToken)
    {
        WarehouseRebuildReport report = await mediator.Send(
            new ResumeWarehouseRebuildCommand(rebuildId),
            cancellationToken
        );

        logger.Information("Warehouse rebuild {RebuildId} resumed.", rebuildId);
        return AcceptedAtAction(
            nameof(GetById),
            new { rebuildId },
            WarehouseRebuildDto.From(report)
        );
    }

    /// <summary>The rebuild report: progress, events processed and the most recent one.</summary>
    [HttpGet("{rebuildId:guid}")]
    public async Task<IActionResult> GetById(Guid rebuildId, CancellationToken cancellationToken)
    {
        WarehouseRebuildReport report = await mediator.Send(
            new GetWarehouseRebuildQuery(rebuildId),
            cancellationToken
        );

        return Ok(WarehouseRebuildDto.From(report));
    }
}
