# R19 新鲜度静态合同独立审查

审查日期：2026-10-15。审查者为独立上下文 Agent。范围仅为当前 `language_core/verification/session/15-action-planning.dv` 中 R19 新增扫描与 refresh 修改、该文件既有 freshness 比较，以及 `language_core/core/20-observation.dv` 的 observation-batch 生产路径；对照 `r19-protected-change-proposal.txt` 及适用 AGENTS 合同。未读取设计文件、历史试玩、私有规划或正式会话原始记录。提案中的试玩描述未作为本审查的行为证据。

结论：在上述静态范围内，未发现实现偏离提案的阻断问题。该结论仅支持机制合同审查，不代表游戏体验、章节完成或实际行为改善。

- **本人已接收信息的来源**：`dv_update_mind`（20-observation.dv:224–285）要求 observation.subject 等于 mind.subject、拒绝重复 ID，并先执行既有权重验证。接受列表在每条信息处理末尾克隆该 observation，最终作为 `kind = observation-batch` 的 observations 写入本人 history。当前 core/session 中该 kind 的生产点仅见此处。`older-than-current` 也会进入接受列表；这意味着信息已送达，不意味着覆盖当前 cell 或内容为真。R19 只读取时间，对这种记录的使用与“收到但未采信”边界相符。
- **时间来源与回退**：`dvp_latest_received_time`（15-action-planning.dv:745–758）从 mind.time（缺失时 0）开始，只扫描本 mind.history 中 observation-batch 的 observations[].time。history 或 observations 缺失时按空列表处理；空批不改变下界。使用最大值，所以不会因历史记录较旧而低于 mind.time。非 observation-batch 项不进入内层扫描；代码不读取 physical、其他主体、预测 world 或预测事件。
- **惰性扫描与停止条件**：`dvp_refresh_current_evidence`（15-action-planning.dv:761–777）仅处理显式 kind=evidence 且含 refresh_until 的目标，并以 actor.mind 对该目标自身的命题判真。命题不是 true 时才扫描；scanned 在首次扫描后置 true，因此一次 refresh 调用最多扫描一次，多个未停止目标复用同一精确时间值。没有未停止的标记目标时不扫描；已知 true 的目标独立保留原 minimum_time。
- **不改旧目标与认知**：函数新建 result 列表；只有需刷新的局部 goal 经既有持久更新接口 dv_with 产生替换记录，再加入新列表。它未对 actor.mind、history、旧 observation、旧 goals 或 goal 内部字段调用原地修改。普通目标和停止目标沿用原记录。本审查以既有 dv_with 的持久更新合同为前提，未扩展到 ABI 实现的重新验证。
- **精确数与边界语义**：新增代码只用 DV 数值的 dv_max 和原值传递，没有字符串解析、浮点转换、舍入或宿主时钟换算。原 `dvp_source_supported_since`（15-action-planning.dv:291–301）仍要求来源与 cell.value 一致，且 minimum_time <= claim.time，即相等时间仍满足。仅将时间下界提高不能自行生成支持或改写认知。若没有更晚的已接收信息，本改动不能保证旧检视变得过时或再次检视会发生。

限制：本审查未运行程序、未检查新中性验证输出，也未审查本文件之外的目标组合调用点；“每次组合”在本报告中精确指一次 refresh 调用，外部调用频次由宿主集成另行核对。history 项与 observation 的记录形状沿用既有生产合同，R19 未引入对畸形存档的新验证。预算、判答、真实输入和碰撞未在本审查中执行；一级、二级、三级游戏验收均未执行。

审查时源码 SHA-256：

| 文件 | SHA-256 |
| --- | --- |
| language_core/verification/session/15-action-planning.dv | 8DAECC5DC2AD830CCEAD563FC4912E3EACC9BD25A6FDEFEBF2B3BC72DF025802 |
| language_core/core/20-observation.dv | F8F0805B72928931DAAC9C830625270FD30D5082A7B924492BEE333840E56264 |