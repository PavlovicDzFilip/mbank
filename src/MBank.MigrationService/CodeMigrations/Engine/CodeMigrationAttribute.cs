namespace MBank.MigrationService.CodeMigrations.Engine;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CodeMigrationAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}
