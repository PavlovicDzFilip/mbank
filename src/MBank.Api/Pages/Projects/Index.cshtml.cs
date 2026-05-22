using System.ComponentModel.DataAnnotations;
using MBank.Api.Domain;
using MBank.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MBank.Api.Pages.Projects;

public sealed class IndexModel(ProjectWorkspaceService workspace) : AuthenticatedPageModel
{
    public IReadOnlyList<Project> Projects { get; private set; } = [];

    [BindProperty]
    [Required]
    [Display(Name = "Project name")]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Include default agent guidance")]
    public bool SeedStarterPack { get; set; } = true;

    public string? Error { get; private set; }

    public bool ShowCreateProject { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Challenge();
        }

        Projects = await workspace.ListProjectsAsync(userId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return await ShowCreateErrorAsync(userId, "Enter a project name.", cancellationToken);
        }

        try
        {
            var project = await workspace.CreateProjectAsync(userId, Name, SeedStarterPack, cancellationToken);
            return RedirectToPage("/Projects/Detail", new { id = project.Id.Value });
        }
        catch (ArgumentException)
        {
            return await ShowCreateErrorAsync(userId, "Enter a project name.", cancellationToken);
        }
    }

    private async Task<IActionResult> ShowCreateErrorAsync(
        UserId userId,
        string error,
        CancellationToken cancellationToken)
    {
        Error = error;
        ShowCreateProject = true;
        Projects = await workspace.ListProjectsAsync(userId, cancellationToken);
        return Page();
    }
}
