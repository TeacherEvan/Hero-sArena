#!/usr/bin/env bash
# tools/smoke_scenes.sh — boot each main scene headless and fail if any C#
# exception frame ("at HeroArena.") appears. Catches the "scene parses but is
# dead at runtime" bug class (missing node_paths wiring, invalid node types,
# unwired [Export] node references) that unit tests cannot see.
#
# Env overrides:
#   DOTNET_ROOT  (default: ~/.dotnet8)   CI: setup-dotnet already exports it
#   GODOT_BIN    (default: godot on PATH, else ~/.local/bin/godot-mono)
#
# Exit: 0 = all scenes clean, 1 = at least one scene threw.
set -u

DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet8}"
export DOTNET_ROOT
if [ -n "${GODOT_BIN:-}" ]; then
    GODOT="$GODOT_BIN"
elif command -v godot > /dev/null 2>&1; then
    GODOT="$(command -v godot)"
elif [ -x "$HOME/.local/bin/godot-mono" ]; then
    GODOT="$HOME/.local/bin/godot-mono"
else
    echo "smoke_scenes: no godot binary found (set GODOT_BIN)" >&2
    exit 1
fi

SCENES=(
    scenes/Main.tscn
    scenes/ui/MainMenu.tscn
    scenes/ui/Tutorial.tscn
    scenes/ui/UpgradeMenu.tscn
    scenes/maps/Sandbox.tscn
)

LOG="${SMOKE_LOG:-/tmp/herosarena-smoke.log}"
fail=0
for scene in "${SCENES[@]}"; do
    # Scenes run until killed: the timeout is the normal end of a clean boot.
    timeout 20 "$GODOT" --headless --path . "res://$scene" > "$LOG" 2>&1
    if grep -q "at HeroArena" "$LOG"; then
        echo "SMOKE FAIL: $scene"
        grep -m3 -A2 "at HeroArena" "$LOG"
        fail=1
    else
        echo "SMOKE OK: $scene"
    fi
done
exit $fail
