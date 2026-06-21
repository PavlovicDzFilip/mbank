namespace MBank.Api.Domain;

public sealed class ProjectMembership
{
    private ProjectMembership()
    {
    }

    public UserId UserId { get; private set; }

    public ProjectId ProjectId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastInstructionUpdateNotificationAt { get; private set; }

    public static ProjectMembership Create(UserId userId, ProjectId projectId)
    {
        return new ProjectMembership
        {
            UserId = userId,
            ProjectId = projectId
        };
    }
}
