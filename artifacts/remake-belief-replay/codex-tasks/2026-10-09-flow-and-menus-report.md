# 2026-10-09 流程与菜单任务交付报告

本轮按 `2026-10-09-flow-and-menus.md` 完成编译器和宿主实现。剧本重新生成通过，Godot C# 解决方案构建为 **0 错误**。实现结论来自源码核对与构建，不代表游戏运行验收通过。

遵守本轮约束：没有读取 `docs/`，没有运行 Godot、检查程序、自动回归或盲玩，没有提交或推送。保留原有任务文件和工作区。没有改写原有剧情、对白正文；`.story` 修改仅涉及地点声明顺序、地点与物件绑定，以及从 C# 迁出的交互文案。

## 一、编译器规格

本表中的工具均在 `godot_csharp/tools/`。

| 规格 | 状态与实现位置 |
| --- | --- |
| 1. 阶段进入房间执行流程 | 已实现。`story_chapter1.py/read_stage` 编译 `进入 …：流程 …`；`compile_story.py/validate_rules` 校验流程引用。 |
| 2. `已出门` 条件 | 已实现。`story_chapter1.py`、`compile_story.py` 注册单参数查询；第一章 `Chapter1Flows.cs` 查询实际寝区外闩状态。 |
| 3. 最早出门 | 已实现。`story_chapter1.py/read_dorm_case` 编译日期与分钟，校验当日分钟范围；生成的三个案件分别保留第 2 日 540、第 3 日 360、第 3 日 690。 |
| 4. 续篇地点、流程、选择和时刻表 | 已实现。`story_script.py` 生成 `design.flows`、`design.schedule` 与夜间入口；复用 `parse_flow`，支持嵌套选择、条件与继续流程。验证本规格列出的全部指令、流程和场面引用，拒绝宿主不支持的条件。`Living` 已进入地点绑定。 |
| 5. 场面省略日期 | 已支持。沿用原有可选日期编译方式；`Chapter2Narrative.cs` 使用可空日期，未声明日期的事件可在任意日期匹配。`night_hint` 已包含在本轮生成物中。 |
| 6. 带参数的调停动作 | 已实现。`world_script.py` 将 `mediate` 定义为一个字符串参数；`Chapter2Narrative.cs` 校验三种合法写法并传给宿主。 |
| 7. 新场面事件 | 已接入。新事件保持作者声明，进入 `generated_chapter_02.json`；时段流程按场面 ID 播放，调停按各人的结果分派 accept/reject 事件。 |
| 8. 删除旧场面与开场指令引用 | 已同步。生成物来自当前源；`StoryPacketCheck` 与 `flow_check.gd` 不再启动已删场面。报偿行号在 `s3_broadcast` 中核对；开场不再调用公告框展示和旧逐人演出。 |

补充：生态编译现在补入晨间、出区日常所引用的续篇地点声明。因此第一章低精度推演具有 `room.Living`，不会因为出区步骤引用起居厅而缺少地点实体；这不增加第一章可去的地图。

## 二、第一章规格

下表文件位于 `godot_csharp/scenes/Awakening/`。

| 规格 | 状态与实现位置 |
| --- | --- |
| 1. 读不走钟 | 已实现。编译器接受 0 分钟；`Script/Chapter1Clock.cs` 的普通交谈、协助场面未单列时长时使用对应行动时长，避免再次套用默认场面时长。证物检视沿用行动时长 0，未增加耗时提示。显式声明的剧情场面仍按作者数据结算。 |
| 2. 人物邀约 | 已实现。`Chapter1Interaction.cs` 删除卡片协助选项；`Chapter1SessionFlows.cs/Talk` 消费并清除 `invite_help_*`，启动并行 `InviteHelp`。同伴走到证物附近后执行 `请人看看`，绑定物与本次交谈的人；走位期间不占用侦探控制。重复邀约保护与会话重置一并处理。 |
| 3. 主动读公告框 | 已实现。`Chapter1SessionFlows.cs/OpenBoard` 才写 `board_read` 和侦探的规则认知。相关开场断言改为未读，主动阅读后才断言已读。 |
| 4. 门外的人 | 已实现。`Chapter1Session.cs/Triggers` 按实际进入房间触发流程；离开房间后可再次触发，流程自己的旗控制只演一次。`door_voices` 使用作者的无人物旁白。 |
| 5. 分批抬闩与甲区低精度地点 | 已实现。`Night/MorningEcology.cs` 分开记录“答毕”和“外闩已开”；仅答毕且到最早时刻才开放。甲区原有低阈值与 `Living` 出区步骤来自剧本，生态编译补齐对应地点。 |
| 6. 终局立绘 | 已实现对白立绘。`compile_story.py` 从人物档案的 sprite 属性生成 `dialogue_cast`，包含 `thief`、`poet`。**未新增门外站位精灵**：第一章现场仍只管理既有四人，采用规格允许的对白立绘方案；未实机确认显示效果。 |

## 三、第二章规格

下表文件默认位于 `godot_csharp/scenes/Chapter2/Prototype/`。

| 规格 | 状态与实现位置 |
| --- | --- |
| 1. 流程执行器 | 已实现。新增 `Chapter2SessionFlows.cs`，执行流程栈、条件、选择和所有指定指令，以 `(Day, Slot)` 集合防止时段流程重复。`Chapter2InteractionSession.cs` 删除写死的开场排队及配给、夜间提示、清晨、傍晚、议事特判。对白与选择沿用正式回执。 |
| 2. 首日实际时间 | 已实现。`Chapter2Access.cs/InitializeAccess` 取第一章共享进度中的实际分钟与表中 720 起点的较大值；`AdvanceCalendar` 始终取较大值，避免后续时段倒拨分钟。 |
| 3. 三条连廊 | 已实现。`Chapter2Access.cs` 初始关闭三廊；流程更新权限、实际通路连接和每人的内部通路投影，并提供决策禁行规则。`Host.cs/HostMoveDet` 拒绝不允许的地点。`Chapter2SessionView.cs` 输出所有地图权限；`Field/CampusMapView.gd`、共享 `WorldView.gd` 拦截地图点击、出口与室外边界换图，使用作者 `corridor_closed` 文案。正式场面换图由 C# 先校验，避免旧快照误拦刚打开的廊。 |
| 4. 分批出门与起居厅 | 已实现。`Model.cs` 追加 `Living`，`Chapter2SessionView.cs` 映射 `living_hall`；`Script/Chapter2Script.cs` 将已出门居民放在起居厅，未出门居民保留在自己的寝区。`GameLoop.cs/Available` 阻止未出门人物参与日程，快照也不展示他们。`dorm_out` 释放该区居民后恢复日程。起居厅交互点与独立公告板绑定同步。 |
| 5. 备餐间晚饭与 99 份配给 | 已实现。新增 `Cmd.EatPantry`；`GameLoop.cs` 用备餐间存粮更新饱食状态，不支出粮仓库存。`GameOptions.StartRations` 为 99；配给初始化、争执与领粮入口从 `RationFirstDay`（第 3 日）启用。 |
| 6. 每夜就地睡与床标记 | 已实现。夜间 T 和耗时动作启动作者 `夜里·T` 流程；取消不结算，睡眠在当前地点完成并推进到清晨。`Player.cs` 不再让侦探先走回寝区。`tools/chapter2_art/rooms.py` 添加床标记源，通过 `build_chapter2_scenes.py --sites-only zone_yi` 生成真实 `Sites/bed`；删除 `EXTRA_SITES` 中的临时床。 |
| 7. 按接受倾向平铺人物菜单 | 已实现。新增 `Chapter2SocialMenu.cs`、`Chapter2SocialActions.cs`；固定询问与取消，其他动作共用 Core 社交公式，再加价值、欲望与当下习惯相合分。仅保留正分，按分数取前 5 项，以稳定 ID 排同分。规矩先筛当前处境；标签读取 `.story`，删除原请求、规矩和同行子菜单。 |
| 8. 拒绝、取消不占时段 | 已实现。`Commit` 在 `RunSlot` 前做社交判定；拒绝只输出回应，取消与返回不调用时段结算。已接受请求保留预判与请求，避免重复提出。讲规矩使用同一社交分并写入接受认知；实际请求投影记录 `RequestCompatibility`，使后续 NPC 自主决策复用相合分。 |
| 9. 证物卡取消同行 | 已实现。`Chapter2InteractionSession.cs/ShowCard` 仅提供返回现场；同行从人物菜单发起。 |
| 10. 三种配给调停 | 已实现。`HostMediate` 对茉拉、阮清越分别调用 `SocialSystem`，按病人优先与记录的相合计算各自结果；仅两人均 Accept 才设为成功。结果与信任通过 `Observation` 写入各人的内部世界，分别播放作者 accept/reject 场面。删除自动代玩家说出方案的 C# 路径。即时调停也写入有序重演记录。 |
| 11. 第 3 日首课、自由与配给；机器人同步 | 已实现。安排由当前作者时刻表执行，首课、自由场面和配给不再由首日特判驱动。`RationFirstDay` 明确为 `FirstDay + 1`；`Bots.cs` 将调停及交钥匙的相关日程移到第二日，调停带明确参数。 |

权限变更、居民出门和即时调停加入 `RoomPresentation.cs/FreeEvents` 并在重建时按顺序恢复；`NightSnapshot.cs` 保存连廊与寝区释放集合，避免重演恢复为初始关闭状态。

送口粮请求从接收者的内部世界绑定已知饥饿对象；不知道任何饥饿对象时不提供该项。帮忙和请求的目的地也先检查连廊权限，避免接受后才发现动作无法到达。

## 四、修改文件

- Core：`csharp/SocialSystem.cs`、`csharp/AGENTS.md`。
- 编译与地图工具：`godot_csharp/tools/compile_story.py`、`story_chapter1.py`、`story_script.py`、`world_script.py`、`build_chapter2_scenes.py`、`chapter2_art/rooms.py`。
- 第一章：`Night/MorningEcology.cs`；`Script/Chapter1Clock.cs`、`Chapter1Flows.cs`、`Chapter1Interaction.cs`、`Chapter1Script.cs`、`Chapter1Session.cs`、`Chapter1SessionFlows.cs`。
- 第二章新增：`Prototype/Chapter2Access.cs`、`Chapter2SessionFlows.cs`、`Chapter2SocialActions.cs`、`Chapter2SocialMenu.cs`。
- 第二章既有：`Prototype/Bots.cs`、`Chapter2Bindings.cs`、`Chapter2InteractionContent.cs`、`Chapter2InteractionSession.cs`、`Chapter2Narrative.cs`、`Chapter2SessionView.cs`、`GameLoop.cs`、`Host.cs`、`Model.cs`、`NightSnapshot.cs`、`Player.cs`、`RoomPresentation.cs`；`Script/Chapter2Script.cs`；`Field/CampusMapView.gd`。
- 共享现场：`godot_csharp/scenes/Shared/Field/WorldView.gd`。架构与管线说明：`godot_csharp/AGENTS.md`。
- 剧本机械绑定与文案迁移：`Script/story/续篇/00_时间与文案.story`、`01_物件.story`、`02_交互点.story`、`07_家具与文本.story`、`08_流程.story`。起居厅声明提前以满足声明后引用；交互点使用起居厅实体；菜单、社交回复、讲规矩反馈与笔记模板由 C# 迁入剧本。原有场面对白和流程步骤正文未改。
- 检查源码：`Awakening/check/StoryPacketCheck/Program.cs`、`FormalScriptCheck/Program.cs`、`SharedRuntimeCheck/Program.cs`、`flow_check.gd`、`final_shared_runtime_check.gd`、`chapter1_fixture.gd`。更新合并后的广播、人物邀约、主动读板和起居厅断言。
- 生成文件：`Chapter1CompiledDialogue.cs`、`generated_awakening_content.json`、`generated_awakening_dialogue.json`、`generated_awakening_layout.json`、`generated_chapter_01_world.json`、`generated_lines_zh.csv`、第二章 `generated_chapter_02.json`；地图 `Maps/zone_yi.tscn` 与 `ShareAsset/Internal/Props/Chapter2/manifest.json`。全部通过相应工具生成。

## 五、构建与未验证项

| 命令/工作 | 结果 | 本目录证据 |
| --- | --- | --- |
| `python godot_csharp/tools/compile_story.py` | 通过；重新生成 7 份产物 | `2026-10-09-story-compile.log` |
| `dotnet build godot_csharp/DualOntologyWorldEngine.sln --nologo -m:1 -p:BuildInParallel=false` | 通过；0 错误、2 条 NU1900 警告 | `2026-10-09-build.log` |
| 构建 `StoryPacketCheck.csproj`，相同单进程参数 | 通过；0 错误、0 警告；未执行 | `2026-10-09-story-packet-build.log` |
| 构建 `FormalScriptCheck.csproj`，相同单进程参数 | 通过；0 错误、0 警告；未执行 | `2026-10-09-formal-script-build.log` |
| 构建 `SharedRuntimeCheck.csproj`，相同单进程参数 | 通过；0 错误、0 警告；未执行 | `2026-10-09-shared-runtime-build.log` |
| 地图热点生成与 `git diff --check` | 已完成；最终无空白差异错误 | 地图、manifest 和工作区差异 |

默认并行还原最初失败，切换为 `--disable-parallel -m:1 -p:BuildInParallel=false` 后还原成功，后续解决方案构建采用单进程参数。保留 `build-restore-diagnostic.log`。一次清理未使用字段时遗留引用造成 CS0103，已修复；保留 `2026-10-09-build-failed-compilation.log`，最终成功日志独立保存。编译器收紧条件和文案契约时发现的无参数字段、遗漏文案键也已修正，最终生成通过。

NU1900 的原因是无法访问 `https://api.nuget.org/v3/index.json` 的漏洞数据服务；未阻止缓存依赖还原和构建。本轮未遇到最终阻塞交付的 AppData 写入失败。

**未验证**：没有执行一级剧情与引擎验收、二级 Godot UI 验收或三级盲玩；没有验证实际走位、碰撞、地图边界输入、立绘渲染、菜单体验、真实时长或文学审评。GDScript 仅做源码核对，未由 Godot 加载解析；资源未重新导入。三个 C# 检查项目的构建通过不等于其断言运行通过。

**实现范围的明确限制**：终局未摆放新增的两位人物站位精灵，采用任务允许的对白立绘；除此之外没有将规格项留作后续实现。构建通过只能确认编译交付，运行效果需在获准测试的后续轮次验收。
