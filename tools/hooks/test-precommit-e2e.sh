#!/usr/bin/env bash
# End-to-end check: in a throwaway clone with the hook installed, real `git commit`
# attempts containing blocked files must be rejected, and a harmless file must pass.
# Files are synthetic placeholders; nothing here is game data. Leaves the source repo untouched.
set -u
src=$(cd "$(dirname "$0")/../.." && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
git clone -q "$src" "$tmp/c" || { echo "FAIL: clone"; exit 1; }
cd "$tmp/c" || exit 1
git config user.email t@example.invalid; git config user.name t
git config commit.gpgsign false
bash tools/install-hooks.sh >/dev/null
bad=0
reject() { # reject <label> <path> [bytes]
  mkdir -p "$(dirname "$2")"; head -c "${3:-16}" /dev/zero > "$2"
  git add -f -- "$2"
  if git commit -q -m "probe $1" >/dev/null 2>&1; then echo "FAIL: $1 ($2) was committed"; bad=1; git reset -q --hard HEAD~1
  else echo "ok: $1 ($2) rejected"; fi
  git reset -q; rm -f "$2"
}
reject iso-file game.iso
reject chd-file image.chd
reject eboot-file PSP_GAME/SYSDIR/EBOOT.BIN
reject eboot-elf EBOOT.elf
reject assets-dir assets/hm-raw/x.txt
reject oversize notes/big.txt $((5 * 1024 * 1024 + 1))
echo "probe" > docs/_probe.md; git add docs/_probe.md
if git commit -q -m "probe harmless" >/dev/null 2>&1; then echo "ok: harmless file accepted"; else echo "FAIL: harmless file rejected"; bad=1; fi
[ "$bad" -eq 0 ] && echo "precommit-e2e: all verdicts correct"
exit "$bad"
