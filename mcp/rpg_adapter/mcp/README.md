# RPG asset production MCP

This optional Python stdio service shares the current asset conversion and drawing
helpers. The Godot editor plugin in the parent directory works independently of
this process. Production gameplay and content live under `dv_core/modules`; this asset service
uses no DV session.

## Start

Install this directory's `requirements.txt` into the Python environment used by
your MCP client, then configure that client to run:

```text
python mcp/server.py
```

If the environment is inside the Godot project, put `.gdignore` at its root so
Godot does not import it. `GODOT_BIN` names the Godot .NET executable for explicitly
requested UI inspection; starting this service does not launch Godot. The repository server owns stdio transport and imports an operation only when it is called.
Use tool `rpg_adapter` with `{ "operation": "inspect_image", "arguments": { ... } }`.
The former adapter `server.py` command forwards to the repository entrypoint.

## Available work

| Area | Entry points | Inputs and outputs |
| --- | --- | --- |
| Drawing and conversion | `inspect_image`, `draw_asset`, `adapt_asset`, `build_tiles` | Explicit crops, native dimensions, drawing commands and original artwork; derivatives stay separate from originals. |
| Map authoring | `audit_layout`, `compose_map`, `export_godot` | Authored floor plans, tiles and collision footprints; the generic workflow exports a development preview. |
| Shared drawing recipes | `native_art_catalog`, `read_native_recipe`, `render_native_prop`, `render_native_material`, `native_art_contact_sheet` | Recovered `mcp/asset_pipeline/first_awakening_art` furniture and material helpers, also consumed by Chapter2 artwork; actual source hashes identify recipes. |
| Artist handoff | `inspect_pixel_style`, `quantize_asset`, `layered_sprite`, `export_aseprite` | Explicit image inputs and layers; quantization is reported as a derivative. |
| Work records | `review_dialogue`, `create_plan`, `read_plan`, `update_task`, `next_task` | Supplied text, constraints and a bounded plan; no background agent or literary judgement. |
| Existing UI | `ui_design_contract`, `inspect_godot_ui`, `measure_dialogue_text` | Existing scenes below `scenes/UI` and actual Godot font metrics; inspection does not instantiate the Host scene. |

`tilemap_workflow()` describes the generic asset workflow. Consult the current
`docs/design-standard.md` for design decisions. `ui_design_contract()` identifies
current scenes and the theme generator; layout reports describe resolved
properties without imposing the retired preview's style rules.

The generic map preview is saved under `mcp/sandboxes/generated` and keeps its movable collision body and uses the first
`idle_front` frame from the current `PixelCast/detective.tres` resource. The
`Sprite2D` receives that frame's `Texture2D`; it does not load the retired
`Characters/atlas` family or generate replacement artwork.

## Sources and publication

Original artwork belongs under `mcp/asset_pipeline/sources`; runtime assets belong
under `godot_csharp/ShareAsset`. The temporary Godot workspace maps these to
`res://AssetsSource` and `res://ShareAsset`. Preserve source images, editable layers, crop choices and provenance
records. Replacing a tool-owned derivative verifies its recorded source hash;
unowned artwork and artist edits are not silently overwritten. Reload the service
after editing drawing recipes so imported code and reported hashes remain aligned.

Six shared drawing modules were recovered from repository commit
`1403465243def4fdc21d148d97208d70ca362b70`. Their origin and remaining source gaps
are documented in `mcp/asset_pipeline/README.md`. This restores reusable drawing
code, not missing character originals, terrain data or a retired game scene.

Current map production uses `mcp/asset_pipeline/build_chapter2_scenes.py`, published map labels
or an explicit text-binding input, and the original outdoor source supplied via
`--site-source`. Asset production never fabricates missing source inputs.

The old `build_diegetic_ui`, `rebuild_native_interior` and dedicated full-interior
handoff interfaces are retired. They no longer recreate `mind_theme.tres`, old
`mind-ui-*` textures or the isolated FirstAwakening scene. Generic layered-image
and Aseprite tools and existing-UI measurement remain available.

## Engineering checks

`check_native_art.py` covers current stdio discovery and source ownership;
`check_pixel_handoff.py` covers binary/image handoff; `check_production.py` covers
the bounded plan and supplied-text helpers. `check.py` and `demo.py` operate on the
generic TileMapWorkflow development family and can write its preview assets;
use a disposable copy for that work.

Run only the check needed for the selected engineering question after edits are
complete. These tools do not establish gameplay, narrative, artistic or performance
acceptance. Do not run a combined flow regression.

The current refactor migrates existing check paths only; protocol assertions and execution are deferred until all implementation Steps are complete.
