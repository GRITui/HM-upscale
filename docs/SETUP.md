# Tooling setup (Phase 1)

Host: macOS (Apple Silicon). Game data lives only in `assets/hm-raw/` (git-ignored). Never commit it.

## Tool versions

| Tool | Version | Install |
|------|---------|---------|
| Node | 26.10.0 (rea needs 22.19+) | preinstalled |
| Bun | 1.4.2 | preinstalled |
| Python | 3.14.8 | preinstalled |
| ffmpeg | 9.0.2 | preinstalled |
| gh | 2.98.0 | preinstalled |
| chdman | 0.289 | `brew install rom-tools` |
| Ghidra | 12.1.4 PUBLIC | `brew install ghidra` (launch: `ghidraRun`) |
| ghidra-allegrex | v21.4 (built for 12.1.3, patched to 12.1.4) | see below |
| PPSSPP | 1.20.4 | `/Applications/PPSSPPSDL.app` |
| rea (rea-agents) | 6.4.0 | `npx -y rea-agents setup` |

## Checksums (SHA-1)

| File | SHA-1 |
|------|-------|
| `assets/hm-raw/iso/hm.iso` | `c3c966e14356421f52f32476499dab45f8581d38` |
| `assets/hm-raw/iso/hm.chd` | `e1ddf30105c518530f8b154191455e3fce517313` |
| ghidra-allegrex zip (v21.4) | `de7eaf212ac3fc7396b802fe5430cc5b6be335da` |

Verify: `shasum -a1 assets/hm-raw/iso/hm.iso`

## ISO from CHD

    chdman info -i assets/hm-raw/iso/hm.chd
    chdman extractdvd -i assets/hm-raw/iso/hm.chd -o assets/hm-raw/iso/hm.iso   # fall back to extractcd if CD layout

## Mount and extract the ISO

`bsdtar` extracts nothing from this ISO; use the macOS mount and copy.

    hdiutil attach -readonly -nobrowse assets/hm-raw/iso/hm.iso
    # note the printed /Volumes/<name>
    cp -R "/Volumes/<name>/." assets/hm-raw/extracted/
    hdiutil detach "/Volumes/<name>"

Result: `assets/hm-raw/extracted/PSP_GAME/`, `UMD_DATA.BIN` (21 files, about 306 MB).

## rea

Non-interactive registration with Claude Code (interactive prompts avoided with `--client` and `--yes`):

    export GHIDRA_INSTALL_DIR=/opt/homebrew/Cellar/ghidra/12.1.4/libexec
    npx -y rea-agents setup --client claude_code --yes

Installed:
- MCP server `rea` in `~/.claude.json` (backup `~/.claude.json.rea.backup`), command `npx -y rea-agents@6.4.0 mcp`. `claude mcp list` shows it Connected. Restart Claude Code to load it.
- Skill `reverse-engineer-anything` in `~/.claude/skills/`.

Invoke: `npx -y rea-agents doctor` (health), `npx -y rea-agents --help` (CLI commands), or use the MCP tools / skill from Claude Code.
Doctor reports Ghidra healthy. Hopper and IDA are not installed (optional). Pi and Hermes clients were detected but not configured.
`GHIDRA_INSTALL_DIR` must be set (or detected) for the Ghidra provider.

## Ghidra Allegrex (PSP CPU) support

Source: https://github.com/kotcrab/ghidra-allegrex (processor module, language `Allegrex:LE:32:default`; not a file loader).

    D=~/Library/ghidra/ghidra_12.1.4_PUBLIC/Extensions
    mkdir -p "$D" && cd "$D"
    gh release download v21.4 -R kotcrab/ghidra-allegrex -p '*.zip'
    unzip -o *.zip
    sed -i '' 's/^version=.*/version=12.1.4/' ghidra-allegrex/extension.properties

The release targets Ghidra 12.1.3; the version line was patched so 12.1.4 accepts it. Then enable it in Ghidra: File > Install Extensions (tick ghidra-allegrex), restart.

Verified headless: the extension scripts path loads and a binary import with
`-processor "Allegrex:LE:32:default" -loader BinaryLoader` succeeds.

Known limit: `PSP_GAME/SYSDIR/EBOOT.BIN` is encrypted (`~PSP` header), so Ghidra's auto-loader finds no load spec. It must be decrypted first (tracked in issue #4). No separate PSP PRX/ELF loader was found; decrypted ELF/PRX files import with the standard ELF loader once the Allegrex language exists.

## Baseline capture

    /Applications/PPSSPPSDL.app/Contents/MacOS/PPSSPPSDL assets/hm-raw/iso/hm.iso

Boots to the title screen (ULUS10142, Harvest Moon: Boy & Girl). Capture the emulator window only (full-screen capture picks up other apps):

    osascript -e 'tell application "System Events" to tell process "PPSSPPSDL" to get {position, size} of window 1'
    screencapture -x -R<x>,<y>,<w>,<h> assets/hm-raw/baseline/title_screen.png
    screencapture -x -V 6 -R<x>,<y>,<w>,<h> assets/hm-raw/baseline/title_clip.mov

Saved in `assets/hm-raw/baseline/` (git-ignored): `title_screen.png`, `title_clip.mov`, `ppsspp.log`.

Not achieved: gameplay footage. Reaching gameplay needs controller input past the title menu, and unattended key injection was not set up. Capture manually (PPSSPP: File > Record > Record video, or screencapture as above) and drop files in the same folder.

## Reproduce from scratch

1. `brew install rom-tools ghidra`; install PPSSPP.
2. Place `hm.chd` in `assets/hm-raw/iso/`, verify SHA-1, run the chdman commands.
3. Verify the ISO SHA-1, mount and extract as above.
4. Run the rea and Ghidra Allegrex steps.
5. Boot the ISO in PPSSPP and capture the baseline.
