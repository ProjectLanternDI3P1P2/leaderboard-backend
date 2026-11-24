using FluentAssertions;
using Leaderboard.Presentation.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leaderboard.Test.Presentation.Authorization;

public sealed class RequireRoleAttributeTests
{
    [Theory]
    [InlineData("operator", true)]
    [InlineData("player,operator", true)]
    [InlineData("{\"roles\":[\"player\",\"operator\"]}", true)]
    [InlineData("{\"roles\":[\"player\"]}", false)]
    [InlineData("{\"roles\":[\"operator\"", false)]
    [InlineData("superoperator", false)]
    public void OnAuthorization_RequiresExactOperatorRole(string roles, bool authorized)
    {
        AuthorizationFilterContext context = CreateContext("authenticated-user", roles);

        new RequireRoleAttribute("Warehouse:OperatorRole", "operator").OnAuthorization(context);

        if (authorized)
        {
            context.Result.Should().BeNull();
        }
        else
        {
            context
                .Result.Should()
                .BeOfType<ObjectResult>()
                .Which.StatusCode.Should()
                .Be(StatusCodes.Status403Forbidden);
        }
    }

    [Fact]
    public void OnAuthorization_RejectsMissingIdentityBeforeCheckingRole()
    {
        AuthorizationFilterContext context = CreateContext(null, "operator");

        new RequireRoleAttribute("Warehouse:OperatorRole", "operator").OnAuthorization(context);

        context
            .Result.Should()
            .BeOfType<ObjectResult>()
            .Which.StatusCode.Should()
            .Be(StatusCodes.Status401Unauthorized);
    }

    private static AuthorizationFilterContext CreateContext(string? userId, string roles)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
                .BuildServiceProvider(),
        };
        if (userId is not null)
        {
            httpContext.Request.Headers[RequireRoleAttribute.UserIdHeader] = userId;
        }
        httpContext.Request.Headers[RequireRoleAttribute.RolesHeader] = roles;

        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            []
        );
    }
}
