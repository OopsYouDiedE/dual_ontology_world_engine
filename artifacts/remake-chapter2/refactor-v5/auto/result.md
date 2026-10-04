# 独立自动回归第1轮：构建通过，渲染解析失败

这是自动回归与脚本截图检查，不是盲玩。未读取 docs、README 或历史试玩报告，未运行全量 Core 或机器人。

## 已验证

- Prototype.csproj：build退出码0；首轮2个nullable警告，无错误。第二次增量build日志保存在 build-prototype.log。
- Godot host csproj：build退出码0；首轮25个nullable/addon警告，无错误。第二次增量build日志保存在 build-host.log。
- Paper、HintOverlay、FieldNavigation、Dialogue原版与第二章副本SHA256逐对一致，见copied-assets.txt。
- 源码定向审查：Bridge.Snapshot仅序列化ConsoleUI.LastReplay。Chapter2Game.accept_replay仅消费该Dictionary并传result.frames给ReplayStage。ReplayStage没有重新创建C#世界或调用ReplayNight，图形为shadow单墨影、黑色匿名剪影；未查状态通过findings.status=unvisited保留。这是源码审查结论，运行回放未完成。

## 阻断及未完成项

唯一Godot窗口以gl_compatibility启动，实际OpenGL3.3/NVIDIA RTX3070设备。ReplayStage.gd:152的 `var stands := host.map.get_node_or_null("Stands")` 无法推断类型，导致Chapter2Game依赖脚本编译失败。后续Nil异常均为初始化失败链。已立即上报主代理并中止进程，保存godot-render-initial-failed.log和退出码说明。

脚本在识别阻断前保存了01-intro/02-field/03-notes/04-board-editor四张PNG。实际view_image检查01-intro确认只有旧现场、旧HUD，没有对白；因此所有这些失败运行截图均无验收效力。不得将其计为开场/笔记截图通过。GUI完整轨迹、CLI/GUI逐行比较、回放/对质/结局画面均未验证。

## 下一轮独立Agent检查入口

- godot_csharp/scenes/Chapter2/Game/check/regression_v5.gd：仅GDScript，不需csproj exclude；生成输入fixture、Godot visible_output与八类窗口渲染截图。已补初始化失败即退出的保护，避免收集无效截图。此最后修改未运行。
- godot_csharp/scenes/Chapter2/Game/check/compare_v5.ps1：复用GUI生成的同一inputs.txt，运行CLI play，仅去掉Program脚本输入echo `> `行，逐物理行大小写敏感比较；保留全部空行、重复文本与顺序。输出cli原始/可见日志、退出码与逐行差异报告。此程序未运行。

从仓库根目录运行：

```powershell
& 'C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe' --path godot_csharp --rendering-method gl_compatibility --script res://scenes/Chapter2/Game/check/regression_v5.gd
& ./godot_csharp/scenes/Chapter2/Game/check/compare_v5.ps1
```

复测需全新Agent和新报告目录。regression_v5.gd内folder默认指本轮auto，复测应改自己的检查脚本输出目录，避免覆盖失败证据。CLI fixture比较脚本接受-OutputDirectory。脚本推进用歇/睡/空对质，绝不可称盲玩。画面需要新Agent逐张view_image；已暂停后不再运行任何测试。
