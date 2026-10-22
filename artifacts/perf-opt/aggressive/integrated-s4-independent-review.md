# integration-S4 独立正式会话审查

审查对象：主仓 `artifacts/perf-opt/aggressive/integration-S4`，任务指定源码 `5b0aeb0d`，Foolish_Detective / zone_yi。采用全新审查上下文；未读取设计文件，未构建、启动会话、执行全因果扫描或重新扫描完整 DAG。依据现有 actual/raw-event 索引定位，再读取 stdout 中对应原始事件投影及玩家可见正文；未把审查当作 Godot 画面、输入或碰撞验收。

本轮支持以下有限结论：本人拒绝和他人异议实际执行；雀儿实际开外闩后，侦探仅获得声响观察，再到门厅检视后获得外闩状态，随后拔内闩、开门、跨出；正文声响中性。侦探活着到达第二章末的实际检视。不能据此宣布整体通过：S5 两死是本版本已知未修问题，本轮 S4 不覆盖或消除它；端到端也没有达到 180 秒。

## 拒绝、异议与答复边界

- e852，游戏第1日08:08（minute 488），过程 3699.8053 ms：`detective.inquiry.decline-cooperation` 已实际执行；可见原文为“查管道、重演这些事我不做。你们去查，结论告诉我就行。”
- e1061，第1日08:12（492），3869.9528 ms：苏泠 `su.inquiry.object-refusal` 实际执行。
- e1093，第1日08:13（493），3904.2890 ms：霍铎 `huo.inquiry.object-refusal` 实际执行。
- e1723，第1日08:24（504），5450.1365 ms：沈鼎 `shen.inquiry.object-refusal` 实际执行；侦探可见正文为“大家还关在里面，你倒把活全推给别人。这样搭伙，我可不放心。”

actual-event 索引中侦探未出现 answer 类正式 execution；乙区也未出现带 yi 的 answer execution。结合其可见正文，本次不存在侦探完成两问完整正确答复的证据。开门不应写成其正确作答被接受；其他区的人物答复不是乙区侦探的答复。

## 开闩、声音与现场检视

| 事件 | 游戏时刻 | 过程毫秒 | 实际记录 |
| --- | --- | ---: | --- |
| e2370 | 第1日13:17（797） | 6574.6827 | 雀儿 `quer.departure.pick-outer`，status=applied |
| e2382 | 第1日13:17（797） | 6577.3718 | 侦探仅得 `departure.door.latch_sound=true` |
| e2404 | 第1日13:19（799） | 6604.0655 | 侦探实际到门厅 |
| e2454 | 第1日13:20（800） | 6632.8644 | `detective.departure.inspect-door`，applied |
| e2481 | 第1日13:21（801） | 6684.4386 | 实际拔内闩，applied |
| e2509 | 第1日13:22（802） | 6740.2020 | 实际开门，applied |
| e2543 | 第1日13:24（804） | 6773.3609 | 实际跨出至公共起居厅，applied |

e2371 是雀儿本人观察，含 `outer_released=true`、`picked_by_quer=true`、`living_exit=true`；这些不属于侦探。e2374 的 listener_parse observations 为空。侦探的 e2382 observations 只含 `latch_sound`，没有外闩状态或雀儿身份事实。直到现场检视后的 e2455，侦探才直接观察 `outer_released=true`、`inner_down=true`、`open=false` 及 `living_exit=true`。

玩家可见声音署名“现场声响”，正文“门外传来几下金属拨动声，接着是一声闩杆碰响。”它没有写出雀儿、开闩者身份或“外闩已开”的结论。内部 e2382 的 actor/action label 仍带实际来源，但这是调试证据，不是此处的玩家正文。上述证据支持本片段的知识传播边界；未进行全历史知识因果穷举。

## 存活、结束检视及封存

侦探第一章真实跨出（e2543）后继续第二章，并在 e38939 实际执行 `detective.c2.daily.closing.oath_court`，第5日09:31（6331），126212.5437 ms，status=applied。e38940 在126221.5910 ms提供实际现场观察：`council_closed=true`、`council_ended=false`、`underground_tremor=true`。玩家正文确实说明议事时段已结束而签名未齐，未把未齐事项写成一致达成。result 的实际状态摘要中 detective alive=true；本轮摘要没有死亡人物。此结论仅针对 S4，不能替代已知 S5 两死失败。

result 停止原因同时为 `objective-chapter-two-closing-record` 与 `actual-viewer-closing-inspection`，不是只算到章末而未检视。现有 raw-dag-summary 报告38942个提交事件、final checkpoint、seal、无残尾；stdout 投影覆盖38942事件，无未投影提交尾。此处引用已有封存检查，没有重新认证全 DAG。result evidence_complete=true，但 causal_check_complete=false，既有因果分析1秒超时；不声称完整因果检查通过。

构建26.047秒，正式执行126.703秒，后处理145.953秒，三者合计298.703秒（约4分58.703秒）。126.703秒仅是正式执行；即使执行低于180秒，也不能报告端到端180秒达标。

所有读取均由已退出的短时进程完成，无保留文件句柄或后台审查进程。
