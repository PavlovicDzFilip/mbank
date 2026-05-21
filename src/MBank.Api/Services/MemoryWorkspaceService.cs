using MBank.Api.Data;
using MBank.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MBank.Api.Services;

public sealed class MemoryWorkspaceService(MemoryDbContext db)
{
    public const int PageSize = 50;

    public Task<int?> GetMemoryCountAsync(
        UserId userId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        return GetAccessibleCountAsync(userId, projectId, includeSuperseded: true, cancellationToken);
    }

    public async Task<MemoryListPage?> ListMemoriesAsync(
        UserId userId,
        ProjectId projectId,
        bool includeSuperseded,
        int page,
        CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, projectId, cancellationToken))
        {
            return null;
        }

        page = Math.Max(page, 1);
        var query = db.Memories
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId);

        if (!includeSuperseded)
        {
            query = query.Where(x => x.Status == MemoryEntry.ActiveStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var conflictMemoryIds = await GetConflictMemoryIdsAsync(
            projectId,
            entries.Select(x => x.Id).ToArray(),
            cancellationToken);

        var items = entries
            .Select(x => new MemoryListItem(
                x.Id.Value,
                x.Type.Value,
                x.Subject,
                x.Summary,
                x.Status,
                x.UpdatedAt,
                conflictMemoryIds.Contains(x.Id)))
            .ToArray();

        return new MemoryListPage(items, totalCount, page, PageSize);
    }

    public async Task<MemoryDetail?> GetMemoryAsync(
        UserId userId,
        ProjectId projectId,
        MemoryId memoryId,
        CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, projectId, cancellationToken))
        {
            return null;
        }

        var memory = await db.Memories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == memoryId && x.ProjectId == projectId, cancellationToken);

        if (memory is null)
        {
            return null;
        }

        SourceRecord? source = null;
        if (memory.SourceRecordId is not null)
        {
            var sourceRecordId = memory.SourceRecordId.Value;
            source = await db.SourceRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == sourceRecordId, cancellationToken);
        }

        var projectMemoryIds = db.Memories
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Id);
        var links = await db.MemoryLinks
            .AsNoTracking()
            .Where(x => x.FromMemoryId == memoryId || x.ToMemoryId == memoryId)
            .Where(x => projectMemoryIds.Contains(x.FromMemoryId) && projectMemoryIds.Contains(x.ToMemoryId))
            .ToListAsync(cancellationToken);
        var linkDetails = links
            .Select(x => new MemoryLinkDetail(
                (x.FromMemoryId == memoryId ? x.ToMemoryId : x.FromMemoryId).Value,
                x.Relation,
                x.Description,
                x.CreatedAt))
            .ToArray();

        return new MemoryDetail(
            memory.Id.Value,
            memory.Type,
            memory.Summary,
            memory.Subject,
            memory.Tags,
            memory.Confidence,
            memory.Status,
            source is null
                ? null
                : new SourceDetail(
                    source.SourceKind,
                    source.SourceText,
                    source.SourceUrl,
                    source.ConversationUrl,
                    source.MetadataJson),
            linkDetails.Select(x => x.MemoryId).ToArray(),
            linkDetails,
            memory.CreatedAt,
            memory.UpdatedAt);
    }

    private async Task<int?> GetAccessibleCountAsync(
        UserId userId,
        ProjectId projectId,
        bool includeSuperseded,
        CancellationToken cancellationToken)
    {
        if (!await IsMemberAsync(userId, projectId, cancellationToken))
        {
            return null;
        }

        var query = db.Memories.AsNoTracking().Where(x => x.ProjectId == projectId);
        if (!includeSuperseded)
        {
            query = query.Where(x => x.Status == MemoryEntry.ActiveStatus);
        }

        return await query.CountAsync(cancellationToken);
    }

    private async Task<HashSet<MemoryId>> GetConflictMemoryIdsAsync(
        ProjectId projectId,
        IReadOnlyList<MemoryId> memoryIds,
        CancellationToken cancellationToken)
    {
        if (memoryIds.Count == 0)
        {
            return [];
        }

        var projectMemoryIds = db.Memories
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Id);

        var conflictLinks = await db.MemoryLinks
            .AsNoTracking()
            .Where(x => x.Relation == MemoryLink.ConflictRelation)
            .Where(x => projectMemoryIds.Contains(x.FromMemoryId) && projectMemoryIds.Contains(x.ToMemoryId))
            .Where(x => memoryIds.Contains(x.FromMemoryId) || memoryIds.Contains(x.ToMemoryId))
            .Select(x => new { x.FromMemoryId, x.ToMemoryId })
            .ToListAsync(cancellationToken);

        var result = new HashSet<MemoryId>();
        foreach (var link in conflictLinks)
        {
            if (memoryIds.Contains(link.FromMemoryId))
            {
                result.Add(link.FromMemoryId);
            }

            if (memoryIds.Contains(link.ToMemoryId))
            {
                result.Add(link.ToMemoryId);
            }
        }

        return result;
    }

    private Task<bool> IsMemberAsync(
        UserId userId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        return db.ProjectMemberships.AnyAsync(
            x => x.UserId == userId && x.ProjectId == projectId,
            cancellationToken);
    }
}

public sealed record MemoryListItem(
    Guid Id,
    string Type,
    string? Subject,
    string Summary,
    string Status,
    DateTimeOffset UpdatedAt,
    bool HasConflictLinks);

public sealed record MemoryListPage(
    IReadOnlyList<MemoryListItem> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int RangeStart => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int RangeEnd => TotalCount == 0 ? 0 : Math.Min(Page * PageSize, TotalCount);
}
