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
builder.Services.AddScoped<IMemoryService, MemoryService>();

builder.Services
    .AddMcpServer()
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
