# S1 历史重演回归复核

复核日期：2026-10-23。源码基点 `179340bd`，独立分支 `opt/fix-replay-regression`。只读现有 S1 的 stdout.dvlog 与 raw-event-index.dvlog，通过 binary_evidence.records/read_frame 校验帧 SHA-256 后解码；未读取设计文件、未扫描原始大 DAG、未构建、未运行新正式会话或修改机制。

## 结论

当前证据没有支持所报的历史重演回归。`integrated-standard-review.md` 识别了 e2292 的单项切片与 e2293 的不完整竞争候选，却遗漏了 e2295 的完整解释范围。该报告的“完整正确推演节点本轮不能据此确证”应更正为“591 分钟存在一个完整声明历史候选与本人现有七项观察全部相符；唯一解释仍未证明”。不需要提高预算、删比较特征、改变判定或预置真相。

## 直接证据

证据目录：`C:/Users/iii/Desktop/dual_ontology_aggressive/artifacts/perf-opt/aggressive/S1/`。

- e2292：history-case-0，calculation_scope.kind=causal-refutation，仅石板第一行 explained。
- e2293：history-case-0，declared-plan，inconsistent / plan_complete=false，unknown-required-response-context（history.huo）。这不是后续不同候选的结论。
- e2294：model_probe_reuse；不是另一次 mind_simulation。
- **e2295：history-case-1，calculation_scope.kind=declared-plan、plan_complete=true；result.status=explained、stop_reason=declared-plan-complete、expanded=89、remaining=102、replay_stages_expanded=24、failures=[]、contradictions=[]。**
- e2297：history-case-2，inconsistent / plan_complete=false，unknown-required-response-context（history.su）。
- e2299：history-case-3，unknown / budget-exhausted；该候选的预算耗尽不能覆盖此前 e2295 的已完成结果。

raw-event-index 独立定位 e2295：ordinal=2295、node_id=93347、commit_frame=95645、type=mind_simulation、actor=detective、game_time=591、process_elapsed_ms=5815.793000000001；parents=[e2290,e2293,e2294]。stdout 的 raw_event_ref 同为 node_id=93347、definition_frame=95644。现实耗时直接来自该进程索引，约 5.815793 秒，未按游戏时间换算。

|e2295 比较特征|prediction|observation|covered|result|confidence|
|---|---|---|---|---|---|
|隔离门内闩|true|true|true|match|0.5|
|木楔上的烟灰|true|true|true|match|0.5|
|靴印朝向内闩|true|true|true|match|0.5|
|湿布压住靴印|true|true|true|match|0.5|
|石板第一行刻痕|true|true|true|match|0.5|
|石板第二行图记|true|true|true|match|0.5|
|乐谱中的残缺词句|true|true|true|match|0.5|

旧 history-independent-review.md 对 03d5c802 的完整候选记录是 e2218、history-case-1、expanded=89、24 个阶段、七项全部 match。当前 e2295 对应相同候选和这些结果，预算剩余 102 与旧报告 90 不同；当前存在 probe 复用，不能把两份不同源码会话写成严格同条件性能比较，但此差异没有构成已报告的完整解释缺失。

## 边界

本复核没有证明唯一历史：竞争候选的未知响应和预算耗尽仍需保留。没有把完整声明模型吻合升级为真实过去已恢复、全部候选已排除或设计验收通过。未重新展开原始 trace 或 actor_before；其原字段由原始 DAG 保留，本次只用有校验的已有投影及索引纠正 scope/事件归属。未发现需要修改 20/30 的缺陷，因此未作猜测性机制变更，也未改 40 内容。
