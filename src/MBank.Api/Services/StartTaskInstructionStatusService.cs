using MBank.Api.Auth;
using MBank.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MBank.Api.Services;

public sealed class StartTaskInstructionStatusService(
    MemoryDbContext db,
    AgentInstructionService agentInstructions,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromDays(7);

    public async Task<InstructionStatusResponse> GetAsync(
        AuthenticatedMemoryScope scope,
        string? reportedVersion,
        CancellationToken cancellationToken)
    {
        var status = agentInstructions.GetInstructionStatus(reportedVersion);
        if (!string.Equals(status.Status, "outdated", StringComparison.Ordinal))
        {
            return status;
        }

        var now = timeProvider.GetUtcNow();
        var cooldownCutoff = now - NotificationCooldown;
        var notificationReserved = await db.ProjectMemberships
            .Where(x => x.UserId == scope.UserId && x.ProjectId == scope.ProjectId)
            .Where(x => x.LastInstructionUpdateNotificationAt == null
                || x.LastInstructionUpdateNotificationAt <= cooldownCutoff)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.LastInstructionUpdateNotificationAt,
                    now),
                cancellationToken);

        if (notificationReserved > 0)
        {
            return status;
        }

        var membershipExists = await db.ProjectMemberships
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == scope.UserId && x.ProjectId == scope.ProjectId,
                cancellationToken);
        if (!membershipExists)
        {
            throw new InvalidOperationException(
                "The authenticated user is not a member of the current project.");
        }

        var normalizedReportedVersion = status.ReportedVersion
            ?? throw new InvalidOperationException("An outdated instruction status must include its reported version.");

        return agentInstructions.GetInstructionStatusDuringNotificationCooldown(
            normalizedReportedVersion);
    }
}
