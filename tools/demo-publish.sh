#!/usr/bin/env bash
# Publishes one PR/branch preview into the `demo` branch under /<slug>/ as a HEADLESS
# build (Build/* + a build.json describing the hashed filenames; no per-build
# index.html - the single build-agnostic shell at demo root loads it). The demo
# branch is kept as one orphan commit so 32MB builds don't pile up in history;
# writes are serialized by the `tessera-demo-write` concurrency group.
#
# Env: GH_TOKEN, SLUG, BRANCH, GITHUB_REPOSITORY, GITHUB_WORKSPACE, GITHUB_SHA.
# Optional: PR (number), TITLE (dropdown label; defaults to BRANCH).
# Expects the build at $GITHUB_WORKSPACE/artifact and the shell at
# $GITHUB_WORKSPACE/tools/pages-shell/index.html. A stub artifact may ship its own
# build.json ({"type":"stub","label":...}); then no Unity index.html is required.
set -euo pipefail
shopt -s nullglob

: "${GH_TOKEN:?}"; : "${SLUG:?}"; : "${BRANCH:?}"
PR="${PR:-}"; TITLE="${TITLE:-$BRANCH}"
REPO_URL="https://x-access-token:${GH_TOKEN}@github.com/${GITHUB_REPOSITORY}.git"
ART="$GITHUB_WORKSPACE/artifact"
SHELL_SRC="$GITHUB_WORKSPACE/tools/pages-shell/index.html"

WT="$(mktemp -d)"
git clone --depth 1 --branch demo --single-branch "$REPO_URL" "$WT" 2>/dev/null || true
cd "$WT"
rm -rf .git
git init -q
[ -f previews.json ] || echo '[]' > previews.json
rm -f branches.json   # superseded by previews.json (migration from the old layout)

# Swap in this preview's files.
rm -rf "$SLUG"
mkdir -p "$SLUG"
cp -r "$ART/." "$SLUG/"

# Real Unity builds: derive build.json from the build's index.html (the resolved
# hashed filenames), then drop index.html so the folder is just engine files.
if [ ! -f "$SLUG/build.json" ] && [ -f "$SLUG/index.html" ]; then
  idx="$SLUG/index.html"
  loader="$(grep -oE '[A-Za-z0-9]+\.loader\.js' "$idx" | head -1)"
  data="$(grep -oE '[A-Za-z0-9]+\.data' "$idx" | head -1)"
  fw="$(grep -oE '[A-Za-z0-9]+\.framework\.js' "$idx" | head -1)"
  code="$(grep -oE '[A-Za-z0-9]+\.wasm' "$idx" | head -1)"
  jq -n --arg l "Build/$loader" --arg d "Build/$data" --arg f "Build/$fw" --arg c "Build/$code" \
    '{type:"unity", loaderUrl:$l, dataUrl:$d, frameworkUrl:$f, codeUrl:$c}' > "$SLUG/build.json"
fi
rm -f "$SLUG/index.html"

# previews.json (dropdown source of truth): replace this slug's entry; main first, then by PR number.
jq --arg slug "$SLUG" --arg branch "$BRANCH" --arg pr "$PR" --arg title "$TITLE" \
  '[ .[] | select(.slug != $slug) ]
   + [ {slug:$slug, branch:$branch, pr:(try ($pr|tonumber) catch null), title:$title} ]
   | sort_by(.slug != "main", ((.pr) // 0))' \
  previews.json > previews.json.tmp && mv previews.json.tmp previews.json

# Drop any top-level folder not in the manifest (old layout, closed previews).
KEEP="$(jq -r '.[].slug' previews.json)"
for d in */; do
  d="${d%/}"
  printf '%s\n' "$KEEP" | grep -qxF "$d" || rm -rf "$d"
done

cp "$SHELL_SRC" index.html
touch .nojekyll

git add -A
git -c user.email=actions@github.com -c user.name="github-actions[bot]" \
  commit -q -m "preview: ${SLUG} (${GITHUB_SHA::7})"
git push -f "$REPO_URL" HEAD:demo
echo "Published /$SLUG/ to demo (pr=${PR:-none}, title=${TITLE})."
