# 通用结构化运行时迁移可行性审查（2026-10-19）

本报告只读审查平台容器、宿主序列化与编译器输出，不读取设计文档，不运行构建、探针或正式会话，不修改源码。章节 DV 内容由其他工作负责。本报告是实施方案，不能用作事实等价、性能改善或游戏验收的证据。审查时工作区已有修改，以下行号属于读取时快照；实施前应重新定位。

读取时 SHA-256：Runtime.cs `D690520A441215F3428FB2E3069C50970AF7D34DF716E3749D790F858014D75B`；Session.cs `D08424D7CF238691FD2DA90BDD944AA7BD81064672E4F44F4D1F33A8091D1D2C`；compiler.dv `CEFD7FA52CFC8507AE3F540DD56B389996AE75E166333E355C9E655B0D981141`。这些是审查定位指纹，不是受保护测量基线。

## 可立即落地的最小层

可把 FrozenList 从 List<object> 子类改为密封的 IReadOnlyList<object> 实现，以其私有 object[] 保存顺序与子节点。MutableItems、list、push、replace 和生成代码的构造结果继续使用 List<object>。这会真正移除冻结值的可变 List 继承接口，并让新快照独占采用本轮递归冻结生成的 children 数组；不需要修改 DV 语义、剧情或宿主动作合同。

Runtime.cs:692–728 当前先分配 `new object[items.Count]`，再 `new FrozenList(children.Length, hash)` 分配 List 后备数组，最后 AddRange(children)。新构造器只接受内部独占数组和计算完的 hash；命中驻留池则丢弃临时 children，未命中直接收养。该数组必须不返回给调用者，不经过外部 object[] 输入，不可在发布后写入。冻结树的子节点已完成冻结，所有原可变 list 保留原数据；这不会使原可变子对象进入证据。空数组可复用 Array.Empty<object>()。

最小结构轮廓：

```csharp
private sealed class FrozenList : IReadOnlyList<object>
{
    private readonly object[] children;
    internal FrozenList(object[] ownedChildren, ulong hash) { children = ownedChildren; Hash = hash; }
    public int Count => children.Length;
    public object this[int i] => children[i];
    public IEnumerator<object> GetEnumerator() => ((IEnumerable<object>)children).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    // 原有 hash、索引和只读缓存保持按冻结身份持有。
}
public static IReadOnlyList<object> Items(object value) => value switch {
    List<object> mutable => mutable,
    FrozenList frozen => frozen,
    _ => throw new Exception("Expected List")
};
```

必须另有严格 TryItems / IsDvList 助手，识别且只识别 `List<object>`（包括原本合法的其子类）与内部 FrozenList。不能把 `value is IReadOnlyList<object>` 作为合法性谓词，否则 object[]、ReadOnlyCollection<object>、ImmutableArray/object 自定义实现会被顺带接纳。IReadOnlyList 是读接口，不是 DV 值域扩张。MutableItems 保持 `List<object>` 返回类型，先对 FrozenList 抛出原快照写入错误，其余类型继续报 Expected List。

## 逐处接线清单

| 源码位置 | 必须调整的内容 | 保持的约束 |
| --- | --- | --- |
| Runtime.cs:21 | Items 返回 IReadOnlyList<object>，新增严格列表识别 | 外部数组与任意只读集合仍非法；既有 List 子类仍合法 |
| :88–99 | show / FormatList 识别两类 DV 列表，读接口参数 | 先匹配 atom，未知类型原格式错误 |
| :117–208 | record_index / record_find 的 pair 类型快捷判断改严格 TryItems | 末项优先、非文本 key 的 eq、缺字段 sentinel、原扫描顺序 |
| :214–268 | SameValue、HasValue 以及 Backbone 查询分离读视图和可变身份 | MutableStates 只能查询真正 List；查找不解冻；数值等价沿现行 EqValue |
| :272–379 | record_with 与 record_with_many、TryStringRecordUpdate 改读接口参数；foreach 不再强转 List pair | 返回根仍可变；替换值 copy_value；未改分支冻结共享 |
| :381–430 | memo partition 标签读取、memo owner 检查、is_list/copy_value | MemoMiss 不顺带修订；copy 每次独占根，memo 命中仍 copy_value |
| :449–550 | hash 遍历与 membership 索引读参数改接口 | membership 桶本身继续 List；不能把所有 List 泛型替换成 IReadOnlyList |
| :555–615 | MutableState/Thaw/LinkParent/Invalidate | CWT 键和 parent 弱引用继续 List；删除 LinkParent 中已不可成立的 FrozenList 分支；冻结子节点无父失效链 |
| :617–651 | FrozenIndex 读取内部冻结 pair；MutableIndex 识别两类 pair，只链接可变 pair | 非规则 pair 不预先抛错；可变 key pair 变更必须使 owner 索引失效 |
| :652–740 | MutableItems、SnapshotValue、ReadItem | ReadItem 先用读视图取值，需要解冻时才获得 MutableItems(owner) 写回；禁止向 IReadOnlyList 下标赋值 |
| :799–827 | WriteJsonFast 的 list 分支、冻结 JSON 缓存 | JSON 外形、顺序、有理数文本编码、double 写法保持；未知类型仍拒绝 |
| :908、:1023–1048 | raw_ref、RawDagWriter.Node 根及子节点类型判断 | 冻结节点也必须进入 Node 递归，否则会遗漏 DAG 引用；原帧、提交、异常边界不变 |
| Session.cs:74–79、:100–111 | SnapshotValue、HostValue 的识别与 pair 枚举 | Host record 仍仅非空且全部为二元文本 key pair；重复 key 末项覆盖；空表仍数组 |
| Session.cs:16、:30–46 | Save/WriteSave/WriteTrace 的 System.Text.Json 路径 | 自定义只读类型的序列化须显式保证数组形状；Trace Rational 字符串与 Save 的 $dv_rational 标记分别保持 |

Thaw 可继续 `new List<object>(frozen)`。FrozenList 实现 IEnumerable<object> 后可编译；但失去 List 的 ICollection 快速复制路径可能让构造器扩容。最小实现显式 `new List<object>(frozen.Count)` 后按索引 Add，避免把采用 children 带来的收益移到解冻扩容；不公开可写 ICollection 来求快路径。

Session 序列化建议增加仅匹配内部 FrozenList 的 converter（由 Runtime 提供注册入口或对类型内部暴露给同程序集）。不要增加接受全部 IEnumerable 的 converter。converter 逐项 JsonSerializer.Serialize(writer, item, options)，确保保存 Rational 继续走 SnapshotExactConverter；TraceOptions 与 SnapshotOptions 分别注册列表 converter。另一选择是给 Runtime 提供明确的 Trace/Save 流式写入口，后者必须支持既有 Rational 标签，不能直接用 json_stringify 替换 Save。仅依赖默认反射是否把私有 IReadOnlyList 当集合，不能作为验收依据。

Session 的 ReadValue / ReadJson 继续构造可变 List，不把 JSON array 改读成 object[]。Restore/Complete 的输入值域不变。DebugCheckpointResume.cs:29 也按同一保存标签读取，当前构造端无需改。Godot scenes/Host/GameEngine.cs 的调用通过 DvSession 的字符串边界，没有直接容器 ABI，可保持方法签名。

## 编译输出与 ABI 边界

compiler/compiler.dv:442 的 sf_emit_array 输出 `new List<object>{...}`；:654–658 的 comprehension 创建 List、使用 `R.Items(source).Count`、通过 R.at 取得子项，再 Add 结果。它们与只读 Items 返回值兼容，第一层不需要改变生成形态；尤其 R.at 的所有权读取不能替换为直接接口下标。

build_session.py:35 经当前 DV 编译器生成 DVProgram.cs，Session/DuelView.Session.csproj 接入生成代码与 Runtime。build_core.py:38、:47–69 亦复制 Runtime 并生成各阶段编译器。Items 从 List 到 IReadOnlyList 是 CLR 方法返回签名变化，既有编译二进制可能失配；生产 Session、编译自举阶段和引用它的宿主必须统一重建，不能拿旧生成程序集混搭新 Runtime。生成 C# 从源重新生成，不直接编辑 artifact 或 obj 中产物。编译器 C# 所有 F_ 参数/结果仍 object，函数业务 ABI 无需批量类型化。

seed Reader/Value 属另一语法容器边界，不因名称 List 就迁移；不把世界执行放回 seed。后续如更改 compiler 输出应由 DV compiler 源实施并同步工程参考与自举合同。

## 有序 record slot：现在可做的索引层

record 仍是有序 pair 列表，冻结容器懒建 slot metadata，不引入按字段名硬编码的 C# 领域对象。现有 FrozenIndex 是 Dictionary<string,int> 的末项索引，可推广为：验证状态、FirstSlot、LastSlot，以及更新专用的 SlotsByName（仅需要时建，存出现位置）。有序 children 是权威，索引只是旁路；迭代、JSON、hash、raw DAG 全按 children 顺序。

严格区分三种操作：record_find/index 读最后出现的位置；record_with 仅更新第一个匹配位置并保留后续重复项（:282–304）；record_with_many 将每个既有重复 key 的 value 更新为 updates 的最后值，并只为原记录缺失 key 追加一次，追加顺序由 updates 首次出现决定（:364–377）。不能用同一 dictionary set 覆盖这三种行为。

lookup 的现有索引对非空 pair 允许非文本 key、额外 pair 元素，且字符串查询只索引文本 key；批量快路径则要求每个 pair 恰有两个元素且 key 为 string。应分开保存 eligibility，不能把全部 lookup 缩紧为严格二元记录。零长度 pair / 非 list 元素应使快路径静默退回原扫描，保留“查找从末向前，更新从前向后”各自首先触发的错误；索引构造不可提前抛后来才会访问的坏元素错误。

最小轮可沿现有 LastSlot 索引接线，并为 record_with 的规则字符串表添加 FirstSlot；不必本轮增加所有索引缓存。小表继续线性扫描，避免替每个二元 pair 分配 dictionary。仅为冻结值缓存形状资格；MutableIndex 仍通过现有 parent invalidation 维护。

## 独立复制、别名与错误顺序风险

- 当前 copy_value 已是“冻结共享、读时解冻、根独占”，不是每次递归复制所有分支。只读 FrozenList 解决了实际类型与双数组，不能宣称消除了全部深拷贝或已经达到路径更新复杂度。
- 两份 copy 的同一冻结子节点在各自 owner 下 R.at 时必须各建可变 child；一份修改不可污染原冻结值、另一 copy、历史事件或 memo。LinkParent 的多父、递归失效保留，不能仅维护一个 owner。
- 读取冻结根返回冻结 child；对其 push/replace 仍应拒绝。读取可变根的冻结 child 会在该根安装解冻 child。record_find 的两层 ReadItem 是该机制的一部分。
- list_with_index:108–115 通过 ReadItem 取得非替换项，而且匹配使用 eq(i,index)，不是 Index(index)。可变源已有 child 可能被结果直接共享，这是其现有别名行为；不能顺带改为 snapshot 全源或强制整数索引。负数、分数、文本索引的异常/无替换表现需保持。
- eq 是 atom/身份相等，same_value 是结构相等。FrozenList 仍保留引用 Equals，不实现结构 Equals；否则驻留查重、memo key 与 eq 可能变化。
- SnapshotPool 的身份 hash 保持 exact/double 区别、double 按位比较（含负零），membership hash 仍仅作结构比较筛选。不得用新 record 索引改变池中 ordered hash。
- replace/push 先 Invalidate 后计算 Index 或写入。异常可能伴随缓存失效但不改变值；迁移不应为了简洁改变表达式求值顺序。record_with 先 Items(record)、再 snapshot(record)、再逐项匹配；批量更新空 changes 直接返回原 record，不访问坏 record。
- 原生 C# 通过 Items 返回的 List 可绕开 Invalidate 修改是既有平台边界；新 Items 的静态只读返回减少偶发绕过，但不能把外部原 List 的直接写入识别成已安全支持。生产生成代码只用既有 push/replace。
- 自循环 List 当前 snapshot 不具有周期图语义，不能在此顺带改成接受、复制或无限等待的新行为；独立边界 probe 应与旧实现对照。

## 需要下一层结构才能实现的路径更新

只读数组 FrozenList 的读取为 O(1)，冻结本身仍需 O(n) 扫描/数组分配，record_with 返回 mutable List 也仍需 O(n) 根数组。仅加入 slot dictionary 不会把更新变成 O(log n)。

后续可以为冻结序列增加内部 FrozenVector：固定宽度 32 的 leaf，count、height 和只读 node；Update(slot,value) 仅复制根至 leaf 的路径，Append 使用尾块；短 pair/小列表仍采用 flat children。FrozenList 对外维持相同读接口，内部 storage 支持 flat/vector 两种，JSON/raw DAG/同值比较仍按逻辑顺序。flat 可继续独占采用 children，vector 新建路径节点也独占采用内部数组。

但若每次 Update 后立刻 Thaw 为 List，仍会复制整个根，收益被抵消。必须先引入显式“持久更新返回冻结值、需要可变所有权时复制”的调用合同或内部 OwnedList overlay，并迁移 DV 中真正以新返回值替代旧值的更新点。用户要求 MutableItems 保持 List，意味着 overlay 不能暗中伪装 List；新类型必须经新的明示 ABI 与 owner materialization 合同引入，先只用于已证明没有后续原地 mutation/别名观察的路径。正式 copy_value 仍独占根。不能本轮把所有 record_with/list_with_index 的返回直接换成 frozen 后宣称等价。

完整 vector 层还需处理：保留当前顺序 hash 的增量组合（简单复用旧 flat hash 需要扫描 n）；驻留池的相等比较与身份复用；节点共享不使 RawDag 多一层逻辑节点；尾块与未知字段顺序；采用表等独占 builder 的发布边界；错误访问顺序。可先 flat 数组 + slot metadata 落地，再依据实际分配热点选择后续路径，不能只换数据结构名称。

## 实施与验证边界

第一层实施集中改 Runtime、Session 序列化，核对全部 List 模式匹配及 CWT 身份，更新 language_core/AGENTS.md、core/README.md、compiler/README.md 和必要 Session 工程说明；不用修改章节内容。代码审读完成后才按主任务授权执行集中工程排错。

必要的中性差分证据应覆盖：嵌套 copy 分叉/别名、多父失效、冻结写拒绝、严格非法集合、空/坏/重复/非文本 record、更新及查询错误先后、exact/double/负零、memo、Save/Restore、Trace、HostJson、raw DAG 编码。仅使用中性值，不推进游戏；这些验证只说明相应容器工程合同。工程和游戏窗口分别记录，不以测试数量或运行时微基准认定剧情、机制或体验改善。本审查尚未执行这些验证。
