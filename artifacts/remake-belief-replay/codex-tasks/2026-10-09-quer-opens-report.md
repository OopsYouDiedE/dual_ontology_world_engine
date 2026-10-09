# 2026-10-09 正午雀儿撬门任务报告

五项规格已完成源码接入。最终 `compile_story.py` 成功重新生成全部 **7 份产物**；Godot C# 解决方案构建 **0 错误、1 条 NU1900 警告**。本结论只覆盖源码与编译交付，不代表游戏运行验收通过。

开始时分支为 `codex/ch1-quer-opens`，HEAD 为 `7c2f020`，工作区干净。先读取本任务文件与 `2026-10-09-flow-and-menus.md`，按其“约束”执行：没有读取 `docs/`，没有运行 Godot、检查程序、自动回归或盲玩，没有提交或推送。重新生成使用当前工作树的全部剧本源，包含主代理已提交的最新台词修改；未用上一轮生成物还原台词。

## 规格实现

| 规格 | 实现位置与行为 |
| --- | --- |
| 1. 阶段时刻触发 | `tools/story_chapter1.py/read_stage` 支持 `时刻 <分钟>：流程 <名>`，校验分钟范围、重复时刻及声明格式，按分钟排序。`compile_story.py` 校验流程引用。`Script/Chapter1Clock.cs/TakeStageTimeFlow` 按当前阶段和游戏分钟取到期流程，用 `stage_times` 进度保证只执行一次。`Chapter1Session.cs/Tick` 先结算远处生态，再在现场空闲时调度；行动跨过 720、对白结束或返回调查阶段后也能处理到期项。流程仍自行判断甲区是否出门与门是否已开，不在 C# 复制该判断。 |
| 2. 调查阶段开门、门洞提示与 E1 | `Chapter1SessionFlows.cs/Command` 的 `door_open` 可在调查阶段执行，首次执行记录开门分钟；快照提供 `isolation_open`，`AwakeningMap.gd` 同步门洞与遮罩，沿用第一章通行边界，不增加起居厅地图。门洞坐标和邻近半径来自 `.story`，现场通过 context 报告邻近，进入范围时显示 `文案与守则.story` 的 `旁白·门外暂留`，停在范围内不逐帧重复。E1 不增加门开禁用条件，试验、完成记录与内闩触物闪回仍沿用原流程；给 `exp_E1` 增加开门分支，避免继续显示关闭状态的叙述，原有四句台词及编号保留。 |
| 3. 两位演员入场 | 编译器接受并校验 `enter:<人>:<地点>` 的人名、立绘、房间与入场锚点。`Chapter1SessionFlows.cs/EnterActor` 通过 `stage.spawn` 等待真实现场回执，再登记 `entered_actors`。共享 `WorldView.gd` 创建并定位演员。人名、热点与门厅入场坐标补入 `.story`；立绘键取现有 `dialogue_cast`。使用已有 `PixelCast/thief.tres`、`PixelCast/poet.tres` 及其 PNG 行走图集，**无需采用只有立绘的替代方案**。两人在门厅站定，可复用现有转身、近距离召集与让路机制；被演出占用时也进入 `pinned`，避免让路动作打断召集；没有添加新美术资产。 |
| 4. 交谈与四人案件隔离 | 动态来访者进入现场 `people` 与交谈热点；未入场时热点关闭。交谈复用作者 `流程 交谈`，绑定 `quer` / `lucian`，播放 `talk_quer` / `talk_lucian`。`Party`、`Companions`、`Person` 与 `Names.PersonKeys` 保持原来的案件成员范围；比手印、邀请比手、请人辨认入口显式排除来访者，声路、回忆与重演仍使用四人集合。`MorningEcology.cs/LocateFieldResident` 将同一个现实人物绑定到现场位置并暂停其远处日常，避免现场与低精度推演同时移动该人；没有将两人并入乙区居民、寝区出门统计或重演变量。 |
| 5. 提前开门终局与续章 | 当前作者的 `终局` 流程保持原有 `quer_opened` 分支，通过既有流程解释器播放 `s10_open_early`、保存、显示章末并停止，跳过通常路线的抬闩场面。`clock.door_open_minutes` 只记首次 `door_open`，终局对白不会覆盖它。第二章 `Chapter2Access.cs/InitializeAccess` 使用该分钟与作者起点的较大值，旧会话无此字段时兼容 `game_minutes`。提前开门快照不显示通常开门路线的痕迹精灵。 |

补充一致性调整：`流程 门外的人` 的退出条件加入 `door_open`，避免入场后再次走进门厅仍播放门外场面。`check/flow_check.gd` 的场面清单和终局断言兼容提前开门、通常开门两条作者路线，引用稳定台词 ID；本轮没有运行该检查。

## 修改文件

以下路径除报告与日志外均相对 `godot_csharp/`：

- 编译器：`tools/story_chapter1.py`、`tools/compile_story.py`。
- 第一章宿主：`scenes/Awakening/Script/Chapter1Clock.cs`、`Chapter1Session.cs`、`Chapter1SessionFlows.cs`；`scenes/Awakening/Night/MorningEcology.cs`；`scenes/Awakening/AwakeningMap.gd`。
- 共享现场与续章：`scenes/Shared/Field/WorldView.gd`、`scenes/Chapter2/Prototype/Chapter2Access.cs`。
- 剧本补充：`scenes/Awakening/Script/story/第一章/热点与布局.story`、`提示与站位.story`、`文案与守则.story`、`阶段与流程.story`、`场面/02_试验与第一次反转.story`。除必要的 E1 开门分支、指定门洞提示和机械绑定外，没有重写作者台词；`12_雀儿撬门.story` 与原有终局台词未手改。
- 现有检查兼容：`scenes/Awakening/check/flow_check.gd`。
- 架构说明同步：`AGENTS.md`，只记录时刻调度、动态演员注册、位置同步与开门时间传递。
- 全部生成产物：第一章 `Chapter1CompiledDialogue.cs`、`generated_awakening_content.json`、`generated_awakening_dialogue.json`、`generated_awakening_layout.json`、`generated_chapter_01_world.json`、`generated_lines_zh.csv`；第二章 `generated_chapter_02.json`。全部由编译器生成，未手改。产物差异也包含此前作者修改而尚未更新到生成物的台词。

没有修改独立 Core 源码或项目结构。

## 编译证据

命令均在仓库根目录执行，还原和构建采用指定的串行参数。

| 命令 | 最终结果 | 证据 |
| --- | --- | --- |
| `python godot_csharp/tools/compile_story.py` | 退出码 0；`generated 7 outputs from chapter_01.story` | [剧本编译日志](2026-10-09-quer-opens-story-compile.log) |
| `dotnet restore godot_csharp/DualOntologyWorldEngine.sln --disable-parallel -m:1 -p:BuildInParallel=false --nologo` | 退出码 0，依赖还原成功 | [还原日志](2026-10-09-quer-opens-restore.log) |
| `dotnet build godot_csharp/DualOntologyWorldEngine.sln --no-restore --nologo -m:1 -p:BuildInParallel=false` | 退出码 0；0 错误、1 警告。构建内的生成物检查确认 7 份产物一致 | [最终构建日志](2026-10-09-quer-opens-build.log) |
| `git diff --check` | 退出码 0，无空白错误 | 本轮工作区差异 |

NU1900 来自包漏洞数据服务无法连接，日志显示被拒绝的代理地址为 `127.0.0.1:9`；没有阻止还原或构建。本轮没有构建错误，也没有 AppData 写入失败。

## 验收范围与未验证项

- **构建**：通过上述指定的编译交付要求。
- **机制**：五项已接入，结论依据源码与生成数据；没有执行运行时验证。
- **人物行为合理性与文学审评**：未验证；按约束未读设计文档，未对作者台词作独立审评。
- **游玩体验**：未验证。
- **一级、二级、三级验收**：均未执行；没有生成全程记录、截图或盲玩证据。
- **Godot 专属未覆盖项**：GDScript 未由 Godot 加载解析，未重新导入资源；演员入场、立绘与行走渲染、门洞显示、布局遮挡、碰撞、交谈输入和真实续章切换均未实机检查。现有检查源码的兼容调整不代表断言运行通过。

没有留作后续实现的规格项；运行表现仍需在后续获准运行游戏的轮次验证。
