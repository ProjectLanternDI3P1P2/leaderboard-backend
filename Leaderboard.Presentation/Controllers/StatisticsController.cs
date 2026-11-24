using Leaderboard.Application.Features.StatisticsUseCase.GetLeagueTable;
using Leaderboard.Application.Features.StatisticsUseCase.GetPlayerStatistics;
using Leaderboard.Presentation.DTO;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Leaderboard.Presentation.Controllers;

/// <summary>Reads the last consolidated warehouse generation, never a rebuild in progress.</summary>
[ApiController]
[Route("api/v1")]
public sealed class StatisticsController(IMediator mediator) : ControllerBase
{
    [HttpGet("players/{playerId}/statistics")]
    public async Task<IActionResult> GetPlayerStatistics(
        string playerId,
        CancellationToken cancellationToken
    )
    {
        GetPlayerStatisticsResult result = await mediator.Send(
            new GetPlayerStatisticsQuery(playerId),
            cancellationToken
        );

        return Ok(PlayerStatisticsDto.From(result));
    }

    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeagueTable(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20
    )
    {
        GetLeagueTableResult result = await mediator.Send(
            new GetLeagueTableQuery(page, pageSize),
            cancellationToken
        );

        return Ok(LeagueTableDto.From(result));
    }
}
