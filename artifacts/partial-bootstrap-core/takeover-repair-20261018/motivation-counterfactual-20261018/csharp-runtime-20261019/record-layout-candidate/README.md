# RecordLayout 隔离候选（2026-10-19）

只写本目录，未修改生产、未构建、未运行探针或会话、未读取设计文件。Runtime.baseline.cs.txt与Session.baseline.cs.txt保留复制时生产原字节，SHA见BASELINE-SHA256.json；Runtime.cs是在该底本上只修改FrozenIndex并加入私有layout metadata及有界池的候选。

FrozenIndex先验证全部pair非空，只对纯文本键、最多128 slot、每布局累计最多4096 UTF16字符尝试共享。指纹包含原顺序与count，桶内仍逐项Ordinal核对所有位置；同key重复出现也入布局。命中不分配keys/dictionary；miss才建立独占keys和末slot字典。字典发布后不再写入。混合key保留原文本部分索引；坏pair返回null保留原扫描/首错；超准入或大表沿局部索引，不拒绝DV值。

SnapshotLock保护所有池读取/写入。容量按布局数量计，最多128布局；累计key长度最多65536 UTF16字符，重复位置照计。加入新布局将超数量或总字符门时只清池根再加入，不清任何已经发布的Index字典。旧FrozenList继续持有其旧字典。阈值8、snapshot/memo/DAG身份及其他运行时机制未改。

本层共享metadata只影响第一次创建索引的存储分配，不是O(1)形状识别，也没有事实等价或游戏收益证据。命中仍扫描键并完整比较；长key、冷schema及churn成本待测。池容量不是全进程活Index字典数量上限。

## 集中工程执行入口（尚未执行）

```powershell
python artifacts/partial-bootstrap-core/takeover-repair-20261018/motivation-counterfactual-20261018/csharp-runtime-20261019/record-layout-candidate/engineering/run.py --out artifacts/partial-bootstrap-core/takeover-repair-20261018/motivation-counterfactual-20261018/csharp-runtime-20261019/record-layout-candidate/engineering/run-01
```

必须指定不存在的新目录。before用本目录原Runtime/Session底本；after用本目录Runtime.cs和同一Session底本。新工具复制自structured-engineering，不修改已封存旧工具。保留其原生Session/stub/严格容器/原始DAG差分及三进程有限性能样本，另加layout模式；先双份快照后串行构建/执行。源码字节、日志、具体失败均保留。主代理集中工程授权后才执行。

layout-semantics.json比较同schema不同值、重复和换序key、1/2/3元素pair、混合key、坏pair两端/外部array、129形状两轮驱逐、128/129字段、512/513字符Unicode键、总池字符边界、Ordinal组合形式/大小写/空串/代理对及别名key修改。候选池有界状态通过反射观察并检查，不写入DV值也不跨版本比较其内部表示。已有冻结根在清池后重新查询。

layout-metrics.json独立记录同schema不同值的构造冻结、首次索引、重复索引分配与时间；结果语义摘要在计时外产生并加入逐字比较，metrics只作观察，不参与一致性判定，也不外推游戏。所有样本根保留至结束。

## 待主代理集成核对

当前只静态审读，编译和实际差分仍未知。生产集成须同步工程参考与AGENTS维护说明、集中工程排错，独立记录正式一级证据。此候选不含任何剧情与人物专用C#规则。失败样本及未执行范围不能用计数评价替代。
