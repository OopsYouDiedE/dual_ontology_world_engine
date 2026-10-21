# 本轮独立正式会话证据审查

2026-10-21。全新独立审查上下文，仅检查本轮封存的输入、可见记录、正式事件、截图和性能报告；未读取设计文件、生产源码或其他轮结论，未启动会话。这是对引擎自身侦探的有限实机诊断记录审查，不是独立盲玩。

## 行为与关键节点

19 个侦探 applied 由原始 `run/host-events.dvlog` 的 dv_present 独立计数，并逐一核对其 execution_event 存在于封存 DAG 的 action_execution；它们包括12次 inspect、4次 move、1次 talk、1次 sound_test、1次 replay。这个计数只表明正式行动执行，不能等同19次谜题进展、19个正确推论或章节达成。

| 节点 | 本轮证据与边界 |
| --- | --- |
| 首次有效检视 | 开场醒来 e137（游戏08:01、宿主送达5.234s）已有具体正文；自身镜片检视 e360（08:03、13.463s）明确显示刻痕与图记吻合。若限定案件现场线索，首次为 E7 e1136（08:08、21.920s），正式 execution.status=applied、action104，并在visible记录frame2802—2859逐步显示石板正文且最后complete=true。三种口径应明确区分。 |
| 正确推演 | 未建立。e1883（09:13、85.401s）为 replay 正式执行，action147 的 execution.status=applied；正文为“这次重演尚未形成能够完整说明现有观察的解释。”visible frame11445—11530最后complete=true，86.591s截图显示完整句。原始1943个事件中的10个 inference 均属于其他主体，没有侦探 inference 事件；一般 inference 事件存在也不能替代正确重演。 |
| 正确作答被接受 | 未发现证明。已送达presentation均judgment=false；不把judgment的真假单独当作正确性标准。侦探19项内没有可据以成立的正确作答接受正文或结果。 |
| 实际章节交接 | 未发生于本窗口。初始至末尾快照chapter=1，末尾chapter_summary=false、isolation_open=false，游戏钟09:14。不能据此推断整局无法交接。 |

现实秒是宿主交付边界，不能写成第一可见帧或精确执行完成时刻。游戏时间按正式记录读取，不由四倍速换算。E7、E8与E1—E5的现场检视提供了实际线索；声路试验正文仍要求询问实际在场者，不能将试验执行当作已获得所有证言。

实际输入共444条：配置1、start1、view_ready1、dv_complete153、dv_next153、keyboard90、control_signal45。配置明确controller=ai、playback_speed=4；其中部分dv_next带reading-background调度。审查没有代选行动或注入状态，也没有将后台NPC推进归为同条件纯计算优化。完整路线、碰撞和每次门禁正确性未逐帧穷尽核查。

## 截图实际核查

以下5张均通过图像工具实际打开，引用为本轮绝对路径。

- [22.026s活动厅](C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/partial-bootstrap-core/single-action-path/detective-rpg-20261021/perf-integrated-90s-x4-03/run/frame-000022026.png)：活动厅场景与四个人物可见，侦探肖像、姓名及继续按钮清楚；正文区此刻为空，符合刚开始呈现，不能单凭此图证明已读完E7。
- [24.039s石板正文](C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/partial-bootstrap-core/single-action-path/detective-rpg-20261021/perf-integrated-90s-x4-03/run/frame-000024039.png)：同场景正文可读，显示石板第一行符号；这是正文显示证据，完整多段内容仍须结合visible记录。
- [30.093s铜口检视开始](C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/partial-bootstrap-core/single-action-path/detective-rpg-20261021/perf-integrated-90s-x4-03/run/frame-000030093.png)：正文只有“铜”字，属于打字中；不能把正文尚短或人物静止称为计算卡住。
- [84.969s称谓问题](C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/partial-bootstrap-core/single-action-path/detective-rpg-20261021/perf-integrated-90s-x4-03/run/frame-000084969.png)：对白姓名栏直接显示英文内部标识“demon”，正文为“昨夜警铃骤起。主楼曾有人向诸位呼喊。”。visible记录frame10198至11286也含该姓名。这是实际玩家可见问题，不能由无错误日志或正式链已执行掩盖；本审查不根据设计推定其应显示何名。
- [86.591s重演完整结果](C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/partial-bootstrap-core/single-action-path/detective-rpg-20261021/perf-integrated-90s-x4-03/run/frame-000086591.png)：侦探姓名及完整失败重演正文清楚，无末句裁切。该图比打字中的85.988s图更直接证明本次未形成完整解释。

纸页覆盖了画面下部；已查看图中未见正文溢出或核心按钮被遮住，但不能以5张截图代替全场景、全部布局、碰撞与连续输入验收。

## 封存、时限与一致性

已通过binary_evidence读取校验二进制帧，并独立重算session.dvdag全部73,599帧的序号、prev链及SHA-256，未发现失败。含73,595个DAG节点，initial/final检查点及seal齐备；其根events字段保留1,943个正式事件，末事件e1942为session_stopped，reason=host-closed-during-active-session，actions_executed=153。顶层seal的events=0是导出检查点包装计数，不能误报为没有原始正式事件。

close-result为complete=true、audit_mode=true、trace_policy=full、unprocessed_commands=0、errors=[]；capture-close的archive_errors=[]。这些支持记录已关闭与封存成功，不等同剧情或所有行动完成。关闭仍有pending_action=action153，末尾是prepared的霍铎回应；不能补写它已Complete。save文件存在，但本审查没有执行恢复，也未独立重放存档。

capture_end_real_ms=90001，archive_and_host_closed_real_ms=91477；封存尾部1.476s与90秒行为窗口分列。启动器94.907s是另一口径。首个游戏钟采样2.956s为08:00，末尾09:14，实测推进74游戏分钟。停止是授权时限结束，不是行动上限、时域终点、剧情达成或整局完成。

source-before.json与source-after.json逐项相同，result.dvlog的source_changes为空，清单含运行程序集哈希；launch记录commit=9f434197e0bb8d32c401b442aac924ad7b5e2bea。可支持所列文件及DLL在本窗口前后一致。本审查未读取生产源码，也未独立重建程序集；性能报告另引的2252文件freeze/preflight/postflight及旧携带记录不在本次独立复核范围，不将它们当成自己已验证的事实。

## 性能报告表述复核

已读取父目录performance-integrated-report-03.md。其明确将19项applied与谜题进展区分，区分时限、关闭及剧情完成，并将正确推演、正确作答接受与交接标为本窗口未建立；与原始证据一致。阶段墙钟的嵌套/并发、阅读与真实现场等待、截图采集开销、托管等待样本及CPU利用率均作了适当区分；没有宣称同条件提速、整局达成或独立盲玩成功。

本审查未重新计算全部性能阶段聚合、采样栈归因或读取实现，因此性能数值与源码级热点解释仅属于报告作者的分析，不能转述为本审查独立证实。报告关于无Godot错误行与退出码0属于工程事实，不代表体验无问题。建议最终汇总同时引用上述“demon”可见姓名问题，并用86.591s完整正文截图补充重演节点，保留现有原始图与记录。
