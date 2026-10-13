#!/bin/bash
# Capture the PPSSPPSDL window. Needs the Screen Recording permission.
#   winshot.sh OUT.png               one screenshot
#   winshot.sh -v SECONDS OUT.mov    window video for SECONDS seconds
PID=$(pgrep -n -f "PPSSPPSDL.app/Contents/MacOS/PPSSPPSDL")
ID=$(swift -e "import CoreGraphics
let l = CGWindowListCopyWindowInfo(.optionOnScreenOnly, kCGNullWindowID) as! [[String:Any]]
for w in l where (w[\"kCGWindowOwnerPID\"] as? Int) == $PID && (w[\"kCGWindowLayer\"] as? Int) == 0 { print(w[\"kCGWindowNumber\"]!); break }" 2>&1 | tail -1)
if [ "$1" = "-v" ]; then
  screencapture -x -V"$2" -l"$ID" "$3"
else
  screencapture -x -l"$ID" "$1"
fi
