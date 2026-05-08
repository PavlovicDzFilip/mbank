using System.Text;
using MBank.Api.Data;
using MBank.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace MBank.Api.Services;

public sealed class MemoryService(MemoryDbContext db, IEmbeddingService embeddings) : IMemoryService
{
    private const double RrfK = 60;

    private static readonly HashSet<string> WarningTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "warning",
        "constraint"
    };

    public async Task<RememberResponse> RememberAsync(RememberRequest request, CancellationToken cancellationToken)
    {
        if (request.Memories.Count == 0)
        {
            return new RememberResponse([], []);
        }

        SourceRecord? source = null;
        if (request.Source is not null)
        {
            source = new SourceRecord
            {
                SourceKind = NormalizeOptional(request.Source.SourceKind),
                SourceText = NormalizeOptional(request.Source.SourceText),
                SourceUrl = NormalizeOptional(request.Source.SourceUrl),
                ConversationUrl = NormalizeOptional(request.Source.ConversationUrl),
                MetadataJson = NormalizeOptional(request.Source.MetadataJson)
            };
            db.SourceRecords.Add(source);
        }

        var stored = new List<StoredMemory>();
        var possibleConflicts = new List<PossibleConflict>();

        foreach (var draft in request.Memories)
        {
            ValidateDraft(draft);
            var tags = NormalizeTags(draft.Tags);
            var searchText = BuildMemorySearchText(draft.Type, draft.Summary, draft.Subject, draft.SubjectKind, tags, request.Source?.SourceText);
            var vector = new Vector(await embeddings.GenerateEmbeddingAsync(searchText, cancellationToken));

            var entry = new MemoryEntry
            {
                Type = draft.Type.Trim(),
                Summary = draft.Summary.Trim(),
                Subject = NormalizeOptional(draft.Subject),
                SubjectKind = NormalizeOptional(draft.SubjectKind),
                Tags = tags,
                Confidence = Math.Clamp(draft.Confidence ?? 0.7, 0, 1),
                SearchText = searchText,
                Embedding = vector,
                SourceRecordId = source?.Id
            };

            var related = await FindSimilarMemoriesAsync(vector, entry.Subject, entry.SubjectKind, 4, cancellationToken);

            db.Memories.Add(entry);
            stored.Add(ToStoredMemory(entry));

            foreach (var similar in related.Where(x => x.Distance <= 0.18))
            {
                possibleConflicts.Add(new PossibleConflict(
                    similar.Memory.Id,
                    entry.Id,
                    similar.Memory.Summary,
                    BuildConflictReason(entry, similar.Memory),
                    SimilarityFromCosineDistance(similar.Distance)));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new RememberResponse(stored, possibleConflicts);
    }

    public async Task<TaskContextResponse> GetTaskContextAsync(TaskContextRequest request, CancellationToken cancellationToken)
    {
        ValidateTaskRequest(request);

        var queryText = BuildTaskQueryText(request);
        var queryVector = new Vector(await embeddings.GenerateEmbeddingAsync(queryText, cancellationToken));
        var limit = Math.Clamp(request.Limit, 1, 20);

        var candidateLimit = Math.Max(limit * 4, 20);
        var vectorCandidateRows = await db.Memories
            .AsNoTracking()
            .Where(x => x.Status == "active" && x.Embedding != null)
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
                .Where(x => x.Status == "active"
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
            .Take(limit)
            .ToList();

        var warnings = scored
            .Where(x => WarningTypes.Contains(x.Memory.Type))
            .Select(x => x.Memory.Summary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var conflicts = FindContextConflicts(scored);
        var questions = BuildClarifyingQuestions(request, scored, conflicts);

        var status = scored.Count == 0 ? "no_context_found" : "context_found";
        var summary = scored.Count == 0
            ? "No relevant memories were found for this task."
            : $"Found {scored.Count} relevant memories for this task.";

        return new TaskContextResponse(
            status,
            summary,
            scored.Select(x => new RelevantMemory(
                x.Memory.Id,
                x.Memory.Type,
                x.Memory.Summary,
                x.Memory.Subject,
                x.Memory.SubjectKind,
                x.Memory.Tags,
                x.Memory.Confidence,
                Math.Round(x.Score, 4),
                x.WhyRelevant)).ToArray(),
            warnings,
            conflicts,
            questions);
    }

    public async Task<MemoryDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var memory = await db.Memories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (memory is null)
        {
            return null;
        }

        SourceRecord? source = null;
        if (memory.SourceRecordId is not null)
        {
            source = await db.SourceRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == memory.SourceRecordId, cancellationToken);
        }

        var linkedIds = await db.MemoryLinks
            .AsNoTracking()
            .Where(x => x.FromMemoryId == id || x.ToMemoryId == id)
            .Select(x => x.FromMemoryId == id ? x.ToMemoryId : x.FromMemoryId)
            .ToListAsync(cancellationToken);

        return new MemoryDetail(
            memory.Id,
            memory.Type,
            memory.Summary,
            memory.Subject,
            memory.SubjectKind,
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
            linkedIds,
            memory.CreatedAt,
            memory.UpdatedAt);
    }

    public async Task<ReportConflictResponse> ReportConflictAsync(ReportConflictRequest request, CancellationToken cancellationToken)
    {
        if (request.MemoryIds.Count < 2)
        {
            return new ReportConflictResponse(false, "Report at least two memory ids to record a conflict.");
        }

        var ids = request.MemoryIds.Distinct().ToArray();
        var memories = await db.Memories
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (memories.Count < 2)
        {
            return new ReportConflictResponse(false, "At least two referenced memories must exist.");
        }

        for (var i = 0; i < memories.Count; i++)
        {
            for (var j = i + 1; j < memories.Count; j++)
            {
                var from = memories[i].Id;
                var to = memories[j].Id;
                var exists = await db.MemoryLinks.AnyAsync(
                    x => x.FromMemoryId == from && x.ToMemoryId == to && x.Relation == "conflicts",
                    cancellationToken);

                if (!exists)
                {
                    db.MemoryLinks.Add(new MemoryLink
                    {
                        FromMemoryId = from,
                        ToMemoryId = to,
                        Relation = "conflicts"
                    });
                }
            }
        }

        var source = new SourceRecord
        {
            SourceKind = "conflict_report",
            SourceText = request.Description
        };
        db.SourceRecords.Add(source);

        await db.SaveChangesAsync(cancellationToken);
        return new ReportConflictResponse(true, "Conflict report recorded.");
    }

    private async Task<List<(MemoryEntry Memory, double Distance)>> FindSimilarMemoriesAsync(
        Vector vector,
        string? subject,
        string? subjectKind,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.Memories
            .AsNoTracking()
            .Where(x => x.Status == "active" && x.Embedding != null);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            query = query.Where(x => x.Subject == subject || x.SubjectKind == subjectKind);
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

    private static List<HybridCandidate> MergeCandidatesWithRrf(
        IReadOnlyList<VectorCandidate> vectorCandidates,
        IReadOnlyList<TextCandidate> textCandidates)
    {
        var fused = new Dictionary<Guid, HybridCandidateBuilder>();

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
        Dictionary<Guid, HybridCandidateBuilder> fused,
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
        var reasons = new List<string> { $"RRF {rrfScore:0.0000}" };

        if (vectorDistance is not null)
        {
            var similarity = SimilarityFromCosineDistance(vectorDistance.Value);
            score += similarity * 0.12;
            reasons.Add($"vector similarity {similarity:0.00}");
        }

        if (textRank is not null)
        {
            score += Math.Min(textRank.Value, 1) * 0.08;
            reasons.Add($"full-text rank {textRank:0.00}");
        }

        if (TextEquals(memory.Subject, request.WorkArea?.Subject))
        {
            score += 0.15;
            reasons.Add("subject match");
        }

        if (TextEquals(memory.SubjectKind, request.WorkArea?.SubjectKind))
        {
            score += 0.08;
            reasons.Add("subject kind match");
        }

        var metadataTerms = new HashSet<string>(
            [.. memory.Tags, memory.Subject ?? "", memory.SubjectKind ?? "", memory.Type],
            StringComparer.OrdinalIgnoreCase);

        var metadataMatches = metadataTerms.Count(x => !string.IsNullOrWhiteSpace(x) && queryTerms.Contains(x));
        if (metadataMatches > 0)
        {
            score += Math.Min(metadataMatches * 0.03, 0.12);
            reasons.Add("metadata match");
        }

        var textMatches = queryTerms.Count(x => memory.SearchText.Contains(x, StringComparison.OrdinalIgnoreCase));
        if (textMatches > 0)
        {
            score += Math.Min(textMatches * 0.01, 0.08);
            reasons.Add("keyword match");
        }

        score += memory.Confidence * 0.07;

        return new ScoredMemory(memory, score, string.Join(", ", reasons));
    }

    private static IReadOnlyList<PossibleConflict> FindContextConflicts(IReadOnlyList<ScoredMemory> scored)
    {
        var conflicts = new List<PossibleConflict>();

        foreach (var group in scored
            .Where(x => !string.IsNullOrWhiteSpace(x.Memory.Subject))
            .GroupBy(x => x.Memory.Subject, StringComparer.OrdinalIgnoreCase))
        {
            var conflictLinks = group
                .SelectMany(x => group, (left, right) => (Left: left.Memory, Right: right.Memory))
                .Where(x => x.Left.Id != x.Right.Id)
                .Where(x => IsPotentialConflict(x.Left, x.Right))
                .Take(3);

            foreach (var (left, right) in conflictLinks)
            {
                conflicts.Add(new PossibleConflict(
                    left.Id,
                    right.Id,
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

    private static IReadOnlyList<string> BuildClarifyingQuestions(
        TaskContextRequest request,
        IReadOnlyList<ScoredMemory> scored,
        IReadOnlyList<PossibleConflict> conflicts)
    {
        var questions = new List<string>();

        if (string.IsNullOrWhiteSpace(request.WorkArea?.Subject))
        {
            questions.Add("What product capability or technical area is this task primarily about?");
        }

        if (scored.Count == 0)
        {
            questions.Add("Is there any product rationale, technical convention, or constraint that should guide this task?");
        }

        if (conflicts.Count > 0)
        {
            questions.Add("There may be conflicting memories. Which one should guide this task?");
        }

        return questions;
    }

    private static string BuildTaskQueryText(TaskContextRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Task kind: {request.Task.Kind}");
        builder.AppendLine($"Title: {request.Task.Title}");
        builder.AppendLine($"Description: {request.Task.Description}");
        builder.AppendLine($"Subject: {request.WorkArea?.Subject}");
        builder.AppendLine($"Subject kind: {request.WorkArea?.SubjectKind}");
        builder.AppendLine($"Files: {string.Join(", ", request.WorkArea?.Files ?? [])}");
        builder.AppendLine($"Modules: {string.Join(", ", request.WorkArea?.Modules ?? [])}");
        return builder.ToString();
    }

    private static string BuildMemorySearchText(
        string type,
        string summary,
        string? subject,
        string? subjectKind,
        IReadOnlyList<string> tags,
        string? sourceText)
    {
        var sourceExcerpt = string.IsNullOrWhiteSpace(sourceText)
            ? ""
            : sourceText.Length <= 1200 ? sourceText : sourceText[..1200];

        return string.Join(
            "\n",
            $"Type: {type}",
            $"Subject: {subject}",
            $"Subject kind: {subjectKind}",
            $"Summary: {summary}",
            $"Tags: {string.Join(", ", tags)}",
            $"Source excerpt: {sourceExcerpt}");
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
            entry.Id,
            entry.Type,
            entry.Summary,
            entry.Subject,
            entry.SubjectKind,
            entry.Tags,
            entry.Confidence);
    }

    private static string[] NormalizeTags(IReadOnlyList<string>? tags)
    {
        return tags?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray() ?? [];
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateDraft(MemoryDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Type))
        {
            throw new ArgumentException("Each memory needs a type.");
        }

        if (string.IsNullOrWhiteSpace(draft.Summary))
        {
            throw new ArgumentException("Each memory needs a summary.");
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
    }

    private static bool TextEquals(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPotentialConflict(MemoryEntry left, MemoryEntry right)
    {
        return WarningTypes.Contains(left.Type)
            || WarningTypes.Contains(right.Type)
            || !string.Equals(left.Type, right.Type, StringComparison.OrdinalIgnoreCase);
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

    private sealed record ScoredMemory(MemoryEntry Memory, double Score, string WhyRelevant);
}
