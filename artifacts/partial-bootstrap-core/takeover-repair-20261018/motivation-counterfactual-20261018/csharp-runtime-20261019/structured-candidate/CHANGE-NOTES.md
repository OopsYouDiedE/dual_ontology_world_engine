# 第一层只读冻结结构候选

范围：仅本目录新增文件；生产源码、章节 DV、编译器 DV 与生成物均未修改。未读取项目设计文件。底本复制先于编辑，原字节与 SHA-256 保存在本目录。Runtime 使用第二批 stage02，不包含第三批未经整局收益确认的数值表示候选。

## 完整改动

- 增加严格 IsDvList/TryItems：只识别 List<object>（含既有子类）和内部 FrozenList。Items 返回 IReadOnlyList<object>；object[]、外部任意 IReadOnlyList、其他集合均没有因此成为 DV 列表。辅助 API 的公共可见性供同程序集外的 Session 源边界调用，不允许构造冻结对象。
- FrozenList 是 private sealed IReadOnlyList<object>，私有 children 数组由 SnapshotValue 新建、递归填完后独占采用；空表采用 Array.Empty。移除 List 继承、第二份后备数组及 AddRange。下标主动保留原 List 越界异常类型与 index 参数；枚举只暴露只读元素遍历，不暴露数组。所有原 hash/索引/成员集合/JSON/memo 缓存仍依同一冻结对象身份持有。
- show/FormatList、结构相等、成员查询及其有界 query hash、字符串记录更新快路径与 memo partition 标签均改用严格读视图；CWT 查询只对实际可变 List 做。membership 哈希桶、构造结果、memo miss 以及可变父引用继续使用原 List。
- record_index/find 的有序反向扫描和末项索引识别冻结 pair；record_with 的正向首次替换保持；record_with_many 的每个既有重复项更新、updates 末值与缺失键首次出现追加顺序保持。字符串批更新只在全部二元文本 key pair 时进入快路径，原始 item 身份用于未修改分支。非法/空 pair 的资格检查仍无预先抛错，退回原扫描位置。
- copy_value 识别冻结输入并返回独占可变根。Thaw 按已知 Count 一次预分配，再逐项 Add。MutableItems 继续只返回 List，FrozenList 保留原 Cannot mutate 错误。ReadItem 先通过只读视图读取，仅可变 owner 下的冻结 child 被解冻、链接并写回该 owner。
- LinkParent 删除已不可能的 FrozenList/List 子类型判断；MutableIndex 识别两类 pair，只链接可变 pair。单父、多父弱引用、死亡引用清理、循环失效遍历、Backbone/Index 失效规则未改。SnapshotValue 只链接真正的可变 child。
- WriteJsonFast 对严格 DV 列表写有序数组；json_stringify 的冻结身份缓存保持。Raw DAG 的 root、child、raw_ref 判定同步识别 FrozenList，节点引用与数组顺序保持；未改帧摘要、flush、seal、partial 与 I/O 异常边界。
- Session SnapshotValue/HostValue 显式使用严格列表识别。Host record 仍须非空、全部二元文本 key pair，重复 key 后值覆盖；空表仍投影数组。ReadValue/ReadJson 仍创建 List。
- Save/WriteSave/WriteTrace 保留默认 System.Text.Json 集合路径与原 Rational 转换规则，未注册宽泛 IEnumerable converter。README 明确默认 IReadOnlyList 数组识别及嵌套精确数行为尚待工程验证。

## 保留边界

驻留表的存活身份、精确/普通数身份 hash、负零按位比较、碰撞后的原序比较、memo 命中后的 copy_value 均未调整。replace/push 的 Invalidate 先于索引/写入，record_with 的 Items→snapshot→扫描，空 updates 提前返回均保留。eq 未改为结构 Equals。候选未扩展自循环 snapshot 的值域或引入周期图语义。未添加 C# 世界或人物专用类型。

## 风险与待验证

这里只完成源码文字迁移与 diff/列表模式审读；没有运行编译器、构建、工程探针、正式一级、UI 或三级会话，也没有事实等价或速度证据。

Items 返回签名是 CLR ABI 改变，所有消费程序集和编译器自举阶段需要统一重建；生成代码仍应由 DV 源生成。静态阅读预期现有 Count/枚举和 R.at 调用兼容，实际编译仍未知。

待集中核对嵌套 copy 分叉、冻结写拒绝、多父与循环失效、非法集合、空/坏/重复/非文本 key、访问与异常先后、exact/double/负零、memo、Save/Restore、Trace、Host JSON、Raw DAG 原始字节。默认 System.Text.Json 识别私有 IReadOnlyList 的数组形状及不同 options 的 Rational 输出尤其需要验证。Host 的未知输入透传保留旧边界，没有把未知集合声明为合法 DV 值。

冻结仍扫描 n 个 children 并分配平面数组；可变根更新/解冻仍线性复制。接口分派、数组枚举和范围检查的实际成本未知。未实现 FirstSlot 元数据、持久向量、overlay 或 O(log n) 路径更新；不以去掉第二数组推断正式会话收益。

主代理集成前补充：FrozenList提供保持List枚举器状态语义的struct Enumerator；count/at和记录pair读取走已知具体类型，避免新增接口调度/枚举器分配；成员哈希的只读列表遍历按索引执行。这些改动同样等待集中工程差分。
