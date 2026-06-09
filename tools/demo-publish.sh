#!/usr/bin/env bash
# Publishes one branch's WebGL build into the `demo` branch under /<slug>/, keeping
# every other branch's preview intact. The demo branch is kept as a SINGLE orphan
# commit (no history bloat - builds are ~32MB each): each run fetches the current
# demo tree, swaps in this branch's folder, regenerates branches.json, and
# force-pushes a fresh commit. Serialized by the `tessera-demo-write` concurrency
# group so concurrent branch publishes don't clobber each other.
#
# Required env: GH_TOKEN, SLUG, BRANCH, GITHUB_REPOSITORY, GITHUB_WORKSPACE, GITHUB_SHA.
# Expects the fresh build at $GITHUB_WORKSPACE/artifact and the root shell at
# $GITHUB_WORKSPACE/tools/pages-shell/index.html.
set -euo pipefail
shopt -s nullglob

: "${GH_TOKEN:?}"; : "${SLUG:?}"; : "${BRANCH:?}"
REPO_URL="https://x-access-token:${GH_TOKEN}@github.com/${GITHUB_REPOSITORY}.git"
ART="$GITHUB_WORKSPACE/artifact"
SHELL_SRC="$GITHUB_WORKSPACE/tools/pages-shell/index.html"

WT="$(mktemp -d)"
git clone --depth 1 --branch demo --single-branch "$REPO_URL" "$WT" 2>/dev/null || true
cd "$WT"
rm -rf .git          # rebuild as a fresh single-commit orphan
git init -q
[ -f branches.json ] || echo '[]' > branches.json

# Swap in this branch's freshly built folder.
rm -rf "$SLUG"
mkdir -p "$SLUG"
cp -r "$ART/." "$SLUG/"

# Manifest is the source of truth for the switcher: drop any stale entry for this
# slug, add the current one, keep main first then alphabetical by branch name.
jq --arg slug "$SLUG" --arg branch "$BRANCH" \
  '[ .[] | select(.slug != $slug) ] + [ {slug:$slug, branch:$branch} ]
   | sort_by(.slug != "main", (.branch | ascii_downcase))' \
  branches.json > branches.json.tmp && mv branches.json.tmp branches.json

# Drop any top-level folder not in the manifest (old flat layout, junk).
KEEP="$(jq -r '.[].slug' branches.json)"
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
echo "Published /$SLUG/ to demo. Pages will redeploy shortly."
