#!/usr/bin/env bash
# Self-test for check-paths.sh. Exits non-zero on any wrong verdict.
set -u
here=$(cd "$(dirname "$0")" && pwd)
bad=0
expect() { # expect <block|pass> <path>
  if printf '%s\n' "$2" | "$here/check-paths.sh" >/dev/null; then got=pass; else got=block; fi
  [ "$got" = "$1" ] || { echo "FAIL: $2 expected $1 got $got"; bad=1; }
}
for p in EBOOT.BIN PSP_GAME/SYSDIR/EBOOT.BIN x/EBOOT.elf game.iso a.chd boot.elf voice.vag \
         tex.tm2 map.dat blob.gz assets/hm-converted/map.json assets/hm-raw/x.txt UMD.PAK; do
  expect block "$p"
done
for p in unity/Assets/_HM/Core/GameBootstrap.cs unity/Assets/_HM/Gameplay/Bed.cs \
         docs/performance.md tools/hooks/check-paths.sh unity/Assets/_HM/Art/Models/farmhouse.glb; do
  expect pass "$p"
done
[ "$bad" -eq 0 ] && echo "check-paths: all verdicts correct"
exit "$bad"
