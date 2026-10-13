#!/bin/bash
# Launch PPSSPPSDL windowed with the remote debugger on, logging to OUTDIR.
# usage: launch.sh ISO OUTDIR [STATE.ppst]   (STATE is loaded at boot, skipping the intro)
set -u
ISO="$1"; OUT="$2"; STATE="${3:-}"; mkdir -p "$OUT"
STATEARG=(); [ -n "$STATE" ] && STATEARG=(--state="$STATE")
cat > "$OUT/pp.ini" <<INI
[General]
RemoteDebuggerOnStartup = True
PauseOnLostFocus = False
INI
pkill -f "PPSSPPSDL.app/Contents/MacOS/PPSSPPSDL" 2>/dev/null
nohup /Applications/PPSSPPSDL.app/Contents/MacOS/PPSSPPSDL --windowed \
  --appendconfig="$OUT/pp.ini" --log="$OUT/ppsspp.log" "${STATEARG[@]}" "$ISO" >"$OUT/stdout.log" 2>&1 &
# wait (bounded) for the debugger port to show up
for i in $(seq 1 40); do
  if lsof -nP -iTCP -sTCP:LISTEN -a -c PPSSPPSDL 2>/dev/null | grep -q LISTEN; then exit 0; fi
  sleep 1
done
echo "debugger port not found" >&2; exit 1
