#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Release}"
RID="${1:-${RID:-linux-x64}}"
VERSION="${VERSION:-1.1.0}"
APP_ID="parallax-capture"
APP_NAME="Parallax Capture"

normalize_version() {
  local input="$1"
  local version="${input#v}"

  if [[ "$input" != "$version" && "$input" != v* ]]; then
    echo "Version must use an optional lowercase v prefix: $input" >&2
    exit 64
  fi

  if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$ ]]; then
    echo "Version must be SemVer without build metadata, for example v1.2.3 or v1.2.3-rc.1: $input" >&2
    exit 64
  fi

  printf '%s' "$version"
}

PACKAGE_VERSION="$(normalize_version "$VERSION")"
if [[ "$VERSION" == v* ]]; then
  ARTIFACT_VERSION="v$PACKAGE_VERSION"
else
  ARTIFACT_VERSION="$PACKAGE_VERSION"
fi

case "$RID" in
  linux-x64) ;;
  *) echo "Linux packaging supports linux-x64. Requested: $RID" >&2; exit 64 ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ARTIFACT_ROOT="${ARTIFACTS_DIRECTORY:-$REPO_ROOT/artifacts/release}"
PUBLISH_ROOT="$REPO_ROOT/artifacts/publish/$RID"
APPDIR="$REPO_ROOT/artifacts/package/$RID/AppDir"
USR="$APPDIR/usr"
BIN="$USR/bin"
SHARE="$USR/share"
PACKAGE_BASENAME="ParallaxCapture-$ARTIFACT_VERSION-$RID"
TAR_PATH="$ARTIFACT_ROOT/$PACKAGE_BASENAME.tar.gz"
APPIMAGE_PATH="$ARTIFACT_ROOT/$PACKAGE_BASENAME.AppImage"
DEB_ROOT="$REPO_ROOT/artifacts/package/$RID/deb"
RPM_ROOT="$REPO_ROOT/artifacts/package/$RID/rpm"

rm -rf "$PUBLISH_ROOT" "$APPDIR" "$DEB_ROOT" "$RPM_ROOT"
mkdir -p "$ARTIFACT_ROOT" "$BIN" "$SHARE/applications" "$SHARE/icons/hicolor/256x256/apps" "$SHARE/metainfo"

dotnet publish "$REPO_ROOT/src/Parallax.App.Avalonia/Parallax.App.Avalonia.csproj" \
  -c "$CONFIGURATION" \
  -r "$RID" \
  --self-contained false \
  -p:PublishSingleFile=false \
  -p:PublishReadyToRun=false \
  -p:Version="$PACKAGE_VERSION" \
  -o "$PUBLISH_ROOT"

cp -R "$PUBLISH_ROOT"/. "$BIN/"
if [ -f "$BIN/Parallax.App.Avalonia" ]; then
  cp "$BIN/Parallax.App.Avalonia" "$BIN/Parallax Capture"
fi
cat > "$BIN/$APP_ID" <<'LAUNCHER'
#!/usr/bin/env sh
set -eu
DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec "$DIR/Parallax Capture" "$@"
LAUNCHER
chmod +x "$BIN/$APP_ID" "$BIN/Parallax Capture" 2>/dev/null || true

cp "$REPO_ROOT/packaging/linux/parallax-capture.desktop" "$SHARE/applications/$APP_ID.desktop"
cp "$REPO_ROOT/packaging/linux/parallax-capture.metainfo.xml" "$SHARE/metainfo/com.master0ffate.parallax-capture.metainfo.xml"
cp "$REPO_ROOT/icon.ico" "$SHARE/icons/hicolor/256x256/apps/$APP_ID.ico"
cp "$REPO_ROOT/packaging/linux/parallax-capture.desktop" "$APPDIR/$APP_ID.desktop"
cp "$REPO_ROOT/icon.ico" "$APPDIR/$APP_ID.ico"

rm -f "$TAR_PATH" "$APPIMAGE_PATH"
tar -czf "$TAR_PATH" -C "$APPDIR" .

if command -v appimagetool >/dev/null 2>&1; then
  ARCH=x86_64 appimagetool "$APPDIR" "$APPIMAGE_PATH"
else
  echo "appimagetool is unavailable. The tar.gz AppDir artifact remains the portable Linux package."
fi

if command -v dpkg-deb >/dev/null 2>&1; then
  mkdir -p "$DEB_ROOT/DEBIAN" "$DEB_ROOT/usr/bin" "$DEB_ROOT/usr/share"
  cp -R "$BIN"/. "$DEB_ROOT/usr/bin/"
  cp -R "$SHARE"/. "$DEB_ROOT/usr/share/"
  cat > "$DEB_ROOT/DEBIAN/control" <<CONTROL
Package: parallax-capture
Version: $PACKAGE_VERSION
Section: graphics
Priority: optional
Architecture: amd64
Maintainer: Master0fFate
Description: Screenshot and screen recording tool with cross-platform capture workflows.
CONTROL
  dpkg-deb --build "$DEB_ROOT" "$ARTIFACT_ROOT/$PACKAGE_BASENAME.deb"
else
  echo "dpkg-deb is unavailable. Skipping deb artifact on this host."
fi

if command -v rpmbuild >/dev/null 2>&1; then
  mkdir -p "$RPM_ROOT/BUILD" "$RPM_ROOT/RPMS" "$RPM_ROOT/SOURCES" "$RPM_ROOT/SPECS" "$RPM_ROOT/SRPMS"
  cp "$TAR_PATH" "$RPM_ROOT/SOURCES/$PACKAGE_BASENAME.tar.gz"
  cat > "$RPM_ROOT/SPECS/parallax-capture.spec" <<SPEC
Name: parallax-capture
Version: $PACKAGE_VERSION
Release: 1%{?dist}
Summary: Screenshot and screen recording tool
License: MIT
BuildArch: x86_64
Source0: $PACKAGE_BASENAME.tar.gz

%description
Parallax Capture is a desktop screenshot and recording app.

%prep
mkdir -p %{_builddir}/parallax-capture
tar -xzf %{SOURCE0} -C %{_builddir}/parallax-capture

%install
mkdir -p %{buildroot}/opt/parallax-capture
cp -R %{_builddir}/parallax-capture/. %{buildroot}/opt/parallax-capture/

%files
/opt/parallax-capture
SPEC
  rpmbuild --define "_topdir $RPM_ROOT" -bb "$RPM_ROOT/SPECS/parallax-capture.spec"
  find "$RPM_ROOT/RPMS" -type f -name '*.rpm' -exec cp {} "$ARTIFACT_ROOT/" \;
else
  echo "rpmbuild is unavailable. Skipping rpm artifact on this host."
fi

if command -v pwsh >/dev/null 2>&1; then
  pwsh "$SCRIPT_DIR/generate-checksums.ps1" -ArtifactDirectory "$ARTIFACT_ROOT"
else
  (cd "$ARTIFACT_ROOT" && sha256sum ParallaxCapture-* > SHA256SUMS)
fi

echo "Linux package artifacts are in $ARTIFACT_ROOT"
