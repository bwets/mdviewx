#!/usr/bin/env bash
# Builds a .deb from a published Skia-desktop linux-x64 output directory.
#
# Usage: build-deb.sh <version> <publish-dir>
#   <version>     e.g. 1.0.5
#   <publish-dir> the `dotnet publish` output (contains the `mdviewx` binary,
#                 plus mdviewx.desktop and mdviewx.svg copied in by the workflow)
set -euo pipefail

VERSION="${1:?version required}"
PUBLISH_DIR="${2:?publish dir required}"

PKG="mdviewx_${VERSION}_amd64"
ROOT="$PKG"
rm -rf "$ROOT"

# Layout: app under /opt, launcher symlink in /usr/bin, desktop + icon under /usr/share.
install -dm755 "$ROOT/opt/mdviewx"
cp -a "$PUBLISH_DIR/." "$ROOT/opt/mdviewx/"
chmod 755 "$ROOT/opt/mdviewx/mdviewx"

install -dm755 "$ROOT/usr/bin"
ln -sf /opt/mdviewx/mdviewx "$ROOT/usr/bin/mdviewx"

install -Dm644 "$PUBLISH_DIR/mdviewx.desktop" "$ROOT/usr/share/applications/mdviewx.desktop"
install -Dm644 "$PUBLISH_DIR/mdviewx.svg" "$ROOT/usr/share/icons/hicolor/scalable/apps/mdviewx.svg"

install -dm755 "$ROOT/DEBIAN"
cat > "$ROOT/DEBIAN/control" <<EOF
Package: mdviewx
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: amd64
Depends: libgtk-3-0, libwebkit2gtk-4.1-0
Maintainer: bwets
Description: A fast, offline, feature-rich Markdown viewer (Uno Platform).
EOF

dpkg-deb --root-owner-group --build "$ROOT" "${PKG}.deb"
echo "Built ${PKG}.deb"
