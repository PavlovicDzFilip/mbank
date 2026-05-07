namespace MBank.Api.Services;

public interface IMemoryService
{
    Task<RememberResponse> RememberAsync(RememberRequest request, CancellationToken cancellationToken);

    Task<TaskContextResponse> GetTaskContextAsync(TaskContextRequest request, CancellationToken cancellationToken);

    Task<MemoryDetail?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ReportConflictResponse> ReportConflictAsync(ReportConflictRequest request, CancellationToken cancellationToken);
}

