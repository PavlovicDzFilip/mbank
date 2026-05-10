using MBank.Api.Data;
using MBank.Api.Mcp;
using MBank.Api.Options;
using MBank.Api.Services;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

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
builder.Services.AddSingleton<IAgentInstructionService, AgentInstructionService>();
builder.Services.AddScoped<IMemoryService, MemoryService>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInstructions = """
            MBank stores durable project memory for AI coding and product agents. At the start of any product, planning, implementation, debugging, or review task, call get_task_context before proposing a solution or making changes. Use remember only for durable product or implementation guidance that should matter in future sessions. Use get when a retrieved memory needs source evidence. Use report_conflict when memories disagree and surface the conflict to the user. Use check_onboarding to verify that repo agent instructions are installed and current. If MBank reports missing or outdated agent instruction files, notify the user and ask for explicit approval before updating files or running the bootstrap script.
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

app.MapMcp("/mcp");

app.Run();

public partial class Program;
