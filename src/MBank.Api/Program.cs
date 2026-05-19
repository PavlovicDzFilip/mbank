using MBank.Api.Admin;
using MBank.Api.Auth;
using MBank.Api.Data;
using MBank.Api.Mcp;
using MBank.Api.Options;
using MBank.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services
    .AddOptions<AdminAuthOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        options.Token = configuration["MBANK_ADMIN_TOKEN"];
    })
    .Validate(options => !string.IsNullOrWhiteSpace(options.Token), "MBANK_ADMIN_TOKEN is required.")
    .ValidateOnStart();

builder.Services
    .AddOptions<OpenAiOptions>()
    .Bind(builder.Configuration.GetSection(OpenAiOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "OpenAI:ApiKey is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.EmbeddingModel), "OpenAI:EmbeddingModel is required.")
    .Validate(options => options.EmbeddingDimensions > 0, "OpenAI:EmbeddingDimensions must be greater than zero.")
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("MemoryDb")
    ?? throw new InvalidOperationException("ConnectionStrings:MemoryDb is required.");

builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

builder.Services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();
builder.Services.AddSingleton<AgentInstructionService>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddScoped<MemoryScopeProvider>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInstructions = """
            MBank stores durable project memory and agent guidance for AI coding and product agents.
            At the start of any product, planning, implementation, debugging, or review task,
            call start_task and follow the returned rules for the current task.
            Then call get_task_context before proposing a solution or making changes.
            Use remember only for durable product or implementation guidance that should matter in future sessions, and use only valid MemoryType values returned by start_task.
            Use get when a retrieved memory needs source evidence.
            Use report_conflict when memories disagree and surface the conflict to the user.
            Use resolve_conflict when the user clarifies which conflicting memory should remain authoritative.
            Use check_onboarding to verify that repo agent instructions are installed and current.
            If MBank reports missing or outdated agent instruction files, notify the user and ask for explicit approval before updating files or running the bootstrap script.
            """;
    })
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly(typeof(MemoryTools).Assembly);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "MBank",
    status = "running",
    mcp = "/mcp"
}));

app.MapGet("/onboarding/agent-instructions", (AgentInstructionService agentInstructions) =>
{
    var block = agentInstructions.GetInstructionBlock();
    return Results.Text(block.Content, "text/markdown");
});

app.MapAdminProvisioningEndpoints();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp"),
    branch => branch.UseMiddleware<McpProjectTokenMiddleware>());

app.MapMcp("/mcp");

app.Run();

public partial class Program;
