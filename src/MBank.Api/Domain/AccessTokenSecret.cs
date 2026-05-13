using System.Security.Cryptography;
using System.Text;

namespace MBank.Api.Domain;

public sealed record AccessTokenSecret
{
    private const string TokenPrefix = "mbp_";

    private AccessTokenSecret(string value)
    {
        Value = value;
        Hash = ComputeHash(value);
    }

    public string Value { get; }

    public string Hash { get; }

    public static AccessTokenSecret Create()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var value = TokenPrefix + Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return new AccessTokenSecret(value);
    }

    public static AccessTokenSecret FromPlainText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Token secret is required.", nameof(value));
        }

        return new AccessTokenSecret(value.Trim());
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
