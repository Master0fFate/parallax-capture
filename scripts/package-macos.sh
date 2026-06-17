#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Release}"
RID="${1:-${RID:-osx-x64}}"
VERSION="${VERSION:-1.1.0}"
PLIST_VERSION="${VERSION#v}"
APP_NAME="Parallax Capture"
BUNDLE_ID="com.master0ffate.parallax-capture"

case "$RID" in
  osx-x64|osx-arm64) ;;
  *) echo "macOS packaging supports osx-x64 and osx-arm64. Requested: $RID" >&2; exit 64 ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ARTIFACT_ROOT="${ARTIFACTS_DIRECTORY:-$REPO_ROOT/artifacts/release}"
PUBLISH_ROOT="$REPO_ROOT/artifacts/publish/$RID"
APP_ROOT="$REPO_ROOT/artifacts/package/$RID/$APP_NAME.app"
CONTENTS="$APP_ROOT/Contents"
MACOS="$CONTENTS/MacOS"
RESOURCES="$CONTENTS/Resources"
ARCHIVE="$ARTIFACT_ROOT/ParallaxCapture-$VERSION-$RID-app.tar.gz"
DMG="$ARTIFACT_ROOT/ParallaxCapture-$VERSION-$RID.dmg"

rm -rf "$PUBLISH_ROOT" "$APP_ROOT"
mkdir -p "$ARTIFACT_ROOT" "$MACOS" "$RESOURCES"

dotnet publish "$REPO_ROOT/src/Parallax.App.Avalonia/Parallax.App.Avalonia.csproj" \
  -c "$CONFIGURATION" \
  -r "$RID" \
  --self-contained false \
  -p:PublishSingleFile=false \
  -p:PublishReadyToRun=false \
  -o "$PUBLISH_ROOT"

cp -R "$PUBLISH_ROOT"/. "$MACOS/"
cp "$REPO_ROOT/icon.ico" "$RESOURCES/icon.ico"
if [ -f "$MACOS/Parallax.App.Avalonia" ]; then
  cp "$MACOS/Parallax.App.Avalonia" "$MACOS/$APP_NAME"
fi
sed \
  -e "s/@VERSION@/$PLIST_VERSION/g" \
  -e "s/@EXECUTABLE@/$APP_NAME/g" \
  -e "s/@BUNDLE_ID@/$BUNDLE_ID/g" \
  "$REPO_ROOT/packaging/macos/Info.plist" > "$CONTENTS/Info.plist"

chmod +x "$MACOS/$APP_NAME" 2>/dev/null || true

if [[ -n "${MACOS_CODESIGN_IDENTITY:-}" ]]; then
  codesign --force --timestamp --options runtime \
    --entitlements "$REPO_ROOT/packaging/macos/Entitlements.plist" \
    --sign "$MACOS_CODESIGN_IDENTITY" \
    "$APP_ROOT"
else
  echo "MACOS_CODESIGN_IDENTITY is not set. Leaving $APP_NAME.app unsigned for local development."
fi

rm -f "$ARCHIVE" "$DMG"
tar -czf "$ARCHIVE" -C "$(dirname "$APP_ROOT")" "$APP_NAME.app"

if command -v hdiutil >/dev/null 2>&1; then
  hdiutil create -volname "$APP_NAME" -srcfolder "$APP_ROOT" -ov -format UDZO "$DMG"
  if [[ -n "${MACOS_NOTARY_PROFILE:-}" ]]; then
    xcrun notarytool submit "$DMG" --keychain-profile "$MACOS_NOTARY_PROFILE" --wait
    xcrun stapler staple "$DMG"
  else
    echo "MACOS_NOTARY_PROFILE is not set. DMG notarization was skipped."
  fi
else
  echo "hdiutil is unavailable. Created tar.gz app bundle distribution only."
fi

if command -v pwsh >/dev/null 2>&1; then
  pwsh "$SCRIPT_DIR/generate-checksums.ps1" -ArtifactDirectory "$ARTIFACT_ROOT"
else
  (cd "$ARTIFACT_ROOT" && shasum -a 256 ParallaxCapture-* > SHA256SUMS)
fi

echo "macOS package artifacts are in $ARTIFACT_ROOT"
