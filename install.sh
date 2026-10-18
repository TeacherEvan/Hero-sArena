#!/usr/bin/env bash
#
# Hero's Arena — Installer
#
# One window, basic settings, then "Play HeroArena" on your desktop icon.
#
# What it does:
#   * Copies the pre-built Linux game (HeroArena.x86_64 + .pck + .NET assemblies) to an
#     install location you choose.
#   * Makes the binary executable.
#   * Generates a "Play HeroArena" desktop icon + applications-menu entry.
#   * Optionally launches the game when install finishes.
#
# Requirements:
#   * zenity            (GUI dialogs) — present on this machine
#   * build/linux/HeroArena.x86_64  +  build/linux/HeroArena.pck
#   * build/linux/data_Hero-sArena_linuxbsd_x86_64  (.NET assemblies)
#   * icon.svg          (window icon, copied into the install folder)
#
# Exit codes:
#   0  success
#   1  user cancelled
#   2  missing source artifact(s)
#   3  install failure
#
set -u

# ── Resolve repo root (directory of this script) ──────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

GAME_NAME="Hero's Arena"
DESKTOP_NAME="Play HeroArena"
APP_ID="play-heroarena"
SOURCE_BIN="build/linux/HeroArena.x86_64"
SOURCE_PCK="build/linux/HeroArena.pck"
SOURCE_ICON="icon.svg"
SOURCE_DATA_DIR="build/linux/data_Hero-sArena_linuxbsd_x86_64"
if [ ! -d "$SOURCE_DATA_DIR" ]; then
    discovered_data_dir="$(find build/linux -maxdepth 1 -type d -name "data_*_linuxbsd*" 2>/dev/null | head -n 1)"
    if [ -n "$discovered_data_dir" ] && [ -d "$discovered_data_dir" ]; then
        SOURCE_DATA_DIR="$discovered_data_dir"
    fi
fi
LOG_FILE="$HOME/.local/share/HeroArena/install.log"

# ── Uninstall ─────────────────────────────────────────────────────────────
# Removed when invoked with:  install.sh --uninstall
do_uninstall() {
    local install_location="$HOME/.local/share/HeroArena"
    local removed=0

    if [ -d "$install_location" ]; then
        rm -rf "$install_location" && log "Removed install dir: $install_location" && removed=1
    else
        log "Install dir not present: $install_location"
    fi

    for f in "$HOME/Desktop/$DESKTOP_NAME.desktop" \
             "$HOME/.local/share/applications/${APP_ID}.desktop"; do
        if [ -e "$f" ]; then
            rm -f "$f" && log "Removed: $f" && removed=1
        fi
    done

    if command -v update-desktop-database >/dev/null 2>&1; then
        update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
    fi

    if [ "$removed" -eq 1 ]; then
        zenity --info --title="$GAME_NAME — Uninstalled" \
            --text="$GAME_NAME has been removed completely." \
            --width=360 2>/dev/null || true
        log "Uninstall complete."
    else
        zenity --info --title="$GAME_NAME — Uninstall" \
            --text="Nothing installed to remove." \
            --width=360 2>/dev/null || true
        log "Nothing to uninstall."
    fi
    exit 0
}

# ── Logging helper ────────────────────────────────────────────────────────
log() {
    mkdir -p "$(dirname "$LOG_FILE")" 2>/dev/null
    printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" | tee -a "$LOG_FILE"
}

die() {
    log "ERROR: $*"
    zenity --error --title="$GAME_NAME Installer" --text="$*" --width=400 2>/dev/null
    exit "${2:-3}"
}

# ── Verify source artifacts exist ─────────────────────────────────────────
verify_sources() {
    local missing=0
    for f in "$SOURCE_BIN" "$SOURCE_PCK" "$SOURCE_ICON" "$SOURCE_DATA_DIR"; do
        if [ ! -e "$f" ]; then
            log "MISSING source artifact: $f"
            missing=1
        fi
    done
    if [ "$missing" -ne 0 ]; then
        die "Build artifacts not found. Run the game build first (expected: $SOURCE_BIN, $SOURCE_PCK, $SOURCE_ICON, and $SOURCE_DATA_DIR)." 2
    fi
    log "Source artifacts verified: $SOURCE_BIN, $SOURCE_PCK, $SOURCE_ICON, $SOURCE_DATA_DIR"
}

# ── GUI: welcome ──────────────────────────────────────────────────────────
welcome() {
    zenity --question \
        --title="$GAME_NAME — Installer" \
        --text="<b>$GAME_NAME</b>\n\nA top-down arena shooter built with Godot 4.3 + C# (.NET 8).\n\nClick <b>Install</b> to set up the game and add a \"$DESKTOP_NAME\" icon to your desktop and applications menu." \
        --ok-label="Install" \
        --cancel-label="Cancel" \
        --width=420 \
        2>/dev/null
    return $?
}

# ── GUI: basic settings ───────────────────────────────────────────────────
# Returns a newline-separated list of chosen options on stdout.
settings() {
    zenity --list \
        --title="$GAME_NAME — Basic settings" \
        --text="Choose install options:" \
        --checklist \
        --column="✓" --column="Setting" --column="Description" \
        TRUE   "Install location"   "Install to ~/.local/share/HeroArena" \
        TRUE   "Desktop icon"       "Add \"$DESKTOP_NAME\" icon to ~/Desktop" \
        TRUE   "Applications menu"  "Add \"$DESKTOP_NAME\" to your applications menu" \
        FALSE  "Launch after install" "Start the game once install finishes" \
        --separator=$'\n' \
        --width=560 \
        --height=340 \
        2>/dev/null
    return $?
}

# ── GUI: progress ─────────────────────────────────────────────────────────
# Pulses while work runs in the background; we feed it percent steps.
progress() {
    # Called by caller; this is just a helper to create the dialog.
    :
}

# ── Install core ──────────────────────────────────────────────────────────
do_install() {
    local install_location="$1"
    local want_desktop="$2"
    local want_menu="$3"
    local want_launch="$4"

    log "Install starting: location=$install_location desktop=$want_desktop menu=$want_menu launch=$want_launch"

    # Make install dir
    mkdir -p "$install_location" || die "Cannot create install directory: $install_location"

    # Copy artifacts
    # Note: rm -f first to unlink the inode in case the game binary is running/open (avoids ETXTBSY)
    rm -f "$install_location/HeroArena.x86_64" 2>/dev/null || true
    cp "$SOURCE_BIN" "$install_location/HeroArena.x86_64" || die "Failed to copy game binary."
    cp "$SOURCE_PCK" "$install_location/HeroArena.pck"     || die "Failed to copy game data pack."
    cp "$SOURCE_ICON" "$install_location/icon.png"           || die "Failed to copy game icon."
    cp -r "$SOURCE_DATA_DIR" "$install_location/"          || die "Failed to copy .NET assemblies directory."
    chmod +x "$install_location/HeroArena.x86_64"          || die "Failed to make binary executable."
    rm -f "$install_location/Hero-sArena.x86_64" "$install_location/Hero-sArena.pck" 2>/dev/null || true
    log "Artifacts installed to $install_location"

    # Write desktop entry (applications menu)
    local desktop_file="$HOME/.local/share/applications/${APP_ID}.desktop"
    if [ "$want_menu" = "TRUE" ] || [ "$want_desktop" = "TRUE" ]; then
        mkdir -p "$HOME/.local/share/applications"
        cat > "$desktop_file" <<DESKTOP
[Desktop Entry]
Name=$DESKTOP_NAME
GenericName=$GAME_NAME
Comment=A top-down arena shooter built with Godot 4.3 and C# (.NET 8)
Exec=$install_location/HeroArena.x86_64
Path=$install_location
Icon=$install_location/icon.png
Type=Application
Categories=Game;ActionGame;
Terminal=false
StartupNotify=true
DESKTOP
        chmod +x "$desktop_file"
        log "Applications-menu entry written: $desktop_file"
    fi

    # Desktop icon
    if [ "$want_desktop" = "TRUE" ]; then
        mkdir -p "$HOME/Desktop"
        cp "$desktop_file" "$HOME/Desktop/$DESKTOP_NAME.desktop" 2>/dev/null || {
            # Fallback: write it directly if applications entry wasn't made
            mkdir -p "$HOME/.local/share/applications"
            cat > "$HOME/Desktop/$DESKTOP_NAME.desktop" <<DESKTOP
[Desktop Entry]
Name=$DESKTOP_NAME
GenericName=$GAME_NAME
Comment=A top-down arena shooter built with Godot 4.3 and C# (.NET 8)
Exec=$install_location/HeroArena.x86_64
Path=$install_location
Icon=$install_location/icon.png
Type=Application
Categories=Game;ActionGame;
Terminal=false
StartupNotify=true
DESKTOP
            chmod +x "$HOME/Desktop/$DESKTOP_NAME.desktop"
        }
        chmod +x "$HOME/Desktop/$DESKTOP_NAME.desktop"
        log "Desktop icon written: $HOME/Desktop/$DESKTOP_NAME.desktop"
    fi

    # Refresh desktop database so the icon appears in menus
    if command -v update-desktop-database >/dev/null 2>&1; then
        update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
    fi

    log "Install complete."

    # Launch option
    if [ "$want_launch" = "TRUE" ]; then
        log "Launching game after install."
        (
            cd "$install_location"
            setsid ./HeroArena.x86_64 >"$LOG_FILE" 2>&1 &
        )
    fi

    # Done dialog
    zenity --info \
        --title="$GAME_NAME — Installed" \
        --text="<b>$GAME_NAME installed successfully.</b>\n\nLocation: $install_location\n\nDouble-click the \"$DESKTOP_NAME\" icon on your desktop to play.\n\nInstall log: $LOG_FILE" \
        --ok-label="Done" \
        --width=460 \
        2>/dev/null
    return 0
}

# ── Main ──────────────────────────────────────────────────────────────────
main() {
    : > "$LOG_FILE"
    log "=== $GAME_NAME Installer started ==="

    if [ "${1:-}" = "--uninstall" ]; then
        do_uninstall
    fi

    verify_sources

    if ! welcome; then
        log "User cancelled at welcome."
        exit 1
    fi

    local chosen
    if ! chosen=$(settings); then
        log "User cancelled at settings."
        exit 1
    fi

    # Parse checklist results
    local want_loc="TRUE"   # always on (checked, non-removable)
    local want_desktop="FALSE"
    local want_menu="FALSE"
    local want_launch="FALSE"
    while IFS= read -r line; do
        case "$line" in
            "Install location"*)   want_loc="TRUE"   ;;
            "Desktop icon"*)       want_desktop="TRUE" ;;
            "Applications menu"*)  want_menu="TRUE"   ;;
            "Launch after install"*) want_launch="TRUE" ;;
        esac
    done <<< "$chosen"

    # Default install location
    local install_location="$HOME/.local/share/HeroArena"

    # Let user pick a different location if they want
    if zenity --question \
        --title="$GAME_NAME — Install location" \
        --text="Install to:\n$install_location\n\nClick OK to confirm, Cancel to choose another folder." \
        --ok-label="OK" \
        --cancel-label="Choose folder" \
        --width=420 \
        2>/dev/null; then
        : # use default
    else
        local picked
        picked=$(zenity --file-selection --directory \
            --title="Choose install folder" \
            --filename="$HOME" \
            --width=560 \
            --height=420 \
            2>/dev/null) || die "No folder chosen; install aborted."
        install_location="$picked"
        log "Custom install location: $install_location"
    fi

    do_install "$install_location" "$want_desktop" "$want_menu" "$want_launch"
    local rc=$?
    log "=== Installer finished (exit $rc) ==="
    exit $rc
}

main "$@"
