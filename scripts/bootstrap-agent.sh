#!/usr/bin/env sh
set -eu

# Bump this version whenever the MBank instruction block changes.
VERSION="1.0.0"
DEFAULT_CLIENT="codex"
CLIENT="$DEFAULT_CLIENT"
YES="0"

usage() {
    cat <<USAGE
Usage: bootstrap-agent.sh [--client codex|claude|cursor|all] [--yes]

Adds or updates the versioned MBank instruction block in repo agent instruction files.
The script only changes content between the mbank:start and mbank:end markers.
USAGE
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --client)
            CLIENT="${2:-}"
            shift 2
            ;;
        --client=*)
            CLIENT="${1#*=}"
            shift
            ;;
        --yes|-y)
            YES="1"
            shift
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 1
            ;;
    esac
done

case "$CLIENT" in
    codex|claude|cursor|all) ;;
    *)
        echo "Unsupported client: $CLIENT" >&2
        usage >&2
        exit 1
        ;;
esac

BLOCK=$(cat <<BLOCK_TEXT
<!-- mbank:start version="$VERSION" -->
## MBank Memory

Use the configured \`mbank\` MCP server proactively for durable project memory.

At the start of a new product, planning, implementation, debugging, or review task, call \`get_task_context\` with a structured task description before proposing a plan or making changes.

Use \`remember\` when the user provides durable product or implementation guidance that is likely to matter in future sessions, such as product direction, architectural decisions, coding conventions, constraints, or next steps.

Use \`get\` to inspect full memory details when a retrieved memory needs source context.

Use \`report_conflict\` when memories appear inconsistent. Surface the conflict to the user instead of deciding truth automatically.

Do not store ordinary conversation, temporary reasoning, or details only relevant to the current response.

If MBank reports that this instruction block is missing or outdated, ask the user before updating this file or running the bootstrap script. Do not modify agent instruction files without explicit user approval.
<!-- mbank:end -->
BLOCK_TEXT
)

targets_for_client() {
    case "$CLIENT" in
        codex)
            printf '%s\n' "AGENTS.md"
            ;;
        claude)
            printf '%s\n' "CLAUDE.md"
            ;;
        cursor)
            printf '%s\n' ".cursor/rules/mbank.mdc"
            ;;
        all)
            printf '%s\n' "AGENTS.md" "CLAUDE.md" ".cursor/rules/mbank.mdc"
            ;;
    esac
}

replace_block() {
    file="$1"
    tmp="${file}.mbank.tmp"

    if [ ! -f "$file" ]; then
        mkdir -p "$(dirname "$file")"
        printf '%s\n' "$BLOCK" > "$file"
        return
    fi

    if grep -q '<!--[[:space:]]*mbank:start' "$file" && grep -q '<!--[[:space:]]*mbank:end[[:space:]]*-->' "$file"; then
        awk -v block="$BLOCK" '
            /<!--[[:space:]]*mbank:start/ {
                print block
                in_mbank = 1
                next
            }
            /<!--[[:space:]]*mbank:end[[:space:]]*-->/ {
                in_mbank = 0
                next
            }
            !in_mbank {
                print
            }
        ' "$file" > "$tmp"
        mv "$tmp" "$file"
        return
    fi

    {
        cat "$file"
        printf '\n%s\n' "$BLOCK"
    } > "$tmp"
    mv "$tmp" "$file"
}

echo "MBank agent instruction bootstrap"
echo
echo "This will add or update the versioned MBank block in:"
targets_for_client | sed 's/^/- /'
echo
echo "Version: $VERSION"
echo "Repo: $(pwd)"
echo

if [ "$YES" != "1" ]; then
    printf 'Proceed? [y/N] '
    if [ -r /dev/tty ]; then
        read answer < /dev/tty
    else
        read answer || answer=""
    fi
    case "$answer" in
        y|Y|yes|YES) ;;
        *)
            echo "No files changed."
            exit 0
            ;;
    esac
fi

targets_for_client | while IFS= read -r target; do
    replace_block "$target"
    echo "Updated $target"
done

echo
echo "Done. The MBank block is version $VERSION."
