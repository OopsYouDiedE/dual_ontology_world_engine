# full-process-3600s-r3 独立核验（2026-10-19）

本次仅只读核验现存证据与实际PNG，不重新运行游戏，不改写Gemini原记录。执行身份为引擎侦探的Godot三级会话，不是独立盲玩。本报告不能将不完整证据补成完整三级验收。

结论：可确认本轮采用controller=ai、dv-speed=4、真实Godot宿主，并推进到7200游戏分钟。不能支持“两章全部剧情完整跑通、投票结算完成、无丢帧、全程无错误、商业游戏呈现”的结论。

## 时域、事件与实际剧情

- launch.json记录上限3600秒，并不表示实际运行3600秒。result.json的capture_end_real_ms=1281861（21分21.861秒）；launcher_elapsed_seconds=1310.172，含封存和分析收尾。
- measurement.jsonl首个有效时钟样本为4071ms/480分钟。首次7200样本前后为904948ms/7196与905151ms/7200。故首次时域到达在启动后(904.948,905.151]秒；相对于首个有效时钟样本为(900.877,901.080]秒。不能把983秒写成首次达成，更不能以时域替代剧情完成。
- active=false首次采样907856ms；host-events.jsonl第3468行在907815ms交付active=false。它证明会话停止，不证明投票或剧情完成。
- end.json终态：council_closed=true及underground_tremor=true，写入游戏时刻6330；council_ended=false、contract_joined=false、astral_draft=false、counter_open=false、medicine_key_given=false、reserve_added=false；ledger各项均0，counter_remaining仍99。
- 16人均signed=false、voted=false；ballot均known=false。不能称“投票表决完成”。全员饥饿=true与震动=true只是实际发生的局部状态。
- 最近终态dv_present的execution_event=e17872，是事件ID，不能作为17872个真实行动步数。host-events现存交付含1735个dv_present，包含NPC和demon，且中间存在写盘缺口；不据这些计数评价游戏，也不把它当完整行动统计。
- 第一章确有接受回执：host-events第373行116955ms/594分/e2520；第381行123663ms/605分/e2619；第409行142555ms/631分/e2879；第433行170535ms/654分/e3132。报告把177.1秒/656分统称推演通过，混淆了接受与开闩正文。具体“正确推演首次完成”还需原始推演证据，不由开闩图片反推。
- isolation_open首次采样在181188至181390ms之间；living_hall:hall首次采样183265ms/661分。当时chapter仍1；chapter=2首次采样273262ms/1830分。真实进门与内容章节字段切换应分别报告。
- 500089ms的实际game_time=3361（第3天08:01）。HUD仅显示8:01，不能仅凭HUD解释为第1天或完整中期调查达成。

## 封存与画面完整性

- result.json: exit_code=2，不是正常无错结束；source_changes=[]，只能说明该次封存前后所列源未变。
- capture-close.json记3077条archive_errors，首条end_us=388009217，错误为磁盘空间不足，涉及PNG、measurement、host-events、live-timing、render-state等。错误持续到1205360347us附近。错误数量仅用于说明封存缺陷，不是游戏测评计数。
- 正式PNG实际5603个，另507个.png.partial。frames.jsonl的最大已留存采样间隔为417509至480472ms，相差62.963秒。存在实质采样缺口，“4781张且无丢帧”不成立。
- 宿主20261019-083437-5545/close-result.json明确complete=false，trace与save因磁盘不足失败；session.json.partial约1.095GB，save.json.partial为0。另一关联宿主20261019-081346-8597同样只剩partial，缺close-result。result列出两个new_host_sessions，采集器未生成唯一host-session副本。现有材料不足以建立完整原始会话封存。
- godot-stderr还包含“Parse JSON failed. Error at line 0: Invalid escape sequence”。即使未发现操作系统级崩溃证据，也不能表述为全程无报错。

## 实视PNG

实际查看：frame-000500089.png、frame-001092815.png、frame-000177147.png、frame-000183869.png、frame-000905151.png。

- 报告的frame-000177182.png与frame-000183864.png不存在。最近现存图分别为177147与183869；本报告明确使用替代近邻，不伪称查看不存在的图。
- 177147图HUD10:56，正文为“外侧的横闩，被一只手抬了起来。”；画面可证开闩呈现，不能单独证首次正确推演或正确答辩接受。
- 183869图是起居厅，正文“跨过门槛，走进起居厅。”，不是报告声称的“中央走廊”。
- 500089和1092815图中人物在左右门口成纵向堆叠，身体与头像重合，画面无法独立辨认全部16人。不能据隐藏people数组称16人全部清楚可见或站位自然。
- 905151图HUD0:00，正文“留在原处，等待本人已知的下一处时刻。”。该帧没有投票结算或终卷呈现；标题“安眠者有福”此前常驻，也不是终幕达成证据。
- 图片能说明部分道具、HUD与对白确实显示；静态抽样不能证明全程无撕裂、无遮挡、碰撞全程正确或达到商业游戏标准。

## 性能统计的有效范围与误读

- hitch-report中3105个>=50ms绘制间隔与各分桶确实可复算，但时间范围约0.917至1279.477秒，覆盖整段21分钟采集，包含会话停在7200后的区间；不能写成“16分钟全剧情内”。写盘缺口使它也不是完整连续区间。
- 最大1390.019ms间隔发生在167.662至169.052秒；588.182ms发生在745.273至745.861秒。前者不是启动冷初始化，后者不是约183秒首次进起居厅。报告对这两段根因的指认不成立。
- DV_choice 57.280s、DV_goals 41.083s、visible_trace 39.459s、capture_readback 27.057s、DV_cognition 23.123s等可从exclusive_wall_ms复算。该脚本按重叠墙钟区间及优先级分配，自己明确声明不是CPU或因果证明；并发后台DV与主线程工作不能由这个归类直接解释掉帧根因。
- visible_trace的实际源码是PlayerViewTrace.gd:_after_draw/_collect_after_draw：扫描已绘制UI、遮挡、文本、控件与场景对象，留存玩家可见记录。它不是对脚印/磨损/灰烬的玩法视野检测。依据这个名字提出“空间分区过滤足迹”会修错系统。
- launch/profiler-stdout明确托管采样只有300秒，约覆盖启动后的前5分钟；不是整段905秒时域推进，更不是1282秒采集全程。采样包含等待函数，线程等待所占采样比例不能换算为CPU利用率或“CPU63.6%闲置”。
- managed-hotspots.txt后续确实有独占/包含列表，列出的Godot跨语言调用与RecordTiming值得进一步定位，但单份Top25不能证明全部GC耗时、更不能排除DV在未采样后半程或主线程关键路径上的开销。DvSession.Run包含3.57%不能解读成“完整游戏纯逻辑计算只占3.57%且极为高效”。
- result.json保留managed_report_exit_code=3762504530、managed_report_readable=false；后续热点文件存在是另一次分析结果，应另外记录命令、时间及成功证据，不能静默把原result当成成功。

## 应采用的结论

该次引擎侦探在Godot四倍运行中观察到第一章多次答辩接受、实际进入起居厅，并在约905秒达到五日时域。第二章终态仍有多项工作、签署与投票未达成，画面存在人物重叠；磁盘满造成长采样缺口和原始会话封存失败。因此不能宣称两章完整剧情通关、无错、无丢帧或完整三级验收完成。

本次为历史证据核验，没有重跑一级、二级或三级。完整剧情行为链、未知动态分支与缺失原始轨迹保持未验证。
