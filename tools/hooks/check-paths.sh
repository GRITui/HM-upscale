#!/usr/bin/env bash
# Reads repo-relative paths on stdin; prints BLOCKED lines and exits 1 if any
# path looks like copyrighted game data. Shared by the pre-commit hook and CI.
# Everything extracted or converted from the game lives under the repo-root
# assets/ folder (git-ignored), e.g. assets/hm-raw and assets/hm-converted.
set -uo pipefail
BAD_EXT='\.(iso|chd|cso|pbp|bin|hdt|prx|sfo|at3|at9|pmf|gim|pak|elf|vag|tm2|dat|gz)$'
BAD_PATH='^assets/|(^|/)EBOOT[^/]*$'
MAX_BYTES=$((5 * 1024 * 1024))
fail=0
while IFS= read -r f; do
  [ -n "$f" ] || continue
  if echo "$f" | grep -Eiq "$BAD_EXT|$BAD_PATH"; then
    echo "BLOCKED (game-data path/extension): $f"; fail=1; continue
  fi
  [ -f "$f" ] || continue
  sz=$(wc -c < "$f")
  if [ "$sz" -gt "$MAX_BYTES" ]; then
    echo "BLOCKED (>5 MB): $f ($sz bytes)"; fail=1
  fi
done
exit "$fail"
