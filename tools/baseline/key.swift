// Post one key press to the frontmost window through the HID event tap. PPSSPP's debugger
// socket cannot press the save/load-state hotkeys, so those go through here.
//
// usage:  swiftc key.swift -o /tmp/hmkey && /tmp/hmkey <macos-keycode>
// macOS keycodes: F2 = 120 (save state), F4 = 118 (load state), Escape = 53.
// The PPSSPP window must be frontmost (see capture.py focus()). Arrow keys posted this way
// did NOT reach the game, so movement uses the debugger's virtual pad instead.
import Foundation
import CoreGraphics
let a = CommandLine.arguments
guard a.count >= 2, let code = UInt16(a[1]) else { print("usage: hmkey <keycode>"); exit(2) }
for down in [true, false] {
    CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: down)!.post(tap: .cghidEventTap)
    usleep(120_000)
}
