# 正式两章默认宿主定向自动实机验收

2026-10-04。全新独立 Agent 执行，不读取设计文件或历史报告。使用既有宿主构建，不重复 build、全量测试或全章通关。验证胶水为 `godot_csharp/scenes/Chapter2/Game/check/formal_chapter_host_check.gd`；无实现改动。

运行 Godot 4.6.1 Mono 真实窗口，`--rendering-method gl_compatibility`，实际日志为 NVIDIA GeForce RTX 3070 / OpenGL 3.3 Compatibility。默认 Main2D 与默认 Chapter2Game 先后实例化，无 world-script 参数，无外置示例 DLL。

本轮 18 项断言，17 项通过、1 项来源诊断失败，退出码 1，流程墙钟 22.002 秒。日志见 [formal-host-runtime.log](formal-host-runtime.log)。不能将本轮报告称为全部通过。

## 第一章

- 默认 `Engine.ScriptId()` 为 `chapter1`，原开场正常显示。截图中侦探心声“……”与运行时原对白首句一致。
- `STORY.raw_text == Engine.DialogueText() == FileAccess.get_file_as_string(原 awakening.dialogue)` 全等。Godot 文本读取所得内容长度 15105 字符。
- 运行时原 DialogueManager 编译资源与原 importer 加载资源的 `titles` 字典全等、`lines` 字典全等：88 个 title、827 个 line。这验证完整原编码对白图，而非仅标题数或抽样对白。没有对其他翻译语言、全部剧情分支作执行遍历。
- 正常自动揭字/推进原开场到 `roam`；站起后将玩家置于实际房间入口 `(440,340)` 触发原 meeting，原姓名输入与公告确认选择执行后到 `investigate`，姓名为“正式宿主验收”，`board_read` 已写入。
- 执行原 E2 实验后，`experiments` 含 E2 并回到原证物卡。实际显示“门底的灰与木楔”、原 Core CardFacts、来源以及“楔尖的新磨痕……一道窄缝”的实验结果。卡片文本与 Core 原事实一致。

已人工查看截图：

- [第一章原开场](formal-chapter1-opening.png)
- [第一章 E2 原实验卡](formal-chapter1-e2-card.png)

## 第二章

默认 `world_script_path` 为空；`Bridge.ScriptId()` 返回 `chapter2`。但本轮构建的 `Bridge.ScriptAssembly()` 使用 `Assembly.Location`，实机返回空字符串，严格要求以 `Chapter2Script.dll` 结尾的来源断言失败。已及时向主代理报告；主代理已把诊断 API 改为实际世界类型的程序集名。该修复未在本轮重新构建或执行，需由下一全新 Agent 单独复核正式程序集来源。运行的实际原剧情正文没有因此失败。

原开场实际显示：“第 1 日傍晚，四个寝区的隔离门都开了。十六个人在起居厅里第一次见面——人人都以为彼此是初识。” 自动正常推进开场到 idle 后，调用原命令 `查 中央基座`。等待真实动作结束，全程未跳帧、修改时钟或 busy 强制换图；14.464 秒后自然收敛到原调查笔记卡。

实际结果包含“你细看了中央厅”“中央基座完好，石面上光洁，没有刻字”“板上什么也没贴”，以及原广播、教师话语和人物动向。结果为第 2 日上午；可见地图 `central_hall`、Core `det_place=Hall`、宿主 state 与 MAP_PLACE 映射一致。现场有 15 个可见演员对象，累计 119 个原 Core action frame。本轮日志 max_jobs=0 为胶水闭包标量捕获计数问题，不能据此推断真实动作未播放，也不据此宣称并行动作指标；胶水已改为引用容器供后续使用，但未重跑正文。

已人工查看截图，原文字清晰，人物与中央厅现场正常显示：

- [第二章原开场](formal-chapter2-opening.png)
- [第二章中央基座原调查卡](formal-chapter2-pedestal-card.png)

## 运行错误与边界

日志未出现启动异常、SCRIPT ERROR、C# exception 或原调查“出错”输出。退出时出现 `ObjectDB instances leaked at exit` WARNING；本轮如实保留该警告，未进一步定位。所启动 exec session 已正常结束，不停止其他既有 Godot 进程。

本轮属于自动回归，不是盲玩。胶水直接调用对白 reveal/advance/choose、姓名与公告确认、E2 原实验及第二章 send；第一章移动触发处使用位置注入。未验收自然玩家推理、有效游玩时长、盲玩可用性或全章结局。所有等待使用 Time.get_ticks_msec 墙钟限时，不以固定帧数量作为行动完成超时。原开场、选择/mutation、调查与卡片链均在运行时实际执行。
