# 采集、回放与诊断工具

本目录包含二进制证据读写、采集启动器、诊断和独立检查。生产逻辑位于
`dv_core/modules`，调试与检查闭包位于 `dv_core/checks`，运行资产位于
`godot_csharp/ShareAsset`。资产生成器在 `mcp/asset_pipeline`；下文保留相关
生成与采集前提的导航。

同一任务使用固定测试输出目录。每次新测试前，先等待旧进程结束，核对绝对路径并递归清理上一轮输出；新测试替换旧测试，不滚动归档或复制历史记录。空目录和独占创建要求适用于清理后的本轮写入；失败记录与 partial 保存到下一轮清理为止。清理规则见仓库根 AGENTS.md。

`render_terminal_export.py --svg <CLI导出.svg> --out <新.png> [--font <中文字体>]`
将CLI生产渲染器的定位单元画布导出为PNG。MCP入口为diagnostics /
terminal-export。该工具只解析CLI生成的rect/text SVG子集，不启动会话；
它是渲染导出而非物理终端屏幕截取。需要Pillow，默认使用Windows微软雅黑；
清理上一轮输出后，本轮路径独占创建。

`capture_cli.py --out <本任务固定测试目录> --timeout 90 --format tui` 转发操作者逐行输入，
不代选动作或开启自主循环；默认 player，可显式指定 --controller ai、--viewer ID、
--format jsonl 或 --replay FILE。原始 stdin/stdout/stderr 按原字节写入校验二进制
console.dvlog，CLI 封存 session.dvdag/dvsave，并核对源码和程序集身份。
默认 90 秒硬超时，范围 0..600 秒；超时只终止本次子进程树，返回 124 并保留
console.dvlog.partial 与现有证据。输入线程及管道退出等待有界，避免 stdin 或
子进程封存阻塞采集器。MCP 入口 diagnostics / cli-capture。
CLI 的 capture 单独保存当前 UI；截图不改写为行动。PNG 转换保留 SVG 标题字重。

`check_cli_protocol.py --out <本任务固定测试目录> --timeout 90` 串行检查人工选择和内置 AI，以及各自一次到期行动及回放。人工选择若先入队，仅再完成当前最先到期的一次行动，不追跑后续剧情。每条命令最多等待 25 秒，每个采集窗口最多 90 秒；仅选择当前显示的首项，不读取客观答案决定行动。检查 JSON 命令输入、视角与版本、评分保留、取消清除、定位/确认、排程边界和播放暂停，完整原始数据仍由 capture_cli 写入二进制记录；summary.json 只存工程结果。

## 工具工程入口

统一 MCP 中，Python 命令属于 `diagnostics`，GDScript 采集与观察属于 `godot_tools`。地图截图为 `capture-maps`，UI 采集为 `capture-casebook`、`capture-period`、`capture-start-reveal`、`capture-title`，宿主入口为 `capture-saved-game`、`capture-level3`、`host-startup`，独立观察为 `ui-input`、`living-floor`、`music-context`。路由直接指向本目录现役脚本；列举工具不执行它们。`ui-input` 自动采用一次性工具用户数据。

所有命令从仓库根目录执行。`mcp/godot_workspace.py` 链接生产场景、成品、
契约和本目录，创建独立临时 Godot 工程。`--csharp` 构建工具程序集并编入
`FrameArchive.cs`，随后导入该工程资源，再启动指定脚本。FrameArchive 的
唯一工具资源路径为 `res://tools/FrameArchive.cs`。

```powershell
python mcp/godot_workspace.py --csharp --godot $env:GODOT --script res://tools/capture_title_3d.gd -- --out=C:/Temp/title-current
python mcp/godot_workspace.py --csharp --godot $env:GODOT --script res://tools/capture_start_reveal.gd -- --out=C:/Temp/opening-current --dv-audit
python mcp/verification/run_saved_game_capture.py --out artifacts/capture/current --seconds 90 --mode detective
python mcp/verification/profile_dv_capture.py --out artifacts/profile/current --seconds 90 --godot $env:GODOT
```

两个 Python 采集启动器共用 `capture_workspace.py`：建立工具工程、构建、
导入资源，再冻结源码/实际工具程序集/导入资源指纹，最后启动渲染进程。
宿主帧取证显式开启 `--dv-audit`。临时工作区退出时清理，证据保存在指定
的本任务固定目录，新测试前清理旧输出。UI-only 工具不打开 DV 会话；使用 Main 的工具通过现役 `flow`
读取开局、阅读、关闭和书签状态，现场执行仍由 `field_executor` 承担。

`--sandbox` 和默认编辑器工具工程采用纯 GDScript MapSandbox 主场景，
不构建 C# 或 DV。需要 FrameArchive 的采集脚本使用 `--csharp`。

## Field input and hint checks

Same-map authored rooms may share open floor edges without a D tile.
`CampusMapView.passage_at` checks the actual body's source, room membership,
cardinally adjacent authored floor cells, unchanged walkability and a real-body
`test_move` over at most one existing terrain cell. It returns geometry only.
WorldView matches a unique currently available from/to move before requesting
Prepare, then retains real collision and Complete. Keyboard boundary motion,
nearby E and short boundary clicks share this check; distant clicks still walk
within the source room first. No door, radius, collision shape or position is
invented. Finite verification must separately cover open edges, solid walls,
unavailable/ambiguous actions, release cancellation and real neighbour arrival;
static review does not establish these outcomes or repair camera clipping.

During an actual continuous request, the field hint projects its current phase
and cancellation flag in Chinese, Japanese or English, ahead of ordinary field
hints even when formal submission is closed. Dialogue/modal UI and hidden
execution fields still suppress it. Preparation, cancellation, confirmation and
return messages are UI only; they assert no arrival or new world fact and clear
with the request. Verify a delayed request and cancellation separately from a
successful crossing.

Production E interaction combines nearby same-map doorway cells and authored
cross-map `Exits/Shape` rectangles. `CampusMapView.manual_exit_targets_near` is a
geometry query; `WorldView` matches only currently available doorway actions,
deduplicates their IDs and submits only a unique candidate through the existing
Prepare / physical execution / Complete chain. No capture tool may replace this
with a position write or an invented action. With no hotspot hint, an actionable
field shows that admitted door label and E, or brief Chinese/Japanese/English
controls; modal panels and dialogue suppress this guidance. New-site sparks use
the same visibility filter as interaction, and reduced motion keeps a static mark.

Finite rendered checks should cover a unique nearby cross-map exit, out-of-range
and closed exits, ambiguous actions, same-map doors, visible/hidden new hotspots
and runtime reduced-motion changes. Static review or a patch application does
not establish input, collision or screenshot acceptance. Movement transactions retain the committed source while physical execution runs.
The host consumes the actual Core completion outcome to commit arrival or restore
the original source; capture records retain the real receipt and presentation.

Furniture hover keeps the existing draw/visibility checks and prefers an actual
hit sprite with a current `sprite_sites` binding before a generic furniture name.
A static interaction marker requires a visible site/person with a current
available option and an actionable field; it is not an unread/new-evidence flag.
E with no admitted nearby hotspot or unique door gives a localized UI notice at
the existing submission boundary. These affordances do not change reach,
collision, observation or action eligibility. Check them using a specific object
overlapping a table, an unavailable nearby person and modal/dialogue suppression.

Field mouse presses use the current event's viewport position, converted through
the field canvas inverse, for the same `WorldView.pointer_hit` query used by
hover and for ground navigation. Captured motion/press/release packets can arrive
in one process interval; a previous-frame hover is not a click target. This does
not bypass unhandled-input propagation or modal, drawing, sight, collision and
action gates. A captured click with no motion alone does not establish a defect:
pair its event coordinates with UI coverage, current field gates and route result.

Released continuous passages have a narrow manual retry proof: the real body
must be stopped in the verified source room with no transfers and exactly equal
Prepared source and actual positions. Main independently matches the live
prepared action, request, actor, serial, rooms, map and source geometry before
adding a `source-cancel-v1` certificate; a receipt cannot mark itself verified.
The cancellation notice is UI only. DV keeps the rejected result and existing
cost/position/observation rules and permits only fresh manual input, without
automatic redispatch. A request-time point is diagnostic, not the Prepared
source. The current executor handles rejected completion through its shared settle/restore
transaction. The cancellation certificate remains an independently recorded
physical-input case.

## Theme generation

`build_ui_theme.py` writes all three current themes and their common native popup SVG icons under `scenes/UI/Styles`.
They share the generator's paper / ink / hand palette, type sizes and font
fallbacks. Update that source before regenerating; do not hand-edit the outputs
or reproduce their styling in scene-local overrides.

| Generated theme | Current consumers and scope |
| --- | --- |
| `TitleTheme.tres` | Title and shared physical casebook; settings, archive text, notes and book controls. |
| `DialogueTheme.tres` | Dialogue prose, speaker labels, response choices and history. |
| `OverlayTheme.tres` | `FieldHUD` and independent `InvestigationUI`; paper panels, editorial headings, manuscript text, notes, action rows and focus states. |

```powershell
python mcp/asset_pipeline/build_ui_theme.py
python mcp/asset_pipeline/build_ui_theme.py --check
```

`--check` compares the themes and popup icons with deterministic generator output. It checks
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
`GameEngine`. World state still comes from Session `WriteSave` / `RestoreFile` and its latest committed stable state.
The optional binary `<basename>.casebook.dvlog` binds versioned UI data to the
same viewer and SHA256 of the exact `.dvsave` bytes. It excludes UI preferences.
A game-time switch first saves a new current-session backup with its bound
casebook; it does not overwrite an existing save. Missing old sidecars yield an
empty casebook; rejected sidecars yield an explicit warning and no foreign data.
See `godot_csharp/AGENTS.md` for gates, events and partial-failure handling.

### Geometry and painted materials

`build_painterly_desk.py` runs under Blender 4.2. It writes the editable source to `mcp/asset_pipeline/sources/Internal/Props/UIDesk/Painterly3D/painterly_desk.blend` and the runtime model to `ShareAsset/Internal/Props/UIDesk/Painterly3D/desk.glb`. The runtime directory also contains the WebP material atlas and page/cloth textures. Keep editable sources separate from imported runtime assets. Blender is an authoring dependency, not a running-game dependency.

`build_desk_surface_maps.py` authors separate wood grooves, woven cloth, paper fibers and brass scratches in Blender nodes and bakes CPU Cycles normal/roughness data. The current reference albedo contributes only a bounded high-frequency registration term. Outputs are a 1024-square OpenGL tangent +Y normal atlas, a linear scalar roughness atlas, and matching 512-square cloth/paper crops. Data PNGs have no display or gamma transform. The editable node source and per-file hashes are saved beside the geometric source as `desk_surface_maps.blend` and `reference-surface-maps-manifest.json`. The runtime consumes these maps on desk props, static book surfaces and both sides of animated pages. Albedo generation, numerical data baking and manual painting are distinct provenance claims; see the source README.

The title camera stays close at the specified 60-degree viewing angle. Elevation and target distance are independent controls; moving closer does not lower the viewing angle. Elevation, target distance and vertical field of view stay fixed during opening. Opening only translates the camera target to follow the readable page. Enlarged physical props are composed through placement and natural edge cropping; their bounds and the moving cover never widen the lens or move the camera upward/backward. A cover moving outside the frame is allowed. The viewport keeps vertical framing and changes side margins as its width changes. `BookSurfaceDetail3D.gd` supplies bounded bevels and `paper_mount.gdshader` affects only the paper slip background. Other glass retains transparency, Fresnel highlights and scene reflection. Only `LensGlass` uses `scenes/UI/magnifier_lens.gdshader` for screen-space, depth-constrained magnification and refraction with Fresnel response. Current shader defaults are `magnification = 1.32` and `refractive_index = 1.52`; the shader is authoritative. Its screen copy comes from the opaque stage, so the sampled image excludes subsequently drawn transparent flame and glass. This is a screen-space approximation, not ray-traced optics or a reproduction of any referenced brand's optical performance.

`mcp/asset_pipeline/sources/Internal/Props/UIDesk/Painterly3D/desk_physical_spec.json` is the single physical-size input to the geometry builder. It records the assumed book-cover length, millimeters per scene unit, comparable real-product specifications, target dimensions and prop layout. The builder measures actual mesh vertices, calibrates the relevant object axes, seats every prop on the desk and exports measured dimensions plus the wick-tip world position in `ShareAsset/Internal/Props/UIDesk/Painterly3D/desk-physical-layout.json`. Stage flame and light positions consume that generated configuration. Changing physical scale requires checking the entire page-turn sweep, visible control occlusion and reflection bounds; successful native button events alone do not establish unobstructed text. Product references establish size classes, not reproduction of a branded product.

From the repository root, with the relevant executables available on PATH:

```powershell
blender --factory-startup --background --python-exit-code 1 --python mcp/asset_pipeline/build_painterly_desk.py
blender --factory-startup --background --python-exit-code 1 --python mcp/asset_pipeline/build_desk_surface_maps.py
python mcp/godot_workspace.py --csharp --editor --godot $env:GODOT
```

Use the Godot .NET executable for both the editor and capture. The executable name may differ on a local installation; substitute its actual path if it is not named `godot`.

### Capture the current 3D title UI

`capture_title_3d.gd` instantiates the production TitleUI without Main or a DV session. It drives actual mouse events through the camera/page projection, observes native button activations and UI events, and records PNG frames plus the binary `ui-input.dvlog` archive. Run it with a visible rendering window, without `--headless`; the workspace launcher builds its C# project and imports assets before launch. Supply a new absolute output directory; existing evidence must not be overwritten.

```powershell
python mcp/godot_workspace.py --csharp --godot $env:GODOT --script res://tools/capture_title_3d.gd -- --out=C:/Temp/title-3d-capture-NEW
```

The capture covers the title presentation, including pointer response, light variation and page navigation. Start is observed as a locked cover with opening ownership; without Main, this UI-only tool does not reveal a prepared game field or expect a separate start page. It does not establish successful game startup, save loading, snapshot reconstruction or path/session recovery. The tool rejects an animation capture when the saved reduce-motion preference is enabled; it does not change that preference. The retired 2D title capture is preserved only in Git history; current title observation uses this 3D entry or capture_start_reveal.gd for the real opening handoff. Documentation of these contracts does not claim that the build or capture has passed; report results from the actual run and its evidence.

The title's `load/path` event uses the existing GameEngine serial command queue
and DvSession.RestoreFile. A save awaiting a physical execution receipt is rejected.
File-validation failure returns `load_error` without installing a session; valid
save restoration still requires a separate fresh-session/physical-state review.

### Capture the shared casebook UI

`capture_casebook_ui.gd` hosts production `TitleUI`, `CasebookUI`,
`BookArchivePage` and `InvestigationUI` in a rendered Godot .NET tool workspace.
The launcher builds and imports before starting the script; supply a task-fixed absolute output directory cleared before each new test. Do not
use `--headless`.

```powershell
python mcp/godot_workspace.py --csharp --godot $env:GODOT --script res://tools/capture_casebook_ui.gd -- --outpath=C:/Temp/casebook-ui-capture-current --frames
```

`--out=` is an alias for `--outpath=`. The default capture saves key page-turn
angles; optional `--frames` adds samples at 80 ms real elapsed-time intervals
without accelerating the animation. The tool preserves the saved reduce-motion
preference: when enabled, pages settle directly and the log records that no
page-turn-angle evidence was captured. PNGs, binary `ui-input.dvlog` and
`capture-manifest.dvlog` are written through `FrameArchive`; archives publish
from `.partial` only after the background queue drains. The tool has a 41-second
soft deadline and a 44-second hard deadline. Failure keeps the current test images,
records and partial files until the next test clears and replaces that output directory.

The fixture is a bounded, approved excerpt of delivered text and artwork from
`40-chapter1.dv` (E2/E7), with one portrait. Its binary metadata identifies it as
UI-only input. The script does not load Main, open a DV session or call Core.
Its source hash resolves `dv_core/modules/40-chapter1.dv` from the temporary
project's `duelview_tools/repository` setting. It does not cross a `res://..`
resource boundary.
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
`mcp/asset_pipeline/sources/Internal/Props/Chapter2/CampusSite`; `--site-source` can point to a
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
`build_character_atlas.py` require original sheets in `mcp/asset_pipeline/sources/Internal/Characters`.
PixelCast also needs its original palette JSON. These inputs are not all in this
repository. Entry points check selected sources before publishing derivatives;
missing originals produce an error, existing runtime PNGs remain intact, and no
replacement source is invented. The AnimeCast module supplies shared crop constants
and functions to PixelCast and DialogueCast, so it remains in their authoring chain.

For newly generated, reviewed 3x6 masters, `publish_redrawn_cast.py --source DIR
--review NEW_DIR [--only KEY...]` uses the existing native cell/animation contract.
It checks all selected sheets before publishing. The alpha>=200 coverage mask
is explicit for both gutter registration and final binary alpha. A per-character
publication recipe preserves standing height across rebuilds; previews and source,
old-asset and new-asset hashes go to the new review directory. No collision,
animation resource or timing is rewritten.

`build_ui_theme.py`, `build_ui_desk.py` and `build_ui_paper.py` maintain current
shared UI. `build_surface_materials.py` derives normal maps from existing room PNGs.
`build_room_table_props.py` reuses recovered furniture helpers. Keep source images
and scripts when retaining their derivatives.

## Capture and inspection

`run_saved_game_capture.py` and `capture_saved_game.gd` capture the production
Host, request safe bookmark pairs and resume them through the ordinary load path.
See [SAVED_GAME_CAPTURE.md](SAVED_GAME_CAPTURE.md) for commands, source fingerprints,
input packets, copy-failure handling and the limits of restored-run evidence.

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
`check_living_floor.gd` instantiates the published `living_floor` scene and moves
a `CharacterBody2D` with the production foot shape through its declared doors
using `move_and_collide`. Internal doors cover an actual round trip; exterior
doors cover the local approach to the exit. Use `--door=<contract door ID>` to
limit the run to one passage. Its report identifies the published collision
scope: no DV session, live access barriers, cross-map transfer or gameplay
acceptance is established. `check_geometry_contracts.py` separately compares
all maps, anchors, exits and door topology with the scene text; that result is
static evidence and does not establish physical movement.

`capture_period_ui.gd` reviews the production opening dialogue, then closes the
host and renders isolated production UI components. It checks manuscript/guide
nesting, restored reading position, wrapped choices, UI locale and reduced motion
at 1280x720 and 1600x900. Long text and choices are explicitly marked layout
samples and never reach Core. Run with a rendering Godot .NET build and
`python mcp/godot_workspace.py --csharp --godot GODOT_PATH --script res://tools/capture_period_ui.gd -- --out=NEW_ABSOLUTE_DIR --dv-audit`.
The directory must not already exist. PNGs and checksummed `ui-review.dvlog`
retain rendered evidence and raw input; this is level-two UI diagnosis, not a
full game run. See [the presentation contracts](../../docs/ui-presentation.md)
for the visual review scope.

`profile_dv_capture.py` launches `dv_level3_capture.gd`, using the normal Host scene
and FrameArchive. This gameplay capture runs only when that testing scope is
requested. `dv_host_startup_probe.gd` is a targeted startup diagnostic, not complete gameplay acceptance.
The obsolete `capture_ui_start.gd` naming-form flow has been retired; production opening observation uses `capture_start_reveal.gd`.

`report_runtime_hitches.py`, `summarize_interaction_timing.py` and
`encode_dv_capture.py` consume existing binary evidence through
the same-directory `binary_evidence.py`; they do not rerun the game. The old JSON
blind-session summarizer and isolated Blender camera experiment remain in Git
history and are no longer live entry points.

## Static audits

`run_debug.py` imports the build toolchain from `../build`; binary evidence,
raw DAG and checkpoint readers import their peers from this directory. Their
executables and data readers remain outside the production assemblies.

Repository-level `mcp/verification/audit_action_paths.py --out NEW.json` inventories current
sources and literal references without starting a session. Optional
`--removed-manifest REMOVED.json` accepts an array of removed repository paths for
one cleanup. It has no mandatory dependency on historical artifacts and directly
classifies the compiler, production modules and isolated checks. Missing literal resources, unresolved dynamic
paths and gameplay behavior are separate questions; static inventory does not
establish runtime or visual correctness.

## 现有记录回放

Core 的 `SmartReplay` 由 CLI 和游戏开发控制台共用。它直接读取 `.dvdag`
和 `.dvlog`，归一原始事件、调试投影和宿主可见记录。操作入口如下：

```powershell
dotnet run --project dv_core/cli/DvCli.csproj -- --replay artifacts/capture/session.dvdag --speed 4 --tui
dotnet run --project dv_core/cli/DvCli.csproj -- --replay artifacts/capture/visible.dvlog --command step --jsonl
```

该入口播放已记录的结果，忽略录制期间的现实等待；原始日志保持原字节。
`encode_dv_capture.py` 的视频编码继续沿实际 `real_ms` 轴保留采集间隔，两者
用途分别是阅读记录和还原真实录屏时间。


## Detective inquiry input and trace context

The manuscript uses the currently admitted replay_input schema. Unselected fields remain unselected; selecting a hypothesis sends a pure-data action_id/hypothesis_id envelope through the existing String input channel. DV rechecks and pays for one selected reconstruction. The UI neither simulates nor grants knowledge.

PlayerViewTrace scans actual frames when `--dv-audit` is enabled. A single synchronous scan shares its small read-only UI context among control bindings and copies it before adding frame text/object fields. Ordinary play keeps the lightweight event policy. Capture and production performance are reported with their recording modes.

## Shared controller and return input

See `../../godot_csharp/docs/chapter1-input.md` for the default input map, `UIInput` ownership,
one-level right-click/B return, and versioned `GameSettings` binding migration.
`probe_ui_input.gd` is a finite native-Control mechanism probe with isolated user
data and a new absolute `--out` directory. It writes binary input observations
through `FrameArchive`; it does not run Main/DV or validate physical controllers,
book rendering, map traversal or gameplay. Build/import prerequisites, commands
and evidence boundaries are documented there; this index asserts no run result.

### Focused doorway input observations

`dv_level3_capture.gd` retains its normal capture defaults. For an explicitly scoped input/physics observation, `--movement-samples` adds binary per-physics-frame observations while the player is moving or a continuous doorway request exists; `--capture-ms=1000` lowers screenshot readback frequency. The stream records actual body positions, instance identity, input direction, authoritative/physical room and request phase without moving the body or selecting actions. It is diagnostic instrumentation, not an uninstrumented performance baseline.

Transient `input.json` packets now also accept `click` with numeric `button` (1 left, 2 right), `joy_button` with numeric `button` and optional `pressed`, and `joy_axis` with numeric `axis` and `value` in [-1, 1]. These go through `Input.parse_input_event`; they do not establish physical-controller/driver coverage. Held controller buttons and axes are released on capture close and recorded in the binary input stream. Formal game scope and authorization remain as described above; a short doorway observation is not a complete story acceptance.

## 采集封存与进程期限

`run_saved_game_capture.py` 与 `profile_dv_capture.py` 共用 `capture_result.py` 的完成合同：运行进程正常退出、达到请求的采集时长、唯一新宿主会话、完整 end 与 capture-close 记录、宿主 close-result 完成、零归档/脚本/加载错误、源码和工具工程指纹未变化。请求的检查点也必须全部完成。托管采样还要求发现目标进程、采样器正常退出、可读的报告；采样成功不能替代宿主封存。

两入口收集 stdout、stderr 和原生日志。游戏、采样发现、采样进程及报告均有期限，超时只终止本次启动的进程并保留证据。`dv_level3_capture.gd` 在结束时释放所有登记的按键、手柄按钮和轴，包括没有 duration_ms 的显式长按；capture_saved_game 共用该输入实现。底层关闭等待有期限，capture-close 明确记录 host_closed、archive_idle、complete 与 timeout。

## 逻辑记录与领域分析完成语义

`run_debug.py` 分别写出三个结果：`evidence_complete` 表示正常停止、记录封存、配置与源码一致；`causal_check_complete` 直接采用 70-checks 的 `domain_evaluation_complete`；`main_causal_supported` 表示已完成分析中的主因果支持结论。`causal_check_reason` 保留 `completed`、`invalid_integrity`、`missing_initial_state`、`invalid_trace` 或 `configuration_mismatch`，工具启动失败和超时另写 `causal_check_status` 与具体错误。

只有证据完整且领域分析实际完成时，逻辑工具返回 0。完成分析后得到负面领域判断，仍属于已完成分析；调用方应读取主因果结论和领域报告。未进入分析、证据不完整或执行失败返回非零，保留对应原始证据。

几何工具共享 `mcp/asset_pipeline/scene_geometry.py` 的场景读取规则，覆盖锚点、房间、出口碰撞矩形、内部门禁、跨图出生点及室外坐标。`living_hall` 外部目标在合同发布时统一为 `living_floor`；内部门禁不进入跨地图出口清单。这里维护现有检查器的合同接线，工具只在明确请求时执行。
