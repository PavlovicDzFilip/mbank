using System.Diagnostics;

namespace MBank.Api.Domain;

public sealed class AccessToken
{
    private AccessToken()
    {
    }

    public AccessTokenId Id { get; private set; } = AccessTokenId.New();

    public UserId UserId { get; private set; }

    public ProjectId ProjectId { get; private set; }

    public string Name { get; private set; } = null!;

    public string TokenHash { get; private set; } = null!;

    public string TokenPrefix { get; private set; } = null!;

    public string TokenSuffix { get; private set; } = null!;

    public AccessTokenPermissions Permissions { get; private set; } = AccessTokenPermissions.Crud;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedAt { get; private set; }

    public static IssuedAccessToken Create(
        UserId userId,
        ProjectId projectId,
        string name,
        AccessTokenPermissions permissions,
        string? secret = null)
    {
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
            Name = NormalizeName(name),
            Permissions = permissions,
            TokenHash = RequiredText(tokenSecret.Hash, "Token hash", 128, nameof(tokenSecret)),
            TokenPrefix = RequiredText(tokenSecret.Prefix, "Token prefix", 16, nameof(tokenSecret)),
            TokenSuffix = RequiredText(tokenSecret.Suffix, "Token suffix", 16, nameof(tokenSecret))
        };

        return new IssuedAccessToken(accessToken, tokenSecret);
    }

    public void Revoke(DateTimeOffset? revokedAt = null)
    {
        RevokedAt ??= revokedAt ?? DateTimeOffset.UtcNow;
    }

    public static string NormalizeName(string name)
    {
        return RequiredText(name, "Token name", 256, nameof(name));
    }

    private static string RequiredText(string value, string displayName, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} is required.", parameterName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{displayName} cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }
}

public sealed record IssuedAccessToken(AccessToken AccessToken, AccessTokenSecret Secret);

[DebuggerDisplay("{Value}")]
public readonly record struct AccessTokenId
{
    public AccessTokenId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Access token id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static AccessTokenId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
