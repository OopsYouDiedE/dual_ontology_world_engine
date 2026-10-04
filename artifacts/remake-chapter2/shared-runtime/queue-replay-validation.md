# Queue / Replay 定向自动验证

日期：2026-10-04。执行者：全新独立上下文验证 Agent。未读取设计文件、历史测试报告；未修改实现源码。检查使用脚本 fixture、直接宿主调用及状态断言，属于自动回归，不属于盲玩。

## 本轮结果

- 单次 `dotnet build godot_csharp/DualOntologyWorldEngine.sln`：退出 0，26 警告、0 错误。完成后立即通知主代理可启动独立二章盲玩。
- 四个脚本均由 `godot_csharp/tools/compile_world_script.ps1 -Source <source>` 默认 Prototype 契约成功编译：ExampleWorld.cs、ExampleAwakening.cs、FinalAwakeningHostScript.cs、FinalHostScript.source。每次编译 2 个既有 Player.cs nullable 警告、0 错误。
- `dotnet run --project godot_csharp/scenes/Awakening/check/SharedRuntimeCheck -- <ExampleWorld DLL> <ExampleAwakening DLL>`：39 个 PASS，退出 0。
- Godot 4.6.1 mono，非 headless，`--rendering-method gl_compatibility --script res://scenes/Chapter2/Game/check/final_host_runtime_check.gd`：实际 OpenGL 3.3 / NVIDIA RTX 3070；12 个 PASS、3 个 FAIL，退出 1。后续二章媒体及动态帧检查被无同步选择阻断。

## 已通过覆盖

共享运行时验证通过真实 Core 引用与房间引用绑定、局部观察隔离、动态人物/物件观察、动态人物动作提交及身体状态、两章外部新增人物自主取物、等待与平行重复、Rebuild 选择与访问记录恢复、有序 visit → choice → visit、重演修改不污染现实。第一章外部普通 DLL 在实际宿主加载成功，使用工程师原 SpriteFrames 与 inspect 动作；默认第一章正常开启原对话。第一章文本 → 图片 → 后文 → 第二媒体 → 后文每段确认顺序均通过。

## 失败与阻断

1. `chapter2 actual room info investigation returns modal` 失败。
2. 紧随出现：`SCRIPT ERROR: Cannot call method 'queue_free' on a previously freed instance.`，位置 `Chapter2Game.gd:1160`，`_play_actions`。
3. `chapter2 room trigger reaches synchronized choice input` 失败。
4. `chapter2 actual Core room matches visible triggered choice` 失败。
5. `BLOCKED chapter2 remaining checks: no real synchronized story choice`。未完成二章真实 go → choice → 答 → 媒体 → 动态动作帧链，不应宣称通过。

错误发现后立即通知主代理；未自行修实现。二章截图显示可见标题为诊疗所，但笔记本仍写“你在中央厅”，并输出“不认识‘Clinic’这个地方”，提供了可见层与 Core 地点不同步的证据。判断可能涉及测试调用契约或实现，需主代理判断。

## 截图与验证胶水

原 check 无截图参数或默认输出；仅为视觉证据添加 `snapshot(name)`：等待一帧及 `RenderingServer.frame_post_draw`，保存真实 viewport PNG。全部原断言保留，未降低失败门槛。新增调用位于第一章首文本、首图片、二章触发断言后、二章首图片；最后一个因提前阻断未执行。

- `queue-replay-chapter1-text.png`：清楚显示 VALIDATION BEFORE IMAGE 与继续按钮；未見截断。
- `queue-replay-chapter1-image.png`：清楚显示地图图片、背景遮罩与独立继续按钮。
- `queue-replay-chapter2-choice.png`：实际为失败后的诊疗所背景与笔记模态，无有效 story choice，笔记含未知 Clinic 错误。
- `queue-replay-host.log`：本轮原始 Godot 控制台输出。

本轮未执行全量 verify、Core check 或原文迁移复测；没有等待、错误门槛或重复流程凑游玩时长。
