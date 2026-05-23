using System.Diagnostics;
using System.Net.Mail;

namespace MBank.Api.Domain;

public sealed class UserAccount
{
    private UserAccount()
    {
    }

    public UserId Id { get; private set; } = UserId.New();

    public string Name { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static UserAccount Create(string name, string email, UserId? id = null)
    {
        return new UserAccount
        {
            Id = id ?? UserId.New(),
            Name = RequiredText(name, "User name", 256, nameof(name)),
            Email = NormalizeEmail(email)
        };
    }

    public static string NormalizeEmail(string email)
    {
        var normalized = RequiredText(email, "User email", 320, nameof(email));

        try
        {
            var address = new MailAddress(normalized);
            if (!string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("User email must be a valid email address.", nameof(email));
            }
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("User email must be a valid email address.", nameof(email), ex);
        }

        return normalized.ToLowerInvariant();
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

    private static string? OptionalText(string? value, string displayName, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{displayName} cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }
}

[DebuggerDisplay("{Value}")]
public readonly record struct UserId
{
    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static UserId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
