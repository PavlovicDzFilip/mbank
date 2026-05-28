using MBank.Api.Data;
using MBank.Api.Domain;
using MBank.Api.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MBank.Api.Services;

public sealed class ProjectOpenAiCredentialService(
    MemoryDbContext db,
    IDataProtectionProvider dataProtection,
    IOptions<OpenAiOptions> options)
{
    private const string ProtectorPurpose = "MBank.ProjectOpenAiCredential.ApiKey.v1";
    private const int MaxApiKeyLength = 2048;
    private readonly IDataProtector _protector = dataProtection.CreateProtector(ProtectorPurpose);
    private readonly OpenAiOptions _options = options.Value;

    public async Task StoreProjectApiKeyAsync(
        ProjectId projectId,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        var normalizedApiKey = NormalizeApiKey(apiKey);
        if (normalizedApiKey is null)
        {
            return;
        }

        var encryptedApiKey = _protector.Protect(normalizedApiKey);
        var suffix = normalizedApiKey[^Math.Min(normalizedApiKey.Length, 4)..];
        var existing = await db.ProjectOpenAiCredentials.FirstOrDefaultAsync(
            x => x.ProjectId == projectId,
            cancellationToken);

        if (existing is null)
        {
            db.ProjectOpenAiCredentials.Add(ProjectOpenAiCredential.Create(projectId, encryptedApiKey, suffix));
            return;
        }

        existing.Replace(encryptedApiKey, suffix);
    }

    public async Task<string> ResolveApiKeyAsync(ProjectId projectId, CancellationToken cancellationToken)
    {
        var credential = await db.ProjectOpenAiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

        return credential is null
            ? _options.ApiKey
            : _protector.Unprotect(credential.EncryptedApiKey);
    }

    private static string? NormalizeApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length > MaxApiKeyLength)
        {
            throw new ProjectWorkspaceException($"OpenAI API key cannot exceed {MaxApiKeyLength} characters.");
        }

        return trimmed;
    }
}
