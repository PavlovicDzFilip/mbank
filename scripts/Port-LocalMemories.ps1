param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Export", "Import")]
    [string]$Mode,

    [string]$ComposeDir,

    [string]$File,

    [Guid]$UserId,

    [Guid]$ProjectId
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

if ([string]::IsNullOrWhiteSpace($ComposeDir)) {
    $ComposeDir = Join-Path $repoRoot "MBank-local"
}

$composeDirPath = Resolve-Path $ComposeDir
$composeFile = Join-Path $composeDirPath "docker-compose.yml"
$envFile = Join-Path $composeDirPath ".env"

if (-not (Test-Path $composeFile)) {
    throw "Docker compose file was not found at $composeFile."
}

if (-not (Test-Path $envFile)) {
    throw ".env file was not found at $envFile."
}

function Invoke-Compose {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    docker compose --env-file $envFile -f $composeFile @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-PostgresCommand {
    param([string]$Sql)

    Invoke-Compose exec -T postgres psql -U mbank -d mbank_local -v ON_ERROR_STOP=1 -c $Sql
}

function Invoke-PostgresFile {
    param([string]$Sql)

    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
        Set-Content -LiteralPath $tempFile -Value $Sql -Encoding UTF8
        Get-Content -LiteralPath $tempFile -Raw | docker compose --env-file $envFile -f $composeFile exec -T postgres psql -U mbank -d mbank_local -v ON_ERROR_STOP=1
        if ($LASTEXITCODE -ne 0) {
            throw "psql script failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Remove-Item -LiteralPath $tempFile -Force
    }
}

Invoke-Compose up -d postgres

if ($Mode -eq "Export") {
    if ([string]::IsNullOrWhiteSpace($File)) {
        $artifactDir = Join-Path $repoRoot "artifacts"
        New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $File = Join-Path $artifactDir "mbank-local-memory-export-$timestamp.json"
    }

    $exportSql = @'
SELECT jsonb_pretty(jsonb_build_object(
    'formatVersion', 1,
    'exportedAt', now(),
    'source', 'MBank-local',
    'sourceRecords', COALESCE((
        SELECT jsonb_agg(jsonb_build_object(
            'id', "Id",
            'sourceKind', "SourceKind",
            'sourceText', "SourceText",
            'sourceUrl', "SourceUrl",
            'conversationUrl', "ConversationUrl",
            'metadataJson', "MetadataJson",
            'createdAt', "CreatedAt"
        ) ORDER BY "CreatedAt", "Id")
        FROM source_records
    ), '[]'::jsonb),
    'memories', COALESCE((
        SELECT jsonb_agg(jsonb_build_object(
            'id', "Id",
            'type', "Type",
            'summary', "Summary",
            'subject', "Subject",
            'subjectKind', "SubjectKind",
            'tags', to_jsonb("Tags"),
            'confidence', "Confidence",
            'status', "Status",
            'searchText', "SearchText",
            'embedding', CASE WHEN "Embedding" IS NULL THEN NULL ELSE "Embedding"::text END,
            'sourceRecordId', "SourceRecordId",
            'createdAt', "CreatedAt",
            'updatedAt', "UpdatedAt"
        ) ORDER BY "CreatedAt", "Id")
        FROM memories
    ), '[]'::jsonb),
    'memoryLinks', COALESCE((
        SELECT jsonb_agg(jsonb_build_object(
            'id', "Id",
            'fromMemoryId', "FromMemoryId",
            'toMemoryId', "ToMemoryId",
            'relation', "Relation",
            'description', "Description",
            'createdAt', "CreatedAt"
        ) ORDER BY "CreatedAt", "Id")
        FROM memory_links
    ), '[]'::jsonb)
));
'@

    $export = docker compose --env-file $envFile -f $composeFile exec -T postgres psql -U mbank -d mbank_local -v ON_ERROR_STOP=1 -t -A -c $exportSql
    if ($LASTEXITCODE -ne 0) {
        throw "Memory export failed with exit code $LASTEXITCODE."
    }

    Set-Content -LiteralPath $File -Value ($export -join [Environment]::NewLine) -Encoding UTF8
    Write-Host "Exported MBank-local memories to $File."
    return
}

if ([string]::IsNullOrWhiteSpace($File)) {
    throw "-File is required for Import mode."
}

if (-not (Test-Path $File)) {
    throw "Import file was not found at $File."
}

if ($UserId -eq [Guid]::Empty) {
    throw "-UserId is required for Import mode."
}

if ($ProjectId -eq [Guid]::Empty) {
    throw "-ProjectId is required for Import mode."
}

$resolvedImport = Resolve-Path $File
$containerPath = "/tmp/mbank-memory-import.json"
Invoke-Compose cp $resolvedImport "postgres:$containerPath"

$importSql = @"
DO `$`$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM users WHERE "Id" = '$UserId'::uuid) THEN
        RAISE EXCEPTION 'Target user % does not exist. Create it through /admin/users first.', '$UserId';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM projects WHERE "Id" = '$ProjectId'::uuid) THEN
        RAISE EXCEPTION 'Target project % does not exist. Create it through /admin/projects first.', '$ProjectId';
    END IF;
END
`$`$;

WITH payload AS (
    SELECT pg_read_file('$containerPath')::jsonb AS doc
),
source_rows AS (
    SELECT *
    FROM payload,
    jsonb_to_recordset(doc -> 'sourceRecords') AS x(
        id uuid,
        "sourceKind" text,
        "sourceText" text,
        "sourceUrl" text,
        "conversationUrl" text,
        "metadataJson" text,
        "createdAt" timestamptz
    )
)
INSERT INTO source_records (
    "Id",
    "SourceKind",
    "SourceText",
    "SourceUrl",
    "ConversationUrl",
    "MetadataJson",
    "CreatedAt"
)
SELECT
    id,
    "sourceKind",
    "sourceText",
    "sourceUrl",
    "conversationUrl",
    "metadataJson",
    "createdAt"
FROM source_rows
ON CONFLICT ("Id") DO UPDATE SET
    "SourceKind" = EXCLUDED."SourceKind",
    "SourceText" = EXCLUDED."SourceText",
    "SourceUrl" = EXCLUDED."SourceUrl",
    "ConversationUrl" = EXCLUDED."ConversationUrl",
    "MetadataJson" = EXCLUDED."MetadataJson";

WITH payload AS (
    SELECT pg_read_file('$containerPath')::jsonb AS doc
),
memory_rows AS (
    SELECT *
    FROM payload,
    jsonb_to_recordset(doc -> 'memories') AS x(
        id uuid,
        type text,
        summary text,
        subject text,
        "subjectKind" text,
        tags text[],
        confidence double precision,
        status text,
        "searchText" text,
        embedding text,
        "sourceRecordId" uuid,
        "createdAt" timestamptz,
        "updatedAt" timestamptz
    )
)
INSERT INTO memories (
    "Id",
    "Type",
    "Summary",
    "Subject",
    "SubjectKind",
    "Tags",
    "Confidence",
    "Status",
    "SearchText",
    "Embedding",
    "SourceRecordId",
    "CreatedAt",
    "UpdatedAt",
    "UserId",
    "ProjectId"
)
SELECT
    id,
    type,
    summary,
    subject,
    "subjectKind",
    COALESCE(tags, ARRAY[]::text[]),
    COALESCE(confidence, 0.7),
    COALESCE(status, 'active'),
    COALESCE("searchText", ''),
    NULLIF(embedding, '')::vector,
    "sourceRecordId",
    COALESCE("createdAt", now()),
    COALESCE("updatedAt", now()),
    '$UserId'::uuid,
    '$ProjectId'::uuid
FROM memory_rows
ON CONFLICT ("Id") DO UPDATE SET
    "Type" = EXCLUDED."Type",
    "Summary" = EXCLUDED."Summary",
    "Subject" = EXCLUDED."Subject",
    "SubjectKind" = EXCLUDED."SubjectKind",
    "Tags" = EXCLUDED."Tags",
    "Confidence" = EXCLUDED."Confidence",
    "Status" = EXCLUDED."Status",
    "SearchText" = EXCLUDED."SearchText",
    "Embedding" = EXCLUDED."Embedding",
    "SourceRecordId" = EXCLUDED."SourceRecordId",
    "UpdatedAt" = EXCLUDED."UpdatedAt",
    "UserId" = EXCLUDED."UserId",
    "ProjectId" = EXCLUDED."ProjectId";

WITH payload AS (
    SELECT pg_read_file('$containerPath')::jsonb AS doc
),
link_rows AS (
    SELECT *
    FROM payload,
    jsonb_to_recordset(doc -> 'memoryLinks') AS x(
        id uuid,
        "fromMemoryId" uuid,
        "toMemoryId" uuid,
        relation text,
        description text,
        "createdAt" timestamptz
    )
)
INSERT INTO memory_links (
    "Id",
    "FromMemoryId",
    "ToMemoryId",
    "Relation",
    "Description",
    "CreatedAt"
)
SELECT
    id,
    "fromMemoryId",
    "toMemoryId",
    relation,
    description,
    COALESCE("createdAt", now())
FROM link_rows
ON CONFLICT ("FromMemoryId", "ToMemoryId") DO UPDATE SET
    "Relation" = EXCLUDED."Relation",
    "Description" = EXCLUDED."Description";

SELECT jsonb_build_object(
    'sourceRecords', (SELECT count(*) FROM source_records),
    'memoriesInProject', (SELECT count(*) FROM memories WHERE "ProjectId" = '$ProjectId'::uuid),
    'memoryLinks', (SELECT count(*) FROM memory_links)
) AS import_summary;
"@

Invoke-PostgresFile $importSql
Write-Host "Imported memories from $resolvedImport into project $ProjectId for user $UserId."
