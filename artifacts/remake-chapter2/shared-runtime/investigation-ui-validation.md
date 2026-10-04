# 语义调查 UI 独立局部验收：第一轮

本轮为全新独立自动局部验收，仅阅读当前源码及 Godot 子目录协作约束，未读取设计文件或历史报告；不是盲玩，也不是完整通章。实现源码未由验收 Agent 修改。

## 构建与实际 Godot 启动

执行一次 `dotnet build godot_csharp/DualOntologyWorldEngine.sln`：通过，0 错误、26 警告。未运行旧全量 39/216 断言套件。Core、ChapterRuntime、Chapter1Script、Chapter2Script、Prototype 与宿主 DLL 已编译。

实际 Godot 4.6.1 mono 控制台程序以 `gl_compatibility` 启动第二章默认场景，窗口启动使用 `Start-Process -WindowStyle Hidden`，保留用户编辑器进程 30124。第一轮启动失败：`scenes/Shared/UI/ReplayManuscript.gd:59` 对 `number` 使用 `:=` 无法推断类型；第二章依赖编译失败。证据：`semantic-parse-c2.err` 与 `semantic-parse-c2.log`。

失败已即时交付主代理。主代理随后报告已修改显式 String 类型。遵循每轮修复后更换全新 Agent 的要求，本 Agent 未重新启动 Godot 验证该修复；默认两章 parse、窗口界面、截图、单格回调、媒体继续与异步 UI 重演由下一轮新 Agent 验证。因此本报告不将这些项判为通过。

## 纯 C# 定向结果

新建小型验证项目：`godot_csharp/scenes/Chapter2/Game/check/InvestigationCheck/InvestigationCheck.csproj`、`Program.source`。项目直接引用本次已编译 DLL，不再构建原 solution；`.source` 避免宿主默认 Compile 自动收录测试入口。最终 19/19 断言通过，机器结果为 `semantic-csharp-checks.json`。

验证包括：原 Inspect 前无 inspection/thing 记录；第三日前无问题；原 RunSlot 推进日期；原 Inspect 的全部保存文本与 Feed、Describe 完全相同；同房物证顺序保持；来源保留原日/时段/地点与亲见；客观台账变化后，已存 thing 与 inspection 内容保持；人物位置与病情依据侦探投影，未见的真实身体位置/病情变化不会改变人物笔记；真实已知痕迹与原候选资格；问题文本和 context；分组 choices 与原 ReplayMenu 的 1-based 索引及原文本完全一致；原合法假设真实 ReplayNight 使用独立世界返回痕迹，原现实日期、时段、侦探地点与台账发放数保持。

防泄漏测试明确注入了未观察的台账值 9876 及一个人物的客观位置/病情。此注入只用于比较投影/保存记录，不用于候选资格或重演。候选与重演使用重新创建的正式世界。

## 合法候选与重演配方

第 2 日五个白天执行原 `RunSlot(Cmd.Rest)`；第 2 夜执行原 `RunSlot(Cmd.Stakeout, Place.Logistics)`；第 3 晨正常迁到 Logistics 后原 Inspect 台账；第 3 日上午正常迁到 Clinic 后原 Inspect 碗。`SetPlayerPlace` 使用原地点访问接口，是自动验证调用而非人工行走。

该历史得到真实已知痕迹台账、门闩、碗与 14 个合法候选人物，原菜单 26 项投影为 13 组语义问题。首个原菜单 Rule 假设用于真正的 ReplayNight，没有制造候选、添加身量/签名、绕过资格或改用 Examples。

最初测试配方第 2 夜使用 Sleep，Logistics/Clinic Inspect 后缺少身量/名字证据，原合法候选为空。诊断后改用上述原守夜动作，得到合法非空候选。此为验证准备配方修正，不是实现错误或自动通关。

## 边界

C# 重演为同步核心验收，不能代替 Godot 桥异步结果返回。第一章原 5/6 变量、共享手稿大于六组的实际按钮渲染、R 1–3 门槛、休息邻近物件、Q/Tab/HUD 可见与关闭继续，均待新 Agent 的窗口回归。本轮未产生用户指定 semantic-c2 截图，不声称已验证。
