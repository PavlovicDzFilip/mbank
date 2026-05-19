using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace MBank.Api.Domain;

public sealed class MagicLinkLogin
{
    private MagicLinkLogin()
    {
    }

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public MagicLinkLoginId Id { get; private set; } = MagicLinkLoginId.New();

    public string Email { get; private set; } = null!;

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsConsumed => ConsumedAt is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public static IssuedMagicLinkLogin Create(string email)
    {
        var secret = MagicLinkSecret.Create();
        var now = DateTimeOffset.UtcNow;
        var login = new MagicLinkLogin
        {
            Email = UserAccount.NormalizeEmail(email) ?? throw new ArgumentException("Email is required.", nameof(email)),
            TokenHash = secret.Hash,
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime)
        };

        return new IssuedMagicLinkLogin(login, secret);
    }

    public bool Consume(string token, DateTimeOffset now)
    {
        if (IsConsumed || IsExpired(now))
        {
            return false;
        }

        var hash = MagicLinkSecret.FromPlainText(token).Hash;
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(TokenHash),
                Encoding.UTF8.GetBytes(hash)))
        {
            return false;
        }

        ConsumedAt = now;
        return true;
    }
}

public sealed record IssuedMagicLinkLogin(MagicLinkLogin Login, MagicLinkSecret Secret);

[DebuggerDisplay("{Value}")]
public sealed record MagicLinkSecret
{
    private const string TokenPrefix = "mbl_";

    private MagicLinkSecret(string value)
    {
        Value = value;
        Hash = ComputeHash(value);
    }

    public string Value { get; }

    public string Hash { get; }

    public static MagicLinkSecret Create()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var value = TokenPrefix + Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return new MagicLinkSecret(value);
    }

    public static MagicLinkSecret FromPlainText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Magic link token is required.", nameof(value));
        }

        return new MagicLinkSecret(value.Trim());
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

[DebuggerDisplay("{Value}")]
public readonly record struct MagicLinkLoginId
{
    public MagicLinkLoginId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Magic link login id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static MagicLinkLoginId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
