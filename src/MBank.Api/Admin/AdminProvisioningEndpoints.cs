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
        UserAccount user;
        try
        {
            var email = UserAccount.NormalizeEmail(request.Email);
            if (email is not null && await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            {
                return Results.Conflict(new { error = "A user with this email already exists." });
            }

            user = UserAccount.Create(request.Name, request.Email);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/admin/users/{user.Id.Value}",
            new UserResponse(user.Id.Value, user.Name, user.Email, user.CreatedAt));
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        MemoryDbContext db,
        CancellationToken cancellationToken)
    {
        string name;
        try
        {
            name = Project.NormalizeName(request.Name);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        if (await db.Projects.AnyAsync(x => x.Name == name, cancellationToken))
        {
            return Results.Conflict(new { error = "A project with this name already exists." });
        }

        var project = Project.Create(name);

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/admin/projects/{project.Id.Value}",
            new ProjectResponse(project.Id.Value, project.Name, project.CreatedAt));
    }

    private static async Task<IResult> CreateProjectTokenAsync(
        CreateProjectTokenRequest request,
        MemoryDbContext db,
        CancellationToken cancellationToken)
    {
        UserId userId;
        ProjectId projectId;
        string name;
        try
        {
            userId = new UserId(request.UserId);
            projectId = new ProjectId(request.ProjectId);
            name = AccessToken.NormalizeName(request.Name);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        var userExists = await db.Users.AnyAsync(x => x.Id == userId, cancellationToken);
        if (!userExists)
        {
            return Results.NotFound(new { error = "User was not found." });
        }

        var projectExists = await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken);
        if (!projectExists)
        {
            return Results.NotFound(new { error = "Project was not found." });
        }

        var duplicate = await db.AccessTokens.AnyAsync(
            x => x.UserId == userId && x.ProjectId == projectId && x.Name == name,
            cancellationToken);
        if (duplicate)
        {
            return Results.Conflict(new { error = "A token with this name already exists for this user and project." });
        }

        var issuedToken = AccessToken.Create(
            userId,
            projectId,
            name,
            AccessTokenPermissions.Crud);
        var membershipExists = await db.ProjectMemberships.AnyAsync(
            x => x.UserId == userId && x.ProjectId == projectId,
            cancellationToken);
        if (!membershipExists)
        {
            db.ProjectMemberships.Add(ProjectMembership.Create(userId, projectId));
        }

        db.AccessTokens.Add(issuedToken.AccessToken);

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/admin/project-tokens/{issuedToken.AccessToken.Id.Value}",
            new ProjectTokenResponse(
                issuedToken.AccessToken.Id.Value,
                issuedToken.AccessToken.UserId.Value,
                issuedToken.AccessToken.ProjectId.Value,
                issuedToken.AccessToken.Name,
                issuedToken.AccessToken.Permissions,
                issuedToken.AccessToken.TokenPrefix,
                issuedToken.AccessToken.TokenSuffix,
                issuedToken.Secret.Value,
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
    string TokenPrefix,
    string TokenSuffix,
    string Token,
    DateTimeOffset CreatedAt);
