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

var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "latest")
    .WithEndpoint(targetPort: 1025, name: "smtp")
    .WithHttpEndpoint(targetPort: 8025, name: "http");

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
    .WithEnvironment("Smtp__Host", mailpit.GetEndpoint("smtp").Property(EndpointProperty.Host))
    .WithEnvironment("Smtp__Port", mailpit.GetEndpoint("smtp").Property(EndpointProperty.Port))
    .WithEnvironment("Smtp__FromAddress", "noreply@mbank.local")
    .WaitFor(mailpit)
    .WaitFor(migrations);

builder.Build().Run();
