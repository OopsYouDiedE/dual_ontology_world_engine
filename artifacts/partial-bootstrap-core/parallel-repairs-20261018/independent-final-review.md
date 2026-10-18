# 第四轮最终独立审查

审查范围：只读当前机制源码、适用 AGENTS 流程及本轮工程证据；未读取设计规范、design-review 或权威答案，未运行游戏、prepare/complete 或流程回归，未替角色选择行动。最终正式动态结论见本文末段，依据主代理通知结束后的本轮原始证据。

## 工程审读

`core/00-data.dv` 的 dv_read 在缓存前保持 subject、world.objects、ref.object 的原始读取顺序。缓存以完整 world 值为所有者、完整 ref 为键；返回完整字段单元，未丢弃 confidence/time/basis/未知扩展字段。memo 命中经 copy_value 返回，首次返回仍是原 dv_clone 的独立副本。嵌套变更经 Runtime 父链失效传播清除 Backbone；ref 变化也改变键。

constant 快路仅适用于 list 所有者和非 list 常量值。非 list 所有者仍进入 memo 的 Expected List 首错；list 常量保留首次返回原值、后续返回独立副本的旧 ABI。all 的后项仍求值，前项 false 不吞掉后项结构错误。

`15-action-planning.dv` 的 topic 缓存在原 item→question→policy 读取后查询。完整 mind 所有者覆盖 question 和绑定条件；键另含 item、question_ref 的存在性和值、完整 bindings 和 heard_topics。dvs_topic_was_heard 只读 heard_topics；dvp_question 的外部依赖均已包含。personal/basis 快路保持原先顺序。未发现外部输入遗漏。

Runtime 字符串成员快路只对冻结的全字符串列表使用 Ordinal HashSet；混合数值/嵌套容器回退 same_value，因此未以结构哈希错误排除精确数和浮点数等值。record_with_many 先冻结 updates，保留旧记录顺序、重复键全部更新及末更新优先；字符串集合仅在此前键均为字符串时用于去重。copy_value 的根独立，后代读取链接父节点，push/replace 使相关祖先缓存失效。

opening 的 condition 缓存按完整条件值归属，不缓存本人资格或未来结果。白天边界修改属于行为条件变化：声明 daytime_wait_chunk_limit 且尚在 08:00 前时，将等待限制到边界；不是纯提速。既有本人较短 cap 仍优先。

## 中性证据及限制

pure-cache-probe-04 的原始探针覆盖 question 字段变化、争议值、heard_topics、bindings 的 basis/完成条件嵌套变化、跨 actor 共用 mind、返回 cell/basis 修改隔离、world/ref 变化及 list constant 首返/后返别名合同。其错误记录分别保留跨主体、objects 读取优先、非法 objects、缺 field、非 list owner、缺 op/value、all 晚项缺 op、binding 缺 completed_when 的旧新首错。审查逐项查看源码和原始结果，不以汇总数量作评价。

abi-optimized-probe-20261018 的 differential 记录涉及 Unicode 字符串、精确/浮点数、嵌套记录、冻结/可变/副本输入及 malformed record 首错；result 明确 formal_session_executed=false，保存精确数格式、深层嵌套、独立副本和已有 trace/partial 字节。这是工程证据，不能建立人物行为或剧情完成。

wait-window-probes-01 的 normal.retry.stdout 明确旧 180 分钟等待改为 120 分钟，边界前一分钟采用 1 分钟，原 60 分钟 cap 保留，未声明白天策略仍为 180；original_actor_unchanged 为真。condition 返回副本修改后缓存不受污染，改变完整条件得到新边界。缺 not.item 与 all.items 的旧新首错分别一致。此处只验证中性时间合同，实际机会是否被本人采纳须依据最终正式轨迹。

## 正式窗口

最终原生会话结束后的审查记录见下文。此前 run-180s 初始化异常、round2 原生窗口与 profile-round3 插桩诊断独立保留；profile 最后采样覆盖 150 秒，全进程 180 秒，不能与原生窗口直接比较吞吐。Godot 画面、几何与输入未验证。
工程证据追溯补充：当前 Runtime.cs 与 pure-cache-probe-04、wait-window-probes-01 所存 Runtime.cs 的 SHA-256 均为 8E5713C109C2E80E6CF5680940030E5186F2E300D8BB3278597F8953EB9150C8。当前 dv_read/dv_read_fields/dv_truth/dv_truth_uncached 与 topic/question 声明逐体对照 pure-cache 的 bundle.dv 一致；等待及 condition 缓存声明逐体对照 wait 的 probe-bundle.dv 一致。整文件指纹因其他内容变动不同，不以旧 manifest 冒称当前整文件完全相同。

## 最终原生正式一级窗口的实际结论

证据目录：`run-180s-final/`。result.json 明确 executor=formal-model、geometry=false、source_unchanged/build_source_consistent=true，实际进程耗时 180.313 秒，在 logical-session 超时。stderr 为空，正文与原始输出可解析；没有 debug_end、trace.json 或 trace.json.partial，evidence_complete=false。已输出 debug_events 的 from/next 连续覆盖到 next=21684；这只说明现有批次无断口，不能把被终止后的关闭与完整存档视为完成。最后实际执行为 e21682：margaret.wait.first_night.30，game_time=5190，即第四日14:30。

本轮是无窗口正式逻辑输出审查，不是 Godot 或三级验收。未达到7200，没有第五日收尾的实际记录。早期语法/构建与中性容器证据不改变这个结论。

### 逐段实际轨迹与因果证据

开局正文先呈现本人回忆、穿衣、相识与奉茶，随后才出现教师角色信息、报偿与始业之问。门、石板、靴印、湿布与接触闪回均在本人行动后呈现；文本将独白、观察、触觉闪回和对白分开。问题转换与答辩回执顺序可对应实际接受事件：e2319@610、e2471@646、e2893@695、e3064@712。11:21 的重演文本明确“仍是推演”，未抢先宣称正式答辩；11:52 才有末问接受，随后开内闩、开门和11:58跨门分列。它们是逻辑执行事实，几何到达未测。

第一日14:31近看封口与红纸，14:32提出明日现场勘验，晚间再出现搭档邀请；本人未在等待或预测时取得文献调查完成字段。第二日先核对安全与路况、到中央厅、阅读和接受/拒绝条文，再到讲堂。本人对不同条文作不同表态，正文展示具体条文并分段，不以公共张贴代替本人采纳。10:11 的“本课到此”对应 e9030@2051 lesson11.close，其直接观察仅为说话者自己的 said 标记；它是下课，不是会议全员签署或整个章节结束。

文献合作有真实的本人执行和同处结果链：e12517@2286现场核对发现操作尚未完成；e12641@2300沃德出示半纸，侦探在e12643收到ward_half_paper=true；e12654@2303本人清点并在e12655取得emergency_inventory_majority=true；e12661同刻雀儿拆锁，侦探e12663取得lock_no_pry=true；e12782@2320本人拼合并在e12783取得joined_inventory=true。顾平章e12802@2329致歉、沃德e12856@2331归钥，侦探分别有e12804/e12858的direct观察。正文也依次显示半纸、清册、拆锁、拼合、致歉和归钥。这可以支持本窗口文献现场合作与收尾实际发生，不能推广到全章。

第三日侦探在e15517@3455到共鸣厅检视，所见梯、铜屑、步位擦痕、掌印均仍未完成；e15592@3458到沉静室检视，所见压差、安抚、完整底稿也均未完成。本人随后e15869@3475执行压差核对，并在e15870取得vent_pressure_formula=true；e15882@3476安抚，并在e15883取得moran_reassured=true。之后e15927@3478本人离开沉静室到共鸣厅；e16032@3489雀儿查铜屑，侦探在e16034实际得到copper_filings=true，正文显示现场铜屑。莫兰e16171@3521出示底稿、埃莱娜e16188@3535辨认发生在记录中，但侦探没有收到complete_draft的直接观察，正文也未呈现其本人接收底稿并完成判断。不能把其他主体做完操作或系统事件列表当作侦探本人现场收尾。

### 反复、拒绝与等待

本轮原始action_execution中，拒绝分别为 e3531@833 quer.departure.pick-outer、e4690@1113 night.corroborate、e10517@2163 mora.c2.distribute。本窗口未见上述同一拒绝重复执行，也未见移动反复被拒。不能据此承诺后续不会发生。

Mora的新增拒绝有明确前后链：e10337选择从clinic到logistics，e10398@2162移动成功并在e10399观察到本人地点；e10457选择distribute，e10459排至2163；e10516有host_completion，e10517正式结果rejected，实际费用已记录。随后e10559选择wait30，本窗口没有再次distribute。沈鼎e10362@2161的distribute为applied。拒绝事件本身没有给出具体失败条件，审查不从前后状态猜补拒绝理由；这仍是一次实际候选到执行被拒的问题。

调查导航在可见窗口内确实把侦探带到文献、共鸣与沉静现场，未见它在这些房间之间无限折返。第三日09:58返回共鸣厅后，本人未再执行非等待行动，一直等到第四日14:30。白天30分钟等待、夜间180分钟等待与08:00前120分钟等待均真实出现。它们推动时钟，但没有带来新的本人调查进展；本轮不能据时域更远宣称体验或关键节点完成改善。第二日文献收尾后也有长段30分钟等待，正文重复劳动仍明显。

### 未发生、记录不足和停止边界

- 本窗口实际发生：开场答辩接受与逻辑跨门、第二日文献现场合作及致歉归钥、第三日本人压差/安抚与同处铜屑观察。
- 本窗口未发生：7200时域、第五日收尾、侦探本人取得完整底稿后的最终现场结论；未见全员签署或全章终局。下课不能代替这些节点。
- 记录不足：超时后的完整关闭/存档，以及Mora分发被拒的具体失败条件。已有连续批次不补足未产生的debug_end或trace。
- 未验证：Godot画面、布局、输入、寻路碰撞与三级体验。独立审查未读设计或权威答案，因此不就人物设定符合性和全谜题正确性另作判断；由主代理依据设计规范整合。

本轮最终结论：工程缓存与容器审读没有发现需要解冻的具体语义错误；正式逻辑输出有文献合作的具体达成证据，也有Mora分发拒绝、后续调查接收不完整与长时等待。进程在第四日14:30超时，不能宣称两章、五日或最终效果验收完成，也不能与前轮插桩采样直接计算同条件提速。

