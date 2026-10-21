# blind-04 独立审查补正

2026-10-21。原版 `independent-review.md` 原字节保留。本补正取代其中最后一节关于“events=0不能支持全轮原始正式事件链”的推断，其余行为与画面结论不变。

初审把DAG封装层的 `committed_events=0` 与根值内的正式会话事件混淆了。这一推断不成立。根据主代理指出的工程格式疑点，我仅用 `language_core/tools/raw_dag.py` 的只读 `RawDagIndex` 检查本轮封存值；未读取设计、剧情源码或其他轮，也未开启会话。

独立展开检查结果：

- `host-session/session.dvdag` 的final root为58649，其 `events` 字段引用node55077，内含1282条事件；ID按原顺序连续为e0至e1281，没有缺号。封装层initial/final同root是一个完整值导出的封装方式，不能据此判为会话初末状态相同。根内另有独立 `initial`、`final`、`configuration`、`status`、`time` 等字段。
- 根内事件确实包含e352（离R05）、e663（R06取消）、e968（进R08）、e1113（E6）、e1218（出R08）、e1278（进R07）等 `action_execution`，各自保留 `physical_before/after`、`receipt`、`action`、`ledger_before/after`、`quote`、`actual` 引用。末事件e1281为 `session_stopped`，reason=`host-closed-during-active-session`，actions_executed=116。
- `host-session/save.dvsave` 的根值也有1282条events，首尾为e0/e1281；此外有physical、actors、initial等字段，audit_mode=true。
- `RawDagIndex` 对session.dvdag报告complete_sealing=true、invalid_tail=null、residual_tail_bytes=0、uncommitted_suffix_bytes=0、source_changed_during_scan=false，scope=`complete sealed raw values`。此前对两份文件的序号及SHA-256链检查仍有效。

因此，本轮原始正式事件已保存在封存根值内。撤回初版因封装层events=0提出的原始事件缺失疑点。close-result中audit_mode=true/trace_policy=full，与根值内事件及audit状态一致。已确认事件清单连续及关键执行对象存在；仍未逐字段重演全部状态、验证恢复或证明未提交计算可重建，这些是未执行范围，不再表述为事件封存缺陷。

本补正审查已完成并关闭。对外携带初版报告时须一并携带本补正，避免继续引用已撤回的封存疑点。
