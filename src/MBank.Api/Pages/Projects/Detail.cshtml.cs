using MBank.Api.Domain;
using MBank.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MBank.Api.Pages.Projects;

public sealed class DetailModel(
    ProjectWorkspaceService workspace,
    MemoryWorkspaceService memories) : AuthenticatedPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Project? Project { get; private set; }

    public IReadOnlyList<AccessToken> Tokens { get; private set; } = [];

    public int? MemoryCount { get; private set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string TokenName { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public string? IssuedToken { get; private set; }

    public string? OnboardingGuide { get; private set; }

    public bool OpenCreateTokenDialog { get; private set; }

    public bool OpenSettings { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Challenge();
        }

        if (!TryGetProjectId(out var projectId))
        {
            return NotFound();
        }

        Project = await workspace.GetProjectAsync(userId, projectId, cancellationToken);
        if (Project is null)
        {
            return NotFound();
        }

        Name = Project.Name;
        Tokens = await workspace.ListTokensAsync(userId, projectId, cancellationToken);
        MemoryCount = await memories.GetMemoryCountAsync(userId, projectId, cancellationToken);
        IssuedToken = TempData["IssuedToken"] as string;
        OnboardingGuide = TempData["OnboardingGuide"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostRenameAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Challenge();
        }

        if (!TryGetProjectId(out var projectId))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            return await ReloadAsync(userId, projectId, "Enter a project name.", cancellationToken, openSettings: true);
        }

        try
        {
            Project = await workspace.RenameProjectAsync(userId, projectId, Name, cancellationToken);
        }
        catch (ArgumentException)
        {
            return await ReloadAsync(userId, projectId, "Enter a project name.", cancellationToken, openSettings: true);
        }

        if (Project is null)
        {
            return NotFound();
        }

        return RedirectToPage(new { id = Project.Id.Value });
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Challenge();
        }

        if (!TryGetProjectId(out var projectId))
        {
            return NotFound();
        }

        var deleted = await workspace.DeleteProjectAsync(userId, projectId, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return RedirectToPage("/Projects/Index");
    }

    public async Task<IActionResult> OnPostCreateTokenAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Challenge();
        }

        if (!TryGetProjectId(out var projectId))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(TokenName))
        {
            return await ReloadAsync(userId, projectId, "Enter a token name.", cancellationToken, openCreateTokenDialog: true);
        }

        try
        {
            var issued = await workspace.CreateTokenAsync(userId, projectId, TokenName, cancellationToken);
            if (issued is null)
            {
                return NotFound();
            }

            TempData["IssuedToken"] = issued.Secret.Value;
            TempData["OnboardingGuide"] = "token";
            return RedirectToPage(new { id = Id });
        }
        catch (ProjectWorkspaceException ex)
        {
            return await ReloadAsync(userId, projectId, ex.Message, cancellationToken, openCreateTokenDialog: true);
        }
        catch (ArgumentException)
        {
            return await ReloadAsync(userId, projectId, "Enter a token name.", cancellationToken, openCreateTokenDialog: true);
        }
    }

    public async Task<IActionResult> OnPostDeleteTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Challenge();
        }

        if (!TryGetProjectId(out var projectId))
        {
            return NotFound();
        }

        var deleted = await workspace.DeleteTokenAsync(
            userId,
            projectId,
            new AccessTokenId(tokenId),
            cancellationToken);
        if (!deleted)
        {
            return await ReloadAsync(userId, projectId, "Token could not be deleted.", cancellationToken);
        }

        return RedirectToPage(new { id = Id });
    }

    private bool TryGetProjectId(out ProjectId projectId)
    {
        try
        {
            projectId = new ProjectId(Id);
            return true;
        }
        catch (ArgumentException)
        {
            projectId = default;
            return false;
        }
    }

    private async Task<IActionResult> ReloadAsync(
        UserId userId,
        ProjectId projectId,
        string error,
        CancellationToken cancellationToken,
        bool openCreateTokenDialog = false,
        bool openSettings = false)
    {
        Error = error;
        OpenCreateTokenDialog = openCreateTokenDialog;
        OpenSettings = openSettings;
        Project = await workspace.GetProjectAsync(userId, projectId, cancellationToken);
        if (Project is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = Project.Name;
        }

        Tokens = await workspace.ListTokensAsync(userId, projectId, cancellationToken);
        MemoryCount = await memories.GetMemoryCountAsync(userId, projectId, cancellationToken);
        return Page();
    }
}
