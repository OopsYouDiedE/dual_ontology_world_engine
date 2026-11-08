# 重构交接说明

本文件由整理工作产生，面向接手重构的执行者。以下所有决定已经做出，执行者无需再向业主提问。
规格仍以 `goal.md` 为准，工作规则仍以 `AGENTS.md` 为准。若本文件与旧文档冲突，以本文件为准。

## 1. 当前状态（已验证）

| 项目 | 结果 | 依据 |
| --- | --- | --- |
| 五日 AI 全流程 | `completed`，游戏时间 7200，`window_completed=true` | `.runtime/runs/cli-cfcc66b811edf9b7c5b76622/report.yaml` |
| 章节推进 | 1→2→3→4→5（第 6 天为观察结束标记） | 同上 |
| 行动统计 | 完成 2485，被阻止 1791，未预期失败 0 | 同上 |
| 关键节点评估 | 未评估（`completion_assessment: not-assessed`） | 同上，需由作者或人工补充 |
| `--verify-character-actions` | 16 PASS，0 FAIL | 门禁 |
| `--verify-rooms` | 23 PASS，0 FAIL | 门禁 |
| `--verify-drives` | 30 PASS，0 FAIL（已删除依赖固件的检查，见第 3 节） | 门禁外，可随时回门禁 |
| 仓库卫生检查 | 通过 | `python3 tools/ci/verify_repo_hygiene.py` |
| Godot 导入 | 删除 `.godot/` 后两轮导入均为 0 错误 | 见第 6 节 |
| 主场景 `Main.tscn` | 可实例化，脚本错误 0 | 无头诊断 |
| 36 个房间场景 | 运行时全部实例化成功 | 无头诊断 |
| 6 个世界地图场景 | 运行时全部实例化成功 | 无头诊断 |

说明：上述五日回执生成于删除 Lean 与 stage7 之前。之后的改动只涉及验证代码与机制图，没有改动剧情数据；`assets/data/scenarios/campaign.dv` 与仓库版本哈希一致。

## 2. 资产状态

已补齐（来源：用户提交的两个补充包，以及设计仓库 `OopsYouDiedE/dual_ontology_world_engine`，仅补项目中缺失的文件，从未覆盖已提交的版本）：

- KayKit 角色 GLB 2.0 FREE 六个文件，sha256 与 `assets/imported/kaykit/manifest.json` 一致。
- 世界道具 PNG（约 587 个）、世界地图场景 6 个及其依赖 14 个。
- UserModels `model_01–17.glb`。
- 音效 12 个（`assets/environment/Audio/Sfx/`）；音乐全部（含 `confrontation.ogg`）。
- Yozai、ZhaohuaTypeWriter、ZenKurenaido、NewTegomin、MaterialSymbols、NotoSansSC、NotoSerifSC 字体。
- UI 的 SVG 与纹理（`check-on/off.svg`、`paper_pixel.png`、`reference-paper.webp` 等）。
- `VegetationWind.gdshaderinc`，以及着色器包含路径的修正（统一为 `res://assets/shaders/common/`）。

占位（业主批准）：

- 立绘 48 张，位于 `assets/characters/{PixelCast,AnimeCast,DialogueCast}/`，每张标注 PLACEHOLDER。
- 清单：`assets/characters/PLACEHOLDERS.json`。替换真实立绘后，删除清单中对应条目。

明确不导入：

- `godot_csharp/`：旧工程，目标文档要求删除。
- `dv_core/`：旧架构的并行 Core/Session/CLI 副本与旧内容包，不是 stage7 的来源。
- `assets/data/content-revisions/` 的归档：可选，当前代码在目录缺失时直接跳过。

仍然缺失（仓库中也不存在）：

- `assets/data/stage*-character-session.dv`、`stage1-zone-b.dv`、`stage7*` 全部 DV 源与生成物。对应检查已删除。
- `tools/story/story-contract.json`：作者检查步骤已跳过（业主决定）。

## 3. 已做决定（执行者不要重新讨论）

1. **Lean 证明核验：从项目中移除。** 删除了 `CaseProofAuthoring` 类与 `--verify-case-proofs` 调度，删除了 `.runtime/lean`。`CaseProofChecker.cs`（纯 C# 可信核心）保留，它是游戏的一部分。
2. **stage 固件相关的 drives 检查：删除。** 删除了 39 个依赖固件的检查块，保留 30 个纯单元检查，全部通过。`drives` 仍在门禁外，因为业主决定"移出门禁"。当这 30 个检查被认为有意义时，可以把它放回门禁。
3. **RoomBundleChecks：删除整个类与调度。** 它只依赖缺失的 `stage1-zone-b.dv`。房间验证改由 `--verify-rooms` 承担，并已纳入门禁。
4. **`--verify-ontology`、`--verify-cognition`：删除调度。** 两者此前只输出 PASS 或 FAIL 桩，没有真实检查。在 `dual_ontology_state`、`perception_and_erosion` 机制中，`verify_cmd` 已标为 `none`，并保持排除。真实检查需要在重构这两个模块时一并实现，再加入门禁。
5. **作者检查步骤：跳过。** `signed_horn_reasoning` 的门禁状态为 `skipped_author_check`。它的 `verify_cmd` 已更新为可运行的 `author.py export` 命令。
6. **立绘：占位。** 见第 2 节。
7. **KayKit：使用 2.0 FREE。** 清单与文件一致，不需要再变更。
8. **Blender 版本：4.5.3。** 与 `mcp/game/environment.py` 中的 `BLENDER_VERSION` 一致。`docs/33` 中写的 4.2.1 已过期。
9. **卫生检查规则：** 跳过以 `/` 结尾的动态目录前缀，以及 `.runtime/` 下的运行时生成物，因为它们无法静态验证。被跳过的 SFX 与音乐缺口已在第 2 节列明。
10. **studio_web 纳入重构，数据契约如下：**
    - CLI 新增 `--export-studio <目录>`。它输出两个 JSON：
      - `proof_dag.json`：来自 `CaseTheory` 的推演节点与边。
      - `timeline.json`：来自 `actions.yaml` 的行动记录，字段为 `actor`、`start`、`end`、`room`、`label`。
    - 两个页面通过文件选择器读取上述 JSON，不内嵌任何数据。
    - 删除 `timeline_gantt.html` 中硬编码的 9 位角色作息。
    - 工作台 HTML 的存放位置为 `tools/studio_web/`，机制图的 `workbench_tool` 字段保持不变。
11. **测试基线：** `tests/golden/` 在仓库中不存在。重构开始前的第一步，由执行者在当前提交上生成基线：
    - 运行上文第 5 节的全部门禁命令，并保存输出到 `.runtime/baseline/`；
    - 运行一次五日全流程，保存 `report.yaml` 的摘要字段（状态、游戏时间、各类计数）到 `.runtime/baseline/five-day-summary.json`。
    - 此后每个阶段的改动都与该基线比较。
12. **Lean 不需要。** 在任何环境中都不必安装。
13. **`Main.gd` 的两个 C# 脚本变量改为无类型声明。** `session_bridge` 与 `presentation_content` 原先带有 `Node`、`RefCounted` 类型注解，导入时 GDScript 对 C# 脚本做静态检查会误报解析错误（类型尚未注册）。删除注解不改变行为。之后不要为这两个变量重新加回类型注解。

## 4. 环境重建（执行者必须完成，无需询问）

在仓库根目录执行。以下 `.runtime/` 都已被 `.gitignore` 忽略。

- **.NET 8.0.425 SDK：**
  ```bash
  micromamba create -y -p "$PWD/.runtime/dotnet-env" -c conda-forge dotnet=8.0.425
  ln -sfn "$PWD/.runtime/dotnet-env/lib/dotnet" .runtime/dotnet
  ```
  `environment.py` 通过 `.runtime/dotnet/dotnet` 或 PATH 中的 `dotnet` 查找 SDK。
- **Godot 4.7.2 .NET 版：** 下载 GitHub release `Godot_v4.7.2-stable_mono_linux_x86_64.zip`，解压到 `.runtime/godot/4.7.2/`，保证其中含有 `Godot_v4.7.2-stable_mono_linux.x86_64` 与 `GodotSharp/`。
- **MCP 依赖：**
  ```bash
  python3 -m venv .runtime/mcpenv
  .runtime/mcpenv/bin/pip install -r mcp/game/requirements.txt
  ```
- **Blender 脚本（kit）：** 使用 Python 3.11，执行 `pip install bpy==4.5.3`。
- **图形显示（截图与运行时验证需要）：**
  ```bash
  apt-get install -y xvfb mesa-vulkan-drivers vulkan-tools
  Xvfb :99 -screen 0 1600x900x24 &
  export DISPLAY=:99
  ```
- **资产还原：** 确认 `python3 mcp/game/asset_bundle.py` 输出 `"verified": true`（仅当 `assets/packs/` 存在时需要）。

## 5. 已验证的命令

```bash
# 解决方案编译（改动 C# 后必须执行）
dotnet build DuelView.sln --nologo

# 门禁
core/session_cli/bin/Debug/net8.0/DuelView.Cli --verify-character-actions
core/session_cli/bin/Debug/net8.0/DuelView.Cli --verify-rooms

# 仓库卫生
python3 tools/ci/verify_repo_hygiene.py

# Godot 导入（需要运行两次，见第 6 节）
.runtime/godot/4.7.2/Godot_v4.7.2-stable_mono_linux.x86_64 --headless --editor --quit

# 五日全 AI 流程（约 16 分钟，必须后台运行）
setsid nohup .runtime/mcpenv/bin/python mcp/game/run_auto.py \
  --backend cli --duration-seconds 2400 --seed 1 \
  --content-path assets/data/scenarios/campaign.dv \
  --receipt .runtime/runs/five-day-cli-auto.json > /tmp/auto.out 2>&1 < /dev/null &

# 剧情作者视图导出（check 步骤已跳过）
python3 tools/story/author.py export --content assets/data/scenarios/campaign.dv --output .runtime/story/author-view.json

# 焦点审计（独立可运行）
python3 tools/story/focus_audit.py --view .runtime/story/author-view.json --output .runtime/story/focus-audit.json
```

## 6. 工作约束与已知现象

- **命令时长：** 单条 shell 命令上限 300 秒。超过的任务必须用 `setsid nohup ... &` 后台运行，再分次轮询。
- **机器资源：** 参考环境为 1 个 CPU、约 4GB 内存。Lean 已移除，因此不再有内存瓶颈。
- **Godot 导入：** 修复后，删除 `.godot/` 之后的两轮导入都是 0 错误。导入检查仍建议跑两轮，以确认缓存重建正常。注意：`DuelView.sln` 不包含 Godot 宿主工程，宿主必须单独编译 `DualOntologyWorldReconstruct.csproj`。
- **诊断脚本：** 在无头模式下实例化房间后，退出时会出现 RID 泄漏报告。这是诊断脚本未释放实例造成的，不是内容错误。
- **`.runtime/runs/` 下的回执：** 只保留完整五日流程的回执。

## 7. 完成判据

重构被视为完成，当且仅当：

1. `dotnet build DuelView.sln` 0 错误。
2. 门禁中的 `--verify-character-actions` 与 `--verify-rooms` 全部 PASS，0 FAIL。
3. `verify_repo_hygiene.py` 通过。
4. Godot 导入两轮后 0 错误。
5. 五日全 AI 流程 `status=completed`，并且 `window_completed=true`。
6. 按 `goal.md` 的要求完成：
   - UI 符合 19 世纪案卷风格，简洁，不使用现代 UI 风格；
   - 3D 物件设计工具（Prop Workshop）；
   - 房间建造工具（类似建造模式，实时调整结构与内部物品）；
   - studio_web 按第 3 节第 10 条的契约接入，并删除硬编码数据。
7. 每个阶段结束时，与第 3 节第 11 条的基线对比，并在本文件末尾追加一行记录。

## 8. 进展记录

- 整理完成。立绘占位、KayKit 2.0、Lean 移除、stage 检查删除、门禁调整、studio_web 契约均已落实。
- 整理复核：发现 `Main.gd` 的类型注解导致导入解析错误，已修复（第 3 节第 13 条），两轮导入与主场景实例化均通过。

## 9. 本轮重构交付（第二轮）

已完成并验证：
- **Godot 第一天阻塞修复。** `CampaignRoomHost._set_room_ground_anchor` 原先只检查出生点本身，出生点落在家具顶面时宿主永久停滞。现改为环形搜索附近的真实地面，并优先选择能通往房门的点。
- **五日 Godot 宿主验证。** `tools/sandbox/Day1Harness.tscn`（无头，真实 Main 场景，AI 控制）：`VERDICT PASS`，游戏时间 7200 分钟，`window_completed=true`，墙钟 536 秒。注意：该次运行中侦探只访问了 R05 与 B-P 两个房间，时钟完成不等于剧情完整，剧情覆盖仍需人工核对。
- **Godot 后端启动。** 启动器现在把本地 Godot 包源写入用户级 NuGet 配置；`DV_GODOT_EXTRA_ARGS` 可附加引擎参数。
- **音效。** `tools/audio/synth_sfx.py` 程序合成 12 个音效（`assets/environment/Audio/Sfx/`）。此前项目中**没有任何调用点**，本次在门开合（`HingedDoor3D.begin_motion`）、书本翻页（`BookShellUI.open_section`）、HUD 按钮（`FieldHUD`）接入。
- **物件工坊。** `tools/building/prop_workshop.py`：JSON 配方 + 生成器参数校验 + 导出 GLB。`--list`、`--describe <生成器>`、`--all`。示例配方位于 `tools/building/prop_recipes/`，已导出 `study_armchair.glb`、`casebook_reading_table.glb`。
- **案卷风格样式。** `MaterialControls.gd` 改为羊皮纸底、墨色细线、直角与牛血红选中线；`FieldHUD` 统一调用它，进度条重新着色。
- **回归检查。** `DuelView.sln` 0 错误；`--verify-character-actions` 16/0；`--verify-rooms` 23/0；卫生检查通过；Godot 导入两轮 0 错误。
- **回执目录迁移。** `artifacts/five-day/` 是运行产物，已删除，`campaign.dv` 的 `receipt_directory` 改为 `res://.runtime/five-day/godot`。

未完成（如实说明）：
- **标题界面与书本界面布局未重做。** 标题仍是沿用的 3D 书案，书本封面为纯色块，未达到预期的视觉升级。
- **门的悬停高亮、人物交互反馈动效未实现。**
- **剧情覆盖未逐条核对。** 五日流程的时钟与状态可以完成，但关键节点仍为未评估。
- **HUD 案卷样式尚未截图确认。** 只确认了代码路径与编译，需进入场地后截图。
- **门禁外的 `drives`、`ontology`、`cognition`** 维持排除状态（见第 3 节）。

## 10. 复现命令（第二轮新增）

```bash
# 第一天 / 五日 Godot 宿主验证（无头，AI 控制）
.runtime/godot/4.7.2/Godot_v4.7.2-stable_mono_linux.x86_64 --headless --path . \
  res://tools/sandbox/Day1Harness.tscn -- --dv-controller=ai --dv-seed=1 --dv-viewer=detective \
  --day1-target-minutes=7200 --day1-wall-limit=3000
# 结果：.runtime/day1/result.json；进度：.runtime/day1/progress.log

# 音效重新合成
python3 tools/audio/synth_sfx.py [name ...]

# 物件工坊
/home/claude/py311/bin/python tools/building/prop_workshop.py --list
/home/claude/py311/bin/python tools/building/prop_workshop.py --describe chair
/home/claude/py311/bin/python tools/building/prop_workshop.py --all
```
