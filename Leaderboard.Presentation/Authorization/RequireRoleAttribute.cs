using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Leaderboard.Presentation.Authorization;

/// <summary>
/// Business authorization on the authenticated identity and global roles injected by the gateway.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRoleAttribute(string configurationKey, string defaultRole)
    : Attribute,
        IAuthorizationFilter
{
    public const string RolesHeader = "X-Roles";
    public const string UserIdHeader = "X-User-Id";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(context.HttpContext.Request.Headers[UserIdHeader]))
        {
            context.Result = Problem(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "The authenticated user identity is missing."
            );
            return;
        }

        string requiredRole =
            context
                .HttpContext.RequestServices.GetRequiredService<IConfiguration>()
                .GetValue<string>(configurationKey)
            ?? defaultRole;

        bool hasRole = context
            .HttpContext.Request.Headers[RolesHeader]
            .SelectMany(header => ReadRoles(header ?? string.Empty))
            .Any(role => string.Equals(role, requiredRole, StringComparison.Ordinal));

        if (!hasRole)
        {
            context.Result = Problem(
                context,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                $"The '{requiredRole}' role is required."
            );
        }
    }

    private static IEnumerable<string> ReadRoles(string header)
    {
        if (!header.TrimStart().StartsWith('{'))
        {
            return header.Split(',').Select(role => role.Trim());
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(header);
            if (
                document.RootElement.TryGetProperty("roles", out JsonElement roles)
                && roles.ValueKind == JsonValueKind.Array
            )
            {
                return roles
                    .EnumerateArray()
                    .Where(role => role.ValueKind == JsonValueKind.String)
                    .Select(role => role.GetString() ?? string.Empty)
                    .ToArray();
            }
        }
        catch (JsonException)
        {
            // Malformed identity metadata grants no role.
        }

        return [];
    }

    private static ObjectResult Problem(
        AuthorizationFilterContext context,
        int status,
        string title,
        string detail
    ) =>
        new(
            new ProblemDetails
            {
                Type = $"https://httpstatuses.com/{status}",
                Title = title,
                Detail = detail,
                Status = status,
                Instance = context.HttpContext.Request.Path,
            }
        )
        {
            StatusCode = status,
        };
}
