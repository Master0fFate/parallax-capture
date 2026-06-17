#!/usr/bin/env sh
set -eu

REPO="${REPO:-Master0fFate/parallax-capture}"
VERSION="${VERSION:-latest}"
UNINSTALL=0

while [ "$#" -gt 0 ]; do
  case "$1" in
    --repo) REPO="$2"; shift 2 ;;
    --version) VERSION="$2"; shift 2 ;;
    --uninstall) UNINSTALL=1; shift ;;
    *) echo "Unknown option: $1" >&2; exit 64 ;;
  esac
done

OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
  Darwin) PLATFORM="macos" ;;
  Linux) PLATFORM="linux" ;;
  *) echo "Unsupported OS: $OS" >&2; exit 69 ;;
esac

case "$PLATFORM:$ARCH" in
  macos:x86_64) RID="osx-x64"; INSTALL_DIR="$HOME/Applications"; PACKAGE_KIND="app.tar.gz" ;;
  macos:arm64) RID="osx-arm64"; INSTALL_DIR="$HOME/Applications"; PACKAGE_KIND="app.tar.gz" ;;
  linux:x86_64|linux:amd64) RID="linux-x64"; INSTALL_DIR="$HOME/.local/share/parallax-capture"; PACKAGE_KIND="tar.gz" ;;
  *) echo "Unsupported architecture: $ARCH" >&2; exit 69 ;;
esac

BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"

if [ "$UNINSTALL" -eq 1 ]; then
  if [ "$PLATFORM" = "macos" ]; then
    rm -rf "$INSTALL_DIR/Parallax Capture.app"
  else
    rm -rf "$INSTALL_DIR" "$BIN_DIR/parallax-capture" "$DESKTOP_DIR/parallax-capture.desktop"
  fi
  echo "Removed Parallax Capture user install."
  exit 0
fi

need_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 69
  fi
}

need_cmd curl
need_cmd tar

checksum_file_hash() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print $1}'
  else
    echo "sha256sum or shasum -a 256 is required for checksum verification." >&2
    exit 69
  fi
}

download() {
  curl -fsSL "$1" -o "$2"
}

if [ "$VERSION" = "latest" ]; then
  API_URL="https://api.github.com/repos/$REPO/releases/latest"
  RELEASE_JSON="$(curl -fsSL "$API_URL")"
  ARTIFACT_NAME="$(printf '%s' "$RELEASE_JSON" | grep -Eo "ParallaxCapture-[^\"]*-$RID[^\"/]*\\.$PACKAGE_KIND" | head -n 1)"
  if [ -z "$ARTIFACT_NAME" ]; then
    echo "Latest release is missing an artifact for $RID." >&2
    exit 69
  fi
  BASE_URL="https://github.com/$REPO/releases/latest/download"
else
  BASE_URL="https://github.com/$REPO/releases/download/$VERSION"
  if [ "$PLATFORM" = "macos" ]; then
    ARTIFACT_NAME="ParallaxCapture-$VERSION-$RID-app.tar.gz"
  else
    ARTIFACT_NAME="ParallaxCapture-$VERSION-$RID.tar.gz"
  fi
fi

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT INT TERM

ARTIFACT_PATH="$TMP_DIR/$ARTIFACT_NAME"
MANIFEST_PATH="$TMP_DIR/SHA256SUMS"

download "$BASE_URL/$ARTIFACT_NAME" "$ARTIFACT_PATH"
download "$BASE_URL/SHA256SUMS" "$MANIFEST_PATH"

EXPECTED="$(awk -v file="$ARTIFACT_NAME" '$2 == file || $2 == "*" file { print tolower($1); found=1 } END { if (!found) exit 1 }' "$MANIFEST_PATH")" || {
  echo "SHA256SUMS does not contain $ARTIFACT_NAME." >&2
  exit 69
}
ACTUAL="$(checksum_file_hash "$ARTIFACT_PATH" | tr '[:upper:]' '[:lower:]')"

if [ "$EXPECTED" != "$ACTUAL" ]; then
  echo "checksum mismatch for $ARTIFACT_NAME: expected $EXPECTED but got $ACTUAL" >&2
  exit 74
fi

mkdir -p "$INSTALL_DIR"

if [ "$PLATFORM" = "macos" ]; then
  tar -xzf "$ARTIFACT_PATH" -C "$TMP_DIR"
  rm -rf "$INSTALL_DIR/Parallax Capture.app"
  mv "$TMP_DIR/Parallax Capture.app" "$INSTALL_DIR/"
  echo "Installed Parallax Capture to $INSTALL_DIR/Parallax Capture.app."
else
  rm -rf "$INSTALL_DIR"
  mkdir -p "$INSTALL_DIR" "$BIN_DIR" "$DESKTOP_DIR"
  tar -xzf "$ARTIFACT_PATH" -C "$INSTALL_DIR"
  ln -sf "$INSTALL_DIR/usr/bin/parallax-capture" "$BIN_DIR/parallax-capture"
  if [ -f "$INSTALL_DIR/usr/share/applications/parallax-capture.desktop" ]; then
    cp "$INSTALL_DIR/usr/share/applications/parallax-capture.desktop" "$DESKTOP_DIR/parallax-capture.desktop"
  fi
  echo "Installed Parallax Capture to $INSTALL_DIR and linked $BIN_DIR/parallax-capture."
fi
