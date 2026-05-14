using System.Diagnostics;

namespace MBank.Api.Domain;

public sealed class Project
{
    private Project()
    {
    }

    public ProjectId Id { get; private set; } = ProjectId.New();

    public string Name { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static Project Create(string name, ProjectId? id = null)
    {
        return new Project
        {
            Id = id ?? ProjectId.New(),
            Name = RequiredText(name, "Project name", 256, nameof(name))
        };
    }

    public static string NormalizeName(string name)
    {
        return RequiredText(name, "Project name", 256, nameof(name));
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
public readonly record struct ProjectId
{
    public ProjectId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ProjectId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
