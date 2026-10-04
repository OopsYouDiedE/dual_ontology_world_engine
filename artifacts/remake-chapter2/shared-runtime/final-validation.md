# 本轮独立定向验证

日期：2026-10-04。验证者为全新独立上下文 Agent；未读取项目设计文件或历史测试报告，未修改实现源码。本报告是自动回归，包含直接调用、状态注入与对白自动推进，不是盲玩，也不以其运行时长衡量玩家体验。

## 结果

本轮整体未通过。必要宿主构建成功；第一章原流程的 13 项断言、原文迁移 11 项断言通过，但实际执行发现三处实现故障。主代理随后修复了这些故障，本验证者未重新执行受影响范围，因此修复结果尚待下一全新独立 Agent 复核。

| 验证范围 | 本轮运行结果 |
| --- | --- |
| `dotnet build godot_csharp/DualOntologyWorldEngine.csproj --nologo` | 成功，0 错误、25 警告，主要为 nullable；宿主引用实际输出 Core、ChapterRuntime、Chapter2Prototype |
| 外置 `ExampleWorld.cs`、`ExampleAwakening.cs` | 均通过实际 `compile_world_script.ps1` 编译，产生独立 DLL；不能据编译结果断言加载成功 |
| 两章媒体验证外置脚本 | `FinalAwakeningHostScript.cs`、`FinalHostScript.source` 均通过同一工具编译 |
| 第一章实际默认场景加载与原流程 | 13 项断言通过，但启动存在 SCRIPT ERROR，因此该轮不算无错误通过 |
| 原文迁移 | 11 项通过，未发现文字丢失或改写 |
| 原 `SharedRuntimeCheck` 基础断言 | 前 12 项通过，之后因自主动作与脚本动作冲突中止；后续旧断言及追加断言未执行 |
| 两章实际外置 DLL 加载、文字→图片→文字、房间触发 choice | 第一章加载外置 DLL 时依赖解析失败；后续媒体及第二章检查未形成有效结论 |

第一章 13 项包括：默认开场对白→roam；嵌套 Dictionary/Array/Bool/Int Variant 往返；SetDetail 持久化；数字回忆标记键；进入活动厅触发 meeting；姓名和公告确认→investigate；实验 E2；协助 E4；霍铎触物回忆与数字标记；不在场自主取物及钩/痕迹且不泄漏回忆；四步答辩→门开结尾。

原文 11 项包括：PLACES、EVIDENCE、PERSON_NAMES、ACTOR_NODES、ROOM_NAMES、VARIABLES、STATEMENTS 与临时迁移前 `tmp/original-awakening-content.gd` 精确序列化相等；WHISPERS、HELP_NOTES 与 git HEAD 的原字典精确相等；L6/L7 标题、图片标识和逐行 facts 在 git HEAD 原宿主源码中实际存在。迁移前宿主曾有未提交修改且未保存完整副本，所以这里不声称已核验整个宿主与原未提交版本完全一致。SITES 属宿主几何入口，未被当成迁移至 JSON 的内容。

## 实际故障与已提交给主代理的修复

1. 第一章节点初始化：`RoomDoor._ready → refresh_access → access_allowed → can_access_dorm → @stage_getter`，`Awakening.gd:86` 对 Nil engine 调用 `Progress`。每次新场景发生三次。主代理将 engine 提前创建为成员实例，`_ready` 只命名和挂载；本轮未重新验证此改动。
2. 新动态人物与脚本动作：`ExampleWorld.CreateGame()` 后调用 `Story.Pump()`，Core 抛出「该身体已有未完成动作」。堆栈经过 `SimulationSession.Decide/Wake → CTB.RunUntil → StoryRuntime.Pump`，说明已排队自主唤醒与后续脚本占用身体冲突。主代理使 Wake callback 在 Decide 前再次检查 `!IsActing(actor)`；本轮未重新验证此改动。
3. Godot 外置 DLL：实际 `Awakening.run_script()` 对本轮编译的媒体脚本加载时，`ChapterScript.Load → GetExportedTypes` 抛 `FileNotFoundException: ChapterRuntime, Version=1.0.0.0`，继而 NextScript 对空 runtime 抛错。主代理将两种 loader 改为复用契约程序集所在 `AssemblyLoadContext`；本轮未重新验证此改动。

本轮检查入口自身的类型推断错误已修正；`SharedRuntimeCheck` 为新示例选择前的异步 CoreAction 添加一次 `Story.Pump()` 调用胶水，旧断言全部保留，未删去失败断言。源码检查新增在既有 `scenes/*/check` 下。第二章 C# fixture 使用 `.source` 后缀，通过显式 Compile Include 编译，避免进入宿主默认 C# 源码枚举。

## 下一独立 Agent 的受影响范围复核命令

工作目录为仓库根目录；无需重跑全量 verify/check 或已通过的 11 项内容比对。

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.csproj --nologo
& godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs
& godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Shared/Story/Examples/ExampleAwakening.cs
& godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Awakening/check/FinalAwakeningHostScript.cs
& godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Chapter2/Game/check/FinalHostScript.source
dotnet run --project godot_csharp/scenes/Awakening/check/SharedRuntimeCheck -- godot_csharp/tools/bin/world-scripts/ExampleWorld/output/WorldScript.ExampleWorld.dll godot_csharp/tools/bin/world-scripts/ExampleAwakening/output/WorldScript.ExampleAwakening.dll
& 'C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe' --headless --path godot_csharp --script res://scenes/Chapter2/Game/check/final_host_runtime_check.gd --quit-after 15000
```

最后一条实际使用两个章节场景和两个独立外置媒体脚本。第二章地图切至 clinic 经原桥 go 队列进入 Core 房间，然后等待 story choice，选 record 执行实际 Core 动作，检验前文→图片→后文确认顺序。选择时写入 Insight 的来源房间必须由下一验证者进一步检查持久化值，不能只凭可见提示认定 Core 动作正确。

第一章默认启动错误应由下一全新独立启动/盲玩 Agent 检查；本轮没有复跑第一章整组回归。动态人物/房间/物件真实引用、观察隔离、原 Session 状态动作与两章自主取物追加断言位于 `SharedRuntimeCheck/Program.cs`，本轮因上游冲突未执行到它们，必须复核。

日志：`final-awakening.log`、`final-content.log`、`final-core.log`、`final-host.log`。外置脚本编译输出已实际观察，未单独保存构建日志。Godot 回归正常退出同时报告 ObjectDB instances leaked 警告；这不取消上述 SCRIPT ERROR 失败，也不证明长时间资源稳定性。
