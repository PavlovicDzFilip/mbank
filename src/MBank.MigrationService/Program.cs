using MBank.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MemoryDb")
    ?? throw new InvalidOperationException("ConnectionStrings:MemoryDb is required.");

builder.Services.AddDbContext<MemoryDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));


var app = builder.Build();
await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger(nameof(MBank));
logger.LogInformation("Applying MBank database migrations.");
await db.Database.MigrateAsync();
logger.LogInformation("MBank database migrations are up to date.");

