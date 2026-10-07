# 交接清单（2026-10-07）

工作区：`C:\Users\Administrator\dual_ontology_world_engine-remake`，分支 `claude/remake-belief-replay`。设计以 `docs/design-standard.md` 为唯一准则；本文件只列现状与待办，设计条文以文档为准。

## 1. 本轮已完成（见设计文档出处）

- 第二章引擎修复与侦探破案：第三部分 §17（实施记录，含查出的三处“重演对不上现实”的原因）。
- 恶魔不自曝：第一部分 §2“不自曝”；台词改写见第一章 `04_低语与回想`、`08_终局`、`01_开场`，第二章 `06_场面`。
- 无法背负的过去（两层过去）：第一部分 §4。
- 源质、出资方（伯爵／皇家政策／皇家科学院／皇家海军／神秘学公会）与博弈期间岛被隐藏：第一部分 §1.1a。
- 侦探来历改为未知（撤销复制人设定）：第七部分 §1。
- 第二章五日结构、日常篇（课开放不强制、团建由欲望推出且跨寝区）、侦探的推理位：第三部分 §16。
- 多周目与首次体验、先出来的组不开别组的门、非常规动作三层、学园尺度与野外设施与割据：总纲 §五.13—16。

- 两章界面与主代码统一（2026-10-07，分支 `claude/unified-chapter-ui`）：设计第四部分 §2。章节会话实现 `IChapterSession`（第一章 `Awakening/Script/Chapter1Session*.cs`，原宿主的流程编排迁入 C#）；`Shared/ChapterEngine.cs` 一个桥；`Shared/ChapterHost.gd` 只转发；`Shared/UI/ChapterUI.tscn` 与按模块分目录的 `@tool` 组件；`Shared/Field/ChapterField.gd` 加地图适配器。旧宿主、旧 Engine 桥与两章各自的 UI 目录已删除。检查：`dotnet run --project godot_csharp/scenes/Awakening/check/SessionCheck`（无画面协议）、`res://scenes/Shared/check/chapter1_smoke.gd`、`res://scenes/Chapter2/check/check_flow.gd`、`res://scenes/Shared/check/ui_modules_check.gd`。未做：新宿主的独立盲玩。迁移到新宿主但未跑通（用户要求收尾时中止）：`Awakening/check/flow_check.gd`（已按新宿主重写，配套 `check/chapter1_fixture.gd`，尚未完整跑过）、`state_gallery.gd`（出 11 张图后退出时崩溃）；仍是旧宿主写法、需迁移：`final_content_check.gd`、`final_shared_runtime_check.gd`、`hand_outline_check.gd`、`cabinet_fog_current_check.gd`、`Chapter2/Game/check_gallery.gd`、`Shared/check/check_room_fog.gd`、`Shared/check/ui_prefab_check.gd`、`Chapter2/Field/{capture_maps,capture_views,check_transitions}.gd`（后三者仍引用旧 `Chapter2Field.gd`/`FieldHUD`，迁完可删）。盲玩会话 `playtest_session.gd` 已改到新入口，`--chapter=1|2`。

## 2. 待办（按优先级）

### 2.1 第二章：先收尾本轮修复
1. **复核“每阵开始清掉遗留打算”**（`godot_csharp/scenes/Chapter2/Prototype/GameLoop.cs` 的 `RunSlot` 中 `CancelAll()`）。它让重演对上现实、侦探能破案，但原型检查由 4 项失败回到 6 项：封闭不启不可达、同行作证失败、“公告板广撒”改写不了结局。怀疑清掉了合法的跨阵任务；可能应只清剧本控制期间漏过阵末清理的那一类。复核后跑 `dotnet run -c Release --project godot_csharp/scenes/Chapter2/Prototype -- check`。
2. **诊断输出**：`check/DetectiveSimulation/ContinuationSimulation.cs` 的“重演诊断”（只在吻合 0 组时输出）与 `GameLoop.cs` 的 `NightStartAssessment` 可保留为诊断或删去；命令行新增调试模式 `events`、`minds`、`infer`（写进 README 或删去）。
3. 跑全套：`python godot_csharp/tools/check/chaos_simulation.py --seeds 1000 --jobs 8`、`python godot_csharp/tools/check/perturb_simulation.py`、Godot 宿主编译；第一章各检查。提交前已复跑：第一章与 Core 各检查全部通过，混沌测试 40 个种子零失败；一千种子与完整微调扫描未跑。

### 2.2 第二章：引擎与平衡
4. 结局可达：委员会保管、夜间私占、封闭不启。私占：沃德每夜守门，霍铎占好一站后无事可做，被请回即答应——需要“守住一站直到三站凑齐”的模型。
5. Core `SocialSystem.Evaluate`：答应请求只看合作倾向加对来者的信任，不计请求让听者放弃什么。
6. 规矩张贴仍有“作者定：未写理由”（茉拉、维罗妮卡、沈鼎），第三期“由立规者自己选择”未完成。
7. 小问题：温室在设计中已移出本章，人物仍每天去修；贝尔特朗始业日“报告昨夜夜巡”；十二名住民第一章灵魂里的其余规则未逐项排查。
8. Godot 界面缺“张贴”“问铜口”；新加的名录库游说菜单未在界面实测。
9. 续篇侦探白天仍是公开探索策略，没有游说模型。

### 2.3 第二章：五日改制（第三部分 §16，已裁定未动工）
10. 日程：story `续篇/00_时间与文案.story` 的首日/案发日/末日；莫兰发烧日与退烧；对质改到搜查日夜里；私占在对质之后那一夜。
11. 日常篇内容：恢复第十二至十四课（`Demon.cs` 的 `Lessons` 现为死代码）；团建按 §16 表由欲望经方法库推出；自由时间相处露出各人“过去”的端倪；规矩在日常中由各人自己贴出。
12. 采石场、观测站、户外仪器阵三处野外地点（总纲 §五.16），含地图与交互点。
13. 策略机器人与检查再同步一次（`Bots.cs`、`Checks.cs`）。

### 2.4 内容（适合交给 Sonnet 按规格写；子代理不得读设计文档，规格须写进提示）
14. 十六人的两层过去（入学前的隐事、封存的那一段）；侦探留白。
15. 日常篇每人的相处场面、团建场面。
16. 其余三区开局骨架：相遇（各区一处“身体记得”的细节）、广播、核验报偿、始业之问；开场独白已有（`第一章/场面/10_开场独白.story`）。

### 2.5 第一章与长线
17. 生活翼房间几何与演出点坐标仍在 story（地图生成器只输出寝室）。
18. 三个寝区案件的痕迹由选项组合推出，未逐人重演当夜。
19. 只有侦探可选；无 Godot 选人界面；第二章未读取寝区推论。
20. 第一章结局卡“留在恶魔那里的钩”待用户答复。
21. `SharedRuntimeCheck` 在本轮之前就崩溃（第 12 项后），原因未查。
22. 新机制均未做独立盲玩（AGENTS.md：新会话、只给启动方式与通用输入协议）。
23. 待用户确认：整部游戏时长建议（第七部分 §2）；出资各方的目的；伯爵的现状。

## 3. 运行备忘

- 全场模拟：`dotnet run -c Release --project godot_csharp/scenes/Awakening/check/DetectiveSimulation -- --enabled <输出.txt>`；参数 `--duration`、`--persona 键=值`、`--mind-seed`、`--case-seed`、`--play-as`、`--chaos`（见 README）。
- 第二章原型：`dotnet run -c Release --project godot_csharp/scenes/Chapter2/Prototype -- check | bots [-v] | trace N | events N | minds N | infer N`。
- 本机另有 `C:\nmmo` 项目的锦标赛进程会占满 CPU（本轮经用户同意停过一次）；构建与长跑前先看一眼。
- Bash 的 heredoc 会吃掉替换串里的转义；补丁脚本用 Write 写成文件再运行；C# 文件按原换行（CRLF/LF）保留。
