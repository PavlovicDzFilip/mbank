using System.Security.Cryptography;
using System.Text;
using MBank.Api.Auth;
using MBank.Api.Data;
using MBank.Api.Domain;
using MBank.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MBank.Api.Admin;

public static class AdminProvisioningEndpoints
{
    public static RouteGroupBuilder MapAdminProvisioningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin");
        group.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var adminAuth = httpContext.RequestServices.GetRequiredService<IOptions<AdminAuthOptions>>().Value;
            if (!IsAuthorized(httpContext.Request, adminAuth.Token))
            {
                return Unauthorized();
            }

            return await next(context);
        });

        group.MapPost("/users", CreateUserAsync);
        group.MapPost("/projects", CreateProjectAsync);
        group.MapPost("/project-tokens", CreateProjectTokenAsync);

        return group;
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        MemoryDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "User name is required." });
        }

        var email = NormalizeOptional(request.Email);
        if (email is not null && await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return Results.Conflict(new { error = "A user with this email already exists." });
        }

        var user = new UserAccount
        {
            Name = request.Name.Trim(),
            Email = email
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/admin/users/{user.Id}", new UserResponse(user.Id, user.Name, user.Email, user.CreatedAt));
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        MemoryDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Project name is required." });
        }

        var name = request.Name.Trim();
        if (await db.Projects.AnyAsync(x => x.Name == name, cancellationToken))
        {
            return Results.Conflict(new { error = "A project with this name already exists." });
        }

        var project = new Project
        {
            Name = name
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/admin/projects/{project.Id}", new ProjectResponse(project.Id, project.Name, project.CreatedAt));
    }

    private static async Task<IResult> CreateProjectTokenAsync(
        CreateProjectTokenRequest request,
        MemoryDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Token name is required." });
        }

        var userExists = await db.Users.AnyAsync(x => x.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            return Results.NotFound(new { error = "User was not found." });
        }

        var projectExists = await db.Projects.AnyAsync(x => x.Id == request.ProjectId, cancellationToken);
        if (!projectExists)
        {
            return Results.NotFound(new { error = "Project was not found." });
        }

        var name = request.Name.Trim();
        var duplicate = await db.AccessTokens.AnyAsync(
            x => x.UserId == request.UserId && x.ProjectId == request.ProjectId && x.Name == name,
            cancellationToken);
        if (duplicate)
        {
            return Results.Conflict(new { error = "A token with this name already exists for this user and project." });
        }

        var issuedToken = AccessToken.Create(
            request.UserId,
            request.ProjectId,
            name,
            AccessTokenPermissions.Crud);
        db.AccessTokens.Add(issuedToken.AccessToken);

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/admin/project-tokens/{issuedToken.AccessToken.Id}",
            new ProjectTokenResponse(
                issuedToken.AccessToken.Id,
                issuedToken.AccessToken.UserId,
                issuedToken.AccessToken.ProjectId,
                issuedToken.AccessToken.Name,
                issuedToken.AccessToken.Permissions,
                issuedToken.Secret,
                issuedToken.AccessToken.CreatedAt));
    }

    private static bool IsAuthorized(HttpRequest request, string? configuredToken)
    {
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return false;
        }

        var token = McpProjectTokenMiddleware.ReadBearerToken(request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(configuredToken);
        var actual = Encoding.UTF8.GetBytes(token);
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static IResult Unauthorized()
    {
        return Results.Json(
            new { error = "Admin provisioning requires Authorization: Bearer <admin-token>." },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record CreateUserRequest(string Name, string? Email = null);

public sealed record CreateProjectRequest(string Name);

public sealed record CreateProjectTokenRequest(Guid UserId, Guid ProjectId, string Name);

public sealed record UserResponse(Guid Id, string Name, string? Email, DateTimeOffset CreatedAt);

public sealed record ProjectResponse(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed record ProjectTokenResponse(
    Guid Id,
    Guid UserId,
    Guid ProjectId,
    string Name,
    AccessTokenPermissions Permissions,
    string Token,
    DateTimeOffset CreatedAt);
