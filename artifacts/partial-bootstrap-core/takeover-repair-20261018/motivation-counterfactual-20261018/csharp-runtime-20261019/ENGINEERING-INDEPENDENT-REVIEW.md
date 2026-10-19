# 中性工程证据独立审查（2026-10-19）

本审查由独立上下文代理只读检查现存源码封存、工具及原始输出；未读取设计文档，未启动构建、探针、游戏或正式会话，未修改生产源码。本文新增是本代理唯一写入。范围为 `record-layout-candidate/engineering/run-01` 与 `literal-capacity-candidate/engineering/run-01`，不评价剧情、人物、游戏完成度或整局速度，也不构成任何游戏验收级别。

## 来源与独立复核

逐一重新计算两个 comparison 中 before/after 原始文件的 SHA-256，均与各自记录一致；同时重新计算 record-layout source-manifest 中封存副本与源路径，以及 literal-capacity source-hashes 中封存副本与源路径，未发现记录偏差。这是当前读取时的文件核对，不是运行时防篡改监测。

record-layout 的 before Runtime 为 `4857621e54e8191bd31f9736213af57a1eb99cc91be623e181f39bd70bdd9f17`，after 为 `1fcd15aa32086ca2a3f12c0e357c1ff905c48fe2d90c0749bbde4657cd929941`；两边使用相同 Session（`7940aec4df0c10ea5f535ad79dd9580476bcf5d6a9f6421e92d935c683b32665`）、Probe 与 Stub。不是用当前源冒充旧 Runtime。Stub 将生产 Session 所需 DV 函数替换为中性返回，因此这里只覆盖 Session C# 边界及值表示，没有执行真实 campaign 的 prepare/complete 语义。

literal-capacity 的 before 编译器来自 `frozen-04-integrated/source` 已存在的 C3 cache，工具明确在 cache 缺失时停止而不重建；after 使用当前 toolchain C3。封存 compiler.dv 实际差异是 surface list literal 的 List 容量发射；同时 `00-data.dv` 将 dv_pair/dv_single/dv_list3 的 push 构造改为 literal，其余所取 core 源在两边一致。两边 Runtime 都是上述 record-layout after，fixture 与 Wrapper 相同。因此该样本考察“容量发射与小列表构造”组合的增量，不是最初 Runtime 到最终整合版本的总收益，也没有拆分两项修改各自贡献。

literal 工具记录 compiler.dv 源哈希、命令与生成 Program.cs，但未把 after C3 程序及其哈希封存到 run-01；它依赖工作区 obj cache。before C3 则复制了整个缓存。这是长期重现证据的缺口。审查时命令实际指向的 before Program.dll SHA-256 为 `405bb562d446618cd4fe0cf0f8174b4e8ce1d126786a7f3d411c14bed3294256`，after 为 `11c0a8e99134f115850eb0c5c64e04ced1a585602d59a707265865f4bc387092`；这些是事后读取值，不代替运行时封存。两工具也未封存完整运行环境、CPU调度或系统负载记录。

## 值、别名、错误与字节

record-layout 的 observations 与 layout-semantics 前后字节一致。工具实际覆盖冻结值与外部数组/只读集合边界、枚举状态、精确数、重复字段、坏 pair 的位置与形状、非文本键、独立复制与兄弟隔离、共享子值修改后父缓存失效、旧快照稳定、memo 返回隔离、循环失效终止，以及布局池的驱逐、宽度、键长、字符容量与 ordinal 文本匹配边界。实际记录保留了细节：重复键 find/index 取后项，with 改首项；索引改键后为 -1，旧快照仍为 5；坏 pair 的位置会影响先返回还是抛 Expected List；冻结对象 replace 在坏索引前先拒绝修改。不是仅比较一个最终成功值。

Session 的 Save、流式 Save、Restore 后 Save、Trace、流式 Trace 与 host snapshot 均在 before/after 对应文件间保持字节一致；这个 fixture 中 Save 三种路径相同、Trace 两种路径相同，精确数 Save 使用 `$dv_rational` 标签，Trace 使用精确字符串。中性 session prepare/complete 只经过 Stub，不证明真实调度、关闭等待、阶段恢复或游戏存档语义。

`neutral.dag.jsonl` 及重复 event ID 失败后的 `failure.dag.jsonl.partial` 前后原始字节一致。本代理直接按原始行的 payload 字节重算 SHA-256 链，未发现序号、prev 或摘要不一致。正常文件以 final_checkpoint=true 的 seal 收尾；失败文件保留 partial 且无 seal，observations 记录 `Repeated raw event ID: duplicate-id`。这里没有执行磁盘写入失败、进程中断或全游戏 DAG 恢复。

literal-capacity 的 semantics.json 实际保留左到右副作用顺序、空/单/双/三/较长 literal、共享子列表修改、clone 子分支隔离、重复字段更新、精确数与嵌套 JSON，其前后原始字节一致。错误 fixture 实际抛 ArgumentOutOfRangeException，主消息一致，先前副作用只有 before-error，后续标记未发生。exception.json 只比较异常类型、主消息和已发生副作用；完整堆栈另存 exception.raw.txt，没有要求调用栈字节相同。Wrapper 的 catch 也会捕获自己抛出的 Expected error was absent，故一般性判据偏弱；本轮原始 exception 确实是预期的越界类型，不存在本次被掩盖的“没有异常”。literal 工具没有调用 Session Save/Restore/Trace 或 raw DAG；不能把 record-layout 的相同字节证据自动扩展成 literal 组合已单独验证这些边界。

## 已知分配收益与计时边界

record-layout 的单次 layout 工作负载中，同形状记录首次索引分配：宽8为 1,016,000 → 25,200 字节，宽128为 10,216,000 → 35,360 字节。重复索引两边均为240,000字节；构造冻结分配基本不变。三个 fresh-process 的 copy/read/sparse-update 样本均为 9,665,200 → 7,067,568 字节。small freeze 始终9,121,696字节；large freeze 分配约32.5MB且存在小幅变动，不能称其消除了冻结分配。

layout 首次索引的 elapsed milliseconds 在本轮样本下降（宽8：1.2923 → 0.7301；宽128：22.4856 → 10.9466），但构造冻结上升（6.313 → 8.0822；175.7627 → 206.5879），重复宽128也从0.8793升为0.9933。copy/read/update 的 before 为54.6162–82.9574ms，after 为20.0066–23.6191ms，显示本样本方向明确。不能据此宣称所有路径更快或给出整局 CPU 降幅。

literal-capacity 的每个样本构造30,000个保活结果根，当前线程分配均为15,840,000 → 12,240,000字节，即该样本减少3,600,000字节。before 计时为25.6669、27.615、39.2017ms，after 为17.2143、18.5535、19.4905ms；这是有限构造样本的观测。计时只包住构造循环，输入字符串预先准备，JSON与语义哈希在计时外。它不计编译、自举、证据输出或整局认知执行；分配指标不是峰值RSS或存活堆大小。

两工具均先完成全部 before 再完成 after，未交错或随机顺序；进程新开且只有很少预热，tiered JIT、GC与时变负载仍可能影响毫秒结果。Stopwatch 测的是墙钟经过时间，没有采集 Process.TotalProcessorTime，故CPU收益尚未直接测量。record-layout 的 layout 计时也只有一个进程样本。不能把这两组增量毫秒简单相加或将分配差额换算成240秒正式会话覆盖时间。

## 结论与未覆盖范围

现存证据支持两组有限中性输入的输出与所记录边界保持一致，并支持共享布局首次索引、copy/read/update 及 literal 构造样本的具体分配减少。计时支持部分样本变快，同时明确存在构造冻结样本变慢；CPU、长期内存、并发访问、未执行异常与任意嵌套输入仍不确定。

本审查没有运行正式一级会话或 Godot，未审查设计符合性、人物行为或游玩体验。真实会话覆盖、五日时域、剧情节点及证据完整封存须分别依据独立正式运行记录判定。
