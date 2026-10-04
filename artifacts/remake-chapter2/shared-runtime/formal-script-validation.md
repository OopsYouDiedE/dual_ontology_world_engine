# 正式剧本迁移独立自动验证

日期：2026-10-04。本轮由全新独立 Agent 执行；未读取设计文件、历史报告或盲玩记录。本报告是自动编译、来源比对与局部纯 C# 验证，不是盲玩，也不证明完整游戏体验或剧情正确性。未执行 39 项全套、216 穷举或全量 verify。

## 结果

普通 `dotnet build godot_csharp/DualOntologyWorldEngine.sln` 成功，0 错误、26 警告，两正式剧本、CLI 与 Godot 宿主均生成。现有 nullable、DialogueManager 和 QueueReader 警告未处理。宿主具备编译产物，可交实机 Agent 启动；本 Agent 未启动图形游戏。第一次验证调用误加 `--no-restore`，新工程尚无 assets 时失败；随后普通 build 成功，此为验证命令问题，不作为实现缺陷。

`compile_world_script.ps1` 的三次正式输入均成功：

- Chapter1Script.cs → `tmp/formal-chapters/output/ch1/Chapter1Script.dll`
- Chapter2Script.cs → `tmp/formal-chapters/output/ch2/Chapter2Script.dll`
- Chapter1Script.csproj → `tmp/formal-chapters/output/project/Chapter1Script.dll`

正式输出 AssemblyName 是 Chapter1Script / Chapter2Script，未被编成 WorldScript 示例。外置 `Shared/Story/Examples/ExampleWorld.cs` 与 `ExampleAwakening.cs` 分别成功编译为 WorldScript.ExampleWorld.dll 与 WorldScript.ExampleAwakening.dll；本轮没有重复全部示例行为测试。

## 来源复用证据

旧源定位为 `tmp/formal-chapters/chapter1/Night/AwakeningContentSession.cs`、`MorningWorld.cs`，以及 chapter1 根下 JSON/dialogue；第二章旧源为 `tmp/formal-chapters/Chapter2.GameWorld.cs` 与 `Chapter2.Chapter2Content.cs`。

独立 Python 原始字节比对的五项全通过（证据 `tmp/formal-chapters/source-byte-validation.json`）：

1. Chapter1Script.cs 去掉新增 Immutable using 后，以完整旧 ContentSession 原字节为前缀，包含旧文件末尾换行。
2. 原 MorningWorld 构造函数完整原字节出现在正式入口中。
3. 原 MorningWorld 去掉迁出的构造函数后，剩余文件字节与实际 Night/MorningWorld.cs 完全相等。
4. Chapter2Script.cs 以完整旧 GameWorld 文件原字节为前缀。
5. 旧 Chapter2Content 去掉 file-scoped namespace 与其后空行后的完整余文原字节存在于正式入口。

第一章 JSON 与 dialogue 同旧快照逐字节相等，并通过正式 DLL 的 GetManifestResourceStream 实测与磁盘源逐字节相等。Chapter1Script.CreateContent 的 JSON 原文读取、DialogueText 的完整文本读取也与源精确相等。

- AwakeningContent.json SHA256：`d0672912b798040d67a78e0b98786e5014de37b41e4609ee106f42161e8119c4`
- awakening.dialogue SHA256：`a4fc56fdda67330bd5d617f48bc170d191595f918c656dea9f159884ad0ae1e4`

工程静态核查：第一章直接 Compile 正式入口及剩余 Night/*.cs；第二章直接 Compile 正式入口及原 Prototype/*.cs，仅排除 CLI Program.cs。Prototype.csproj 只编译 Program.cs 并引用 Chapter2Script。Godot 宿主排除 Night/Script/Prototype 原源码，使用 ProjectReference，因此没有在宿主或 CLI 重复定义迁出的世界类型。ChapterRuntime.csproj 仅 Compile ChapterSession.cs、StoryRuntime.cs、CoreScriptWorld.cs；三份源码无章命名空间/章枚举绑定，无章硬编码；两正式程序集实际引用清单中无 Godot。

## 局部运行验证

新增胶水验证位于 `godot_csharp/scenes/Awakening/check/FormalScriptCheck/`，仅引用两个正式工程，不改实现。命令：

`dotnet run --project godot_csharp/scenes/Awakening/check/FormalScriptCheck/FormalScriptCheck.csproj`

最终 26 个 PASS；完整输出 `tmp/formal-chapters/runtime-validation.txt`。首次胶水测试误用了不存在的 T.Rations，修正为原源码 T.Ration 后运行成功，此为测试代码问题。

覆盖边界：

- MorningWorld 与 Game 的实际程序集名称分别为正式 DLL 名；两章依赖同一个 ChapterRuntime，反射两章持有的泛型 StoryRuntime 类型与共享类型 ReferenceEquals。
- 第一章实际原世界 4 人、9 房、12 证物，初始无钩、无回忆、登船记忆；侦探 R05、三名同伴 Hall 的真实 SpatialProperty 引用。
- 原 BeginWake 的 @lock → s1_wake → @unlock → @hud 次序，每个输出在确认前稳定，stage 变为 roam；原 meeting 的 s2_meet 等待确认后才推进 @name。
- 第二章实际原世界 16 人、11 空间、第 2 日晨；以有限 CLI 输入“状态 / 查 中央基座”调用原 ConsoleUI.Run，确认原开场/状态输出及合法调查进入原行动记录。
- 原 Game.Rebuild 按行动记录重建后日/时段/记录匹配；改变重建世界房间与粮仓状态不污染真实世界且身体引用独立。

本轮未报告实现失败。以上只验证编译归属、资源复用及必要迁移边界；盲玩时长、卡顿、视觉、推理难度、完整通关应由独立实机/盲玩验证另行结论。
