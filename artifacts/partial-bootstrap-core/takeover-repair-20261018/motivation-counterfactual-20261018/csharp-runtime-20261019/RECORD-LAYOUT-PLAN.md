# RecordLayout 有序键形状共享方案审查（2026-10-19）

范围为当前 Runtime.cs 的只读可行性分析。正式冻结会话与构建正在其他工作中执行；本审查没有修改生产源码，没有运行构建、探针或会话，没有读取设计文档。本文是后续实施及工程样本建议，不是运行证据。

## 结论与最小实现

方案可行：冻结 record 的完整有序文本键序列可以作为只读结构 metadata，共享末项优先的索引。该 metadata 不包含字段值、不参与快照身份、不改变列表表达；有相同键顺序但不同值的记录可以共用一个 dictionary。相比为每个新冻结身份重复建 Dictionary，目标明确且不要求增加语言容器种类。

建议把修改集中在 FrozenIndex 与同一个 SnapshotLock 保护的私有 layout pool。RecordLayout 包含私有独占 string[] OrderedKeys 和只在构造时写入的 Dictionary<string,int> LastSlots；后者继续供 FrozenList.Index 引用。池按 ordered-key 指纹分桶，桶内以完整长度及逐项 StringComparison.Ordinal 确认，不依赖 fingerprint 唯一。lookup 阈值仍为现有8，不改 record_with、with_many 或 MutableIndex。

建议 FrozenIndex 的流程：

1. 先检查 IndexChecked，在 SnapshotLock 下再检查一次，保持当前发布合同。
2. 顺序验证每个 child 是 DV list 且 Count > 0；坏 pair 静默把 IndexChecked 置真并返回 null，与原实现相同。验证过程中记录“是否全部文本键”，只计算 fingerprint/标量，不先创建 Dictionary 或 string[]。不要在第一个非文本键时停止验证。
3. 全文本、条目数 <=128 且满足缓存准入时，查池；命中逐项确认所有键后直接设置 record.Index，再设置 volatile IndexChecked。遍历顺序及重复键位置均保留。
4. 未命中时才创建 OrderedKeys 与 LastSlots，按正序 `LastSlots[key]=slot`，然后发布 layout。池已满且本次确实要加入新 layout 时清空池，再加入新 layout；命中不能触发清空。
5. 非文本键混合表、>128项表及其他不准入表，执行原 FrozenIndex 建法；无需为它们分配 OrderedKeys。坏 pair 已按第2步返回，不在此重新定义处理。

即使池清空，已设置的 FrozenList.Index 仍引用旧字典，照常使用；没有必要把所有既有节点指向新 metadata，也不能清除现有 Index、SnapshotPool、memo 或 RawDag identity。清池只影响后续新冻结身份第一次索引时的复用机会。

## 正确性约束

**重复键完整入形状。** `[a,b,a]` 与 `[a,a,b]` 或 `[a,b]` 不同，即使部分查询的末值偶然相同也不得共享错误 slot。fingerprint 和等值比较必须包含 count 与每一个位置，不只包含唯一 key 集合；字典仍末项优先。文本键比较采用 Ordinal，不 normalize、不改大小写、不用进程文化。

**形状不包含值或 pair 的额外元素。** 现有 lookup 允许 Count>=1 的 pair，不要求恰有两个元素。布局只描述键的位置，Count=1、2或更多元素都可以共享同序键布局；record_find 真正读取不存在的 value 时仍在原 slot 的下标位置抛错。不能为了纯文本 layout 把 lookup 收紧为二元 pair，也不能在建 layout 时提前读取 pair[1]。with_many 的严格二元快路资格保持独立。

**混合 key 原路径是原部分索引。** 当前 FrozenIndex 在所有 pair 非空合法时，会建只包含文本 key 的索引，非文本 key 跳过；非文本查询仍按原逆序 eq 扫描。layout 不适用时应保留此实现，不能顺带使整个记录改走另一种资格或比较语义。外部 object[] 等仍不是合法 pair。

**先验证，后命中。** 若一张记录有坏 pair，即使其余位置与某 layout 类似，也不能使用 layout 越过原 null/fallback；反向查找原本可能在遇到坏 pair 前命中其他 key。验证不抛新错误，fallback 才保留原首次真实访问的错误。全表验证与字段查找的顺序不能合并为提前读 value 的检查。

**字典发布后只读。** FrozenList.Index 继续是 Dictionary 类型可以避免新增查询接口开销，但实现必须把其构造后不再写入作为明确不变量。pool clear 不能调用已共享 dictionary.Clear；MutableIndex 不能借用并修改共享字典。RecordLayout 的 key 数组不返回外部。所有池读写沿现有 SnapshotLock；先赋 Index，再 volatile 写 IndexChecked，不逆序发布。layout 不写进 JSON、DAG、Save、memo key 或 SnapshotValue 的 hash。

## 内存边界

128 layouts × 128 ordered slots，最多16384个 OrderedKeys 引用与不超过16384个唯一 dictionary entry（另加桶/对象/容量开销）。重复 key 仍占 OrderedKeys 的位置，但不会增加 dictionary entry。池容量要统计 layout 数量，不能只统计 fingerprint bucket 数；恶意或偶然碰撞时，一个 bucket 可包含多个 layout。

这是结构数量上界，**不是固定字节上界**。key 字符串长度无限，池的强引用可能保留很长的字符串，原冻结值回收后仍由池持有；OrderedKeys 也会保留重复位置对应的原字符串引用。若要求严格限制额外驻留字节，建议增加仅影响 cache 准入的每布局/全池字符总量门（使用 long 计数防溢出）；超过门继续原本局部建索引，不拒绝 DV 值、不截断 key。该准入门是工程参数，应单独记录，不改变查询结果。

清池之后旧节点仍持有旧 Index，因此整个进程的所有活节点索引内存不受128限制；它只界定共享池额外根保留的 metadata。旧字典本来就会由其节点持有，这不是池的泄漏，但不能宣称“全进程只存在128份布局索引”。旧节点只持有 dictionary 而非 RecordLayout 时，清池后 OrderedKeys 可正常回收。

## 收益可能与代价

最有希望的场景是大量不同值、相同字段顺序的新冻结记录，都被真正查询；同内容已经被 SnapshotPool 驻留成同一 FrozenList 的记录原本就共享 Index，不会额外获益。只冻结不调用 record_find/index 的记录没有索引分配，也不应为了布局预热新增工作。

命中仍需 O(n) 扫描/指纹和逐项确认；它省 dictionary 分配与每 key 插入，不是 O(1) 首次布局识别。可先检查长度/所有文本 key，在同次验证中算 fingerprint，命中时第二遍只比较 key；不要每次先分配 OrderedKeys 再拿去查池。空池、新 schema、长 key 或高 churn 时，hash扫描与桶比较可能比原建法更慢，应保留实测边界。

主代理所述既有 sparse 样本中，每个 large root 是128字段且同键顺序，record_find 在 copy 的未失效 Backbone 上取索引；其中256个不同值 root 的首次查询有共享机会。后续相同 root 的 copy 已借原 FrozenIndex，不会每次都省一份字典；不能把样本1500次copy算成1500次首次布局命中。record_with 的新结果若未再次查字段，也不会触发布局索引收益。

128容量的“满即清空”简单且可证安全，但129种持续轮换的 schema 会周期性丢失全部可复用布局；清池时还会使 OrderedKeys 等额外分配成为短命垃圾。现阶段可保留此策略以缩小实现；报告应区分共享命中样本与churn样本，不单测最有利的一张schema。

## 最小中性样本建议（暂不执行）

先对照旧 FrozenIndex 与候选的返回、异常类型/ParamName/message、输入不变性及序列化值；不得为样本引入世界内容或推进会话。

| 样本 | 构造与操作 | 核对事实 |
| --- | --- | --- |
| 相同schema不同值 | 8项与128项，各建多份不同value冻结根，查首/中/末/缺失键 | 返回对应本根value；分配变化来自首次索引而非驻留复用 |
| 顺序与重复 | 8项，含同key多次；排列相同及交换位置；查询重复key | 末slot；不同ordered序列不得错共享；with/with_many结果原样 |
| 合法非二元pair | 同键序列分别用1/2/3元素pair，查index，再find短pair | index可用；缺value仍原下标错误，不提前拒绝 |
| 混合和坏pair | 文本/数字key混合、空pair、非list元素，分别把坏项放在正/逆扫描前后 | 原部分索引及fallback、原首次错误；外部array仍非法 |
| 容量边界 | 128字段与129字段；129种不同schema连续两轮；同fingerprint候选的比较辅助样本 | >128不缓存；清池不损坏已建节点；碰撞只增比较不误命中 |
| 所有权与回收 | 对共享schema的copy改key/value，另查兄弟及原冻结根；释放root保留池，再清池 | 原值/兄弟/旧索引不变；mutable失效合同保留；池不含值引用 |
| 文本边界 | Unicode组合形式、大小写、空串、长字符串与重复但不同引用的等值字符串 | Ordinal逐项等值；长度准入仅改变缓存，不改变结果 |

性能样本只需三类：大量相同schema不同值的首次索引；相同已索引root的重复查询；129种schema轮换及>128字段局部索引。构造/冻结、首次索引、重复查询分开计时，原始计时与分配各自保存；语义摘要放计时区间外。分配下降与CPU变化分别报告，不用工程样本声称游戏改善，也不把缓存命中计数当作内容验收。
