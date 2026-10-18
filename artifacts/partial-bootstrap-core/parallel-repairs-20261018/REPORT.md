# 并行修复与一级逻辑输出记录（2026-10-18）

任务来源：用户附件pasted-text-1.txt，及用户追加的“自行根据实际剧情，设计第二章收尾”。五个Sol 6.1子代理分工实施，另设全新上下文Sol 6.1审查本轮证据；主代理负责设计取舍、集中构建和正式会话。此前未跟踪的用户文件、历史源码和证据未覆盖。

## 源码结果

| 附件问题 | 核实及处置 |
| --- | --- |
| 可重复移动被拒后反复消耗 | Complete保存本人相关输入的失败语义签名，所有资格分支先核对；无关revision和时钟增量不解锁。formal-model可按显式现场授权观察门状态，native失败缺乏对应起点证明时不授权。 |
| 长等待跨调查机会 | 本人未完成目标的开闭边界限制等待；既有1/5分钟动作承接短片段。第二章仅白天480–1260收紧至原上限与30分钟的较小值，夜间保留原上限。 |
| 调查导航只盯第一项 | 旧指控已部分过时。保留当地ready优先，异地ready增加本人已知路径核对；收尾同样逐段导航，避免提高全体深度或预算。 |
| 6330后无目标 | 7200是工程时域，不是章末标准。按新授权设计真实收尾：全签成立完整公约；截止仅闭会并保留未决。现场持续低鸣/余震另行观察，系统结算保留真实资源、票/签名与调查状态。 |
| 预算耗尽 | 当前planning_limit已是单次搜索上限。保留剩余行动、推理与模型余额，不按章节补充额度。 |
| 封存超时与快照膨胀 | 完整快照保留；恢复撤回事件缺失的完整前后证据。修复已有文件覆盖与错误成功出口，Save/WriteSave/Restore统一精确数格式和深度。序列化耗时由本轮记录判断。 |
| 列表构造样板与重复检查 | 用保序字面量替代c1_list_4..44。独立审计算法保留，补齐dispatch/遮蔽/Replay_Depth/切片、话语接纳与撤回证据漂移，移除无调用旧副本。 |
| 无消费者的室内草案 | 迁入隔离fixture，生产清理无消费者状态、采纳与空动作。室内防护要求未实施，合同明确标注，不以未知输入推断安全。真实气闸/户外暴露保留。 |
| 语法冗余与自举说法 | 实现elif及可嵌套单绑定列表推导式。dv_with_many本已可用；未新增spread/闭包/短路高阶函数。自举仍为DV生成C#，依赖平台ABI与include组合工具，不冒称完整静态类型/目标无关IR完成。 |

设计正文在docs/design-standard.md第五部分原表与3.1更新；接口、源码索引和相应AGENTS同步。新增收尾与计算参数属于新行为条件，不作同条件速度结论。

## 工程证据

- `host-build-frozen.stdout.txt`：最终宿主解决方案成功生成；仅说明工程接线。
- `checks-compile.json`、`room-draft-compile.json`：独立检查和隔离草案静态编译，不执行游戏。
- `syntax-probes-01/`：推导作用域、单次来源求值、过滤和elif真实输出；负面输入保留诊断。初次runner的一处错误文案预期已修正，原记录与复核分列。
- `runtime-probe-evidence/`：现有完整/partial文件字节保留，300层精确数Save/WriteSave相同且Restore往返一致，冻结证据独立。
- `checks-neutral-probe-report.md`：独立派发、遮蔽、重演深度、切片、接纳及撤回证据的中性机制运行；编排换行错误与修正记录分列。
- `artifacts/rejection-probes-01/`：中性失败签名与缓存实际运行。没有prepare/complete、真实人物或正式游戏；不得作为游戏验收。

## 正式会话与独立审查

源码集中冻结后执行唯一一次：`python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/parallel-repairs-20261018/run-180s --timeout 180`。执行器formal-model，无Godot几何，不注入状态或答案。

第一轮实际在71.297现实秒以exit_code=1退出，stderr为`Repeated observation`，调用链为公共感知传播至dv_update_mind；不是180秒超时。源码与构建一致，stdout可解析，正文导出完成，但没有debug_end、完整trace或partial，evidence_complete=false。

独立审查核对：侦探真实移动至文献工房；e12181在2303分钟找出大半纸，e12188雀儿在2303拆锁，e12309在2320拼合，e12310–e12313形成本人及同处观察。e12329在2329只打印道歉applied；异常发生在其后续传播，不能当作完整Complete或道歉已被听见的证据。完整批次止e12322。窗口内没有rejected移动；雀儿pick-outer与gu的night.corroborate曾被拒，未见相同动作连续空转。第五日收尾本窗口未发生；其自主可达性与正文均未验证。

根因：同一行动同时声明public_observers和话语listeners/public_grants，先前本人/现场观察与后续公共观察复用同一个执行—主体—字段身份。修复应在投影入口统一实际感知收据，保留Core拒绝与独立真实话语解析，不创建第二份伪独立来源。第二轮针对该工程错误集中修复、独立中性重现、冻结后重新从全新正式会话复测；第一轮目录及原始字节保留。

第二轮修复已落地：从本次action_execution之后的实际观察尾段一次建立主体/观察身份索引，公共入口仅补新的授权；public_grants_projection保留已有来源，事件grants为实际子集，原声明另存。中性03探针实际保留不同local绑定、部分重叠、全覆盖和新执行首传的行为，重复公共调用及直接重复Core观察仍报错。证据位于`artifacts/public-projection-probes-03/`，01/02排错记录也保留。`host-build-round2.stdout.txt`记录第二轮宿主构建。

第二轮新会话`run-180s-round2/`在180.297秒逻辑阶段超时，最后完整批次19937–19941包含e19939@4890（第四日09:30）的等待及e19940观察；stderr为空、源码一致，未生成debug_end/trace，evidence_complete=false。e12357致歉后产生本人及同处观察，e12395公共投影后e12396继续决策，已实际跨越原中断点。后续压差核对、安抚与底稿/辨认分别发生，但侦探已离开现场，不能将他人的认识当作侦探收尾。第五日闭会/全签/本人核对未发生。本轮结果仅归其新行为条件，不作同条件速度比较。独立审查为`independent-round2-review.md`。

第三轮`profile-round3/`是单独插桩诊断：180.313秒在逻辑阶段超时，最后累计样本仅覆盖150.009秒；剩余时间不外推。热点集中在准备/认知/规划中的真值、查找和容器调用，平台ABI及插桩开销混入；不能据此声称原生函数耗时或序列化为主要瓶颈。独立审查为`profile-independent-review.md`。

诊断后集中处理完整输入的dv_read/topic缓存、非List常量快路、冻结全字符串成员缓存、字符串更新键去重及条件窗口元数据复用；中性差分保留旧算法，并核对首错、完整cell、嵌套失效与返回副本。List常量的第二次返回继续独立，不以快路引入新别名。另将本人已声明白天策略的起点纳入夜间片段上界，6:00以既有120分钟片段到8:00；更小本人上限保留。这项等待边界是新行为条件，不改变预算、费用或判定。证据为`pure-cache-probe-04/`、`abi-optimized-probe-20261018/`及`artifacts/wait-window-probes-01/`，错误探针编排与更正记录均保留。

第4轮原生会话`run-180s-final/`在180.313秒逻辑阶段超时，完整批次止e21682@5190（第四日14:30），无stderr，源码一致；没有debug_end或正式trace，evidence_complete=false。侦探的清册拼合和道歉实际观察已发生；星仪院完整底稿与辨认在侦探离场后发生，不能代替本人的收据。五日终点与章末本窗口未发生。详见independent-final-review.md。

用户随后明确授权一次240秒新会话并要求完整五天。本次在集中核对时域等号边界后冻结源码，仅运行新的run-240s/；不改写历史记录，不通过改判定、补预算或伪造状态满足时域。终点边界工程探针位于artifacts/horizon-probes-01/，确认精确时域只结算已选到期动作，不在终点新开决策；无队列全idle仍会真实停止。host-build-240.stdout.txt记录修订后的宿主构建。调查接收缺口另存investigation-receipt-gap.md；本次没有通过伪造请求、知识或强制留场掩盖它。本次run-240s实际运行240.344现实秒后在封存阶段超时。逻辑循环已结束，debug_end为6333（第五日09:33），停止原因no-scheduled-or-admissible-action；horizon为7200，尚差867分钟。不是达到五日时域，也不是动作上限停止。council_closed与underground_tremor在6330真实成立，但council_ended=false，投票与签署均未发生；不得称完整第二章已完成。

本次stderr为空、源码未变、stdout完整可解析，玩家正文已导出；trace.json未生成，trace.json.partial为778118000字节，evidence_complete=false。run-240s-frozen-program.zip封存本次实际程序集、源码、指纹与原始partial字节；压缩不补全证据。没有追加第二次测试。独立审查见independent-240-review.md。

当前未完成范围：五日时域终点；自主调查的真实请求/后续接收；议事实际投票与签署；完整轨迹封存。现有修复有工程与局部会话证据，但用户要求的整批修复及完整五天尚未达成。

独立证据报告：`independent-acceptance-review.md`。不得据源码存在、工程检查或时间推进宣称完整第二章完成。

二级Godot UI与三级游戏本体本轮均未执行。宿主snapshot的chapter_summary数据字段不代表章末卡片具体渲染已经实现或验收。
