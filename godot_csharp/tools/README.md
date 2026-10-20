# Asset and capture tools

These tools maintain the Godot host's assets and engineering evidence. Current
gameplay is the seven DV files in `language_core`; runtime assets are under
`godot_csharp/ShareAsset`. No tool provides a second gameplay implementation.

## Theme generation

`build_ui_theme.py` writes all three current themes under `scenes/UI/Styles`.
They share the generator's paper / ink / hand palette, type sizes and font
fallbacks. Update that source before regenerating; do not hand-edit the outputs
or reproduce their styling in scene-local overrides.

| Generated theme | Current consumers and scope |
| --- | --- |
| `TitleTheme.tres` | Title and shared physical casebook; settings, archive text, notes and book controls. |
| `DialogueTheme.tres` | Dialogue prose, speaker labels, response choices and history. |
| `OverlayTheme.tres` | `FieldHUD` and independent `InvestigationUI`; paper panels, editorial headings, manuscript text, notes, action rows and focus states. |

```powershell
python godot_csharp/tools/build_ui_theme.py
python godot_csharp/tools/build_ui_theme.py --check
```

`--check` compares all three files with deterministic generator output. It checks
resource-source consistency, not Godot rendering, legibility or input behavior.
Overlay variations include the HUD family, modal margins/stacks, editorial
caption/title, manuscript body/note and quiet/text/choice actions. Keep their
base types aligned with the consuming Controls, including `FooterRow` as an
`HFlowContainer`.

## Title book presentation

The current title desk is a real 3D stage rendered by `DeskStage3D.gd`, hosted by `TitleUI.tscn` / `TitleUI.gd`. Desk, book, lamp and props share one camera, geometry scale and lighting system. The flame, light energy and material response share a flicker signal. Perspective, occlusion and shadows must remain consistent across the whole stage.

`BookProjection3D.gd` uses the stage camera to intersect the pointer ray with the book's page plane, then maps that hit into the `580 × 820` page SubViewport. It forwards actual pointer events to native Controls. Input must follow the displayed geometry during camera movement and window resizing; a screen rectangle or an unrelated 2D projection is not the input contract.

`BookLeafTurn3D.gd` and `book_leaf_mesh_3d.gdshader` render separate front and back page surfaces, a closed thickness and page edges. Covers use the rigid-cover mode; paper leaves bend while staying bound to the spine. Old page contents are rendered once into a dedicated SubViewport with `UPDATE_ONCE`, then frozen as GPU textures. The production page-turn path must not read rendered pixels back to the CPU every frame or recreate textures every frame. The capture tool's explicit image readback is only for evidence collection. With `reduce_motion` enabled, navigation immediately settles on the requested page, ends any active turn and restores input; parallax and visible flicker are disabled.

### Shared casebook and UI data

The in-game `CasebookUI.gd` instantiates the production `TitleUI.tscn` with
`mode = "casebook"`; the title uses `mode = "title"`. Both reuse the same desk,
physical book, page-turn shader and native Control projection. `BookSettingsPage.gd`
supplies their common settings page; `BookArchivePage.gd` renders the casebook
index, lists, details and personal-note editor. These pages are runtime UI, not
additional Blender geometry or text baked into material maps.

`GameLocale` owns the Chinese (`zh_CN`), Japanese (`ja`) and English (`en`) UI
preference in `user://language.cfg`. `GameSettings` owns presentation preferences
in `user://settings.cfg`. Neither preference file becomes Core state, a world-save
field or casebook data. Changing the UI language must preserve the current draft,
focus and scroll position; delivered prose and personal notes retain their
original wording.

`CasebookData.gd` stores the current viewer's delivered line records, actually
opened visible cards, explicitly acquired projections and separate personal
notes. Original `lines` and their source identifiers remain alongside display
summaries. Geometry snapshots and hidden action labels are not archive inputs.
Personal notes remain player drafts; the separate `InvestigationUI` handles
reasoning through its existing event path. Opening a page or saving a note does
not submit a world action or grant an item.

Host bookmark persistence lives in `scenes/Host/CasebookPersistence.cs`, a partial
`GameEngine`. World state still comes from Session `WriteSave` / `RestoreFile`.
The optional binary `<basename>.casebook.dvlog` binds versioned UI data to the
same viewer and SHA256 of the exact `.dvsave` bytes. It excludes UI preferences.
A game-time switch first saves a new current-session backup with its bound
casebook; it does not overwrite an existing save. Missing old sidecars yield an
empty casebook; rejected sidecars yield an explicit warning and no foreign data.
See `godot_csharp/AGENTS.md` for gates, events and partial-failure handling.

### Geometry and painted materials

`build_painterly_desk.py` runs under Blender 4.2. It writes the editable source to `AssetsSource/Internal/Props/UIDesk/Painterly3D/painterly_desk.blend` and the runtime model to `ShareAsset/Internal/Props/UIDesk/Painterly3D/desk.glb`. The runtime directory also contains the WebP material atlas and page/cloth textures. Keep editable sources separate from imported runtime assets. Blender is an authoring dependency, not a running-game dependency.

`build_desk_surface_maps.py` authors separate wood grooves, woven cloth, paper fibers and brass scratches in Blender nodes and bakes CPU Cycles normal/roughness data. The current reference albedo contributes only a bounded high-frequency registration term. Outputs are a 1024-square OpenGL tangent +Y normal atlas, a linear scalar roughness atlas, and matching 512-square cloth/paper crops. Data PNGs have no display or gamma transform. The editable node source and per-file hashes are saved beside the geometric source as `desk_surface_maps.blend` and `reference-surface-maps-manifest.json`. The runtime consumes these maps on desk props, static book surfaces and both sides of animated pages. Albedo generation, numerical data baking and manual painting are distinct provenance claims; see the source README.

The title camera stays close at the specified 60-degree viewing angle. Elevation and target distance are independent controls; moving closer does not lower the viewing angle. Elevation, target distance and vertical field of view stay fixed during opening. Opening only translates the camera target to follow the readable page. Enlarged physical props are composed through placement and natural edge cropping; their bounds and the moving cover never widen the lens or move the camera upward/backward. A cover moving outside the frame is allowed. The viewport keeps vertical framing and changes side margins as its width changes. `BookSurfaceDetail3D.gd` supplies bounded bevels and `paper_mount.gdshader` affects only the paper slip background. Other glass retains transparency, Fresnel highlights and scene reflection. Only `LensGlass` uses `scenes/UI/magnifier_lens.gdshader` for screen-space, depth-constrained magnification and refraction with Fresnel response. Current shader defaults are `magnification = 1.32` and `refractive_index = 1.52`; the shader is authoritative. Its screen copy comes from the opaque stage, so the sampled image excludes subsequently drawn transparent flame and glass. This is a screen-space approximation, not ray-traced optics or a reproduction of any referenced brand's optical performance.

`AssetsSource/Internal/Props/UIDesk/Painterly3D/desk_physical_spec.json` is the single physical-size input to the geometry builder. It records the assumed book-cover length, millimeters per scene unit, comparable real-product specifications, target dimensions and prop layout. The builder measures actual mesh vertices, calibrates the relevant object axes, seats every prop on the desk and exports measured dimensions plus the wick-tip world position in `ShareAsset/Internal/Props/UIDesk/Painterly3D/desk-physical-layout.json`. Stage flame and light positions consume that generated configuration. Changing physical scale requires checking the entire page-turn sweep, visible control occlusion and reflection bounds; successful native button events alone do not establish unobstructed text. Product references establish size classes, not reproduction of a branded product.

From the repository root, with the relevant executables available on PATH:

```powershell
blender --factory-startup --background --python-exit-code 1 --python godot_csharp/tools/build_painterly_desk.py
blender --factory-startup --background --python-exit-code 1 --python godot_csharp/tools/build_desk_surface_maps.py
dotnet build godot_csharp/DualOntologyWorldEngine.csproj
godot --path godot_csharp --editor
```

Use the Godot .NET executable for both the editor and capture. The executable name may differ on a local installation; substitute its actual path if it is not named `godot`.

### Capture the current 3D title UI

`capture_title_3d.gd` instantiates the production TitleUI without Main or a DV session. It drives actual mouse events through the camera/page projection, observes native button activations and UI events, and records PNG frames plus the binary `ui-input.dvlog` archive. Run it with a visible rendering window, without `--headless`, after building the C# project and importing assets. Supply a new absolute output directory; existing evidence must not be overwritten.

```powershell
godot --path godot_csharp --script res://tools/capture_title_3d.gd -- --out=C:/Temp/title-3d-capture-NEW
```

The capture covers the title presentation, including pointer response, light variation and page navigation. It does not establish successful game startup, save loading, snapshot reconstruction or path/session recovery. The tool rejects an animation capture when the saved reduce-motion preference is enabled; it does not change that preference. `capture_title_book.gd` remains the legacy 2D capture entry point and is not evidence for the current 3D stage. Documentation of these contracts does not claim that the build or capture has passed; report results from the actual run and its evidence.

The title's `load/path` event uses the existing GameEngine serial command queue
and DvSession.RestoreFile. A save awaiting a physical execution receipt is rejected.
File-validation failure returns `load_error` without installing a session; valid
save restoration still requires a separate fresh-session/physical-state review.

### Capture the shared casebook UI

`capture_casebook_ui.gd` hosts production `TitleUI`, `CasebookUI`,
`BookArchivePage` and `InvestigationUI` in a rendered Godot .NET process. Build C#
and import resources first, then supply a new absolute output directory. Do not
use `--headless` or reuse an existing evidence directory.

```powershell
godot --path godot_csharp --script res://tools/capture_casebook_ui.gd -- --outpath=C:/Temp/casebook-ui-capture-NEW --frames
```

`--out=` is an alias for `--outpath=`. The default capture saves key page-turn
angles; optional `--frames` adds samples at 80 ms real elapsed-time intervals
without accelerating the animation. The tool preserves the saved reduce-motion
preference: when enabled, pages settle directly and the log records that no
page-turn-angle evidence was captured. PNGs, binary `ui-input.dvlog` and
`capture-manifest.dvlog` are written through `FrameArchive`; archives publish
from `.partial` only after the background queue drains. The tool has a 41-second
soft deadline and a 44-second hard deadline. Failure preserves available images,
records and partial files instead of replacing previous evidence.

The fixture is a bounded, approved excerpt of delivered text and artwork from
`40-chapter1.dv` (E2/E7), with one portrait. Its binary metadata identifies it as
UI-only input. The script does not load Main, open a DV session or call Core.
`book.open` is the independent UI host entry; subsequent navigation, text editing
and save-button activation use actual projected pointer/keyboard events. The
result shows fixture presentation and UI events, not actual in-world acquisition,
formal reasoning success or successful Session save/restore.

Language coverage temporarily uses `TranslationServer.set_locale`, restores the
original locale and compares `settings.cfg` / `language.cfg` hashes. It does not
click the persistent language-selection buttons, so translated screenshots do
not establish that preference persistence was exercised. Report this boundary
alongside the frame and input evidence. The documented command and fixture
contract are not a build or capture result; actual run status must be recorded
separately.

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

`build_living_floor.py` composes the five published living-wing scenes into
`Maps/living_floor.tscn`. Source scenes and textures remain reconstruction inputs.
It aligns shared door columns and inserts six hall rows to keep the north and
south wings from overlapping. Geometry, labels, lights and collision move together;
logical room identifiers remain unchanged. Rebuild this composite after changing
any of its source scenes. Other buildings and exterior regions remain separate.

For independent blind input use `profile_dv_capture.py --controller player
--window-mode offscreen`. The default offscreen, unfocusable window still renders
the real Host scene, physics and UI. A blind operator may read only captured PNGs
and write ordinary key/click packets to the transient `run/input.json`; private
measurement and session records are for the separate reviewer. `--window-mode
window` opts into a visible window. This is bounded diagnosis, not completion.
On Windows the launcher resolves Godot's console wrapper to the actual runtime
executable before attaching managed sampling; the combined engine output is in
`godot-native.log`. Current dotnet-trace defaults to `dotnet-sampled-thread-time`;
these stack shares include waits and are not CPU utilization. Use
`--managed-profile` when an older installed tool requires a different profile.
Ordinary captures do not start the interactive stdout debugger. Script errors
remain in the native log and result record, and make the launcher return failure.
Native full profiling explicitly starts the script debugger and has greater overhead.
`check_living_floor.gd` checks resource loading and collision/exit structure only,
without starting DV or establishing gameplay acceptance.

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
