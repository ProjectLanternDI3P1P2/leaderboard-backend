using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Leaderboard.Presentation.Authorization;

/// <summary>
/// Business authorization on the global roles the gateway injects in <c>X-Roles</c>
/// (ADR-GLOB-004): the gateway has already authenticated the caller.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRoleAttribute(string configurationKey, string defaultRole)
    : Attribute,
        IAuthorizationFilter
{
    public const string RolesHeader = "X-Roles";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        string requiredRole =
            context
                .HttpContext.RequestServices.GetRequiredService<IConfiguration>()
                .GetValue<string>(configurationKey)
            ?? defaultRole;

        bool hasRole = context
            .HttpContext.Request.Headers[RolesHeader]
            .SelectMany(header => (header ?? string.Empty).Split(','))
            .Any(role => string.Equals(role.Trim(), requiredRole, StringComparison.Ordinal));

        if (!hasRole)
        {
            context.Result = new ObjectResult(
                new ProblemDetails
                {
                    Type = "https://httpstatuses.com/403",
                    Title = "Forbidden",
                    Detail = $"The '{requiredRole}' role is required.",
                    Status = StatusCodes.Status403Forbidden,
                    Instance = context.HttpContext.Request.Path,
                }
            )
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
    }
}
