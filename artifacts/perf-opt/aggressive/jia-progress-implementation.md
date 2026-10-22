# 甲区停滞修复及有限正式诊断

分支 opt/fix-jia-progress，基于 dae71fa2d。只修改40-chapter1.dv的既有内容声明，不修改Core、规划预算、NPC身份/深度、题目、答案或判断规则；未读取设计文件。适用于三种跨区安置的相同声明，不按侦探profile分支。

## 原因与修改

原S2-jia中e659实际提问，e679对雀儿仅产生seal_examined=true的0.55信任testimony。雀儿的该项此前初始化为false；提问台词虽然写“请看这份已核对的材料”，行动并未公开展示任何已执行材料。回想法则仍要求本人确认前序检视，故请求并未接通回想/答复链。修复在实际同房提问Complete之后，只向指定回答者公开前序已核对项的perception；不公开待求证回想，不改物理值，不提前授予理由。原正式资格、在场检查、实际提问和回想动作均保留。

答复还显式匹配detective、原请求动作及received_speech_requirement，防止同topic的居民请求被侦探答复候选错误消费。

另一个独立声明问题：原departure候选永久声明牺牲solve_case/evidence，即使本人完整经过已经获接受，仍被Standard价值顺序视为弃案。新增.after-account普通出行候选，要求实际complete_receipt.detective=true，加上原外闩/通行/源位置前提；复用原移动法则、实际执行、报价与回执。未完成时仍保留原离开/调查的价值冲突；完成后不再把普通出行声明成放弃未结案件。没有给线索加分或引入优先白名单。

## 本轮正式诊断

命令：python mcp/verification/run_debug.py --out artifacts/perf-opt/aggressive/branch-jia --timeout 300 --causal-timeout 1 --detective-profile Standard_Detective --detective-zone zone_jia --plan-depth 3 --stop-at-chapter-end on

其余现有优化开关均为默认on，真实run_config已封存。证据位于 C:/Users/iii/Desktop/dual_ontology_jia/artifacts/perf-opt/aggressive/branch-jia/。使用本分支冻结源码，build_source_consistent/source_unchanged均true。本轮与其他诊断可能重叠，不作最终速度比较。尚未包含主代理另行合并的统一NPC规划深度修复及新缓存开关；本分支没有改这些函数。

| 实际事件 | 游戏分钟 | 结果 |
| --- | ---: | --- |
| e661 | 484 | 提问后雀儿实际直接观察seal_examined=true，perception、trust=1。 |
| e802 | 485 | 雀儿实际执行quer.truth.recall.quer_recalled。 |
| e844 | 486 | 雀儿实际执行定向回想答复。 |
| e1089/e1090 | 494 | 侦探提交人物答复并被接受，literal_truth/epistemic_support均true。 |
| e1319/e1320 | 504 | 侦探提交完整经过与理由并被接受，literal_truth/epistemic_support均true。 |
| e1352/e1353 | 506 | 原正式移动法则执行成功，本人观察到threshold.contract。 |
| e1370/e1371 | 508 | 第二段正式移动执行成功，本人观察到living_hall。 |
| e35442/e35443 | 6331 | 本人实际检视誓约庭closing，产生本人观察；不再以他人检视替代。 |

本人本轮实际执行14次move、36次inspect、18次talk、6次read、2次answer及2次experiment，并保留真实等待；不存在原轮只有首问后一直等待的停滞。本人实际投票6218分钟、签名6219分钟；closing可见正文仍明确“公约签名尚未齐备”，不能声称全体公约成功。这里确认的是该寝区既有的两项答复，不声称完成四项问题或Godot体验验收。

正式进程exit_code=0，132.297现实秒、3101步，游戏6331结束；stop_reasons为objective-chapter-two-closing-record与actual-viewer-closing-inspection，未达到7200时域终点。构建47.578秒；后处理121.640秒；三项相加约301.515秒，不满足180秒全命令目标。外层run_debug返回1，因为另设的只读因果检查1秒超时；causal_check_complete=false、main_causal_supported=false，不冒充完整因果证明。

raw-dag-summary记录35445个提交事件、final checkpoint与seal，残尾及未提交后缀均0。stdout游标35445，未投影事件0。原始DAG 731577240字节，SHA-256 ad70b45d278ef8b13d7da75a3487b1dd56e46630175fd87b6d9b9efdd6584669。来源、原始轨迹、玩家可见全文均保留于固定输出目录。

尝试按AGENTS启动全新独立上下文审查，但团队并发线程上限拒绝创建；没有以实现者自审冒充独立审查。主代理需安排新的独立证据审查。本轮未运行Godot/UI/盲玩或其他正式会话。
