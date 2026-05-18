namespace MBank.Api.Data;

public sealed class CodeMigration
{
    private CodeMigration(string id, DateTime appliedAt)
    {
        Id = id;
        AppliedAt = appliedAt;
    }

    public string Id { get; }

    public DateTime AppliedAt { get; }

    public static CodeMigration Create(string id)
    {
        return new CodeMigration(id, DateTime.UtcNow);
    }
}
