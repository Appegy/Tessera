#!/usr/bin/env bash
# Publishes the local WebGL build to the orphan `demo` branch that GitHub
# Pages serves (https://appegy.github.io/Tessera/). The build branch holds only
# the build and never merges anywhere.
#
# Flow:
#   1. In Unity (the env project), build the "Tessera Playground" scene for WebGL
#      into Documentation~/webgl  (File > Build Settings/Profiles > WebGL > Build,
#      target folder = Documentation~/webgl). Compression is set to Disabled so
#      GitHub Pages serves it without Content-Encoding headers.
#   2. Run this script. It force-pushes the build to `demo`; GitHub Pages
#      redeploys automatically within ~1 minute.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="$REPO/Documentation~/webgl"

if [ ! -f "$BUILD/index.html" ]; then
  echo "No WebGL build found at $BUILD"
  echo "Build it first in Unity (WebGL target, output folder Documentation~/webgl)."
  exit 1
fi

REMOTE="$(git -C "$REPO" remote get-url origin)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

cp -r "$BUILD/." "$TMP/"
touch "$TMP/.nojekyll"           # skip Jekyll so Build/ and TemplateData/ are served verbatim

cd "$TMP"
git init -q
git checkout -q -b demo
git add -A
git -c user.email="build@appegy" -c user.name="Tessera Build" commit -q -m "WebGL build: Tessera Playground"
git remote add origin "$REMOTE"
git push -f origin demo

echo "Published to demo. Live shortly at https://appegy.github.io/Tessera/"
