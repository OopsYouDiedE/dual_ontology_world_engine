# Asset and capture tools

These tools maintain the Godot host's assets and engineering evidence. Current
gameplay is the seven DV files in `language_core`; runtime assets are under
`godot_csharp/ShareAsset`. No tool provides a second gameplay implementation.

## Map and artwork inputs

`build_chapter2_scenes.py` uses `chapter2_art/rooms.py` for authored geometry and
`ShareAsset/Internal/Props/Chapter2/manifest.json` for existing room titles and
site labels. This preserves published asset wording without loading the removed
story compiler. `--text-source` accepts that manifest or a flat text-binding JSON
object; supplied asset text is not a DV gameplay authority or actor knowledge.

| Mode | Required inputs | Scope |
| --- | --- | --- |
| `--text-only MAP...` | Current scenes, manifest and text source | Update selected scene and manifest text fields. |
| `--sites-only MAP...` | Current scenes, manifest, text source and geometry recipes | Regenerate selected authored site markers. |
| `--geometry-only MAP...` | Current manifest, scenes and referenced PNGs, plus text and geometry recipes | Regenerate selected interior geometry while reusing textures. |
| `--interiors-only MAP...` | Geometry-only inputs plus shared drawing modules | Also repaint selected backgrounds and baked shadows. |
| Full build | All map inputs plus `--site-source DIRECTORY` | Rebuild outdoor campus and interiors. |

Full reconstruction needs original `campus.py`, `render_site.py`, `site.json` and
their terrain data. The default directory is
`AssetsSource/Internal/Props/Chapter2/CampusSite`; `--site-source` can point to a
complete original elsewhere. Missing source files are reported before any runtime
asset is written. Outdoor source loading and rendering finish before publication
begins. Current map images and scenes remain necessary when sources are unavailable.
Asset regeneration is separate from this cleanup and still needs visual review.

Six pure drawing modules in `first_awakening_art/` were recovered from repository
commit `1403465243def4fdc21d148d97208d70ca362b70`: `__init__.py`, `pixel.py`,
`palette.py`, `props.py`, `architecture.py`, `layout.py`. Chapter2 painters use
their canvas, colours, materials and furniture. Life-wing layout constants describe
the drawing field used by these helpers; they do not restore an old scene,
story compiler or game session.

`build_anime_cast.py`, `build_dialogue_cast.py`, `build_pixel_cast.py` and
`build_character_atlas.py` require original sheets in `AssetsSource/Internal/Characters`.
PixelCast also needs its original palette JSON. These inputs are not all in this
repository. Entry points check selected sources before publishing derivatives;
missing originals produce an error, existing runtime PNGs remain intact, and no
replacement source is invented. The AnimeCast module supplies shared crop constants
and functions to PixelCast and DialogueCast, so it remains in their authoring chain.

`build_ui_theme.py`, `build_ui_desk.py` and `build_ui_paper.py` maintain current
shared UI. `build_surface_materials.py` derives normal maps from existing room PNGs.
`build_room_table_props.py` reuses recovered furniture helpers. Keep source images
and scripts when retaining their derivatives.

## Capture and inspection

`profile_dv_capture.py` launches `dv_level3_capture.gd`, using the normal Host scene
and FrameArchive. This gameplay capture runs only when that testing scope is
requested. `dv_host_startup_probe.gd` and `capture_ui_start.gd` are targeted
startup/UI diagnostics, not complete gameplay acceptance.

`report_runtime_hitches.py`, `summarize_interaction_timing.py` and
`encode_dv_capture.py` consume existing binary evidence through
`language_core/tools/binary_evidence.py`; they do not rerun the game. The old JSON
blind-session summarizer and isolated Blender camera experiment remain in Git
history and are no longer live entry points.

## Static audits

Repository-level `tools/audit_action_paths.py --out NEW.json` inventories current
sources and literal references without starting a session. Optional
`--removed-manifest REMOVED.json` accepts an array of removed repository paths for
one cleanup. It has no mandatory dependency on historical artifacts and directly
classifies the seven current DV files. Missing literal resources, unresolved dynamic
paths and gameplay behavior are separate questions; static inventory does not
establish runtime or visual correctness.
