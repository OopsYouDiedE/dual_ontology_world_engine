# 其他 AI 分支工程合并盘点

- 采样：2026-10-21T14:12:40.544248+08:00；分支 `codex/detective-rpg-20261021`；HEAD `9f434197e0bb8d32c401b442aac924ad7b5e2bea`。
- 只做 Git 祖先、文件/补丁、三方合并盘点；未构建、导入、执行会话、修改生产源码、index 或分支。merge-tree 仅创建临时 Git 对象；merge-file 只使用系统临时副本。
- 未读取项目设计正文；`docs/design-standard.md` 与 `language_core/DESIGN.md` 只比较路径、字节/哈希和统计。其他工程源码可静态审读。
- 工作树包含本轮及其他 AI 大量未提交源、素材与证据；本报告是动态采样，实际整合前应重采样。必须保留音乐 HEAD `9f434197` 及 `c27d0b1e`。

## 结论与建议顺序

1. 先保存当前全部改动的可恢复快照，并保留未跟踪资产和原始证据；不可直接覆盖式 checkout/cherry-pick 到脏工作树。
2. 语言部分以 `core/natural-authoring-20261021` 为整合单位；它已包含 `6634da192`，无需再重复选入 formula 分支。大部分生产文件已经在工作树中，整合重点是建立历史关系并保留 30/40、README、AGENTS 上的本轮叠加。
3. 独立保留 `ui/title-ready-reveal-20261021` 的标题准备/揭幕、地图生成源与产物。它直接基于音乐 HEAD，工作树已含主要补丁；保留现有人工选项、接触回执和音乐阻塞修正。
4. 再整合 `perf/chapter1-seamless-controls-20261021` 三个提交；在 Main、WorldView、TitleUI、UISystem 联合人工协调连续门道请求、正式选择门控、标题准备和关闭/存档生命周期。该分支新增输入/本地化/主题文件目前缺失，必须成组引入。
5. `docs/future-outline-20261020` 仅修改设计规范，正文决策由主代理核对；工作树可反向应用该分支全部补丁，说明文字改动已存在，但不代表设计语义验收。
6. 分支历史 merge-tree 无冲突只说明已提交树；不能替代当前脏工作树三方协调。本盘点不把静态合并结果当作编译、输入、碰撞、剧情或性能证据。

## 基准与历史关系

| 分支 | tip | 与 HEAD 的共同基准 | 已入 HEAD | HEAD 缺少的提交 |
| --- | --- | --- | --- | --- |
| `core/formula-functions-20261021` | `6634da192` | `8dead4599` | 否 | 6634da19 feat(core): add formula functions, shared evaluation and certified solvers |
| `core/natural-authoring-20261021` | `85427a524` | `8dead4599` | 否 | 85427a52 Add natural prose and domain sentences to Core authoring<br>6634da19 feat(core): add formula functions, shared evaluation and certified solvers |
| `perf/chapter1-seamless-controls-20261021` | `54e8a340d` | `601bca705` | 否 | 54e8a340 Record generated UI imports and use monotonic input observations<br>492a54de Release rejected passage safely and verify layered book controls<br>614d7826 Keep chapter-one passage continuous and unify book/controller UI |
| `ui/title-ready-reveal-20261021` | `b93233ca1` | `9f434197e` | 否 | b93233ca Reveal the prepared field from the title |
| `docs/future-outline-20261020` | `0bc5421a5` | `5c6a75482` | 否 | 0bc5421a docs: integrate future chapters and island-bound homecoming |

`formula-functions` 是 `natural-authoring` 的祖先。其余四个 tip 互不包含各自的独立修复；title 分支的基准恰是当前 HEAD。

## 逐分支文件范围与工作树覆盖

判读：`相同`为原始字节完全相同；`补丁已含`为 `git apply --reverse --check` 可完整通过；`需协调`只表示不能完整反向核验，不等于补丁缺失；`缺文件`为工作树没有该路径。原始证据按字节另计，不能覆盖。

### `core/formula-functions-20261021`

- 差异统计：25 files changed, 2003 insertions(+), 169 deletions(-)。
- 分支证据文件：14；工作树字节比较：相同 14。

| 文件 | 工作树状态 |
| --- | --- |
| `AGENTS.md` | 需协调 |
| `docs/syntax.md` | 需协调 |
| `language_core/10-compiler.dv` | 需协调 |
| `language_core/20-core.dv` | 相同 |
| `language_core/30-session.dv` | 补丁已含，另有叠加 |
| `language_core/AGENTS.md` | 需协调 |
| `language_core/DESIGN.md` | 相同 |
| `language_core/README.md` | 需协调 |
| `language_core/REFERENCES.md` | 相同 |
| `language_core/SYNTAX.md` | 需协调 |
| `language_core/compiler/README.md` | 需协调 |

### `core/natural-authoring-20261021`

- 差异统计：893 files changed, 252040 insertions(+), 712 deletions(-)。
- 分支证据文件：879；工作树字节比较：相同 879。

| 文件 | 工作树状态 |
| --- | --- |
| `AGENTS.md` | 补丁已含，另有叠加 |
| `docs/syntax.md` | 相同 |
| `language_core/10-compiler.dv` | 相同 |
| `language_core/20-core.dv` | 相同 |
| `language_core/30-session.dv` | 补丁已含，另有叠加 |
| `language_core/40-chapter1.dv` | 补丁已含，另有叠加 |
| `language_core/50-chapter2.dv` | 相同 |
| `language_core/AGENTS.md` | 补丁已含，另有叠加 |
| `language_core/DESIGN.md` | 相同 |
| `language_core/README.md` | 需协调 |
| `language_core/REFERENCES.md` | 相同 |
| `language_core/SYNTAX.md` | 相同 |
| `language_core/compiler/README.md` | 相同 |
| `language_core/tools/dv_toolchain.py` | 相同 |

### `perf/chapter1-seamless-controls-20261021`

- 差异统计：47 files changed, 2830 insertions(+), 170 deletions(-)。

| 文件 | 工作树状态 |
| --- | --- |
| `AGENTS.md` | 补丁已含，另有叠加 |
| `docs/chapter1-continuity.md` | 相同 |
| `godot_csharp/AGENTS.md` | 需协调 |
| `godot_csharp/docs/chapter1-input.md` | 相同 |
| `godot_csharp/project.godot` | 相同 |
| `godot_csharp/scenes/Chapter2/Field/CampusMapView.gd` | 补丁已含，另有叠加 |
| `godot_csharp/scenes/Host/GameEngine.cs` | 相同 |
| `godot_csharp/scenes/Host/Main.gd` | 需协调 |
| `godot_csharp/scenes/Shared/Field/FieldResources.gd` | 相同 |
| `godot_csharp/scenes/Shared/Field/WorldView.gd` | 需协调 |
| `godot_csharp/scenes/Shared/Input/UIInput.gd` | 相同 |
| `godot_csharp/scenes/Shared/Input/UIInput.gd.uid` | 相同 |
| `godot_csharp/scenes/Shared/Locale/i18n/opening_ui.csv` | 相同 |
| `godot_csharp/scenes/Shared/Locale/i18n/opening_ui.csv.import` | 相同 |
| `godot_csharp/scenes/Shared/Settings/GameSettings.gd` | 相同 |
| `godot_csharp/scenes/UI/BookArchivePage.gd` | 相同 |
| `godot_csharp/scenes/UI/BookOpeningPage.gd` | 相同 |
| `godot_csharp/scenes/UI/BookOpeningPage.gd.uid` | 相同 |
| `godot_csharp/scenes/UI/BookProjection3D.gd` | 相同 |
| `godot_csharp/scenes/UI/BookSettingsPage.gd` | 相同 |
| `godot_csharp/scenes/UI/DialogueUI.gd` | 补丁已含，另有叠加 |
| `godot_csharp/scenes/UI/FieldHUD.gd` | 补丁已含，另有叠加 |
| `godot_csharp/scenes/UI/InvestigationUI.gd` | 补丁已含，另有叠加 |
| `godot_csharp/scenes/UI/ModalPanel.gd` | 相同 |
| `godot_csharp/scenes/UI/Styles/DialogueTheme.tres` | 相同 |
| `godot_csharp/scenes/UI/Styles/OverlayTheme.tres` | 相同 |
| `godot_csharp/scenes/UI/Styles/TitleTheme.tres` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-close.svg` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-close.svg.import` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-radio-off.svg` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-radio-off.svg.import` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-radio-on.svg` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-radio-on.svg.import` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-submenu-mirrored.svg` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-submenu-mirrored.svg.import` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-submenu.svg` | 相同 |
| `godot_csharp/scenes/UI/Styles/native-submenu.svg.import` | 相同 |
| `godot_csharp/scenes/UI/TitleUI.gd` | 需协调 |
| `godot_csharp/scenes/UI/UISystem.gd` | 需协调 |
| `godot_csharp/scenes/UI/UITransition.gd` | 需协调 |
| `godot_csharp/tools/README.md` | 需协调 |
| `godot_csharp/tools/build_ui_theme.py` | 需协调 |
| `godot_csharp/tools/capture_casebook_ui.gd` | 需协调 |
| `godot_csharp/tools/capture_title_3d.gd` | 需协调 |
| `godot_csharp/tools/dv_level3_capture.gd` | 需协调 |
| `godot_csharp/tools/probe_ui_input.gd` | 缺文件 |
| `godot_csharp/tools/probe_ui_input.gd.uid` | 缺文件 |

### `ui/title-ready-reveal-20261021`

- 差异统计：53 files changed, 1372 insertions(+), 51 deletions(-)。
- 分支证据文件：34；工作树字节比较：相同 30，不同 4。

| 文件 | 工作树状态 |
| --- | --- |
| `docs/title-opening.md` | 相同 |
| `godot_csharp/AGENTS.md` | 补丁已含，另有叠加 |
| `godot_csharp/scenes/Chapter2/Maps/living_floor.tscn` | 相同 |
| `godot_csharp/scenes/Chapter2/Maps/zone_bing.tscn` | 相同 |
| `godot_csharp/scenes/Chapter2/Maps/zone_ding.tscn` | 相同 |
| `godot_csharp/scenes/Chapter2/Maps/zone_jia.tscn` | 相同 |
| `godot_csharp/scenes/Host/Main.gd` | 需协调 |
| `godot_csharp/scenes/Shared/Field/WorldView.gd` | 补丁已含，另有叠加 |
| `godot_csharp/scenes/UI/DialogueUI.gd` | 补丁已含，另有叠加 |
| `godot_csharp/scenes/UI/FieldHUD.gd` | 补丁已含，另有叠加 |
| `godot_csharp/scenes/UI/TitleUI.gd` | 相同 |
| `godot_csharp/scenes/UI/UISystem.gd` | 需协调 |
| `godot_csharp/scenes/UI/title_reveal.gdshader` | 相同 |
| `godot_csharp/scenes/UI/title_reveal.gdshader.uid` | 相同 |
| `godot_csharp/tools/build_chapter2_scenes.py` | 相同 |
| `godot_csharp/tools/build_living_floor.py` | 补丁已含，另有叠加 |
| `godot_csharp/tools/capture_start_reveal.gd` | 相同 |
| `godot_csharp/tools/capture_start_reveal.gd.uid` | 相同 |
| `godot_csharp/tools/chapter2_art/rooms.py` | 相同 |

### `docs/future-outline-20261020`

- 差异统计：1 file changed, 248 insertions(+), 108 deletions(-)。

| 文件 | 工作树状态 |
| --- | --- |
| `docs/design-standard.md` | 补丁已含，另有叠加 |

- 设计文档 blob：HEAD `7f722ccd7b47bee10cf73623bb566e4b2589a789`；分支 `9ebafc49a3d2b952577197effb6d294051d59b27`；工作树 `6cba8061140fb2fada8bb8811edec4e54a839b50`。仅列 blob 与统计，不读取正文。

## 已提交树的真实合并冲突

使用 `git merge-tree --write-tree --name-only HEAD <branch>`：formula、natural、title、future-outline 返回 0；perf 返回 1，冲突路径为 Main.gd、UISystem.gd。

Main 的四块冲突：

- field.ui_busy 从 `_field_input_busy` 改接 `_field_ui_busy`，并增加 formal_selection_ready。当前工作树已有独立正式选择门控，需保留它的当前控制语义。
- _drain 的有界批处理与 `not _view_faulted` 条件；二者都应保留，避免积压期间冻结输入或视图错误后继续推进。
- _drain 尾部的 `_try_bookmark_save()` 与故障门控；不能丢失已提交存档安全修复。
- 恢复/替换状态时 `_bookmark_requested/_bookmark_next` 清理与 `_view_faulted/_player_options_open` 重置；不能二选一覆盖。

UISystem 的一块冲突：音乐生命周期 `_presentation_music_token` 与输入生命周期 `_host_input_enabled` 并置，需保留两套职责。

## 当前工作树三方预演

对每个现存非证据文本文件，以共同基准、当前工作树、分支 tip 运行 `git merge-file -p`，不写回生产。以下为文本冲突块数；设计正文跳过。

| 分支 | 文件 | 冲突块 |
| --- | --- | --- |
| `core/natural-authoring-20261021` | `language_core/AGENTS.md` | 1 |
| `core/natural-authoring-20261021` | `language_core/README.md` | 1 |
| `perf/chapter1-seamless-controls-20261021` | `godot_csharp/AGENTS.md` | 1 |
| `perf/chapter1-seamless-controls-20261021` | `godot_csharp/scenes/Host/Main.gd` | 18 |
| `perf/chapter1-seamless-controls-20261021` | `godot_csharp/scenes/Shared/Field/WorldView.gd` | 8 |
| `perf/chapter1-seamless-controls-20261021` | `godot_csharp/scenes/UI/DialogueUI.gd` | 1 |
| `perf/chapter1-seamless-controls-20261021` | `godot_csharp/scenes/UI/TitleUI.gd` | 8 |
| `perf/chapter1-seamless-controls-20261021` | `godot_csharp/scenes/UI/UISystem.gd` | 5 |
| `perf/chapter1-seamless-controls-20261021` | `godot_csharp/tools/README.md` | 1 |
| `ui/title-ready-reveal-20261021` | `godot_csharp/AGENTS.md` | 1 |
| `ui/title-ready-reveal-20261021` | `godot_csharp/scenes/Host/Main.gd` | 2 |
| `ui/title-ready-reveal-20261021` | `godot_csharp/scenes/UI/UISystem.gd` | 1 |

这些是逐文件文本三方预演，不是最终仓库合并树；保留工作树现有功能后仍需源码审读。natural 的 README/AGENTS 新增本轮说明可能与分支段落重叠，不能因此覆盖它们。title 的 Main/UISystem 冲突来自后续人工控制与音乐门控叠加。

## 依赖与遗漏风险

- perf 的连续门道链必须同时接通 Main、WorldView、CampusMapView、FieldResources 和 GameEngine。请求并非到达回执，必须保留 prepare 验证、真实碰撞到达、拒绝释放与必要的返回源房间协调。当前人工控制修复已分离局部几何与正式选择；合并时检查重复声明/函数与提交释放顺序。
- perf 的 UIInput.gd 被标题、模态/书页等调用；project.godot 输入映射、BookOpeningPage、opening_ui.csv/import、GameSettings 与 BookArchive/BookSettings/BookProjection3D 必须同步。只复制调用方会产生 preload 或输入行为缺口。
- perf 的 Theme.tres 与 build_ui_theme.py 配套新增 native SVG/import；应保留可再生来源，不用已有旧主题替代。缺失文件已在范围表逐条列出。
- title 的 shader、capture_start_reveal、living_floor/zone_* 场景、build_living_floor/build_chapter2_scenes/chapter2_art 源是一个发布闭包；当前工作树还叠加本轮生成器/地图工作，不能整文件替换回分支。
- 音乐提交已在 HEAD，Main/UISystem 的场景音乐、标题揭幕阻塞和 presentation token 需要共同保留。
- natural 的 10-compiler、20-core、dv_toolchain、SYNTAX 及 40/50 内容迁移存在编译器/源语法依赖；不单独回退任何文件。其 artifacts 包含大批历史证据，逐字节相同者不应重复覆盖，差异者保留双份并由主代理处理。
- 主工作树未跟踪素材、DeskProjection、shader、工具与证据不属于可删除噪声；盘点没有删除、清理或自动收纳它们。

## 验证边界

已完成：祖先关系、共同基准、路径范围、逐字节/反向补丁检查、已提交树 merge-tree、临时副本三方文本冲突盘点。未执行：构建、语法检查器、Godot 导入、探针、正式逻辑/实机会话、设计语义与效果验收。所有合并建议均须主代理在现有本轮授权与设计核对后实施。
