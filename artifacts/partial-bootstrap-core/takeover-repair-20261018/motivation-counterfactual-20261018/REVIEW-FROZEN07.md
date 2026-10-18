# 冻结07独立证据审查

2026-10-18。只读审查 frozen-07/standard-yi、standard-yi-tiered-off，并与 frozen-06/standard-yi 比较实际 stdout 投影。未读取设计文档，未修改生产、构建或运行会话。本报告仅核对投影、result、source-hashes 与既有 raw-dag-summary；为避免争抢正在进行的诊断，没有重扫大DAG或重新计算大文件哈希。

## 窗口与实际覆盖

standard-yi执行180.265现实秒，末完整投影e19795、2720分，即第2日21:20。standard-yi-tiered-off执行180.297秒，末完整stdout投影e19465、2701分，即第2日21:01。后者raw-dag-summary记最后提交e19468，stdout完整投影与原始提交范围不同；本审查没有解码这三个尾部原始事件，不补猜其时间或内容。

两者均在logical-session超时，stderr为空，未见异常报错。result均记logical_loop_finished=false、world_horizon_observed=false、evidence_complete=false、raw_sealing_complete=false，chapter_summary_reported=null。既有raw摘要均记final_checkpoint_observed=false、seal_observed=false、invalid_tail=null，原始文件为partial。摘要说明可用范围为已提交前缀，不能重建最终会话状态；本审查没有独立重验证原始DAG字节。

五日时域未到达；本窗口没有剧情终局的达成证据；完整封存没有完成。这是不同结论，不由局部推进较远或工具后处理结束替代。

## 来源、配置与共同投影

两个07窗口source-hashes.json逐字节相同。result均记source_unchanged、build_source_consistent、run_config_matches为true；请求和实际配置相同：seed=1、Standard_Detective、zone_yi、su_initial_room=R08、private_execution_benefit_multiplier=1、homecoming_negotiation=false。运行环境记录均为server GC及四堆。

需要保留一个配置证据限制：standard-yi-tiered-off/result.json的runtime_environment只列DOTNET_gcServer=1及DOTNET_GCHeapCount=4，没有DOTNET_TieredCompilation。主代理告知该窗口设置为0，但凭本次所读封存文件不能独立确认实际进程的Tiered设置，目录名本身也不能证明。因此本报告只按窗口名称区分，不作已验证Tiered因果结论。

逐行解析stdout的debug_events.batch.events，保持事件顺序与全部投影字段，仅移除顶层raw_event_ref：两个07窗口分别与06从e0至e18178的完整共同投影一致；两个07窗口之间从e0至e19465的共同投影一致。这是已发布投影前缀一致，不能扩展为全部原始DAG、未执行未来分支或完整语义等价证明。

冻结06同配置窗口末投影为2519分（第2日17:59）。冻结07默认窗口本次覆盖到2720，另一个到2701，确实覆盖更远的局部时间；每个配置只有本次一个窗口，不据此推出稳定倍率，且时域目标仍未达成。

## 本人决定与后续真实执行

全局首次owned_decision实际为顾的e16823，2285分。之前顾2284分e16773实际抵文献工房，2285分e16820为宿主完成回执，e16821将gu.c2.document.apology.undertake实际结算为applied，费用time=1、computation=1、stamina=0；e16822随后给本人直接观察，work.decided.gu.c2.document.apology与work.accepted.gu.c2.document.apology为true。之后2286分e16938实际检视case.contract，2287分e17015实际检视document_workshop。这支持承担后的实际行动，不能把承担或检视等同最终道歉结果已完成。

沃德首次owned_decision是2286分e16899；其前e16897实际执行ward.c2.document.half-paper.undertake，费用time=1、computation=1、stamina=0。2287分e16974实际承担return-key并出现e16976，2288分e17050实际检视钥匙保管，2305分e17441实际执行half-paper，费用time=17、computation=1、stamina=0。上述执行与冻结06共同投影一致；首次决定路径没有旧异常的本轮报错。没有解码本轮owned_decision原始节点，不能从空detail投影重申其previous_event原值。

REVIEW-FROZEN06的“首次本人决定”段实际上围绕沃德首次展开；若读成全局首次则不准确，本轮按主体明确区分。

## 验收边界

这是一级formal-model局部逻辑证据审查，不含Godot几何、碰撞、输入、画面或三级体验。未逐段完成全部玩家正文的顺序、称谓、重复与缺失审查；未读取设计规范，人物动机与设计合理性由主代理裁定。未执行分支与超时之后结果保留未知。
