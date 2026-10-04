# 脚本宿主定向复核（2026-10-04）

本轮为全新独立上下文的自动定向验证，未读取设计文件、历史报告或谜底。不属于盲玩；不评价有效游玩时长。只修改既有 check 入口及其 fixture，未修改实现。本轮到发现故障并由主代理修复前的结果为准；修复后的复核交给下一全新 Agent。

## 实际结果

- `dotnet build godot_csharp/DualOntologyWorldEngine.csproj --nologo` 成功：0 错误，2 个既有 CS8629 警告，位置 Prototype/Player.cs:284、285。
- 普通 C# ExampleWorld.cs、ExampleAwakening.cs 均经 compile_world_script.ps1 实际编译成功；两章媒体 fixture 同样实际编译成功。
- SharedRuntimeCheck：前 16 项通过，包含本地目标、观察隔离、进度隔离、二章房间同步、跨房查证/对话拒绝、真实 DLL 选择、效果先于文字、条件等待、并行与重复。旧检查把新 Feed 文字标记也纳入精确比较，第一次在第 14 项误报；仅在检查端剥离 `\u001estory-text:` 后通过。随后 Rebuild 实际抛出“重演剧情选择与原记录不一致”，停止。原引用绑定、新人物自主取物和同 Session 占用检查位于其后，本轮尚未运行，不能记通过。
- Godot 实际宿主：默认第一章启动原开场对话；第一章 fixture 实际 DLL 文字→图片→文字成功，第二图片及其后文字也成功；ExampleAwakening 实际 DLL 在 Godot 上加载成功，新人物使用原 engineer.tres，原 Core 动作后文字“放大镜”成功。该阶段没有 SCRIPT ERROR。
- 第二章实际桥启动自编译 fixture 成功。进入 Clinic 触发 `Chapter2Game.gd:258` `_spawn_actors` 访问缺失 `room`，继而未得到同步 story choice。本轮因此不能确认二章选择回答、媒体序号、info 后续以及新增人物动作帧。本检查已增加失败后的安全终止，避免后续 Nil modal 引起无关错误。

## 故障交接与后续检查

主代理已收到两处真实故障，报告已最小修复：Rebuild 的 RestoreFree 在事件/choice 读取前 Pump；Bridge 增加 Reads/submitted 计数保证 IsWaiting 不发布命令已取走而 Waiting 未清除的中间快照。本 Agent 没有重新构建或复核该修复。

既有 final_host_runtime_check.gd 已补充定向检查：默认第一章启动、第一章第二媒体、普通 Example DLL、新人物原帧和 inspect 动作；二章普通 info 查证后进入 Clinic 选择、回答、两媒体唯一序号、原 SpriteFrames 的 Core 动作帧、每张图片独立确认及后文字。补充的动作帧与 info 检查尚待下一 Agent 实际执行；不是已通过项。Fixture FinalHostScript.source 新增“验证学员”与 CoreAction 及第二图片；FinalAwakeningHostScript.cs 新增第二图片。

## 精确命令（下一新上下文）

工作目录：C:/Users/iii/Desktop/dual_ontology_world_engine

```powershell
dotnet build godot_csharp/DualOntologyWorldEngine.csproj --nologo
& godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Shared/Story/Examples/ExampleWorld.cs
& godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Shared/Story/Examples/ExampleAwakening.cs
& godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Awakening/check/FinalAwakeningHostScript.cs
& godot_csharp/tools/compile_world_script.ps1 -Source godot_csharp/scenes/Chapter2/Game/check/FinalHostScript.source
dotnet run --project godot_csharp/scenes/Awakening/check/SharedRuntimeCheck -- godot_csharp/tools/bin/world-scripts/ExampleWorld/output/WorldScript.ExampleWorld.dll godot_csharp/tools/bin/world-scripts/ExampleAwakening/output/WorldScript.ExampleAwakening.dll
& 'C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe' --headless --path godot_csharp --script res://scenes/Chapter2/Game/check/final_host_runtime_check.gd
```

实际检查入口是 final_host_runtime_check.gd；Shared/Story/check/host_script_check.gd 不存在。
