using Leaderboard.Application.Features.HeroRanking;
using Leaderboard.Presentation.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Leaderboard.Test.HeroRanking;

public sealed class HeroRankingControllerTests
{
    [Fact]
    public async Task Heroes_without_identity_answer_unauthorized()
    {
        var controller = CreateController(Mock.Of<IHeroRankingQueries>());

        IActionResult result = await controller.GetHeroes(TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Theory]
    [InlineData("rogue", "all-time", "total-xp", 1)]
    [InlineData(null, "unknown", "total-xp", 1)]
    [InlineData(null, "all-time", "unknown", 1)]
    [InlineData(null, "all-time", "total-xp", 0)]
    public async Task Heroes_with_invalid_filters_answer_bad_request(
        string? heroClass,
        string period,
        string sort,
        int page
    )
    {
        var controller = CreateController(Mock.Of<IHeroRankingQueries>(), "player-1");

        IActionResult result = await controller.GetHeroes(
            TestContext.Current.CancellationToken,
            heroClass,
            period,
            null,
            sort,
            page
        );

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Heroes_with_identity_return_the_authenticated_players_page()
    {
        var queries = new Mock<IHeroRankingQueries>();
        var page = new HeroRankingPage(
            null,
            "live",
            new RankingStatusDetail(),
            new RankingSeason("season-3", "Season 3", DateTimeOffset.UtcNow, null),
            1,
            10,
            0,
            [],
            []
        );
        queries
            .Setup(query =>
                query.GetPageAsync(
                    "player-1",
                    null,
                    "all-time",
                    null,
                    "total-xp",
                    1,
                    10,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(page);
        var controller = CreateController(queries.Object, "player-1");

        IActionResult result = await controller.GetHeroes(TestContext.Current.CancellationToken);

        Assert.Same(page, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Heroes_forward_all_filter_and_pagination_parameters()
    {
        var queries = new Mock<IHeroRankingQueries>();
        queries
            .Setup(query =>
                query.GetPageAsync(
                    "player-1",
                    "mage",
                    "season",
                    "Rune",
                    "win-rate",
                    2,
                    25,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new HeroRankingPage(
                    null,
                    "live",
                    new RankingStatusDetail(),
                    new RankingSeason("season-3", "Season 3", DateTimeOffset.UtcNow, null),
                    2,
                    25,
                    0,
                    [],
                    []
                )
            );
        var controller = CreateController(queries.Object, "player-1");

        IActionResult result = await controller.GetHeroes(
            TestContext.Current.CancellationToken,
            "mage",
            "season",
            "Rune",
            "win-rate",
            2,
            25
        );

        Assert.IsType<OkObjectResult>(result);
        queries.Verify(
            query =>
                query.GetPageAsync(
                    "player-1",
                    "mage",
                    "season",
                    "Rune",
                    "win-rate",
                    2,
                    25,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Hero_detail_uses_me_alias_only_for_its_owner()
    {
        var queries = new Mock<IHeroRankingQueries>();
        queries
            .Setup(query => query.GetDetailAsync("hero-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HeroRankingDetail(
                    "hero-1",
                    "Aldric",
                    "warrior",
                    9,
                    0,
                    DateTimeOffset.UtcNow,
                    0,
                    100,
                    [],
                    0,
                    0,
                    "player-1"
                )
            );
        var controller = CreateController(queries.Object, "player-1");

        IActionResult result = await controller.GetHero(
            "hero-1",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            "me",
            Assert.IsType<HeroRankingDetail>(Assert.IsType<OkObjectResult>(result).Value).PlayerId
        );
    }

    [Fact]
    public async Task Hero_detail_unknown_id_answers_not_found()
    {
        var controller = CreateController(Mock.Of<IHeroRankingQueries>(), "player-1");

        IActionResult result = await controller.GetHero(
            "missing",
            TestContext.Current.CancellationToken
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Statistics_for_own_hero_return_backend_values()
    {
        var queries = new Mock<IHeroRankingQueries>();
        queries
            .Setup(query => query.GetDetailAsync("hero-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HeroRankingDetail(
                    "hero-1",
                    "Aldric",
                    "warrior",
                    9,
                    0,
                    DateTimeOffset.UtcNow,
                    0,
                    100,
                    [],
                    0,
                    0,
                    "player-1"
                )
            );
        var statistics = new HeroStatistics("hero-1", null, false, true, [new("games_played", 4)]);
        queries
            .Setup(query => query.GetStatisticsAsync("hero-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(statistics);
        var controller = CreateController(queries.Object, "player-1");

        IActionResult result = await controller.GetHeroStatistics(
            "me",
            "hero-1",
            TestContext.Current.CancellationToken
        );

        Assert.Same(statistics, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Statistics_for_another_players_hero_answer_forbidden()
    {
        var queries = new Mock<IHeroRankingQueries>();
        queries
            .Setup(query => query.GetDetailAsync("hero-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new HeroRankingDetail(
                    "hero-1",
                    "Aldric",
                    "warrior",
                    9,
                    0,
                    DateTimeOffset.UtcNow,
                    0,
                    100,
                    [],
                    0,
                    0,
                    "player-2"
                )
            );
        var controller = CreateController(queries.Object, "player-1");

        IActionResult result = await controller.GetHeroStatistics(
            "me",
            "hero-1",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(result).StatusCode
        );
        queries.Verify(
            query => query.GetStatisticsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Statistics_for_another_player_answer_forbidden_without_an_authentication_scheme()
    {
        var controller = CreateController(Mock.Of<IHeroRankingQueries>(), "authenticated-player");

        IActionResult result = await controller.GetHeroStatistics(
            "another-player",
            "hero-1",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(result).StatusCode
        );
    }

    private static HeroRankingController CreateController(
        IHeroRankingQueries queries,
        string? playerId = null
    )
    {
        var controller = new HeroRankingController(queries)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        if (playerId is not null)
            controller.Request.Headers["X-User-Id"] = playerId;
        return controller;
    }
}
