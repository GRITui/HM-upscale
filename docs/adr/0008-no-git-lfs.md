# ADR 0008: Do not use Git LFS

Status: accepted (2026-10-13)

## Context

The repository holds code, specs and synthetic test fixtures only. Game data, converted
assets and ROM-like files live under the git-ignored `assets/` tree and are blocked by
`tools/hooks/check-paths.sh` (blocked extensions, 5 MB cap). A scan of tracked files on
2026-10-13 found nothing above 155 KB.

## Decision

Git LFS is not used. There is no large binary that we are allowed to commit, so LFS would
only add a second place where game data could leak. If derived work of our own is ever
published, revisit this decision in a new ADR.

## Consequences

- The 5 MB hook cap stays the single size guard.
- `tools/hooks/test-precommit-e2e.sh` proves in a temporary clone that an .iso, .chd,
  EBOOT-like file and an oversize file are rejected.
