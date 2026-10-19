# RecordLayout 候选集成静态审查（2026-10-19）

只读比较当前 language_core/compiler/Runtime.cs 与本目录 Runtime.cs，未读取设计文档，未运行构建、探针或正式会话，未修改生产。对照指纹：当前生产 `4857621E54E8191BD31F9736213AF57A1EB99CC91BE623E181F39BD70BDD9F17`，候选 `1FCD15AA32086CA2A3F12C0E357C1FF905C48FE2D90C0749BBDE4657CD929941`。

## 集成结论

在上述指纹下，差异只包含 FrozenIndex 的布局共享及其私有 metadata/池，没有夹带其它旧版源码覆盖。静态未发现必须先修正的确定性语义阻塞；可交由主代理集成这一候选，然后执行本批授权的集中工程排错。若生产在此后变化，应重新比较，不能按旧指纹整文件覆盖。

本结论不代表已验证运行输出、异常、性能或游戏行为。当前审查没有读取候选 engineering 的结果来代替本次源码判断。

## 重复键、坏 pair 与混合键

- 候选 :679–702 先完整验证每个元素可经 TryItems 且非空，坏元素仍设置 IndexChecked 并返回 null；未在验证中读 value，保留原查找 fallback 及其实际首错。原实现也完整验证后才建索引。本次省掉的是验证前就分配的空 dictionary，不改变正常分配成功时的错误位置。
- 纯文本形状的 fingerprint 包含每一位置及 count；命中还核对长度和全部有序键，以 Ordinal 比较。重复键不会被提前去重，`slots[key]=i` 保留末项优先。顺序不同或重复位置不同的记录不会因同样的唯一key集合而错共享。
- 单项 pair 可以只有 key，也可以有额外元素；代码没有改成二元pair资格。缺失value仅在 record_find 原下标读取时抛错。布局只含key，不包含value、pair长度或值类型，因此这些差异不妨碍共享key位置。
- 混合键表的 allText=false 阻止共享，随后 :743–749 按原方法只索引文本键。非文本查询调用方仍走原逆序 eq 扫描。混合键不是整个记录被拒绝或改成新比较规则。
- 大于128项、文本总字符大于4096的合法表继续局部索引；缓存准入失败不会拒绝值。纯文本key只是缓存资格，IsDvList、外部数组非法边界、mutation ABI和语言record值域均未改变。
- 候选仍依赖现有冻结不变量：FrozenList 内的列表child均已被 SnapshotValue 冻为 FrozenList；首遍合法性验证后，第二遍使用 FrozenList强转，与当前生产的局部索引实现相同。

## 池边界与共享安全

RecordLayout 保持私有，OrderedKeys为新分配数组，LastSlots构造完后未见写入路径。全文件搜索中，发布后字典仅用于查找；MutableIndex继续创建自己的dictionary，Invalidate清的是MutableState.Index引用，不修改被冻结节点持有的字典。

池读写位于已有 SnapshotLock。命中先赋record.Index，再volatile设置IndexChecked；构造、准入、清池和加入都在同一锁内。未引入第二把锁或不一致的字典发布顺序。

容量按 RecordLayoutCount计，而不是fingerprint bucket数；碰撞只使桶内比较增加，不放宽128个layout上限。每个layout最多128个位置、4096个UTF-16 code units，池总计最多65536个code units；long计数足以容纳扫描时最多128个int长度之和，未见溢出问题。

新增miss时，若旧池达到128个或字符预算会溢出，清空的只是RecordLayouts及计数。旧FrozenList.Index仍持有原LastSlots，未调用任何dictionary.Clear。清后重新TryGetValue获取新桶，未把layout加入已脱离池的旧bucket。

字符上限加结构数量上限限制了共享池额外强引用的key内容及对象/槽数量。它不是全进程总内存界：已发布Index仍由活FrozenList持有；清池也不会回收仍被节点引用的字典。OrderedKeys不会持有字段值或整个冻结record。重复key计字符偏保守，属于准入而非值规则。

SnapshotPool、FrozenList.Hash、Equals、memo身份、RawDag节点、JSON、Save/Restore与会话ABI没有变化。清layout池不影响任何上述身份或原始证据。

## 性能风险与范围

没有从静态源码保证性能收益。布局命中省掉新dictionary、bucket/entry后备数组及插入，仍需要顺序验证、key hash与逐项确认。对未查询的冻结值没有工作；对已IndexChecked的值仍直接返回，反复查询不重扫layout。

当前有一个具体额外成本：<=128项的混合键表仍对其中每个文本key计算fingerprint，然后建立原局部索引；<=128项但超过4096字符的表也先hash全部key，再fallback建局部dictionary。相比当前生产，这两种不准入情形可能重复hash字符串，特别是长key或混合表。>128项已用continue避免这部分key hash。该成本不改变语义，不构成本次必修阻塞；集中测量应覆盖不准入样本，不能只测单schema共享命中。

容量满即清空可能在129种schema轮换时产生churn；每个miss仍会分配OrderedKeys和dictionary。清池正确性成立但速度/分配仍取决于输入分布。当前dictionary miss未指定容量，与旧局部构造类似；是否预置容量应作为后续独立小改，不在本次只读审查中扩大范围。

## 集中工程排错需核对的事实

本候选可沿已计划的中性before/after原始输出比较，重点覆盖：完整ordered重复键、混合键、坏pair前后位置及首次错误、1/2/3元素pair、128/129项、4096字符边界、128/129种schema轮换、65536字符清池、清池后旧节点查询、copy改key后的兄弟/原根隔离、Unicode Ordinal差异。碰撞样本须确认命中还有完整key比较，不依赖hash唯一性。

首次索引共享样本、已索引重复查询、不准入/高churn样本应分开测，构造与语义摘要留在计时区间外。源码值等价、分配与CPU变化分别报告，不能以工程摘要宣称正式会话或剧情改善。
