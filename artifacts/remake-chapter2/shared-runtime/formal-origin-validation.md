# 默认正式剧本来源诊断验证

验证日期：2026-10-04。全新独立测试上下文；未阅读项目设计文件或历史报告。本轮是自动来源诊断与默认开场截图验收，不是盲玩，也不评价完整剧情流程。

## 构建与运行

只执行一次增量 `dotnet build godot_csharp/DualOntologyWorldEngine.sln`，退出码 0，0 错误、24 警告，耗时 1.66 秒；正式 Chapter1Script.dll、Chapter2Script.dll 与宿主程序集构建成功。没有 DLL 锁失败。警告主要为 DialogueManager 示例的可空引用诊断，另有 Chapter2Bridge.QueueReader.Reads 的 CS0197。

使用 Godot 4.6.1 stable mono（14d19694e）、`--rendering-method gl_compatibility`，实际 OpenGL 3.3 NVIDIA RTX 3070。两章各自从新进程载入默认 `Main2D.tscn` / `Chapter2Game.tscn`，没有传入外部 world script，也没有加载 Examples。

胶水入口：`godot_csharp/scenes/Chapter2/Game/check/formal_script_origin_check.gd`。命令形式为 `Godot_v4.6.1-stable_mono_win64_console.exe --path godot_csharp --rendering-method gl_compatibility --script res://scenes/Chapter2/Game/check/formal_script_origin_check.gd -- --chapter=chapter1`，第二章替换末尾为 `chapter2`。胶水只读取来源、状态、现有可见开场并保存视口截图，不注入状态、不操作谜题。

## 验证结果

| 检查 | 实测 | 结果 |
| --- | --- | --- |
| 第一章 ScriptId | chapter1 | 通过 |
| 第一章实际 world 所属程序集 | Chapter1Script | 通过 |
| 第一章 STORY.raw_text 与 engine.DialogueText() | 完全相等，15105 字符 | 通过 |
| 第一章原开场 title | s1_wake 存在 | 通过 |
| 第二章默认 world_script_path | 空 | 通过 |
| 第二章 ScriptId | chapter2 | 通过 |
| 第二章实际 world 所属程序集 | Chapter2Script | 通过 |
| 第二章默认 state | 已生成，28 项 | 通过 |
| 第二章侦探位置与逻辑房间 | det_place=Hall，room.place=Hall | 通过 |
| 第二章地图同步 | central_hall 的 MAP_PLACE=Hall | 通过 |
| 第二章开场可见 | 对话面板可见，正文为第1日傍晚初会叙述 | 通过 |

源码核对确认两个 ScriptAssembly API 均直接使用实际 world 的 `GetType().Assembly.GetName().Name`（第一章 `_morning`、第二章 `_g`），不依赖 Assembly.Location。

已人工查看两张截图：第一章显示生活翼 R-05、侦探床上及开场心声；第二章显示中央厅、多名人物与初会开场。最终两次日志均无 SCRIPT ERROR 或 ERROR，两进程正常退出，胶水失败计数均为 0。第一章退出出现 ObjectDB instances leaked 警告，来源与开场检查仍通过；第二章未出现该警告。

初版胶水曾错误把 Main2D 根节点当作带 engine 的 Awakening，产生一次脚本访问错误；仅修正胶水为读取 Main2D/Awakening，随后重启新进程验证通过。未改实现。测试结束后只剩原有用户编辑器 PID30124；没有终止用户编辑器或其他测试进程。

证据：

- [第一章日志](formal-origin-chapter1.log)
- [第二章日志](formal-origin-chapter2.log)
- [第一章截图](formal-origin-chapter1.png)
- [第二章截图](formal-origin-chapter2.png)

本轮未重复纯 C# 26 断言、原文比对、第一章 meeting 实验、第二章查证或全量回归。
