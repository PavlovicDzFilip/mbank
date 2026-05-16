var builder = DistributedApplication.CreateBuilder(args);

var adminToken = builder.AddParameterFromConfiguration(
    "mbankAdminToken",
    "Parameters:mbankAdminToken",
    secret: true);
var openAiApiKey = builder.AddParameterFromConfiguration(
    "openaiApiKey",
    "Parameters:openaiApiKey",
    secret: true);

var postgresPassword = builder.AddParameterFromConfiguration(
    "postgresPassword",
    "Parameters:postgresPassword",
    secret: true);


var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithImage("pgvector/pgvector", "pg16")
    .WithDataVolume(isReadOnly: false);

var memoryDb = postgres.AddDatabase("MemoryDb", "mbank");

var migrations = builder.AddProject<Projects.MBank_MigrationService>("migrate", options =>
    {
        options.ExcludeLaunchProfile = true;
    })
    .WithReference(memoryDb)
    .WaitFor(postgres);

builder.AddProject<Projects.MBank_Api>("api", options =>
    {
        options.LaunchProfileName = "aspire";
    })
    .WithReference(memoryDb)
    .WithEnvironment("MBANK_ADMIN_TOKEN", adminToken)
    .WithEnvironment("OpenAI__ApiKey", openAiApiKey)
    .WaitFor(migrations);

builder.Build().Run();
