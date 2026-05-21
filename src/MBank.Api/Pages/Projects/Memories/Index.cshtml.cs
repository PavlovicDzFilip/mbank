using MBank.Api.Domain;
using MBank.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MBank.Api.Pages.Projects.Memories;

public sealed class IndexModel(
    ProjectWorkspaceService projects,
    MemoryWorkspaceService memories) : AuthenticatedPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool IncludeSuperseded { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public Project? Project { get; private set; }

    public MemoryListPage? List { get; private set; }

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

        Project = await projects.GetProjectAsync(userId, projectId, cancellationToken);
        if (Project is null)
        {
            return NotFound();
        }

        List = await memories.ListMemoriesAsync(
            userId,
            projectId,
            IncludeSuperseded,
            PageNumber,
            cancellationToken);

        return Page();
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
}
