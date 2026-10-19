# 第二批 C# Runtime 独立只读审查

日期：2026-10-19。本审查未读取 docs/design-standard.md 或项目设计文件，未修改源码，未启动构建、探针或正式会话。依据为 Runtime.stage01.cs.txt、当前 compiler/Runtime.cs、Probe.stage02.cs.txt、engineering-04 的源码副本、原始输出、构建日志及 comparison.json。本文件是唯一新增审查交付物。

## 静态结论

本批差异未发现需要阻断候选的具体正确性缺陷。结论限于本次源码审读与现存工程证据，不代表完整异常路径或游戏验收。

MutableState 将首父存为单个弱引用，其余父仍为弱引用列表。LinkParent 的无首父、首父已死、首父重复、其他父重复及追加分支均未丢弃已有 OtherParents。Invalidate 同时遍历首父与附加父，清除 Backbone、IndexChecked 和 Index；引用身份 visited 阻止循环传播无限重复。首父死亡后直接复用该弱引用，可能与附加表中的同一父形成冗余引用，但 visited 仍保持传播终止和缓存清除；这不是失效遗漏。附加表的死亡项在后续枚举中删除。

RawDagWriter 正常帧先清空 payload 缓冲、Reset、生成完整 payload 并 Flush，再计算同一字节的摘要；envelope writer 每帧 Reset 到同一 stream，写完整 envelope、Flush 后才写换行和推进 sequence。重用不跨越未刷新的正常帧。构造器失败、Finish 释放与 Abort 均走 DisposeResources；嵌套 finally 保证前一资源 Dispose 抛错时仍尝试释放后续 writer、digest 和文件。Finish 只在提交刷新和资源释放完成后改为正式文件名。失败释放可能用后续 Dispose 异常覆盖最初异常，现有源码没有保留异常聚合；这影响诊断细节，未发现会发布完整证据的错误路径。

## 工程证据独立复核

本次重新读取字节并计算 SHA-256，确认工程 before 副本等于 Runtime.stage01.cs.txt，after 副本等于审查时 Runtime.cs，after Program.cs 等于 Probe.stage02.cs.txt。

- 基线 Runtime：a3dffd19dca6538b87e4047d931ebb55241b646f40fccf40233ed27232540d82。
- 候选 Runtime：45b8ee25ceeefcda72f3b5b0b30ec2a86a735f7030a44c6318c8176a4b02d31b。
- Probe：b0b22f02d27323a36266306b65197c16c197cfacc7d110b18d4c924e4f4e87bc。

semantics.json 两侧原始字节相同；neutral.dag.jsonl 两侧均为 14520298 字节且逐字节相同，SHA-256 为 374d40e68942182bf81b5d382545d28552a41a1016078a990a67e3980d96fd96；failure.dag.jsonl.partial 两侧均为 2490 字节且逐字节相同，SHA-256 为 11e02b7fb8cfdccef42bd8eaa0e4bf59f7a15faca85f63bbd5236c84bb1cadeb。另独立读取候选两份 DAG 的每行实际 payload 字节，核对连续 seq、prev 和摘要链；正常文件末帧为 seal，重复事件失败文件末帧为 event。未重写证据。

工程构建日志记录 before 与 after 的 Probe 均成功生成，after stderr 为空，stdout 到达探针结束消息。探针实际检查了共享 child 修改后的两个父快照变化、记录键索引失效、独立复制、精确数、存活驻留及重复事件拒绝。工程时间与分配记录仅说明这个中性样本，不外推正式会话耗时，也不据此评价人物或游戏改善。

## 尚未覆盖

探针没有定向制造“首父被回收、其余父存活”或“首父与附加父均死亡再重新链接”的状态；这两项目前仅有静态路径审读。循环样本确实建立父环并执行 replace，能够暴露无限遍历，但没有显式检查循环双方索引缓存的后续查询结果。重复事件拒绝发生在写帧前，因此 failure partial 的字节一致不能证明真实 I/O 失败、Flush 失败、payload 中途异常或 Dispose 抛错的行为。finally 的资源释放保证来自静态审读，没有故障注入证据。

上述为第二批工程审查；其后的正式一级证据审查见下节。Godot UI、几何、碰撞及二级和三级验收未执行。

## frozen-02 正式一级证据追加审查

本节于同日追加，仅审查 frozen-02/standard-yi 与首轮 frozen/standard-yi 的实际文件，不以审查时仍在变化的工作区代替冻结版本。未读取设计文档、未运行会话。读取 result.json、raw-dag-summary.json、raw-event-index.jsonl、stdout.jsonl、可见正文及冻结清单；对原始 DAG 重新计算文件 SHA-256，但没有重新解码全 DAG，逐帧和节点有效性的正式结论引用已有扫描摘要。

### 同条件边界与重叠轨迹

两轮 run_config 相同：seed=1，Standard_Detective，zone_yi，su_initial_room=R08，收益倍率1，homecoming_negotiation=false；均为 formal-model、geometry=false，server GC、HeapCount=4，projection_every=32，限时240秒。两个 source-hashes.json 的差异仅在 Runtime.cs 和三份工程说明。对两份冻结目录实际所有清单内 .dv 文件另行计算 SHA-256，未发现彼此或相应清单的差异。frozen-02 实际 Runtime 的哈希为 45b8ee25ceeefcda72f3b5b0b30ec2a86a735f7030a44c6318c8176a4b02d31b，等于本报告第二批候选；当前工作区第三批改动没有作为本轮审查对象。

逐条比较首轮全部原始索引与第二轮对应前缀，以 actor/game_time/type 为比较键，重叠范围没有差异。另逐条比较首轮全部即时 stdout 行动包与第二轮对应前缀，以 actor/game_time/action_id/status 为键并排除 event_id，也未见差异。这里分别覆盖首轮27096条原始事件和2506条即时行动包；这是描述比较覆盖，不作为游戏效果的评分。比较没有核对完整事件负载中的每个字段，不能据此前缀宣称所有内部状态完全等价。

### 实际末段与玩家可见结果

首轮原始末点为6840（第5日18:00）；第二轮原始末点为7196（第5日23:56）。同一现实限时内，本轮实际记录覆盖到更晚的游戏时刻；只有这一对窗口，不据此给出普遍提速比或推算完成耗时。

延续首轮后的侦探即时行动包依次为：e27177 等待180分钟至7020（21:00），e27314 检视天气至7021（21:01），e27434 等待120分钟至7141（23:01），e27554 等待30分钟至7171（23:31），e27674 等待20分钟至7191（23:51），e27794 等待5分钟至7196（23:56）；这些包的 status 均为 applied。原始索引也记录 e27793 host_completion、e27794 action_execution 和 e27795 observation。故末段存在正式完成的等待与天气检视，不能描述为同一个动作从18:00一直未完成；多数新增行动为等待，不能仅由时间延伸认定剧情完成。

实际可见正文在21:01输出“落灰停了。”“廊外起风了。”，其后显示23:01、23:31、23:51的等待；正文最后是23:51的20分钟等待。09:33的可见收束观察仍保留签名不齐、未签名、未投票和未决事项，以及共鸣和余震来由待查。没有依据把时域推进解释成这些事项已经解决；本审查未读取设计，也不另行制定终局标准。

### 尾部覆盖与封存

原始提交前缀为 e0 至 e27889，末事件是 elena 在7196的 action_scheduled；侦探在该时刻的下一次 action_selected/planning_choice/action_scheduled 为 e27860/e27861/e27862，之后尚无这次下一行动的完成记录。索引不提供该排队动作内容，本审查不补猜。

最后 debug_events 的 next=27736，最后正文是 e27674/7191；e27736 至 e27889 的154个事件尚未进入批量 debug_events/玩家正文投影。但 stdout 即时行动包独立写出，已经覆盖 e27794 的侦探等待和 e27858 的 demon 等待，二者时刻均为7196。因此“154条未投影”应限定为批量调试与正文投影，不能写成 stdout 完全没有其行动证据。未把即时回执自动补写成玩家正文，也未重建尾部。

raw-dag-summary 报告 partial 文件367830861字节全部处于有效提交边界，没有残留尾字节、未提交后缀或 invalid_tail；扫描期间源未变。独立重算文件 SHA-256 为 1335312b66849c022e002f1cff0a444ea068dc88ce2cd4353042b60b7169088f，与摘要相符。现有 initial、stage-day3 和 stage-day5 检查点；final_checkpoint_observed=false，seal_observed=false，complete_sealing=false。有效提交前缀保留与最终会话封存完成分别判断。

### 停止、时域与未验证范围

本轮 execution_elapsed_seconds=240.344，timeout_stage=logical-session，logical_loop_finished=false，未见 debug_end；属于工程限时中断。world_horizon_observed=false，原始实际时间7196仍未记录时域终止事件。chapter_summary_reported=null，narrative_completion 未评估，evidence_complete=false，不能宣布五日时域终点、剧情终局或完整封存。

freeze-result 的 changed_frozen_sources 为空，result 记载 build_source_consistent/source_unchanged=true；workspace_changes_since_freeze 同时列出 Runtime、工程说明与章节内容，故冻结副本稳定不能扩大成当前工作区未变。驱动总耗时318.359秒中，构建39.016秒、正式执行240.344秒、后处理38.687秒分别记录，不能把驱动总时长当作正式会话限时。

本节结论仅适用于本轮正式一级窗口。没有对全篇正文与全部内容来源逐段核对，也未审查剧情条件、人物合理性或二级/三级呈现与体验；原始最后已提交事件之后的未提交计算保持未知。
