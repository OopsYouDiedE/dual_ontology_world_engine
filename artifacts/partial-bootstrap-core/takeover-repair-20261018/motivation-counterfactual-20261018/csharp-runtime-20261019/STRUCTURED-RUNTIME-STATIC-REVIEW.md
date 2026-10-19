# 只读数组 FrozenList 候选静态审查（2026-10-19）

审查对象：compiler/Runtime.cs 与 Session/Session.cs，以同目录 Runtime.stage02.cs.txt 为运行时对照。未读取项目设计文档；未构建、运行探针或会话；未改生产源码。以下结论仅来自源码与生成链阅读，不代表工程运行结果或游戏验收。

本次复读定位 SHA-256：Runtime.cs `44929B8CE57DECC8ADCEDA3D87C07497EAF685FD0F4E5A17CAF10EC2B1DAD085`；Session.cs `7940AEC4DF0C10EA5F535AD79DD9580476BCF5D6A9F6421E92D935C683B32665`；stage02 `45B8EE25CEEEFCDA72F3B5B0B30EC2A86A735F7030A44C6318C8176A4B02D31B`。生产文件仍在集中修改，后续版本需要按差异复核。

## 可行动发现与处理状态

1. **冻结下标错误文本差异已在本次审查期间修正。** 初次读取的 FrozenList 下标使用 `throw new ArgumentOutOfRangeException(nameof(index))`，类型与参数相同而消息不同于 stage02 的 List 下标。已及时发给主代理；复读 :444–456 看到静态空 List `InvalidIndexSource` 与 `return InvalidIndexSource[index]`，现在由原 List 实现生成异常，保留本地化消息、类型与 ParamName。有效读取仍直接读 children。该修正还没有执行证据。

2. **工程文档有既有语义误述，应随本轮修订。** language_core/AGENTS.md:225 称 `rt_record_with` 更新全部同键项，但当前及 stage02 的 record_with 都用 `!found`，只替第一个重复项；record_find/index 则最后项优先，record_with_many 才更新全部既有重复项。该事实已发给主代理。此处应改工程描述，不改源码以迁就错误文档。

3. **新增 slot 与持久路径更新尚未实现。** 当前冻结表仍只有原来的末项文本键 Index，没有 FirstSlot/重复 slot 列表；record_with 扫描全根，record_with_many 仍建新 List；Thaw 按 Count 逐项 Add。因此可以声明“真实只读冻结数组、取消新冻结值的第二份后备数组”，不能声明完成有序 record slot 更新或持久向量路径复制。这是交付范围限制，不是已发现的值语义错误。

除上述已修复异常与文档问题外，本次静态检视未定位到必须先修改才能继续集中工程排错的确定性容器错误。这个结论有下述运行未验证范围。

## ABI 与识别覆盖

- Items 返回 IReadOnlyList<object>，IsDvList/TryItems 只接受 List<object>（保留原 List 子类）和内部 FrozenList；未扩大到 object[]、任意 IReadOnlyList、ReadOnlyCollection 或 IEnumerable。MutableItems 仍返回 List，冻结写操作先走原拒绝错误。
- show/FormatList、SameValue、HasValue、record_index/find/with/with_many、批量快路、memo 分区、copy_value/is_list、membership 查询、冻结/可变记录索引、JSON 与 raw DAG 根/子节点的判断均已适配。剩余 `is List<object>` 用于 MutableState 身份、可变 parent、SnapshotValue 的可变输入和可变 mutation，不是明显的冻结值漏识别。
- record_index/find 的 `(List<object>)entries` 只有 frozen 为 null 时执行；Items 的值域保证此时 entries 是 List，未见非法接口强转。memo_put 的 SnapshotValue(Items(owner)) 收到接口引用，但运行时对象仍是 List/FrozenList，不会把适配视图本身冻成错误 atom。
- compiler.dv 的数组字面量和 comprehension 仍输出可变 List，comprehension 只对 Items 使用 Count，读取使用 R.at，符合新接口。当前源码中的直接 R.Items 外部调用是 Session 的 pair 读取，没有发现赋给 List 局部或直接 Add 的调用点。
- Items CLR 返回签名已变，旧编译程序集不能与新 R 混用。dv_toolchain.py 的源指纹覆盖 compiler 目录文件，Runtime 改变会分开缓存；自举每阶段复制 Runtime 并重新编译。Session csproj 从源码 Include Runtime，先生成 DVProgram，再编译。仍须按本轮集中步骤实际重建宿主及类库，不能静态声称二进制兼容。

## 枚举、错误与独立复制

- FrozenList 的 children 为私有 readonly，构造器只由 SnapshotValue 调用；该方法先递归冻结，再独占采用本轮 children，发布后未见写数组或暴露数组引用。命中驻留池仍复用既有冻结身份。
- 自定义 struct Enumerator 在开始前/结束后，泛型 Current 返回默认 null，非泛型 Current 抛 InvalidOperationException；MoveNext 结束后设终止索引，Reset 重置，符合 stage02 List 枚举器的可观察形态。冻结值无修改版本，因其不再可变无需版本比较。默认零初始化 enumerator 的私有数组为 null，类似默认 List 枚举器只在真正操作时失败，生产路径总由 GetEnumerator 初始化。
- ReadItem 对冻结根返回冻结 child；对可变根使用 MutableItems，在当前 owner 内解冻并 LinkParent，再写当前 slot。copy_value 仍新建根 List，兄弟 copy 共用的冻结节点在各自读取时独立解冻。SnapshotValue 只为真正可变 List 子节点 LinkParent。原多父弱引用、祖先缓存失效传播均保留。
- RawItem 替换原 pair 的 List 快路径/Items fallback，没有调用 R.at，不引入额外解冻。非列表仍 Expected List，空/短 pair 在访问原位置时错误；新冻结下标已复用 List 错误。非文本 key 继续 EqValue，重复项顺序未变化。
- 新容器未重写 Equals/GetHashCode，因此 eq、驻留池 atom/引用比较与 memo 身份不会自动变成结构比较。冻结身份 hash 与 membership hash 的用途仍分开；exact/double 兼容比较仍用后者，原字符串/数值处理未见由本批引起的变化。

## Save/Restore、宿主与证据序列化

- Session SnapshotValue/HostValue 已用 R.TryItems 识别冻结容器；HostValue 的 record 检测仍要求非空、全部二元文本 key pair，重复 key 在 Dictionary 末项覆盖，空列表仍数组。Restore/ReadValue 继续恢复可变 List 与 Rational 标签，未引入数组作为 DV 运行值。
- Runtime WriteJsonFast 已处理两类列表，raw_ref/RawDag.Node 同样识别冻结根与所有嵌套冻结节点。RawDag 的逻辑容器节点和元素顺序不因私有后备数组而增加层级。json_stringify 的冻结身份缓存保持有效。
- Save/WriteSave/WriteTrace 继续使用 JsonSerializer，没有新增 converter。新 FrozenList 实现 IEnumerable<object>/IReadOnlyList<object>，源码形态符合集合序列化而非属性对象的预期；不能仅因其不再继承 List 就认定默认序列化失败。Trace Rational 仍有原类型 converter，Save 的 options converter 覆盖为 $dv_rational。这里尚未实际确认私有嵌套集合运行时元数据、深度、输出字节、异常或恢复后的值；集中中性探针应覆盖冻结根和可变根内的冻结 child，不应只验证 Runtime 的手写 JSON 路径。
- DvSession 方法及 Godot GameEngine 的字符串接口不变。宿主无需把 FrozenList 暴露为公共值类型；类型化公共 API 若曾消费旧 Items 的 List 返回，属于需要重新编译/迁移的源接口边界。

## 可见优化与仍存分配

新冻结值从一份 children 临时数组加 List 后备数组，变为直接采用单份 children；空值采用 Array.Empty。Thaw 明确预分配 Count 后 Add，避免 IEnumerable 构造失去 ICollection 快路后的扩容。冻结类型直接 foreach 使用 struct Enumerator，不分配迭代器对象。

但 Items 返回接口后，text_join、TryQueryMembershipHash、record_with_many 及 TryStringRecordUpdate 等经 IReadOnlyList 的 foreach 会装箱其枚举器；原已知 List 的 foreach 通常使用未装箱 struct。可按实际热点将纯只读 foreach 改索引循环，或直接类型分派，但不是当前语义缺陷，也不能在未测量时认定为性能回退。TryMembershipListHash 已改索引循环。冻结池扫描、新 children 在驻留命中时的临时分配、根 List 构造、递归 hash、末项索引懒建仍存在。

本报告不要求在这一批额外加入整套持久向量；应由主代理明确本轮范围，避免在集中冻结前无限扩张。若目标包含路径更新，就需落实新的所有权/返回合同，否则更新后立即 Thaw 仍复制全部根。

## 集中工程排错仍应覆盖的事实

在主任务允许的统一排错阶段，比较 stage02 与候选的中性输出：非法集合域、双 copy 分叉、多父失效、冻结 mutation、重复/坏/非文本 record、错误类型/ParamName/message、generic/non-generic 枚举、exact/double/负零、memo、三种 JSON 路径、Save/Restore、RawDag。工程 probe 不推进游戏；游戏会话与构建事实分别报告。本审查没有执行这些内容，也不以静态无确定错误作为运行完成声明。
