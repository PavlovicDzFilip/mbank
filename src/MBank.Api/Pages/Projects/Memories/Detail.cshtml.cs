using MBank.Api.Domain;
using MBank.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MBank.Api.Pages.Projects.Memories;

public sealed class DetailModel(
    ProjectWorkspaceService projects,
    MemoryWorkspaceService memories) : AuthenticatedPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid MemoryId { get; set; }

    public Project? Project { get; private set; }

    public MemoryDetail? Memory { get; private set; }

    public bool HasConflictLinks =>
        Memory?.Links.Any(x => x.Relation == MemoryLink.ConflictRelation) == true;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Challenge();
        }

        if (!TryGetProjectId(out var projectId) || !TryGetMemoryId(out var memoryId))
        {
            return NotFound();
        }

        Project = await projects.GetProjectAsync(userId, projectId, cancellationToken);
        if (Project is null)
        {
            return NotFound();
        }

        Memory = await memories.GetMemoryAsync(userId, projectId, memoryId, cancellationToken);
        if (Memory is null)
        {
            return NotFound();
        }

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

    private bool TryGetMemoryId(out MemoryId memoryId)
    {
        try
        {
            memoryId = new MemoryId(this.MemoryId);
            return true;
        }
        catch (ArgumentException)
        {
            memoryId = default;
            return false;
        }
    }
}
