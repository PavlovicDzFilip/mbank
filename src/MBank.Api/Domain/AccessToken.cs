namespace MBank.Api.Domain;

public sealed class AccessToken
{
    private AccessToken()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = "";

    public string TokenHash { get; private set; } = "";

    public AccessTokenPermissions Permissions { get; private set; } = AccessTokenPermissions.Crud;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAt { get; private set; }

    public static IssuedAccessToken Create(
        Guid userId,
        Guid projectId,
        string name,
        AccessTokenPermissions permissions,
        string? secret = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Token name is required.", nameof(name));
        }

        if (permissions == AccessTokenPermissions.None)
        {
            throw new ArgumentException("Token permissions are required.", nameof(permissions));
        }

        var tokenSecret = string.IsNullOrWhiteSpace(secret)
            ? AccessTokenSecret.Create()
            : AccessTokenSecret.FromPlainText(secret);
        var accessToken = new AccessToken
        {
            UserId = userId,
            ProjectId = projectId,
            Name = name.Trim(),
            Permissions = permissions,
            TokenHash = tokenSecret.Hash
        };

        return new IssuedAccessToken(accessToken, tokenSecret.Value);
    }
}

public sealed record IssuedAccessToken(AccessToken AccessToken, string Secret);
