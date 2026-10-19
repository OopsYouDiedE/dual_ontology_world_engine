# C# Runtime 独立只读审查（2026-10-19）

审查范围：当前 compiler/Runtime.cs 与 Runtime.before.cs.txt 的差异、Probe.cs.txt、run_engineering.py、engineering-03 原始工程证据。未读取项目设计文件，未修改源码，未运行正式游戏。候选 Runtime SHA-256：`a3dffd19dca6538b87e4047d931ebb55241b646f40fccf40233ed27232540d82`。审查时当前源码与 engineering-03/after/Runtime.cs 字节相同。

## 静态结论

SameValue 仅用值兼容指纹拒绝不等结构，所有有限 double 与合法 Rational 共用数字 token；指纹碰撞仍沿原顺序比较，不拿保留精确表示的快照身份哈希代替数值相等。缓存仅挂在冻结树及未失效 Backbone 上；现有嵌套写入失效路径保留。

快照池在 Gen2 后分批移除死亡弱引用和空桶，活对象条目继续留池；同值重新构造与 GC 后 memo 查找的工程观察支持该边界。队列与字典仅在同一 SnapshotLock 内维护，递归构造先处理子节点、再取得当前 bucket，未发现清理造成局部 bucket 悬空的路径。

DAG 复用单个 IncrementalHash，每帧 GetHashAndReset；96 字节前缀容纳64字节前摘要、两处换行及 Int64 十进制序号。构造时摘要初始化失败的流释放缺口已修复；正常 Finish 和显式 Abort 释放摘要与流。Frame/FlushCommit 异常将写入器标失败，后续释放仍依赖调用方 raw_abort，未将其误写成自动释放。

JSON 已移除按 history、simulations、world、adoptions 等字段名静默替换为空数组的分支。列表顺序和数值编码保持，16MiB Flush 仅改变缓冲提交时机。这项完整性修复会增加原本被删减JSON的数据量，不能把本轮JSON与旧删减输出的大小/耗时直接当作同条件性能比较。

## 工程证据复核

独立重算了结构结果、完整 neutral DAG 及 duplicate-ID partial 的 SHA-256，并逐帧重算 DAG 摘要链。before/after 对应文件逐字节相同；完整 DAG 末尾为 seal，失败 partial 末尾为已提交 event，保留失败前缀且未伪造封存。两份 stderr 均为空。

中性JSON样例的旧输出丢失history、simulations、world与adoptions；候选输出与输入完整值相同。样例不构成所有输入的穷尽证明。

engineering-03 的长公共前缀结构比较耗时记录为旧253.2084ms、候选1.3358ms，分配均32字节；此为刻意针对不等结构的中性微负载，不外推游戏提速倍数。最终Gen2与维护后快照桶由旧172429保留到候选893；此反映死桶清理，并非完整游戏内存占用测量。活同值重新入池身份及memo在清扫后保持。

## 限制与未验证

未故障注入摘要算法创建异常、磁盘写入/Flush/重命名异常、Dispose异常；这些释放及失败发布边界依据静态审读，duplicate-ID失败不能替代全部I/O异常。未穷举极深/循环非法容器、所有畸形输入的首错顺序、并发调用与字符串哈希碰撞；没有在这些未覆盖边界作行为保证。

本报告仅为工程审查。当前未发现阻止候选进入隔离冻结正式窗口的具体新缺陷；未据此宣称卡死根治、五日剧情达成、人物行为改善或游戏体验验收完成。正式新会话、并发Antigravity变更隔离与封存完整性须由主代理另行核对。
