# Core 热点静态审计 07

本次只读审计现役 `core/00-data.dv`、`verification/session/10-subjective.dv` 与 `compiler/Runtime.cs`，未读设计文件，未修改源码，未构建或执行会话。唯一写入是本报告。`profile-03/profile-summary.json` 是旧插桩来源，仅用于定位历史调用密集处；其中耗时包含插桩和直接 ABI，不能证明现役吞吐或改善幅度。

## 建议优先审查：dv_read_fields 的重复存在性查询

`00-data.dv:182` 在 objects、facets、fields 三层都先 `dv_has_key`，命中后再 `dv_get` 查同一键。Runtime 两者分别进入 `record_index` 与 `record_find`；长记录已有索引，但每层仍重复取得容器、索引和键。

最小候选是每层只做一次 `rt_record_index`，命中时按返回索引读取该 pair 的 value；保持原来的顺序：objects 命中后才读对象 facets，facets 命中后才读 ref.field，fields 命中后仍 `dv_clone` 完整 cell。缺失分支仍新建 `dv_unknown`。不要使用可与合法字段值碰撞的自定义 sentinel，不提前读取 ref.facet/ref.field，不提前检查未到达的层级。

此处无跨调用缓存，无需新增失效键。必须中性对照重复键末项优先、畸形 pair、缺失对象/面/字段、缺失 ref 字段，以及返回 cell 被修改后原 world 与其他读取值的独立性。不能把 `dv_read_fields` 返回值改为直接借用原 cell。

## 已有缓存边界：dv_read 与 dv_truth

`dv_read` 已在 subject 校验、world.objects 与 ref.object 原始领先读取后按完整 world 值和 ref 做 memo。`dv_truth` 已按完整 world 与 proposition memo，常量标量也已有快路。再次加 actor/revision/time 局部键会漏掉完整输入，且可能改变原错误顺序；没有必要在现有层外叠加该类缓存。

`dv_truth_uncached` 的 all 必须原序求值，遇 false 立即结束；unknown 继续检查后继。不可按 op、已知字段或成功概率重排，也不可预收集全部引用来建立窄键：原本被短路遮蔽的非法引用会提前报错。未知/争议 cell 在 op 检查前返回 unknown，未知 op 在该边界下的现有行为同样需要保留。

可另行评估的较大工程候选：为 Runtime memo 提供只返回已冻结 cell 的私有只读路径供真值计算使用，避免 `dv_truth` 的叶子从 memo thaw 一个完整审计 cell 后只读 known/conflicted/value。但它涉及冻结/可变所有权 ABI，收益未知，不建议作为本批快速修改。必须保留 dv_read 原先 subject 与领先读取顺序，且不得将借用 cell 发布给普通 DV 调用者。

## dv_get / dv_find：现役机制已较精简

两者当前均是单次 `rt_record_find` 转发。Runtime 对长记录已有字符串键索引，对短记录从末项反查；FrozenList/可变 backbone 与 ReadItem 的处理维持返回分支所有权。进一步缓存字段值需要连带跟踪嵌套 mutation 的失效，风险高于在调用处消除 has/get 对。

不建议盲目降低当前索引阈值或把短记录统一建立字典。对象记录很短，建索引可能比反查更贵；需要当前程序集的中性微型测量，不能从旧插桩 dv_get 总时长推断。编译器/运行时内联这两个薄包装也只可能降低调用开销，不能据旧插桩函数次数许诺收益；不得另写剧情相关取值快路。

## dvs_event：保留冻结与发布顺序

`10-subjective.dv:3` 当前构造事件后一次 `rt_snapshot`，依次 `rt_raw_event`、`rt_push(events, frozen_event)`。RawDagRecorder.Event 内再次 snapshot，但传入 FrozenList 时 SnapshotValue 立即返回原对象，并不再次复制事件树，因此删除这次调用不是已成立的主要优化点。

不能先 push 再写 raw，也不能因 raw 关闭删掉事件 snapshot：前者改变失败后的已发布前缀，后者让后续 actor/world 修改污染已有证据。事件 id 仍由当前 events 长度产生，不能缓存独立计数器替代。

较小、通用的未来方向是 snapshot 构造侧的临时小记录分配；当前函数已经用一份记录字面量而非多次 dv_with，未见可再移除的显著 DV 层重复。Runtime 的 SnapshotValue 对已有 backbone 已返回，对新树还必须维持哈希碰撞核对、父链失效及共享冻结值。跳过这些步骤不属于安全静态优化。

## 结论与验证边界

本次最具体且范围最小的候选是 `dv_read_fields` 每层存在性与取值的合并；其余重点路径已有通用 memo、索引或冻结复用，未发现可直接删除的大块重复工作。建议先做该小候选的中性值/错误/别名对照，再由主代理按本轮规则集中工程检查和正式证据复核。本报告不建立性能提升或人物行为改善结论。
