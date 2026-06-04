using System.Text;
using MBank.Api.Auth;
using MBank.Api.Data;
using MBank.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace MBank.Api.Services;

public sealed class MemoryService(
    MemoryDbContext db,
    IEmbeddingService embeddings,
    AgentInstructionService agentInstructions,
    MemoryScopeProvider currentScope)
{
    private const double RrfK = 60;
    private const double PossibleMatchRawThreshold = 0.18;
    private const double StrongMatchRawThreshold = 0.28;
    private const double ExcellentMatchRawScore = 0.60;

    private static readonly MemoryType[] StartupGuidanceTypes =
        MemoryType.List.Where(x => x.IsStartupGuidance).ToArray();

    public async Task<int> SeedProjectStarterPackAsync(
        UserId userId,
        ProjectId projectId,
        CancellationToken cancellationToken)
    {
        var source = SourceRecord.Create(
            ProjectStarterPack.SourceKind,
            "Seeded from the MBank default project starter pack.",
            sourceUrl: null,
            conversationUrl: null,
            metadataJson: null);
        db.SourceRecords.Add(source);

        var seeded = 0;
        foreach (var draft in ProjectStarterPack.Memories)
        {
            ValidateDraft(draft);
            var entry = await MemoryEntry.CreateAsync(
                userId,
                projectId,
                draft.Type,
                draft.Summary,
                draft.Subject,
                draft.Tags,
                draft.Confidence ?? 0.95,
                source.SourceText,
                source.Id,
                MemoryEntry.ActiveStatus,
                embeddings,
                cancellationToken);
            db.Memories.Add(entry);

            seeded++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return seeded;
    }

    public async Task<RememberResponse> RememberAsync(RememberRequest request, CancellationToken cancellationToken)
    {
        var scope = RequireScope();
        ValidateRememberRequest(request);

        if (request.Memories.Count == 0)
        {
            return new RememberResponse([], []);
        }

        SourceRecord? source = null;
        if (request.Source is not null)
        {
            source = SourceRecord.Create(
                request.Source.SourceKind,
                request.Source.SourceText,
                request.Source.SourceUrl,
                request.Source.ConversationUrl,
                request.Source.MetadataJson);
            db.SourceRecords.Add(source);
        }

        var stored = new List<StoredMemory>();
        var possibleConflicts = new List<PossibleConflict>();

        foreach (var draft in request.Memories)
        {
            ValidateDraft(draft);
            var entry = await MemoryEntry.CreateAsync(
                scope.UserId,
                scope.ProjectId,
                draft.Type,
                draft.Summary,
                draft.Subject,
                draft.Tags,
                draft.Confidence ?? 0.7,
                request.Source?.SourceText,
                source?.Id,
                ToMemoryStatus(draft.Lifecycle),
                embeddings,
                cancellationToken);

            var related = await FindSimilarMemoriesAsync(scope.ProjectId, entry.Embedding!, entry.Subject, 4, cancellationToken);

            db.Memories.Add(entry);
            stored.Add(ToStoredMemory(entry));

            foreach (var similar in related.Where(x => x.Distance <= 0.18))
            {
                possibleConflicts.Add(new PossibleConflict(
                    similar.Memory.Id.Value,
                    entry.Id.Value,
                    similar.Memory.Summary,
                    BuildConflictReason(entry, similar.Memory),
                    SimilarityFromCosineDistance(similar.Distance)));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new RememberResponse(stored, possibleConflicts);
    }

    public async Task<StartTaskResponse> StartTaskAsync(CancellationToken cancellationToken)
    {
        var scope = RequireScope();
        var guidanceMemories = await db.Memories
            .AsNoTracking()
            .Where(x => x.ProjectId == scope.ProjectId && x.Status == MemoryEntry.ActiveStatus)
            .Where(x => StartupGuidanceTypes.Contains(x.Type))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var canonicalRules = AgentInstructionService.TaskStartRules
            .Select(x => new AgentRule(x, "canonical_instruction"));
        var memoryRules = guidanceMemories
            .Where(IsAgentGuidanceMemory)
            .Take(12)
            .Select(x => new AgentRule(x.Summary, "memory", x.Id.Value));

        var rules = canonicalRules
            .Concat(memoryRules)
            .DistinctBy(x => NormalizeRuleKey(x.Text))
            .ToArray();

        return new StartTaskResponse(
            "Loaded current MBank agent rules for this task. Instruction-file updates affect future task startup; use the returned rules now.",
            rules,
            agentInstructions.CheckOnboarding(),
            "Call `get_task_context` with this task's current focus (title, description, kind, and optional work area). Re-call it when conversation focus changes before substantive project-specific answers, including exploration, and when uncertain how to proceed or before specific implementation decisions.");
    }

    public async Task<TaskContextResponse> GetTaskContextAsync(TaskContextRequest request, CancellationToken cancellationToken)
    {
        var scope = RequireScope();
        ValidateTaskRequest(request);

        var queryText = BuildTaskQueryText(request);
        var queryVector = await embeddings.GenerateEmbeddingAsync(scope.ProjectId, queryText, cancellationToken);
        var limit = Math.Clamp(request.Limit, 1, 20);
        var includeExperimentalMemories = TaskContextAttentionRules.ShouldIncludeExperimentalMemories(request);

        var candidateLimit = Math.Max(limit * 4, 20);
        var vectorCandidateRows = await db.Memories
            .AsNoTracking()
            .Where(x => x.ProjectId == scope.ProjectId
                && (x.Status == MemoryEntry.ActiveStatus
                    || (includeExperimentalMemories && x.Status == MemoryEntry.ExperimentalStatus))
                && x.Embedding != null)
            .OrderBy(x => x.Embedding!.CosineDistance(queryVector))
            .Take(candidateLimit)
            .Select(x => new
            {
                Memory = x,
                Distance = x.Embedding!.CosineDistance(queryVector)
            })
            .ToListAsync(cancellationToken);

        var fullTextQuery = BuildFullTextQuery(queryText);
        IReadOnlyList<TextCandidate> textCandidates = fullTextQuery is null
            ? []
            : await db.Memories
                .AsNoTracking()
                .Where(x => x.ProjectId == scope.ProjectId
                    && (x.Status == MemoryEntry.ActiveStatus
                        || (includeExperimentalMemories && x.Status == MemoryEntry.ExperimentalStatus))
                    && x.SearchVector.Matches(EF.Functions.ToTsQuery("english", fullTextQuery)))
                .OrderByDescending(x => x.SearchVector.Rank(
                    EF.Functions.ToTsQuery("english", fullTextQuery),
                    NpgsqlTsRankingNormalization.DivideByItselfPlusOne))
                .Take(candidateLimit)
                .Select(x => new TextCandidate(
                    x,
                    x.SearchVector.Rank(
                        EF.Functions.ToTsQuery("english", fullTextQuery),
                        NpgsqlTsRankingNormalization.DivideByItselfPlusOne)))
                .ToListAsync(cancellationToken);

        var vectorCandidates = vectorCandidateRows
            .Select(x => new VectorCandidate(x.Memory, x.Distance))
            .ToArray();

        var queryTerms = ExtractTerms(queryText);
        var fusedCandidates = MergeCandidatesWithRrf(vectorCandidates, textCandidates);

        var scored = fusedCandidates
            .Select(x => ScoreCandidate(x.Memory, x.RrfScore, x.VectorDistance, x.TextRank, request, queryTerms))
            .OrderByDescending(x => x.Score)
            .Take(limit * 2)
            .ToList();

        var strongMatches = scored
            .Where(x => x.Score >= StrongMatchRawThreshold)
            .Take(limit)
            .ToArray();

        var possibleMatches = scored
            .Where(x => x.Score >= PossibleMatchRawThreshold && x.Score < StrongMatchRawThreshold)
            .Take(limit)
            .ToArray();

        var conflicts = FindContextConflicts(strongMatches);
        var attentionItems = BuildAttentionItems(request, strongMatches, possibleMatches, conflicts);

        var status = strongMatches.Length > 0
            ? "context_found"
            : possibleMatches.Length > 0 ? "possible_context_found" : "no_context_found";
        var summary = BuildContextSummary(strongMatches.Length, possibleMatches.Length);
        var canProceed = attentionItems.All(x => x.Action != TaskContextAttentionAction.AskUser);

        return new TaskContextResponse(
            status,
            summary,
            strongMatches.Select(ToMemoryMatch).ToArray(),
            possibleMatches.Select(ToMemoryMatch).ToArray(),
            agentInstructions.CheckOnboarding(),
            canProceed,
            attentionItems);
    }

    public async Task<MemoryDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var scope = RequireScope();
        var memoryId = new MemoryId(id);
        var memory = await db.Memories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == memoryId && x.ProjectId == scope.ProjectId, cancellationToken);

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
            .Where(x => x.ProjectId == scope.ProjectId)
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

    public async Task<ReportConflictResponse> ReportConflictAsync(ReportConflictRequest request, CancellationToken cancellationToken)
    {
        var scope = RequireScope();
        if (request.MemoryIds.Count < 2)
        {
            return new ReportConflictResponse(false, "Report at least two memory ids to record a conflict.");
        }

        var ids = request.MemoryIds.Distinct().Select(x => new MemoryId(x)).ToArray();
        var memories = await db.Memories
            .Where(x => x.ProjectId == scope.ProjectId && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (memories.Count < 2)
        {
            return new ReportConflictResponse(false, "At least two referenced memories must exist.");
        }

        var description = NormalizeOptional(request.Description);

        for (var i = 0; i < memories.Count; i++)
        {
            for (var j = i + 1; j < memories.Count; j++)
            {
                var from = memories[i].Id;
                var to = memories[j].Id;
                var existing = await db.MemoryLinks.FirstOrDefaultAsync(
                    x => x.FromMemoryId == from && x.ToMemoryId == to && x.Relation == MemoryLink.ConflictRelation,
                    cancellationToken);

                if (existing is null)
                {
                    db.MemoryLinks.Add(MemoryLink.CreateConflict(from, to, description));
                }
                else if (!string.IsNullOrWhiteSpace(description) && !TextEquals(existing.Description, description))
                {
                    existing.ChangeDescription(description);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ReportConflictResponse(true, "Conflict report recorded.");
    }

    public async Task<ResolveConflictResponse> ResolveConflictAsync(ResolveConflictRequest request, CancellationToken cancellationToken)
    {
        var scope = RequireScope();
        var keptMemoryId = new MemoryId(request.KeptMemoryId);
        var supersededMemoryIds = request.SupersededMemoryIds
            .Distinct()
            .Select(x => new MemoryId(x))
            .ToArray();

        if (supersededMemoryIds.Length == 0)
        {
            return new ResolveConflictResponse(false, "Resolve at least one superseded memory id.");
        }

        if (supersededMemoryIds.Contains(keptMemoryId))
        {
            return new ResolveConflictResponse(false, "The kept memory cannot also be superseded.");
        }

        var clarification = NormalizeOptional(request.Clarification);
        if (clarification is null)
        {
            return new ResolveConflictResponse(false, "A clarification is required to resolve a conflict.");
        }

        var ids = supersededMemoryIds.Append(keptMemoryId).ToArray();
        var memories = await db.Memories
            .Where(x => x.ProjectId == scope.ProjectId && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var memoryById = memories.ToDictionary(x => x.Id);

        if (!memoryById.TryGetValue(keptMemoryId, out var keptMemory))
        {
            return new ResolveConflictResponse(false, "The kept memory must exist in the current project.");
        }

        if (keptMemory.Status != MemoryEntry.ActiveStatus)
        {
            return new ResolveConflictResponse(false, "The kept memory must be active.");
        }

        if (supersededMemoryIds.Any(x => !memoryById.ContainsKey(x)))
        {
            return new ResolveConflictResponse(false, "Every superseded memory must exist in the current project.");
        }

        foreach (var supersededMemoryId in supersededMemoryIds)
        {
            memoryById[supersededMemoryId].Supersede();
            await UpsertSupersedesLinkAsync(keptMemoryId, supersededMemoryId, clarification, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ResolveConflictResponse(
            true,
            "Conflict resolved. Superseded memories will no longer appear in normal retrieval.",
            keptMemoryId.Value,
            supersededMemoryIds.Select(x => x.Value).ToArray());
    }

    public async Task<PromoteMemoryResponse> PromoteMemoryAsync(PromoteMemoryRequest request, CancellationToken cancellationToken)
    {
        var scope = RequireScope();
        var promotedMemoryId = new MemoryId(request.MemoryId);
        var supersededMemoryIds = (request.SupersededMemoryIds ?? [])
            .Distinct()
            .Select(x => new MemoryId(x))
            .ToArray();

        if (supersededMemoryIds.Contains(promotedMemoryId))
        {
            return new PromoteMemoryResponse(false, "The promoted memory cannot also be superseded.");
        }

        var clarification = NormalizeOptional(request.Clarification);
        if (supersededMemoryIds.Length > 0 && clarification is null)
        {
            return new PromoteMemoryResponse(false, "A clarification is required when promotion supersedes accepted memories.");
        }

        var ids = supersededMemoryIds.Append(promotedMemoryId).ToArray();
        var memories = await db.Memories
            .Where(x => x.ProjectId == scope.ProjectId && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var memoryById = memories.ToDictionary(x => x.Id);

        if (!memoryById.TryGetValue(promotedMemoryId, out var promotedMemory))
        {
            return new PromoteMemoryResponse(false, "The experimental memory must exist in the current project.");
        }

        if (promotedMemory.Status != MemoryEntry.ExperimentalStatus)
        {
            return new PromoteMemoryResponse(false, "Only experimental memories can be promoted.");
        }

        if (supersededMemoryIds.Any(x => !memoryById.ContainsKey(x)))
        {
            return new PromoteMemoryResponse(false, "Every superseded memory must exist in the current project.");
        }

        if (supersededMemoryIds.Any(x => memoryById[x].Status != MemoryEntry.ActiveStatus))
        {
            return new PromoteMemoryResponse(false, "Promotion can only supersede accepted active memories.");
        }

        promotedMemory.Promote();

        foreach (var supersededMemoryId in supersededMemoryIds)
        {
            memoryById[supersededMemoryId].Supersede();
            await UpsertSupersedesLinkAsync(
                promotedMemoryId,
                supersededMemoryId,
                clarification ?? "Promoted from experimental memory.",
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new PromoteMemoryResponse(
            true,
            supersededMemoryIds.Length == 0
                ? "Experimental memory promoted. It will now appear in normal retrieval."
                : "Experimental memory promoted and superseded accepted memories. It will now appear in normal retrieval.",
            promotedMemoryId.Value,
            supersededMemoryIds.Select(x => x.Value).ToArray());
    }

    private async Task<List<(MemoryEntry Memory, double Distance)>> FindSimilarMemoriesAsync(
        ProjectId projectId,
        Vector vector,
        string? subject,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.Memories
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Status == MemoryEntry.ActiveStatus && x.Embedding != null);

        if (subject is not null)
        {
            query = query.Where(x => x.Subject == subject);
        }

        var results = await query
            .OrderBy(x => x.Embedding!.CosineDistance(vector))
            .Take(limit)
            .Select(x => new
            {
                Memory = x,
                Distance = x.Embedding!.CosineDistance(vector)
            })
            .ToListAsync(cancellationToken);

        return results.Select(x => (x.Memory, x.Distance)).ToList();
    }

    private async Task UpsertSupersedesLinkAsync(
        MemoryId keptMemoryId,
        MemoryId supersededMemoryId,
        string clarification,
        CancellationToken cancellationToken)
    {
        var direct = await db.MemoryLinks.FirstOrDefaultAsync(
            x => x.FromMemoryId == keptMemoryId && x.ToMemoryId == supersededMemoryId,
            cancellationToken);
        var reverse = await db.MemoryLinks.FirstOrDefaultAsync(
            x => x.FromMemoryId == supersededMemoryId && x.ToMemoryId == keptMemoryId,
            cancellationToken);

        if (direct is not null)
        {
            direct.ChangeToSupersedes(keptMemoryId, supersededMemoryId, clarification);
            if (reverse is not null)
            {
                db.MemoryLinks.Remove(reverse);
            }

            return;
        }

        if (reverse is not null)
        {
            reverse.ChangeToSupersedes(keptMemoryId, supersededMemoryId, clarification);
            return;
        }

        db.MemoryLinks.Add(MemoryLink.CreateSupersedes(keptMemoryId, supersededMemoryId, clarification));
    }

    private static List<HybridCandidate> MergeCandidatesWithRrf(
        IReadOnlyList<VectorCandidate> vectorCandidates,
        IReadOnlyList<TextCandidate> textCandidates)
    {
        var fused = new Dictionary<MemoryId, HybridCandidateBuilder>();

        for (var i = 0; i < vectorCandidates.Count; i++)
        {
            var candidate = vectorCandidates[i];
            var builder = GetOrCreate(fused, candidate.Memory);
            builder.VectorRank = i + 1;
            builder.VectorDistance = candidate.Distance;
            builder.RrfScore += ReciprocalRank(i + 1);
        }

        for (var i = 0; i < textCandidates.Count; i++)
        {
            var candidate = textCandidates[i];
            var builder = GetOrCreate(fused, candidate.Memory);
            builder.TextRankPosition = i + 1;
            builder.TextRank = candidate.Rank;
            builder.RrfScore += ReciprocalRank(i + 1);
        }

        return fused.Values
            .Select(x => x.ToCandidate())
            .OrderByDescending(x => x.RrfScore)
            .ToList();
    }

    private static HybridCandidateBuilder GetOrCreate(
        Dictionary<MemoryId, HybridCandidateBuilder> fused,
        MemoryEntry memory)
    {
        if (!fused.TryGetValue(memory.Id, out var builder))
        {
            builder = new HybridCandidateBuilder(memory);
            fused.Add(memory.Id, builder);
        }

        return builder;
    }

    private static double ReciprocalRank(int rank)
    {
        return 1 / (RrfK + rank);
    }

    private static ScoredMemory ScoreCandidate(
        MemoryEntry memory,
        double rrfScore,
        double? vectorDistance,
        double? textRank,
        TaskContextRequest request,
        HashSet<string> queryTerms)
    {
        var score = rrfScore;

        if (vectorDistance is not null)
        {
            var similarity = SimilarityFromCosineDistance(vectorDistance.Value);
            score += similarity * 0.12;
        }

        if (textRank is not null)
        {
            score += Math.Min(textRank.Value, 1) * 0.08;
        }

        if (TextEquals(memory.Subject, request.WorkArea?.Subject))
        {
            score += 0.15;
        }

        var metadataTerms = new HashSet<string>(
            [.. memory.Tags, memory.Subject ?? "", memory.Type.Value],
            StringComparer.OrdinalIgnoreCase);

        var metadataMatches = metadataTerms.Count(x => !string.IsNullOrWhiteSpace(x) && queryTerms.Contains(x));
        if (metadataMatches > 0)
        {
            score += Math.Min(metadataMatches * 0.03, 0.12);
        }

        var textMatches = queryTerms.Count(x => memory.SearchText.Contains(x, StringComparison.OrdinalIgnoreCase));
        if (textMatches > 0)
        {
            score += Math.Min(textMatches * 0.01, 0.08);
        }

        score += memory.Confidence * 0.07;

        return new ScoredMemory(memory, score);
    }

    private static MemoryMatch ToMemoryMatch(ScoredMemory scored)
    {
        var strength = scored.Score >= StrongMatchRawThreshold ? "strong" : "possible";

        return new MemoryMatch(
            scored.Memory.Id.Value,
            scored.Memory.Type,
            scored.Memory.Summary,
            scored.Memory.Subject,
            scored.Memory.Tags,
            scored.Memory.Confidence,
            NormalizeMatchScore(scored.Score),
            strength,
            scored.Memory.Status,
            ToMemoryLifecycle(scored.Memory.Status));
    }

    private static double NormalizeMatchScore(double rawScore)
    {
        if (rawScore >= StrongMatchRawThreshold)
        {
            var strongRange = ExcellentMatchRawScore - StrongMatchRawThreshold;
            var position = strongRange <= 0 ? 1 : (rawScore - StrongMatchRawThreshold) / strongRange;
            return Math.Round(0.75 + Math.Clamp(position, 0, 1) * 0.25, 2);
        }

        if (rawScore >= PossibleMatchRawThreshold)
        {
            var possibleRange = StrongMatchRawThreshold - PossibleMatchRawThreshold;
            var position = possibleRange <= 0 ? 1 : (rawScore - PossibleMatchRawThreshold) / possibleRange;
            return Math.Round(0.40 + Math.Clamp(position, 0, 1) * 0.34, 2);
        }

        var omittedRange = PossibleMatchRawThreshold;
        var omittedPosition = omittedRange <= 0 ? 0 : rawScore / omittedRange;
        return Math.Round(Math.Clamp(omittedPosition, 0, 1) * 0.39, 2);
    }

    private static string BuildContextSummary(int strongMatchCount, int possibleMatchCount)
    {
        if (strongMatchCount == 0 && possibleMatchCount == 0)
        {
            return "No memory matches were found for this task.";
        }

        if (strongMatchCount == 0)
        {
            return $"Found {possibleMatchCount} possible memory matches for this task.";
        }

        if (possibleMatchCount == 0)
        {
            return $"Found {strongMatchCount} strong memory matches for this task.";
        }

        return $"Found {strongMatchCount} strong memory matches and {possibleMatchCount} possible memory matches for this task.";
    }

    private static IReadOnlyList<PossibleConflict> FindContextConflicts(IReadOnlyList<ScoredMemory> scored)
    {
        var conflicts = new List<PossibleConflict>();

        foreach (var group in scored
            .Where(x => x.Memory.Status == MemoryEntry.ActiveStatus)
            .Where(x => x.Memory.Subject is not null)
            .GroupBy(x => x.Memory.Subject!, StringComparer.OrdinalIgnoreCase))
        {
            var conflictLinks = group
                .SelectMany(x => group, (left, right) => (Left: left.Memory, Right: right.Memory))
                .Where(x => x.Left.Id != x.Right.Id)
                .Where(x => IsPotentialConflict(x.Left, x.Right))
                .Take(3);

            foreach (var (left, right) in conflictLinks)
            {
                conflicts.Add(new PossibleConflict(
                    left.Id.Value,
                    right.Id.Value,
                    left.Summary,
                    "Related memories may need clarification before implementation.",
                    0));
            }
        }

        return conflicts
            .GroupBy(x => new { x.ExistingMemoryId, x.NewMemoryId })
            .Select(x => x.First())
            .Take(5)
            .ToArray();
    }

    private static IReadOnlyList<TaskContextAttentionItem> BuildAttentionItems(
        TaskContextRequest request,
        IReadOnlyList<ScoredMemory> strongMatches,
        IReadOnlyList<ScoredMemory> possibleMatches,
        IReadOnlyList<PossibleConflict> conflicts)
    {
        var items = new List<TaskContextAttentionItem>();

        items.AddRange(strongMatches
            .Where(x => x.Memory.Type.IsWarning)
            .GroupBy(x => x.Memory.Summary, StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                var match = x.First();
                return new TaskContextAttentionItem(
                    "warning",
                    TaskContextAttentionAction.Notice,
                    match.Memory.Summary,
                    MemoryIds: [match.Memory.Id.Value]);
            }));

        var experimentalMatches = strongMatches
            .Concat(possibleMatches)
            .Where(x => x.Memory.Status == MemoryEntry.ExperimentalStatus)
            .Take(5)
            .ToArray();
        if (experimentalMatches.Length > 0)
        {
            items.Add(new TaskContextAttentionItem(
                "experimental_memory",
                TaskContextAttentionAction.Notice,
                "Experimental memories were returned because this task looks exploratory. Treat them as provisional and do not let them supersede accepted guidance unless the user promotes them.",
                "Should any experimental memory be promoted, rejected, or kept experimental?",
                experimentalMatches.Select(x => x.Memory.Id.Value).ToArray()));
        }

        if (string.IsNullOrWhiteSpace(request.WorkArea?.Subject))
        {
            items.Add(new TaskContextAttentionItem(
                "clarifying_question",
                TaskContextAttentionAction.Notice,
                "The task did not include a product capability or technical area.",
                "What product capability or technical area is this task primarily about?"));
        }

        if (strongMatches.Count == 0 && possibleMatches.Count == 0)
        {
            items.Add(new TaskContextAttentionItem(
                "clarifying_question",
                TaskContextAttentionAction.Notice,
                "No memory matches were found for this task.",
                "Is there any product rationale, technical convention, or constraint that should guide this task?"));
        }

        if (strongMatches.Count == 0 && possibleMatches.Count > 0)
        {
            items.Add(new TaskContextAttentionItem(
                "possible_matches",
                TaskContextAttentionAction.Notice,
                "Only possible memory matches were found.",
                "Should any possible memory matches guide this task?",
                possibleMatches.Select(x => x.Memory.Id.Value).ToArray()));
        }

        foreach (var conflict in conflicts)
        {
            items.Add(new TaskContextAttentionItem(
                "conflict",
                TaskContextAttentionAction.AskUser,
                BuildConflictAttentionSummary(conflict, strongMatches),
                "I found conflicting memories. Which one should guide this task?",
                BuildConflictMemoryIds(conflict)));
        }

        if (TaskContextAttentionRules.ShouldOfferCapture(request.Task.Kind))
        {
            items.Add(TaskContextAttentionRules.CreateCaptureOfferItem());
        }

        return items;
    }

    private static IReadOnlyList<Guid> BuildConflictMemoryIds(PossibleConflict conflict)
    {
        if (conflict.NewMemoryId is null)
        {
            return [conflict.ExistingMemoryId];
        }

        return [conflict.ExistingMemoryId, conflict.NewMemoryId.Value];
    }

    private static string BuildConflictAttentionSummary(PossibleConflict conflict, IReadOnlyList<ScoredMemory> strongMatches)
    {
        var existing = strongMatches.FirstOrDefault(x => x.Memory.Id.Value == conflict.ExistingMemoryId)?.Memory.Summary
            ?? conflict.ExistingSummary;
        var current = conflict.NewMemoryId is null
            ? null
            : strongMatches.FirstOrDefault(x => x.Memory.Id.Value == conflict.NewMemoryId.Value)?.Memory.Summary;

        if (current is null)
        {
            return $"Conflicting memory may need clarification: {existing}";
        }

        return $"Conflicting memories may need clarification: {existing} / {current}";
    }

    private static string BuildTaskQueryText(TaskContextRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Task kind: {request.Task.Kind}");
        builder.AppendLine($"Title: {request.Task.Title}");
        builder.AppendLine($"Description: {request.Task.Description}");
        builder.AppendLine($"Subject: {request.WorkArea?.Subject}");
        builder.AppendLine($"Files: {string.Join(", ", request.WorkArea?.Files ?? [])}");
        builder.AppendLine($"Modules: {string.Join(", ", request.WorkArea?.Modules ?? [])}");
        return builder.ToString();
    }

    private static HashSet<string> ExtractTerms(string text)
    {
        return text
            .Split([' ', '\r', '\n', '\t', '.', ',', ';', ':', '/', '\\', '-', '_', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length >= 3)
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? BuildFullTextQuery(string text)
    {
        var terms = ExtractTerms(text)
            .Select(NormalizeTsQueryTerm)
            .Where(x => x is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .Select(x => $"{x}:*")
            .ToArray();

        return terms.Length == 0 ? null : string.Join(" | ", terms);
    }

    private static string? NormalizeTsQueryTerm(string term)
    {
        var normalized = new string(term
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return normalized.Length < 3 ? null : normalized;
    }

    private static StoredMemory ToStoredMemory(MemoryEntry entry)
    {
        return new StoredMemory(
            entry.Id.Value,
            entry.Type,
            entry.Summary,
            entry.Subject,
            entry.Tags,
            entry.Confidence,
            ToMemoryLifecycle(entry.Status),
            entry.Status);
    }

    private static string ToMemoryStatus(MemoryLifecycleValue lifecycle)
    {
        return lifecycle switch
        {
            MemoryLifecycleValue.Accepted => MemoryEntry.ActiveStatus,
            MemoryLifecycleValue.Experimental => MemoryEntry.ExperimentalStatus,
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycle), lifecycle, "Unknown memory lifecycle.")
        };
    }

    private static MemoryLifecycleValue ToMemoryLifecycle(string status)
    {
        return status switch
        {
            MemoryEntry.ActiveStatus => MemoryLifecycleValue.Accepted,
            MemoryEntry.ExperimentalStatus => MemoryLifecycleValue.Experimental,
            _ => MemoryLifecycleValue.Accepted
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateDraft(MemoryDraft draft)
    {
        if (draft.Type is null)
        {
            throw new ArgumentException("Each memory needs a type.");
        }

        if (string.IsNullOrWhiteSpace(draft.Summary))
        {
            throw new ArgumentException("Each memory needs a summary.");
        }
    }

    private static void ValidateRememberRequest(RememberRequest request)
    {
        if (request.Source?.SourceUrl is { } sourceUrl && LooksLikeLocalPath(sourceUrl))
        {
            throw new ArgumentException("Invalid source URL: absolute local paths are not allowed. Send repository-relative paths such as \"src/MBank.Api/Program.cs\". MBank does not store machine-specific repo locations.");
        }
    }

    private static void ValidateTaskRequest(TaskContextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Task.Title))
        {
            throw new ArgumentException("Task title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Task.Description))
        {
            throw new ArgumentException("Task description is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Task.Kind))
        {
            throw new ArgumentException("Task kind is required.");
        }

        foreach (var file in request.WorkArea?.Files ?? [])
        {
            ValidateRelativeFilePath(file);
        }
    }

    private static void ValidateRelativeFilePath(string file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }

        if (LooksLikeLocalPath(file))
        {
            throw new ArgumentException($"Invalid file path \"{file}\": absolute local paths are not allowed. Send repository-relative paths such as \"src/MBank.Api/Program.cs\". MBank does not store machine-specific repo locations.");
        }

        var normalized = file.Replace('\\', '/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            throw new ArgumentException($"Invalid file path \"{file}\": parent-directory traversal is not allowed. Send repository-relative paths such as \"src/MBank.Api/Program.cs\".");
        }
    }

    private static bool LooksLikeLocalPath(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("/", StringComparison.Ordinal)
            || trimmed.StartsWith("\\", StringComparison.Ordinal)
            || trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || (trimmed.Length >= 3
                && char.IsAsciiLetter(trimmed[0])
                && trimmed[1] == ':'
                && (trimmed[2] == '\\' || trimmed[2] == '/'));
    }

    private AuthenticatedMemoryScope RequireScope()
    {
        return currentScope.Current
            ?? throw new InvalidOperationException("MBank memory operations require an authenticated project token.");
    }

    private static bool TextEquals(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPotentialConflict(MemoryEntry left, MemoryEntry right)
    {
        return left.Type.IsWarning
            || right.Type.IsWarning
            || left.Type != right.Type;
    }

    private static bool IsAgentGuidanceMemory(MemoryEntry memory)
    {
        if (memory.Type.IsStartupGuidance)
        {
            return true;
        }

        return false;
    }

    private static string NormalizeRuleKey(string text)
    {
        return string.Join(
            " ",
            text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .ToLowerInvariant();
    }

    private static string BuildConflictReason(MemoryEntry current, MemoryEntry existing)
    {
        if (TextEquals(current.Subject, existing.Subject))
        {
            return "Very similar memory already exists for the same subject; verify whether this is duplicate, supporting context, or a conflict.";
        }

        return "Very similar memory already exists; verify whether this is duplicate, supporting context, or a conflict.";
    }

    private static double SimilarityFromCosineDistance(double distance)
    {
        return Math.Clamp(1 - distance, 0, 1);
    }

    private sealed record HybridCandidate(
        MemoryEntry Memory,
        double RrfScore,
        double? VectorDistance,
        double? TextRank);

    private sealed record VectorCandidate(MemoryEntry Memory, double Distance);

    private sealed record TextCandidate(MemoryEntry Memory, float Rank);

    private sealed class HybridCandidateBuilder(MemoryEntry memory)
    {
        public MemoryEntry Memory { get; } = memory;

        public double RrfScore { get; set; }

        public int? VectorRank { get; set; }

        public double? VectorDistance { get; set; }

        public int? TextRankPosition { get; set; }

        public double? TextRank { get; set; }

        public HybridCandidate ToCandidate()
        {
            return new HybridCandidate(Memory, RrfScore, VectorDistance, TextRank);
        }
    }

    private sealed record ScoredMemory(MemoryEntry Memory, double Score);
}
