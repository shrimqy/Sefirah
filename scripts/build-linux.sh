#!/usr/bin/env bash
# Build, install and run the Sefirah desktop app on Linux.
#
# Usage: scripts/build-linux.sh [options]
#   -c, --configuration <cfg>  Release (default) or Debug
#   -a, --arch <arch>          x64 (default) or arm64
#   -i, --install              Link the build into ~/.local and add a desktop entry and icons
#   -r, --run                  Stop any running instance and start the new build
#       --uninstall            Remove what --install added, then exit
#   -h, --help                 Show this help
#
# The install is a symlink to the publish folder, so after one --install,
# every later build updates the installed app.
#
# The code uses preview C# features that need a .NET 11 SDK. The script uses
# ~/.dotnet when it has one (install it with Microsoft's dotnet-install.sh:
# --channel 11.0 --quality preview --install-dir ~/.dotnet), otherwise the
# dotnet on PATH.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$REPO_ROOT/src/Sefirah/Sefirah.csproj"
DIST="$REPO_ROOT/dist"
APP_ID="com.castle.sefirah"
MIN_SDK_MAJOR=11

INSTALL_DIR="$HOME/.local/opt/sefirah"
BIN_LINK="$HOME/.local/bin/Sefirah"
DESKTOP_FILE="$HOME/.local/share/applications/$APP_ID.desktop"
ICON_ROOT="$HOME/.local/share/icons/hicolor"

configuration=Release
arch=x64
do_install=false
do_run=false

usage() { sed -n '2,/^$/{s/^# \{0,1\}//;p}' "${BASH_SOURCE[0]}"; }
die() { echo "error: $*" >&2; exit 1; }

uninstall() {
    rm -f "$BIN_LINK" "$DESKTOP_FILE"
    [ -L "$INSTALL_DIR" ] && rm -f "$INSTALL_DIR"
    find "$ICON_ROOT" -name "$APP_ID.*" -path "*/apps/*" -delete 2>/dev/null || true
    update-desktop-database "$(dirname "$DESKTOP_FILE")" 2>/dev/null || true
    echo "Removed the Sefirah install. App data in ~/.local/share was left alone."
}

while [ $# -gt 0 ]; do
    case "$1" in
        -c|--configuration) configuration="${2:?missing value for $1}"; shift ;;
        -a|--arch) arch="${2:?missing value for $1}"; shift ;;
        -i|--install) do_install=true ;;
        -r|--run) do_run=true ;;
        --uninstall) uninstall; exit 0 ;;
        -h|--help) usage; exit 0 ;;
        *) die "unknown option '$1' (see --help)" ;;
    esac
    shift
done

case "$configuration" in Release|Debug) ;; *) die "configuration must be Release or Debug" ;; esac
case "$arch" in
    x64) rid=linux-x64; platform=x64 ;;
    arm64) rid=linux-arm64; platform=ARM64 ;;
    *) die "arch must be x64 or arm64" ;;
esac

# Pick a dotnet whose newest SDK is new enough for the preview language features
sdk_major() { "$1" --list-sdks 2>/dev/null | sed -n 's/^\([0-9]*\)\..*/\1/p' | sort -n | tail -1; }

dotnet_bin=""
for candidate in "$HOME/.dotnet/dotnet" "$(command -v dotnet || true)"; do
    [ -n "$candidate" ] && [ -x "$candidate" ] || continue
    major="$(sdk_major "$candidate")"
    if [ -n "$major" ] && [ "$major" -ge "$MIN_SDK_MAJOR" ]; then
        dotnet_bin="$candidate"
        break
    fi
done
[ -n "$dotnet_bin" ] || die "no .NET $MIN_SDK_MAJOR+ SDK found. Install a preview SDK into ~/.dotnet:
  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel $MIN_SDK_MAJOR.0 --quality preview --install-dir ~/.dotnet"

export DOTNET_ROOT="$(dirname "$(readlink -f "$dotnet_bin")")"
export DOTNET_CLI_TELEMETRY_OPTOUT="${DOTNET_CLI_TELEMETRY_OPTOUT:-1}"
export DOTNET_NOLOGO=1

publish_dir="$REPO_ROOT/src/Sefirah/bin/$platform/$configuration/net10.0-desktop/$rid/publish"

echo "==> Publishing $configuration for $rid with .NET SDK $("$dotnet_bin" --version)"
"$dotnet_bin" publish "$PROJECT" \
    -c "$configuration" \
    -f net10.0-desktop \
    -r "$rid" \
    -p:Platform="$platform" \
    --self-contained true

[ -x "$publish_dir/Sefirah" ] || die "publish finished but $publish_dir/Sefirah is missing"
echo "==> Built $publish_dir"

if $do_install; then
    echo "==> Installing into ~/.local"
    if [ -e "$INSTALL_DIR" ] && [ ! -L "$INSTALL_DIR" ]; then
        die "$INSTALL_DIR exists and is not a symlink; move it aside first"
    fi
    mkdir -p "$(dirname "$INSTALL_DIR")" "$(dirname "$BIN_LINK")" "$(dirname "$DESKTOP_FILE")"
    ln -sfn "$publish_dir" "$INSTALL_DIR"
    ln -sf "$INSTALL_DIR/Sefirah" "$BIN_LINK"

    for size_dir in "$DIST"/icons/*/; do
        size="$(basename "$size_dir")"
        for icon in "$size_dir$APP_ID".*; do
            [ -f "$icon" ] && install -Dm644 "$icon" "$ICON_ROOT/$size/apps/$(basename "$icon")"
        done
    done

    # Use an absolute Exec path, since desktop launchers may not have ~/.local/bin on PATH
    sed "s|^Exec=.*|Exec=$BIN_LINK|" "$DIST/$APP_ID.desktop" > "$DESKTOP_FILE"
    update-desktop-database "$(dirname "$DESKTOP_FILE")" 2>/dev/null || true
    gtk-update-icon-cache -q "$ICON_ROOT" 2>/dev/null || true
    echo "==> Installed. Run 'Sefirah' or launch it from the app menu."
fi

if $do_run; then
    if pkill -x Sefirah 2>/dev/null; then
        echo "==> Stopped the running instance"
        for _ in $(seq 50); do pgrep -x Sefirah >/dev/null || break; sleep 0.1; done
    fi
    echo "==> Starting Sefirah (Ctrl+C to quit)"
    cd "$HOME"
    exec "$publish_dir/Sefirah"
fi
