#!/usr/bin/env sh
set -eu

DEFAULT_CLIENT="all"
CLIENT="$DEFAULT_CLIENT"
SERVER_URL="${MBANK_SERVER_URL:-}"
YES="0"

usage() {
    cat <<USAGE
Usage: bootstrap-agent.sh [--client codex|claude|cursor|all] [--server-url URL] [--yes]

Provide --server-url or set MBANK_SERVER_URL.
Fetches the canonical MBank instruction block from a running MBank server and
adds or updates it in repo agent instruction files. By default, it updates
AGENTS.md, CLAUDE.md, and .cursor/rules/mbank.mdc. The script only changes
content between the mbank:start and mbank:end markers.
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
        --server-url)
            if [ "$#" -lt 2 ]; then
                echo "Missing value for --server-url." >&2
                exit 1
            fi
            SERVER_URL="${2:-}"
            shift 2
            ;;
        --server-url=*)
            SERVER_URL="${1#*=}"
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

if [ -z "$SERVER_URL" ]; then
    echo "Pass --server-url with the running MBank server URL or set MBANK_SERVER_URL." >&2
    exit 1
fi

targets_for_client() {
    case "$CLIENT" in
        codex)
            printf '%s|%s\n' "codex" "AGENTS.md"
            ;;
        claude)
            printf '%s|%s\n' "claude" "CLAUDE.md"
            ;;
        cursor)
            printf '%s|%s\n' "cursor" ".cursor/rules/mbank.mdc"
            ;;
        all)
            printf '%s|%s\n' "codex" "AGENTS.md"
            printf '%s|%s\n' "claude" "CLAUDE.md"
            printf '%s|%s\n' "cursor" ".cursor/rules/mbank.mdc"
            ;;
    esac
}

fetch_block() {
    url="${SERVER_URL%/}/onboarding/agent-instructions"
    if ! block=$(curl -fsSL "$url"); then
        echo "Failed to fetch MBank instructions from $url" >&2
        echo "Start MBank or pass --server-url with the running MBank server URL." >&2
        exit 1
    fi

    if ! printf '%s\n' "$block" | grep -q '<!--[[:space:]]*mbank:start'; then
        echo "MBank server returned an invalid instruction block." >&2
        exit 1
    fi

    printf '%s\n' "$block"
}

replace_block() {
    file="$1"
    block_file="$2"
    tmp="${file}.mbank.tmp"

    if [ ! -f "$file" ]; then
        mkdir -p "$(dirname "$file")"
        cat "$block_file" > "$file"
        return
    fi

    if grep -q '<!--[[:space:]]*mbank:start' "$file" && grep -q '<!--[[:space:]]*mbank:end[[:space:]]*-->' "$file"; then
        awk -v block_file="$block_file" '
            function print_block() {
                while ((getline line < block_file) > 0) {
                    print line
                }
                close(block_file)
            }
            /<!--[[:space:]]*mbank:start/ {
                if (!printed_mbank) {
                    print_block()
                    printed_mbank = 1
                }
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
        printf '\n'
        cat "$block_file"
    } > "$tmp"
    mv "$tmp" "$file"
}

tmp_dir=$(mktemp -d 2>/dev/null || mktemp -d -t mbank-bootstrap)
trap 'rm -rf "$tmp_dir"' EXIT

targets_file="$tmp_dir/targets"
block_file="$tmp_dir/mbank-instructions.md"
targets_for_client > "$targets_file"

echo "MBank agent instruction bootstrap"
echo
echo "Fetching canonical instruction block from: ${SERVER_URL%/}"
echo

fetch_block > "$block_file"

echo "This will add or update the versioned MBank block in:"
while IFS='|' read -r _ path; do
    echo "- $path"
done < "$targets_file"
echo
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

while IFS='|' read -r target path; do
    replace_block "$path" "$block_file"
    echo "Updated $path"
done < "$targets_file"

echo
echo "Done. The MBank block was fetched from ${SERVER_URL%/}."
