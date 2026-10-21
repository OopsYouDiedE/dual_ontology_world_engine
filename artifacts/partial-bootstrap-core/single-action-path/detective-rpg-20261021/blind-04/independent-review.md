# blind-04 独立证据审查

审查日期：2026-10-21。新独立审查上下文，仅检查 blind-04 的实际输入、玩家可见输出、正式回执、现场投影与关闭证据，以及二进制读取工具。未读取设计文件、生产源码或其他轮记录，未启动会话、修改源码。以下是本窗口结论，不是全局游戏验收。

本轮有真实离开 R05、进入 R08、首次有效检视 E6、返回走廊和进入 R07。走廊向左的尝试确实停在画面左缘，没有形成正式 corridor.hall 请求。重演、作答被接受和章节交接均未发生。没有“05 取消后再试”，因此不能称该项重试验收；另有一次 R06 入门取消，不能替代。

## 正式节点与可见结果

| 节点 | 本轮证据 | 结论与边界 |
| --- | --- | --- |
| 离开 R05 | inputs.dvlog 的唯一 `detective.move.R05.corridor` select；action31 applied，serial32，actor_instance_id172788552505，实际终点 [2408.01733398438,1058.16345214844]，目标 zone_yi:corridor；正式呈现 e352，游戏08:03 | 已正式到达走廊。host-events 投影时间119691ms；frame-000130246.png 可见走廊08:05。不能把首次选看的截图时间当作首次完成时间。输入起因是105957ms鼠标接近门，125229ms的S按键发生在该正式呈现之后；盲玩笔记“再按S才穿门”的因果说法过粗。 |
| 误触 R06 门道 | `detective.move.corridor.R06` select；action62 rejected，serial63；reason released，continuous_cancel identity_verified=true，留在 zone_yi:corridor；e663 游戏08:05 | 存在源侧释放取消，未进入R06。没有同动作的再次select及成功完成。 |
| 进入 R08 | `detective.move.corridor.R08`；action90 applied，serial91，沿用同一actor_instance_id；实际终点[2168,958.005310058594]，目标zone_yi:north_2；e968 游戏08:07，host-events 365958ms | 正式到达，后续截图及乐谱现场一致。 |
| E6 首次有效检视 | 唯一 `detective.inspect.E6` select，site E6，现场主体处于zone_yi:north_2且near_sites含E6；action103 applied，field contact=true/site=E6/executor=dv-world；e1113 游戏08:08，host-events 390989ms | 有成功正式检视和实际可见乐谱正文，达到本节点。未把它写成推演正确或答案已知。此回执没有显式站点范围、到达误差及实际终点数值，不能由本证据补充这些数值合同结论。 |
| 出R08、入R07 | action111/serial112 applied到走廊，e1218 游戏08:10；action115/serial116 applied到zone_yi:south_2，e1278 游戏08:12 | 真实房间交接已发生，最终仍是第一章活动会话、等待玩家输入。 |

时间均按各记录自身字段报告。host-events 的 real_ms 与 interaction-timing 的 real_us 起点不同，未拼接成统一精确时钟。

## 左侧移动与推演界面

run/input-events.dvlog 记录214704ms按A 5000ms、239390ms按A 4000ms及相应release。render-state 投影显示主体由右侧沿走廊移动，后续停在[2049.26440429688,1089.23413085938]，屏幕横坐标约2.53；第二段A期间仍停在该点。frame-000246632.png中人物确实被裁在左缘，画面没有清晰可辨的前进出口。

interaction-timing 只有一次面向zone_yi:hall的continuous_gate_wait（real_us219790069），全轮没有对应continuous_request_begin；inputs中没有`detective.move.corridor.hall` select、回执，正式呈现中也没有该动作。候选选项中出现该动作，不等于请求或通过。现有证据足以确认未正式进入hall，不足以仅凭画面确定工程根因。

504152ms按R打开推演工作台，frame-000518232.png清楚显示“尚未执行重演”“当前没有可进行的重演”“当前没有可显示的推演假设”“尚无个人批注”。这只是空面板检视；没有实际重演、正确推演结果、答题提交或答案接受。最终chapter=1、chapter_summary=false，无章节交接。

盲玩者操作间有阅读、看图和决定下一输入的空档。本审查不把这些间隔称为游戏CPU卡顿；未读取性能跟踪得出CPU或GPU瓶颈结论。

## 实际图像核查

实际打开并观察四张原图：

- `run/frame-000130246.png`：乙区宿舍走廊08:05，主体可见，05/06/07/08门及门道组合可辨。
- `run/frame-000246632.png`：走廊主体被裁在左边界，底栏通用输入提示可读，左向去路不清。
- `run/frame-000416375.png`：苏泠卧室R08、08:08，乐谱“弱音问号、重读开门、小节两遍”完整可读，人物、书桌与叙述框可见。
- `run/frame-000518232.png`：推演工作台空状态，摘要、假设、个人草稿及合上入口可见。

以上只覆盖选看截图与相应输入/回执，不声称连续观看全部移动、碰撞与每一帧布局。

## 关闭与证据边界

result.dvlog记录capture_end_real_ms=600002，开局游戏时间480、结束492，观察游戏跨度12分钟；host配置实际playback_speed=1。窗口自然截止时仍active，不是剧情终局。最后实际输入576969ms，最后盲玩选看截图588424ms。capture-close记录602024ms已关闭归档和宿主、archive_errors为空；close-result记录complete=true、audit_mode=true、trace_policy=full、pending_action为空、unprocessed_commands=0、errors为空。退出码0。

本目录全部 `.dvlog` 逐帧读取未发现二进制帧校验错误。另独立校验session.dvdag及save.dvsave的序号、prev和SHA-256链，未发现链错误，二者均有seal/final_checkpoint。未发现 `.partial` 文件。

但关闭成功与证据内容完整必须分开：session.dvdag只有header、node、initial/final checkpoint和seal；initial和final均root58649，seal明确events=0。save.dvsave也有events=0。它们字节链完整，不足以据此宣称保存了全轮原始正式事件链。正式行为结论在本审查主要由inputs回执、host-events呈现、可见记录和截图交叉支持；未反序列化并独立验证DAG中全部状态语义及恢复能力。不能以close-result的complete=true掩盖该限制。

审查已完成并关闭，可将本报告及本轮完整证据目录一起携带。没有遗留会话或需要继续运行的审查动作。
