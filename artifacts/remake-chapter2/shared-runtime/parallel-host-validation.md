# 最终宿主演出定向自动验证

日期：2026-10-04。独立验证 Agent；未读取设计文件、历史报告；未修改实现源码。本报告是自动宿主回归，不是盲玩或体验时长验收。使用现有脚本夹具、直接宿主调用与真实 Godot 帧循环。

## 结果

- `dotnet build godot_csharp/DualOntologyWorldEngine.sln --nologo`：成功，0 错误、26 警告。
- 默认 Prototype 契约编译 `FinalAwakeningHostScript.cs` 与 `FinalHostScript.source`：均成功，各 0 错误、2 个 Prototype nullable 警告。
- Godot 4.6.1 Mono、gl_compatibility、RTX 3070 实机入口：最终 **26 项检查通过，0 失败**，进程退出码 0。没有脚本异常；退出有 ObjectDB instances leaked 警告，未做额外全量或泄漏归因测试。
- 二章查证 `查 中央基座` 完整自然演出至信息模态：首次 14.542 秒，补充并行截图的一次定向运行 14.541 秒。没有按帧快进，也没有添加等待凑时长。
- 实机观察到最大 **16 个同时存在的角色 action jobs**，并行断言通过。截图显示多角色沿用原人物像素帧，分布于中央厅站位；未见纹理缺失、大片遮挡或人物失踪。
- 查证演出完全 idle 后才调用诊疗所切图。Core 快照 `det_place == Clinic` 与宿主 `clinic-choice` 同步；合法 `record` 答复继续执行。
- 第一章文字→图片→后续文字→第二图片→第二后续文字，各需独立确认；二章同序列检查与独立确认也通过，两个媒体序号不同。
- 动态人物在两章均使用原 `engineer.tres`；第一章实际 sprite action animation 检查通过，二章 Core 操作帧携带原 frames_path 并通过检查。

## 检查胶水变更

只修改 `godot_csharp/scenes/Chapter2/Game/check/final_host_runtime_check.gd`：将 until、开场与媒体循环期限从 process_frame 次数改为 40 秒墙钟；新增开场自然完成断言、查证耗时、并发 jobs 断言与截图；若查证仍 busy 则直接失败退出，禁止随后 load_map 销毁临时演员。不修改或绕过任何原有断言。

本轮未重复 39 项 C#、11 项原内容全量检查。定向入口运行两次，第二次用于补齐并发截图与指标。帧去重、0.35 秒/行动值的精确时序公式、Field region/exit 的每一个边界分支未分别做新增专门断言；已观察其关联真实查证演出完成、并发以及随后房间/选择同步，不将这些观察宣称为完整分支覆盖。

## 证据

- 日志：`parallel-host-validation.log`
- 第一章文字：`queue-replay-chapter1-text.png`，文字与继续按钮完整可见。
- 第一章图片：`queue-replay-chapter1-image.png`，诊疗所图片与继续按钮完整可见。
- 二章并行演出：`queue-replay-chapter2-parallel-actions.png`，原角色帧在中央厅同时显示。
- 二章选择：`queue-replay-chapter2-choice.png`，诊疗所原人物与动态验证学员同时可见，Core choice 断言通过；截图时对话尚在逐字显示，截图本身不证明完整选项文字已展开。
- 二章图片：`queue-replay-chapter2-image.png`，独立图片确认控件正常显示。

运行命令：

```powershell
& 'C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe' --path godot_csharp --rendering-method gl_compatibility --script res://scenes/Chapter2/Game/check/final_host_runtime_check.gd
```
