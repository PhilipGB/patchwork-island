#!/usr/bin/env bash
set -euo pipefail
REPO="${1:-.}"
HERE="$(cd "$(dirname "$0")" && pwd)"
if [[ ! -f "$REPO/project.godot" || ! -f "$REPO/PatchworkIsland.sln" ]]; then
  echo "Target must be the Patchwork Island Godot repository root." >&2
  exit 2
fi
for item in scenes/world scenes/tools tools/SceneBakeTool.cs tools/WorldSceneAnchors.cs; do
  if [[ -e "$REPO/$item" ]]; then
    echo "Refusing to overwrite existing $REPO/$item" >&2
    exit 3
  fi
done
cp -R "$HERE/scenes/world" "$REPO/scenes/"
cp -R "$HERE/scenes/tools" "$REPO/scenes/"
mkdir -p "$REPO/tools"
cp "$HERE/tools/SceneBakeTool.cs" "$REPO/tools/SceneBakeTool.cs"
cp "$HERE/tools/WorldSceneAnchors.cs" "$REPO/tools/WorldSceneAnchors.cs"
echo "Installed scene preview and bake tool. No gameplay or save files modified."
echo "Next: open res://scenes/world/Town_Preview.tscn in Godot."
