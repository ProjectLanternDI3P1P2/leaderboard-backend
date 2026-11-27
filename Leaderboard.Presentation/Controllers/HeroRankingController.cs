using Leaderboard.Application.Features.HeroRanking;
using Microsoft.AspNetCore.Mvc;

namespace Leaderboard.Presentation.Controllers;

[ApiController]
[Route("api/leaderboard/v1")]
public sealed class HeroRankingController(IHeroRankingQueries queries) : ControllerBase
{
    [HttpGet("heroes")]
    public async Task<IActionResult> GetHeroes(
        CancellationToken cancellationToken,
        [FromQuery(Name = "class")] string? heroClass = null,
        [FromQuery(Name = "period")] string period = "all-time",
        [FromQuery(Name = "search")] string? search = null,
        [FromQuery(Name = "sort")] string sort = "total-xp",
        [FromQuery(Name = "page")] int page = 1,
        [FromQuery(Name = "pageSize")] int pageSize = 10
    )
    {
        string? authenticatedPlayer = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authenticatedPlayer))
            return Unauthorized();
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest("Invalid pagination.");
        if (heroClass is not null && heroClass is not ("warrior" or "shaman" or "mage"))
            return BadRequest("Unknown hero class.");
        if (period is not ("all-time" or "season" or "week"))
            return BadRequest("Unknown period.");
        if (sort is not ("total-xp" or "level" or "win-rate" or "last-activity"))
            return BadRequest("Unknown sort.");

        return Ok(
            await queries.GetPageAsync(
                authenticatedPlayer,
                heroClass,
                period,
                search,
                sort,
                page,
                pageSize,
                cancellationToken
            )
        );
    }

    [HttpGet("public")]
    public async Task<PublicHeroRanking> GetPublic(CancellationToken cancellationToken) =>
        await queries.GetPublicAsync(cancellationToken);

    [HttpGet("heroes/{heroId}")]
    public async Task<IActionResult> GetHero(string heroId, CancellationToken cancellationToken)
    {
        string? authenticatedPlayer = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authenticatedPlayer))
            return Unauthorized();
        HeroRankingDetail? detail = await queries.GetDetailAsync(heroId, cancellationToken);
        if (detail is null)
            return NotFound();
        return Ok(
            detail.PlayerId == authenticatedPlayer ? detail with { PlayerId = "me" } : detail
        );
    }

    [HttpGet("heroes/{heroId}/facts")]
    public async Task<IActionResult> GetFacts(string heroId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Request.Headers["X-User-Id"]))
            return Unauthorized();
        HeroRankingFacts? facts = await queries.GetFactsAsync(heroId, cancellationToken);
        return facts is null ? NotFound() : Ok(facts);
    }

    [HttpGet("players/{playerId}/heroes/{heroId}/statistics")]
    public async Task<IActionResult> GetHeroStatistics(
        string playerId,
        string heroId,
        CancellationToken cancellationToken
    )
    {
        string? authenticatedPlayer = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authenticatedPlayer))
            return Unauthorized();
        if (playerId != "me" && playerId != authenticatedPlayer)
            return StatusCode(StatusCodes.Status403Forbidden);
        HeroRankingDetail? detail = await queries.GetDetailAsync(heroId, cancellationToken);
        if (detail is not null && detail.PlayerId != authenticatedPlayer)
            return StatusCode(StatusCodes.Status403Forbidden);
        return Ok(await queries.GetStatisticsAsync(heroId, cancellationToken));
    }
}
