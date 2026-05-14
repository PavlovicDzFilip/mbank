using MBank.Api.Data;
using MBank.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MBank.Api.Auth;

public sealed class McpProjectTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, MemoryDbContext db, MemoryScopeProvider currentScope)
    {
        var token = ReadBearerToken(httpContext.Request);
        if (token is null)
        {
            await WriteUnauthorizedAsync(httpContext, "MBank MCP requires a project access token. Send Authorization: Bearer <project-token>.");
            return;
        }

        var tokenHash = AccessTokenSecret.FromPlainText(token).Hash;
        var accessToken = await db.AccessTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash && x.RevokedAt == null, httpContext.RequestAborted);
        if (accessToken is null)
        {
            await WriteUnauthorizedAsync(httpContext, "Invalid or revoked MBank project access token.");
            return;
        }

        currentScope.Set(new AuthenticatedMemoryScope(
            accessToken.UserId,
            accessToken.ProjectId,
            accessToken.Permissions));
        await next(httpContext);
    }

    public static string? ReadBearerToken(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        const string prefix = "Bearer ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }

    private static Task WriteUnauthorizedAsync(HttpContext httpContext, string message)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return httpContext.Response.WriteAsJsonAsync(new { error = message });
    }
}
