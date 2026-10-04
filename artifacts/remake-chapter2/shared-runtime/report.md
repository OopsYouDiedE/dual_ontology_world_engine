# Shared runtime 定向自动验证

本轮独立上下文只读相关实现源码，不读项目设计文件或历史测试报告；全部属于定向自动验证，不是盲玩、游玩时长或通关体验验收。未修改实现文件。NPC跨房检查注入空间状态，回放图注入单个墨影帧。

## 结果

- 第二轮 `dotnet build godot_csharp/DualOntologyWorldEngine.sln` 成功：0错误、25警告。首次发现 AwakeningEngine.cs FileAccess 歧义并上报，实现代理修复后通过。
- 独立编译 `Shared/Story/check/ExampleScript/ExampleScript.csproj` 成功，运行时从其实际DLL加载 ExampleWorld，未用预写等价定义替代外置脚本。
- `Awakening/check/SharedRuntimeCheck` 最新31项通过：共用位置/观察/进度隔离；二章位置同步/观察；跨房查证和当面交流拒绝；本地同行对象可去另一房目标；未知地图无旧动作且拒绝旧查证；普通CLI原默认路线；外置C#脚本选择暂停、typed Set先于后文、条件等待、并行和重复；重建选择/访问记录与现实隔离；第一章JSON读取、合法证物可见性、试验/协助/回忆前提、陈述与证物判定、共用meeting的UI确认等待。
- 追加真实 `Game.Rebuild(..., FreeEvents)` 顺序测试：同一slot Visit(Clinic)→Choice(记录当时房间到typed tag)→Visit(Hall) 重建后tag仍为Clinic，FreeEvents逐项一致。Hide隐藏所有侦探可见投影后，共享观察ledger未泄漏中央基座或病床。
- Godot 4.6.1 mono显式console探测通过。第二章headless scene加载无SCRIPT ERROR。第一章初次加载发现 Awakening.gd:1170括号解析错误并上报；修复后由另一全新上下文完成最终加载复核，本报告不宣称第一章最终宿主验收。
- `Chapter2/Game/check/shared_runtime_check.gd` 窗口运行14项通过：旧HEAD AwakeningContent.gd常量实际GD解析，JSON七个数据分组经JSON序列化逐组完全一致；二章桥启动；回放实际切到诊疗厅但真实逻辑位置不变；回放结束恢复原地图/位置且真实位置不变；未知campus_n地图快照无上一房动作/人物。

## 图像核验与限制

查看了 replay-clinic.png、replay-restored.png、unknown-map.png。诊疗厅墨影与心声可见，结束后中央厅人物恢复，未知地图未显示中央厅人物。截图保留自动检查启动时的开场对话框，因此仅核验切图、墨影和恢复，不能作为完整回放UI/游戏体验验收。回放切图检查直接调用_setup/_apply/_teardown，不测试自然夜间流程或播放时长。

首次普通new Game在中央厅，但shared观察表未初始化；SetPlayerPlace同房间早退。入房观察检查采用Clinic→Hall真实位置切换。此项已告知主代理，未把空初始ledger当作独立功能通过证据。

## 复现

1. `dotnet build godot_csharp/scenes/Shared/Story/check/ExampleScript/ExampleScript.csproj`
2. `dotnet run --project godot_csharp/scenes/Awakening/check/SharedRuntimeCheck/SharedRuntimeCheck.csproj -- godot_csharp/scenes/Shared/Story/check/ExampleScript/bin/Debug/net8.0/ExampleScript.dll`
3. 用 `git show HEAD:godot_csharp/scenes/Awakening/AwakeningContent.gd` 原字节保存为 tmp/original-awakening-content.gd；从仓库根运行Godot console `--path godot_csharp --script res://scenes/Chapter2/Game/check/shared_runtime_check.gd`。需要渲染截图，不能带headless。

日志：targeted-check.log、godot-targeted.log、chapter1-load.log、chapter2-load.log。未运行全量verify.py或Core全量检查。
