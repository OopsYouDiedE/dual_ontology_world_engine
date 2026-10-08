# 第一章文案与重演事实迁移

本轮只执行自动回归与静态审计；未读取设计规范、未新增场面台词、未变更已有台词 ID，未提交。初始分支为 `codex/ch1-strings`，初始工作区干净。

## 改动

- `Script/Chapter1SessionFlows.cs`：表单、卡片、结局按钮、侦探心声称呼改读文案键；向回放界面提供结构化事件与控制提示。
- `Script/Chapter1Script.cs`：E12 追加说明改读剧本文案，保留已检视 E7 的条件；章名、欲望描述、证物毁去与缺失物件提示使用模板。
- `Night/NightReplay.cs`：事件采用 `ReplayEvent(Kind, Who, Subject, Object, Room)`，字幕与心声从剧本模板取出并绑定姓名、房间等参数。内部传话使用稳定的消息种类；接收与执行逻辑不再匹配对白。
- `Script/Chapter1Replay.cs`、`Script/Chapter1SessionFlows.cs`、`Script/Chapter1Flows.cs`：两个正式入口保存相同的 `detective_bolted` / `detective_refused_stranger` 事实。前者来自成功完成霍铎请求的行动，后者来自侦探对陌生请求者的社交判定；均不读取显示文字。
- `ReplayStage.gd`：按 `event.kind` 判定移动帧停留时间，按 `event.who` 确定人物；暂停与倍速提示来自宿主事件中的剧本文案。`UI/Panels/Field/ReplayOverlay.tscn` 的重复控制文案已移除，实例创建时由同一剧本文案初始化。
- `Night/NightVariables.cs`、`tools/story_chapter1.py`、`story/第一章/声路与真相.story`：用作者声明的 `句尾含义` 生成 `TailWantsOpen` / `TailHelp`，不从句尾文字猜测含义。
- `Night/Wishes.cs`、`Night/MorningTraces.cs`、`Night/MorningWorld.cs`、`Night/MorningEcology.cs`、`Script/Chapter1Speech.cs`、`Shared/Game/GameSession.cs`：其余玩家文字及认知/日常叙述模板迁入文案。`MorningScriptWorld.cs` 直接保留习惯投影引用，不再按显示名称寻找投影。
- `godot_csharp/AGENTS.md`：仅同步重演协议和句尾含义字段的工程规则。生成物通过 `compile_story.py` 更新，没有手改。

## 中文字面量审计的保留项

已扫描 `scenes/Awakening` 与 `scenes/Shared` 的 `.cs` / `.gd`，区分注释、生成物、文案键、身份/协议键和人工编写的显示正文。

- 文案键、模板槽名、流程/阶段/站位名称、节点名、动画动作键、画像配置字段和状态 Tag 保留。它们是协议身份，显示文字由剧本或 `FieldVocabulary` 提供。
- `Night/NightReplay.cs`：位置异常、带数值的社交判定日志与失败日志属于开发诊断。
- `Night/MorningEcology.cs`：内部观察名、画像覆盖键及信念数值格式属于认知身份或开发记录。
- `Script/Chapter1Session.cs`、`Chapter1SessionFlows.cs`、`Chapter1SessionProbe.cs`、`Chapter1Speech.cs`：未知协议/探针/跳转、缺少回应等技术异常与详细推理诊断保留；正常玩家操作的守卫提示已迁入剧本。
- `Shared/Story/CoreScriptWorld.cs`、`WorldSession.cs`：灵魂投影的内部名称后缀保留。`StoryRuntime.cs` 的异常属于框架开发诊断。
- `Shared/Audio/GameAudio.gd` 的缺少音频警告，以及 `Shared/Audio/audio_check.gd` 的断言文字保留。
- `Awakening/check/**`、`Shared/check/**`、`Shared/Story/check/**` 的测试输入、断言、测试日志和文字断言保留，不进入正式玩家会话。
- `Shared/Story/Examples/ExampleWorld.cs` 是 `Shared/Story/check/ExampleScript` 使用的独立测试示例，其样例文字保留；`Examples/WorldScript/Host/WorldScriptPreview.gd` 的标题与诊断文字仅用于作者预览。
- `Night/DetectivePlanning.cs` 的动机关键词匹配仅由 `Awakening/check/DetectiveSimulation` 使用，属于检查模拟器的文本分析；本轮不改该模拟器。它不是正式玩家流程的行为裁决依据。
- `Chapter1CompiledDialogue.cs` 是从 `.story` 自动生成的对白容器，保留生成内容；人工创作文案仍只维护在 `.story`。

## 验证边界

独立 Agent 的验证结果与日志见同目录的验证报告。全量重演基线来自本轮前 HEAD，覆盖 216 组合、1966 帧、7864 条心声。比较必须逐字核对玩家可见输出，并检查时序、痕迹与结构化标志。

本轮不执行一级三份全程记录、二级 UI 图片验收或三级盲玩；这些级别均未验证。自动注入状态的协议检查不能作为游玩证据。没有依据本轮工作评价人物行为审美或整体游玩体验。
