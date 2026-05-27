using MBank.Api.Auth;
using MBank.Api.Data;
using MBank.Api.Mcp;
using MBank.Api.Options;
using MBank.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services
    .AddOptions<OpenAiOptions>()
    .Bind(builder.Configuration.GetSection(OpenAiOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "OpenAI:ApiKey is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.EmbeddingModel), "OpenAI:EmbeddingModel is required.")
    .Validate(options => options.EmbeddingDimensions > 0, "OpenAI:EmbeddingDimensions must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Smtp:Host is required.")
    .Validate(options => options.Port > 0, "Smtp:Port must be greater than zero.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.FromAddress), "Smtp:FromAddress is required.")
    .ValidateOnStart();

builder.Services
    .AddOptions<MagicLinkOptions>()
    .Bind(builder.Configuration.GetSection(MagicLinkOptions.SectionName))
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("MemoryDb")
    ?? throw new InvalidOperationException("ConnectionStrings:MemoryDb is required.");

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.Cookie.Name = "MBank.Auth";
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Projects");
});

builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

builder.Services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<AgentInstructionService>();
builder.Services.AddScoped<MagicLinkAuthenticationService>();
builder.Services.AddScoped<ProjectWorkspaceService>();
builder.Services.AddScoped<MemoryWorkspaceService>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddScoped<MemoryScopeProvider>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInstructions = """
            MBank stores durable project memory and agent guidance for AI coding and product agents.
            At the start of any product, planning, implementation, debugging, or review task,
            call start_task and follow the returned rules for the current task.
            Call get_task_context for the current focus at task start and re-call it when focus shifts
            before substantive project-specific answers, including exploration.
            Call get_task_context when uncertain how to proceed and before specific implementation decisions
            that may depend on project history, prior decisions, constraints, or user preferences.
            Use remember only for durable guidance the user provided or explicitly agreed to store after an exploration offer.
            Treat user questions about why one approach was chosen instead of another as a strong signal
            that an assumption, preference, or rule may have been missed; offer memory capture when durable guidance emerges.
            After ideas or direction work, offer capture first; call remember only on approval. Use describe_memory_types for captureOfferPolicy and captureHint type guidance.
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

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (HttpContext httpContext) =>
{
    var destination = httpContext.User.Identity?.IsAuthenticated == true
        ? "/projects"
        : "/login";
    return Results.Redirect(destination);
});

app.MapGet("/onboarding/agent-instructions", (AgentInstructionService agentInstructions) =>
{
    var block = agentInstructions.GetInstructionBlock();
    return Results.Text(block.Content, "text/markdown");
});
app.MapGet("/bootstrap-agent.sh", (IWebHostEnvironment environment) =>
{
    return ServeScript(environment, "bootstrap-agent.sh", "text/x-shellscript");
});
app.MapGet("/bootstrap-agent.ps1", (IWebHostEnvironment environment) =>
{
    return ServeScript(environment, "bootstrap-agent.ps1", "text/plain");
});

app.MapRazorPages();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp"),
    branch => branch.UseMiddleware<McpProjectTokenMiddleware>());

app.MapMcp("/mcp");

app.Run();

static IResult ServeScript(IWebHostEnvironment environment, string fileName, string contentType)
{
    var candidates = new[]
    {
        Path.Combine(environment.WebRootPath ?? "", fileName),
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "scripts", fileName))
    };
    var scriptPath = candidates.FirstOrDefault(File.Exists);

    return scriptPath is null
        ? Results.NotFound($"{fileName} was not found.")
        : Results.File(scriptPath, contentType);
}

public partial class Program;
