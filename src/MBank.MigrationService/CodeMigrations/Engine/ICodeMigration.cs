namespace MBank.MigrationService.CodeMigrations.Engine;

public interface ICodeMigration
{
    Task RunAsync();
}