---
description: Create README preview images for a NEW grid type, matching the existing image set
argument-hint: <new grid, e.g. "TriangleGrid(10, 10, 1f)" or the grid class + intent>
allowed-tools: mcp__UnityMCP__set_active_instance, mcp__UnityMCP__manage_editor, mcp__UnityMCP__execute_code, mcp__UnityMCP__manage_scene, mcp__UnityMCP__read_console, Bash, Read, Glob
---

Create the two preview images (`-light` + `-dark` webp) for a **new** grid type so it slots into the existing
README set. This adds images for `$ARGUMENTS`; it does **not** regenerate the grids that already have previews.

## Step 1 - Study the existing set first

Before rendering anything, look at what's already there so the new tile matches:

- List the current previews: the `images/` folder holds `<slug>-light.webp` / `<slug>-dark.webp` pairs
  (`square`, `hexagonal`, `voronoi`, `classic-puzzle`, `draradech-puzzle`).
- `Read` two or three of them (e.g. `images/square-light.webp`, `images/voronoi-dark.webp`) and note the look you must match:
  - **Square 1000×1000 canvas**, grid built with **near-1:1 bounds** and tight framing - the tile fills the frame.
  - **Comparable density**: ~56-64 cells (puzzles are chunkier at 36). Pick the new grid's parameters to land in that range.
  - **Transparent background**, only lines drawn.
  - **Theme-aware line colour** (dark-blue on light, light-blue on dark), set via `<picture>` in the README.
  - **Constant line weight** (~4.5px), tied to the camera ortho size.

## Step 2 - Define the new grid

From `$ARGUMENTS`, work out:

- The **slug** (kebab-case, e.g. `triangle`, `rhombille`). The two outputs will be `<slug>-light.webp` / `<slug>-dark.webp`.
- How to **construct** the grid as an `Appegy.Tessera.ITessellation`. Read its constructor in `src/Runtime/Grids/...`
  if unsure. Choose dimensions giving near-square bounds and ~56-64 cells so it matches the set.

Anything implementing `ITessellation` renders with the capture below unchanged.

## Step 3 - Render (PNG)

1. **Pin the instance**: `set_active_instance` → `Appegy.Tessera.Lab` (name prefix; hash changes per session).
2. **Enter Play Mode** (`manage_editor` `play`) with `Tessera Playground.unity` loaded - the capture needs the live
   `GridView` + `Main Camera`. Confirm both are found with a quick `execute_code` first.
3. Run the capture, **substituting the new grid and slug** in the two marked lines:

```csharp
var gv = GameObject.FindObjectOfType<Appegy.Tessera.Demo.GridView>();
var cam = Camera.main;
if (gv == null || cam == null) return "missing GridView or Camera - is the Tessera Playground scene in Play Mode?";

// >>> SUBSTITUTE for the new grid <<<
Appegy.Tessera.ITessellation grid = new Appegy.Tessera.SquareGrid(8, 8, 1f); // replace with the new grid
string slug = "REPLACE-ME";                                                  // kebab-case slug
// >>> end substitution <<<

int W = 1000, H = 1000;
float pad = 1.06f;
float targetLinePx = 4.5f;
string dir = System.IO.Path.GetFullPath(Application.dataPath + "/../../images"); // repo-root/images
System.IO.Directory.CreateDirectory(dir);

cam.orthographic = true;
cam.clearFlags = CameraClearFlags.SolidColor;
cam.aspect = (float)W / H;
float zpos = cam.transform.position.z;

// Line colours mirror PlaygroundUI.ApplyTheme: index 0 = light theme, 1 = dark theme.
Color[] lineCols = new Color[] { new Color(0.34f,0.38f,0.52f), new Color(0.62f,0.67f,0.88f) };
string[] themeNames = new string[] { "light", "dark" };

var log = new System.Text.StringBuilder();
for (int t = 0; t < themeNames.Length; t++)
{
    var size = grid.Bounds.Size;
    float halfH = size.y * 0.5f;
    float halfW = (size.x * 0.5f) / cam.aspect;
    float ortho = Mathf.Max(halfH, halfW) * pad;
    if (ortho < 0.01f) ortho = 0.01f;

    gv.LineColor = lineCols[t];
    gv.LineWidth = ortho * (targetLinePx * 2f / H); // constant pixel line weight
    gv.SetGrid(grid);
    gv.ClearHighlight();

    cam.orthographicSize = ortho;
    cam.transform.position = new Vector3(0f, 0f, zpos);
    cam.backgroundColor = new Color(0f, 0f, 0f, 0f);

    var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
    rt.antiAliasing = 8;
    cam.targetTexture = rt;
    cam.Render();
    var prev = RenderTexture.active;
    RenderTexture.active = rt;
    var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
    tex.Apply();
    RenderTexture.active = prev;
    cam.targetTexture = null;
    System.IO.File.WriteAllBytes(dir + "/" + slug + "-" + themeNames[t] + ".png", tex.EncodeToPNG());
    UnityEngine.Object.Destroy(tex);
    rt.Release();
    UnityEngine.Object.Destroy(rt);
    log.Append(slug + "-" + themeNames[t] + "; ");
}
return "wrote " + dir + ": " + log.ToString();
```

4. **Exit Play Mode** (`manage_editor` `stop`).

## Step 4 - Convert the new PNGs to lossless WebP (only the new slug)

```bash
cd images && for f in <slug>-light.png <slug>-dark.png; do magick "$f" -define webp:lossless=true -define webp:method=6 -define webp:exact=true "${f%.png}.webp"; done && rm -f <slug>-light.png <slug>-dark.png && ls -la <slug>-*.webp
```

## Step 5 - Verify and wire into the README

- `Read` the two new webp files and compare against the existing set: same framing, density, line weight, transparency.
  If the new grid looks too sparse/dense or non-square, adjust its parameters in step 2 and rerun.
- Add the grid to `README.md` under **Grid types**, matching the siblings: a `<picture>` block
  (`-dark` source + `-light` `<img width="420">`), a one-line description, and a **parameter table**.
- Naming must stay `<slug>-<theme>.webp` - the README references images by relative path and `release.yml`
  folds `images/` into the package, so no extra wiring is needed.

Requires ImageMagick (`magick`) for the WebP step.
