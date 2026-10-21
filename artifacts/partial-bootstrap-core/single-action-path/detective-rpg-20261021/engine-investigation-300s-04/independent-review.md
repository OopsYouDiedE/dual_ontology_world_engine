# engine-investigation-300s-04 独立正式会话审查

审查日期：2026-10-21。本审查使用全新独立 Agent，只读取本轮封存、输入、可见记录、宿主事件及读取工具；没有读取设计文件、生产剧情源码或其他轮记录，没有重开会话或指导输入。原始事件中携带的来源路径仅作证据元数据，不作为设计依据。本审查不是独立盲玩，也不是性能测量。

## 结论与实际节点

本窗口确实提出了始业问题，侦探本人取得了问题；侦探进行了可见检视和一次未成功的完整重演，并有两个分问获得正式接受。窗口结束仍在第1章，第三问“原话是什么？”之后没有侦探作答接受或章节交接证据。关闭成功不能解释为剧情完成。

| 节点 | 本轮证据与判断 |
| --- | --- |
| 问题实际提出 | e2513 / demon.question.prompt，游戏569分钟（9:29），宿主投影126801ms。可见帧17341完整显示“门是谁闩上的？闩门之时，此人可曾听见门外的呼喊？”。 |
| 侦探本人听取 | 原始 e2581、e2582，游戏570分钟，actor=detective，public_topic_heard 分别为 original_q2_complete、identity。与问题可见正文共同支持本人实际取得问题，不能仅凭公共世界问题字段代替本人听取。 |
| 首次有效检视 | 若含开场辨认，e360 / identify-own-lens 在游戏483分钟已出现放大镜刻痕观察。若限定明确 inspect 案件动作，首个是 e1136 / detective.inspect.E7，游戏488分钟（8:08），宿主呈现22052ms；正文为石板两行符号，并进入本人的封存案簿。action104 有 applied Complete。它早于始业问题，不应写成“听题后首次检视”。 |
| 首次模型／正确推演 | e2843 / detective.replay，游戏599分钟（9:59），宿主呈现143476ms，正文明确“尚未形成能够完整说明现有观察的解释。推演总预算已耗尽。”可见帧19377有完整正文。本窗口未到达可确认的完整正确模型；正式事件类型有129次 model_argument_unavailable，没有 model_found。两次分问接受不等于完整重演成功。 |
| 首次答辩接受 | e2913“是我闩的门。”，游戏611分钟（10:11），宿主呈现146142ms；e2915 answer_accepted，accept.identity，literal_truth / epistemic_support / question_match / answer_complete 均为 true，law_ready=true，truth_level=1。可见帧19864完整显示接受文字。 |
| 第二次答辩接受 | e2995“声音来自铜口，经管道传来。”，游戏622分钟（10:22），宿主呈现152534ms；e2997 answer_accepted，accept.voice_source，同样有正式正确性与支持判定。可见帧20727完整显示接受文字。 |
| 后续问题 | e3030 游戏623分钟提出“原话是什么？”，e3060 是侦探本人 words 话题听取；e3049 本人复问的正文可见。之后没有侦探 answer_submitted / answer_accepted。 |
| 章节交接 | 未到达。结束投影 chapter=1、chapter_summary=false、active=true；没有本轮实际章节交接证据。不能据此推断整局无法交接。 |

表中现实时间是宿主捕获的 real_ms 呈现时间，不是由四倍速换算；游戏时间保留事件本身字段。Prepare、执行、呈现可能有不同时间值，表格不把报价时间当成完整费用结算后的时钟。

## 接受次数与移动复核

“2次 answer_accepted”必须限定为侦探本人。独立遍历 session.dvdag 的顶层 events 列表得到6218个事件，全主体 answer_accepted 实为4次：侦探 e2915/e2997，以及 ward 的 e5763、gu 的 e5776（后二者游戏850分钟、contract.identity，各自 accepted_listeners 仅本人及 demon）。后二者不能计为侦探解答，亦没有在本轮玩家案簿中提供新的侦探答辩进展。

从 host-events.dvlog 独立筛选 actor=detective、status=applied 的 dv_present，并以 action_id 区分方向，hall→corridor 有72次，corridor→hall有72次，合计144次已执行呈现；这是144条移动边，不能说成144个完整往返。首条为游戏487分钟、21443ms；末条为游戏869分钟、298825ms。游戏623分钟之后，侦探的123条已执行呈现全部属于这两方向（62及61），没有新的侦探检视、答辩或重演呈现。因而本窗口后段呈现出重复导航且没有已记录的侦探问题进展；次数本身不评价玩法质量，也不能替代失败原因调查。

输入流独立读取到408个 dv_complete、408个 dv_next、136个 keyboard、68个 control_signal，以及一次 host_configuration/start/view_ready。配置为AI控制、四倍速；键盘记录包括对话面板 Space。没有人为路线或答案注入的审查行为。Prepare有409条而完成/执行呈现为408条，尾部待办需另列。

检视及答辩所核回执（action32、action104、action207、action210、action213）都是 executor=godot、outcome=applied，但 field.executor=dv-world，site为空、contact=true。因此本轮支持正式采纳和可见观察，不能仅据这些回执证明每次检视都完成了显式站点接触、到达误差和真实碰撞核对。本审查没有补做交互或几何验收。

## 实际截图核查

实际打开并查看了以下4张原始PNG：

- run/frame-000022096.png：活动厅8:08，场景、人物和侦探对话面板可见，此张处于文字尚未显出的开头，不能单凭该图证明石板正文已读；正文由后续可见记录及案簿补证。
- run/frame-000127967.png：活动厅9:29，沈鼎酵种台词完整显示，正文、角色立绘与继续按钮可见，没有明显遮挡或裁剪；此图不是始业问题本身。
- run/frame-000152828.png：活动厅10:22，侦探“声音来自铜口”正在逐字显示，验证第二次答辩确有画面呈现，但接受文字的完整显示由可见帧20727补证。
- run/frame-000299823.png：HUD为“乙区·宿舍走廊”14:31，但画面仍显示活动厅内的书架、长桌与多人。尾部有待执行 corridor→hall，且接近捕获终点，单张图不足区分房间过渡时序与长期错位；不能用这张图宣称几何状态完全一致。

截图只覆盖所列时点，没有检查所有帧、所有碰撞、所有输入或全局UI组合。

## 关闭与证据完整性

独立使用 RawDagIndex 校验 session.dvdag：SHA-256为 fff64d39294c15d04f46c2c7af6a9780766821c635f1d542a6ac5a86cf81299a，70538030字节，165237帧、165233节点；initial/final checkpoint与seal均存在，无残余尾部、未提交后缀或invalid_tail。这里是完整值导出，journal committed_events=0，而顶层 events 实有6218条；不能拿journal计数直接宣布无会话事件。现有 investigation-selected-events.dvlog 的空 events 因而不能作为行为审查的完整事件选择结果。

原始末事件e6217为session_stopped，reason=host-closed-during-active-session，actions_executed=408。close-result.dvlog记载complete=true、audit_mode=true、trace_policy=full、unprocessed_commands=0、errors=[]，同时pending_action=action408。结束快照中该动作是prepared的detective.move.corridor.hall；无对应Complete，不能将它算作第409条完成。capture-close记载300004ms停止捕获、305557ms完成宿主及归档关闭，archive_errors=[]。存在save.dvsave、案簿、inputs/visible等最终文件，未见本目录partial尾文件。上述证明本轮关闭及所读封存有完成记录，不证明剧情已结束，也没有实际恢复存档检验。

## 复算范围和未验证范围

本审查独立复算了正式事件类型、侦探接受范围、两方向移动次数、后段动作类别、输入类别，并独立校验DAG封存。未独立复算CPU/GPU、帧耗时分位数、托管热点、性能分段或源码指纹差异；性能Agent报告中的这些数值不因本审查而获得第二份独立性能验证。没有运行工程构建、流程回归、游戏复测或存档恢复。

未到达：完整正确模型、第三问答辩接受、实际章节交接。未执行：新会话复测、盲玩、恢复检验和全局几何/UI检查。证据不足：单凭普通检视回执确认全部显式接触条件、单张尾图定位呈现错位原因、未完成计算的内部结果。它们分别保留，不能合并为“全绿”或“整局失败”。
