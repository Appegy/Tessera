#!/usr/bin/env bash
# Removes one branch's preview folder from the `demo` branch when its PR is merged
# or closed, then regenerates branches.json. Same single-orphan-commit model and
# `tessera-demo-write` serialization as demo-publish.sh.
#
# Required env: GH_TOKEN, SLUG, GITHUB_REPOSITORY, GITHUB_WORKSPACE.
set -euo pipefail
shopt -s nullglob

: "${GH_TOKEN:?}"; : "${SLUG:?}"
REPO_URL="https://x-access-token:${GH_TOKEN}@github.com/${GITHUB_REPOSITORY}.git"
SHELL_SRC="$GITHUB_WORKSPACE/tools/pages-shell/index.html"

WT="$(mktemp -d)"
if ! git clone --depth 1 --branch demo --single-branch "$REPO_URL" "$WT" 2>/dev/null; then
  echo "No demo branch yet; nothing to clean."
  exit 0
fi
cd "$WT"
rm -rf .git
git init -q
[ -f branches.json ] || echo '[]' > branches.json

rm -rf "$SLUG"
jq --arg slug "$SLUG" '[ .[] | select(.slug != $slug) ]' \
  branches.json > branches.json.tmp && mv branches.json.tmp branches.json

KEEP="$(jq -r '.[].slug' branches.json)"
for d in */; do
  d="${d%/}"
  printf '%s\n' "$KEEP" | grep -qxF "$d" || rm -rf "$d"
done

cp "$SHELL_SRC" index.html
touch .nojekyll

git add -A
git -c user.email=actions@github.com -c user.name="github-actions[bot]" \
  commit -q -m "preview cleanup: ${SLUG}"
git push -f "$REPO_URL" HEAD:demo
echo "Removed /$SLUG/ from demo."
