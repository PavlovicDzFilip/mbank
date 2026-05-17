using System.Reflection;
using System.Text.RegularExpressions;

namespace MBank.MigrationService.CodeMigrations.Engine;

public class CodeMigrationDescriptor(string id, Type type)
{
    public string Id { get; } = id;
    public Type Type { get; } = type;

    public static CodeMigrationDescriptor Create(Type type)
    {
        var attribute = type.GetCustomAttribute<CodeMigrationAttribute>() ??
                        throw new InvalidOperationException(
                            $"Type {type.FullName} does not have a {nameof(CodeMigrationAttribute)}.");

        if (!MigrationIdRegex.IsMatch(attribute.Id))
        {
            throw new InvalidOperationException($"Invalid migration ID format: {attribute.Id}");
        }

        return new CodeMigrationDescriptor(attribute.Id, type);
    }

    // A regex that validates the format is "yyyyMMddHHmmss_description", where "description" is a lowercase alphanumeric string that can contain underscores.
    private static readonly Regex MigrationIdRegex = new(@"^\d{14}_[a-z0-9]+(?:_[a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
}

public class CodeMigration
{
    public string Id { get; }
    public DateTime AppliedAt { get; }

    private CodeMigration(string id, DateTime appliedAt)
    {
        Id = id;
        AppliedAt = appliedAt;
    }

    public static CodeMigration Create(CodeMigrationDescriptor migrationDescriptor) =>
        new(migrationDescriptor.Id, DateTime.UtcNow);
}
