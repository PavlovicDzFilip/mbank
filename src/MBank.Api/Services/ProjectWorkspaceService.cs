using MBank.Api.Data;
using MBank.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MBank.Api.Services;

public sealed class ProjectWorkspaceService(MemoryDbContext db)
{
    public async Task<IReadOnlyList<Project>> ListProjectsAsync(
        UserId userId,
        CancellationToken cancellationToken)
    {
        return await (
            from membership in db.ProjectMemberships
            join project in db.Projects on membership.ProjectId equals project.Id
            where membership.UserId == userId
            orderby project.Name
            select project)
            .ToListAsync(cancellationToken);
    }

    public async Task<Project> CreateProjectAsync(
        UserId userId,
        string name,
        CancellationToken cancellationToken)
    {
        var project = Project.Create(name);
        db.Projects.Add(project);
        db.ProjectMemberships.Add(ProjectMembership.Create(userId, project.Id));
        await db.SaveChangesAsync(cancellationToken);

        return project;
    }

    public Task<Project?> GetProjectAsync(
        UserId userId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        return FindMemberProjectAsync(userId, projectId, cancellationToken);
    }

    public async Task<Project?> RenameProjectAsync(
        UserId userId,
        ProjectId projectId,
        string name,
        CancellationToken cancellationToken)
    {
        var project = await FindMemberProjectAsync(userId, projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        project.Rename(name);
        await db.SaveChangesAsync(cancellationToken);

        return project;
    }

    public async Task<bool> DeleteProjectAsync(
        UserId userId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var project = await FindMemberProjectAsync(userId, projectId, cancellationToken);
        if (project is null)
        {
            return false;
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AccessToken>> ListTokensAsync(
        UserId userId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, projectId, cancellationToken))
        {
            return [];
        }

        return await db.AccessTokens
            .Where(x => x.UserId == userId && x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IssuedAccessToken?> CreateTokenAsync(
        UserId userId,
        ProjectId projectId,
        string name,
        CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, projectId, cancellationToken))
        {
            return null;
        }

        string tokenName;
        try
        {
            tokenName = AccessToken.NormalizeName(name);
        }
        catch (ArgumentException ex)
        {
            throw new ProjectWorkspaceException(ex.Message);
        }

        var duplicate = await db.AccessTokens.AnyAsync(
            x => x.UserId == userId && x.ProjectId == projectId && x.Name == tokenName,
            cancellationToken);
        if (duplicate)
        {
            throw new ProjectWorkspaceException("A token with this name already exists for this project.");
        }

        var issuedToken = AccessToken.Create(
            userId,
            projectId,
            tokenName,
            AccessTokenPermissions.Crud);

        db.AccessTokens.Add(issuedToken.AccessToken);
        await db.SaveChangesAsync(cancellationToken);

        return issuedToken;
    }

    public async Task<bool> RevokeTokenAsync(
        UserId userId,
        ProjectId projectId,
        AccessTokenId tokenId,
        CancellationToken cancellationToken)
    {
        var token = await db.AccessTokens.FirstOrDefaultAsync(
            x => x.Id == tokenId && x.UserId == userId && x.ProjectId == projectId,
            cancellationToken);
        if (token is null || token.RevokedAt is not null)
        {
            return false;
        }

        token.Revoke();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Project?> FindMemberProjectAsync(
        UserId userId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, projectId, cancellationToken))
        {
            return null;
        }

        return await db.Projects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
    }

    private Task<bool> IsMemberAsync(
        UserId userId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        return db.ProjectMemberships.AnyAsync(
            x => x.UserId == userId && x.ProjectId == projectId,
            cancellationToken);
    }
}

public sealed class ProjectWorkspaceException(string message) : Exception(message);
