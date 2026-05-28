using System.Diagnostics;

namespace MBank.Api.Domain;

public sealed class ProjectOpenAiCredential
{
    private ProjectOpenAiCredential()
    {
    }

    public ProjectOpenAiCredentialId Id { get; private set; } = ProjectOpenAiCredentialId.New();

    public ProjectId ProjectId { get; private set; }

    public string EncryptedApiKey { get; private set; } = null!;

    public string ApiKeySuffix { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static ProjectOpenAiCredential Create(ProjectId projectId, string encryptedApiKey, string apiKeySuffix)
    {
        return new ProjectOpenAiCredential
        {
            ProjectId = projectId,
            EncryptedApiKey = RequiredText(encryptedApiKey, "Encrypted OpenAI API key", 4096, nameof(encryptedApiKey)),
            ApiKeySuffix = RequiredText(apiKeySuffix, "OpenAI API key suffix", 16, nameof(apiKeySuffix))
        };
    }

    public void Replace(string encryptedApiKey, string apiKeySuffix)
    {
        EncryptedApiKey = RequiredText(encryptedApiKey, "Encrypted OpenAI API key", 4096, nameof(encryptedApiKey));
        ApiKeySuffix = RequiredText(apiKeySuffix, "OpenAI API key suffix", 16, nameof(apiKeySuffix));
        UpdatedAt = DateTimeOffset.UtcNow;
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

[DebuggerDisplay("{Value}")]
public readonly record struct ProjectOpenAiCredentialId
{
    public ProjectOpenAiCredentialId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Project OpenAI credential id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ProjectOpenAiCredentialId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
