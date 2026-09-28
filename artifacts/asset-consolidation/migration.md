# Asset consolidation migration record

## Canonical roots

- Runtime assets are under `godot_csharp/ShareAsset/{External,Internal}`. Game runtime files, scenes, and generated resources refer only to this tree.
- Source artwork is under `godot_csharp/AssetsSource/{External,Internal}`. `AssetsSource/.gdignore` prevents Godot import/indexing; no runtime resource should refer into it.
- Music and non-runtime reports/concepts remain in the repository's existing `music/` and `artifacts/` locations.
- Third-party plugin files remain under their existing add-on directories.

## Applied mappings

| Previous location | Canonical location | Notes |
| --- | --- | --- |
| `ShareAsset/Images/characters/**` | `ShareAsset/Internal/Characters/**` | Runtime character atlases and docs; source and generation scripts moved to the matching `AssetsSource/Internal/Characters/` and `godot_csharp/tools/` locations. |
| `artifacts/character-animation-v2/source/**` | `AssetsSource/Internal/Characters/AnimationV2/**` | Production inputs moved; reports and bounds remain under `artifacts/character-animation-v2/`. |
| `artifacts/demo-animation/source/**` | `AssetsSource/Internal/Characters/DemoAnimation/**` | Production inputs moved; reports and bounds remain under `artifacts/demo-animation/`. |
| Character standing/story-music source drafts and reference images | `AssetsSource/Internal/Characters/{FrontStanding,StoryMusician,References}/` | Runtime atlases remain under `ShareAsset/Internal/Characters/`. |
| Identity and flag runtime files | `ShareAsset/Internal/Identity/` (including `Flags/`) | Identity source/reference artwork is under `AssetsSource/Internal/Identity/`. |
| `ShareAsset/Props/**`, `ShareAsset/ReasoningStudy/**` | `ShareAsset/Internal/Props/**`, `ShareAsset/Internal/Props/ReasoningStudy/**` | Generated furniture, decorations, and Reasoning Study atlas family. |
| `ShareAsset/TileMap/Academy/**` | `AssetsSource/Internal/Props/AcademyTileMap/**` | Preserves the old 48px Academy source atlas family and unsupported `AcademyTerrain48.tres` outside runtime. No scene references the old TileSet; its textures produce atlas-coordinate errors in Godot. The 48px art is retained for deterministic 32px story/shot build tools. |
| Shot01/Shot02/story_hall runtime furniture and tile assets | `ShareAsset/Internal/Props/` | Flattened to the common props area. The ReasoningStudy and AcademyTileMap subfolders are retained as coherent atlas families, not shot folders. |
| Victorian runtime package | `ShareAsset/External/Victorian/` | Package subdirectories (`atlases`, `tilesets`, `animations`, `characters`, `scenes`, `textures`) remain grouped by source package. |
| Victorian source PNGs | `AssetsSource/External/Victorian/` | The batch converter copies source-root PNGs byte-for-byte to `ShareAsset/External/Victorian/textures/` before conversion, so saved TileSets can use importable runtime paths. |
| Self-made generated hall/kitchen/joinery PNGs and prompts | `AssetsSource/Internal/Props/` | Runtime slices and assembled resources are under `ShareAsset/Internal/Props/`. ReasoningStudy originals and prompts are in `AssetsSource/Internal/Props/ReasoningStudy/source/`. |
| Root game-specific atlas-generation scripts | `godot_csharp/tools/` | Music-only tools stay at root `tools/`. |

## Collision and preservation handling

Before merging same-named images, contents were compared by SHA-256. Identical duplicates were consolidated. Different images were preserved under semantic names, including `hall_wall_lantern`, `kitchen_wall_lantern`, `central_hall_research_table_legacy`, `shot01_banner_N`, and `story_banner_N`. Differing resized detail variants keep explicit `hearth`, `pantry`, and `prep_island` names. The byte-level PNG inventory comparison is recorded in [image-integrity.json](image-integrity.json).

Runtime references in scenes, scripts, C# code, resource files, tools, and relevant package documentation were updated. Dynamic paths were checked as well as literal paths; e.g. `CentralHall.gd` now loads the consolidated `Props/tree.res`, and shot banner generation/consumption uses the same `shot01_banner_N` names. The old Academy TileSet is source-archived rather than falsely presented as a valid runtime asset.

`run_batch_convert.gd` now copies all top-level Victorian source PNGs into the package's runtime `textures/` directory before invoking the adapter. The adapter reads source pixels for conversion but serializes normal atlas textures against the runtime texture copy. The six non-RM PNGs used by `AcademyTerrain48.tres` have been copied and SHA-256 checked. Thus neither generated Victorian TileSets nor hand-authored scenes need runtime access to `AssetsSource`.

## Verification notes

- `image-integrity.json` records the pre/post source-image comparison: all 328 PNGs and 283 distinct image contents were preserved at the time of the initial check.
- `resource-validation.json` is the first Godot dependency audit and contains issues subsequently fixed in this migration. The deprecated 48px Academy TileSet has since been removed from the runtime tree. Re-run `godot_csharp/tools/check_asset_layout.gd` after the final editor import and Victorian batch conversion to refresh final evidence.
- Rebuilding the existing thief atlas changes 65,502 pixels relative to the checked-in output; the existing output was restored and the rebuild limitation is documented in `AssetsSource/Internal/Characters/DemoAnimation`/`artifacts/demo-animation` notes. Do not treat that output as safely reproducible from its current source.
- No exported game package validation is claimed.
