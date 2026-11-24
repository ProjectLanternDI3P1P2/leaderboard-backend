using Leaderboard.Application.Features.HeroRanking;
using Leaderboard.Presentation.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Leaderboard.Test.HeroRanking;

public sealed class HeroRankingControllerTests
{
    [Fact]
    public async Task Statistics_for_another_player_answer_forbidden_without_an_authentication_scheme()
    {
        var controller = new HeroRankingController(Mock.Of<IHeroRankingQueries>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.Request.Headers["X-User-Id"] = "authenticated-player";

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
}
