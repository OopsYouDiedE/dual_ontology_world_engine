# Structured engineering 独立只读审查

审查日期：2026-10-19。审查上下文独立；未读取设计文档，未运行 probe、构建、游戏或正式会话，未修改生产源码。依据根目录和 language_core/AGENTS.md 的工程证据约束，读取 structured-engineering 工具、run-01 至 run-04 原始产物及当前 Runtime.cs、Session.cs。仅执行已有文件的读取、SHA-256 重算、原字节比较与 DAG 校验。复算明细另存 STRUCTURED-ENGINEERING-INDEPENDENT-HASH-AUDIT.json。

## 结论及适用范围

run-01 的空冻结集合枚举兼容错误真实存在，原始 observations.json 和 comparison.json 均保留了差异。run-02 对空集合委托原生空 List 的接口枚举器后，在既有输入中恢复了原先的泛型/非泛型 Current、Reset 与结束状态。run-03 的 CopyRoot 和 run-04 的 record_with/FrozenIndex 修改没有改变这些中性样本的原始输出。当前生产 Runtime.cs 与 Session.cs 分别和 run-04/after 的源文件逐字相同。

重算后的源码清单指纹、comparison.json 的 before/after 哈希和字节相等字段均与实际文件相符。performance-observations.json 与各进程原始 performance-metrics.json 内容吻合。未发现摘要把实际输出差异删掉或改成相同。此结论限于已保存输入与文件，不证明任意 DV 值、完整 Session、章节执行或游戏体验等价。

分配字节在该有限容器负载中减少；CPU/耗时收益没有稳定成立。尤其复制、分支读、追加和单字段更新的合并阶段仍可变慢，不能由本报告宣称正式五日窗口提速。

## 证据与版本链

各轮封存的 Program.cs 相同，SHA-256 为 `1f1245fa2e754855f17ba302fd4f3fa287c4254affc36134e5968ceba63274fd`。after/Session.cs 各轮相同，SHA-256 为 `7940aec4df0c10ea5f535ad79dd9580476bcf5d6a9f6421e92d935c683b32665`。after/Runtime.cs 依次为：

| 轮次 | SHA-256 | 相邻版本实际差异 |
| --- | --- | --- |
| run-01 | `44929b8ce57decc8adceda3d87c07497eaf685fd0f4e5a17caf10ec2b1dad085` | 冻结只读容器候选 |
| run-02 | `f5e57d0f270a047af841c5c20102e1a533f800a213b59bb1a5536c571ddd8a65` | 空集合两个 IEnumerable 接口委托原生空 List |
| run-03 | `6ee60ba4f6e30890dfbc8fed238df8c2932c4eb6fab52bbc458a53a152f1a532` | CopyRoot 使用私有数组建立独占 List 根，Thaw 调用它 |
| run-04 | `4857621e54e8191bd31f9736213af57a1eb99cc91be623e181f39bd70bdd9f17` | record_with 保存长度、直接取得 FrozenList、RawItem 读取；FrozenIndex 在验证后读取冻结 pair |

四轮 before/after 的构建与探针原始 stderr 为空，execution-status 记录子进程正常退出。run-01 的最终 Python 比较会因 observations 差异退出，但该最终比较退出码不在 execution-status 中；不能把该文件的子进程 exit_code=0 解读为整轮兼容。run-01 具体差异包括 generic.Current 从“未开始/已结束”异常变成 null，以及 nongeneric.Current 的异常文本变化。此失败证据未被后续目录覆盖。

## 序列化、原始 DAG 与异常记录

实际 Save、WriteSave 与 Restore 后 Save 的原始字节相同，保留有序 pair、重复 key 和 `$dv_rational` 标签；样本精确值包括 `1/3`、`9007199254740993`。Trace 与 WriteTrace 输出对应精确文本字符串，未改为浮点近似。HostJson 按原合同把规则 pair 列表映射成对象，重复 key 留最后值，同时保留精确数标签、嵌套数组和空列表。该 HostJson 变换不能替代原始 Save；工具确实分别保存并比较了它们。

原始 neutral.dag.jsonl 与 failure.dag.jsonl.partial 的双方字节在各轮相同；复算直接提取每行原始 payload 字节，依 `previous ASCII + LF + sequence ASCII + LF + payload UTF-8` 核对链，并逐行核对 seq/prev。未发现链断裂。正常文件以 seal 结束；重复事件样本的失败文件以首条事件结束、保留 partial，未发布 failure.dag.jsonl。observations 保留重复事件的异常类型/消息。这里验证的是主动重复事件拒绝和 Abort 路径，没有模拟磁盘满、写入中断、Flush/Dispose/Move 失败或进程强杀，因此不能扩大成所有异常封存均已验证。

Session 探针采用真实 Session.cs 配合中性 DVProgram stub：Prepare 返回原值，Complete 返回 receipt，SessionClose 返回原值。它能观察 CLR 容器与 JSON 边界，不能验证生产 prepare/complete、真实快照、SessionClose 副作用、章节扩展或完整恢复。512 深度、非有限数字、深树、畸形标签及所有解析错误顺序没有由此样本实际覆盖。

## 分叉、别名与静态边界

observations 实际保存原冻结树、兄弟 copy 与变更 copy；样本检查了修改一份 copy 的选中嵌套分支后，另一份 copy 与冻结源保持原值。共享可变子树的两个父根失效、记录 key 修改后的索引失效、旧冻结索引稳定、memo 取回独立副本和仍存活规范身份均有具体输入。循环样本只验证已建立父关系的失效遍历会终止，没有对循环执行 snapshot/JSON，也不能声称支持循环 DV 值。

当前 FrozenList 的私有 object[] 不暴露；CopyRoot 的 `new(children)` 复制根数组，后代仍是冻结子树；ReadItem 仅在可变所有者选择分支时 Thaw 并建立父失效关系。run-04 的 RawItem 避免在 record_with 扫描中物化分支，更新输出仍按原序选择首个 key；FrozenIndex 在第一轮验证后，冻结记录中的合法列表子项均由递归 snapshot 转成 FrozenList，因此具体类型读取在现有合法值边界内有源码依据。根复制和更新仍为线性遍历。

工具有意不比较冻结表示的 CLR 类名；其 Outcome 把值按 DV JSON 表示比较，并单独记录异常类型/消息。因此不能从输出一致推出所有 CLR 身份、反射、类型检测或第三方 IEnumerable 行为一致。Items 的返回签名发生变化还要求生成编译器、Session 和 Godot 宿主统一重建；中性 stub 工程不能验证整个生产依赖闭包，需由主代理当前工程构建另行给出实际证据。

## 实际性能观察

输入构造、JSON 与摘要计算排除在计时之外；全部冻结根、copy 根与更新根保留到进程结束。每版本先完整运行，再运行另一版本，未交错/反向平衡；进程独立且继承环境，没有保存完整 GC/Tiered/CPU 负载配置，也没有 CPU 时间指标。Stopwatch 是经过时间，不能精确归因到 CPU。JIT、分层编译、GC 与外部调度噪声均未隔离。

run-04 原始观察如下；每格按三个独立进程顺序列出，单位 ms：

| 负载 | before 耗时 | after 耗时 | before → after 当前线程分配字节 |
| --- | --- | --- | --- |
| 唯一小树冻结 | 19.231 / 21.5905 / 21.2485 | 17.1394 / 18.5772 / 19.1889 | 10,041,696 → 9,121,696 |
| 唯一大树冻结 | 113.3785 / 125.1029 / 120.5238 | 91.9069 / 110.638 / 82.1897 | 36,191,448 → 32,513,464 / 32,513,464 / 32,513,256 |
| 独立复制、读、追加、稀疏更新 | 47.2066 / 43.3986 / 51.8897 | 44.9445 / 70.0183 / 53.0326 | 11,417,200 → 9,665,200 |

分配观察与源码减少冻结容器额外存储的方向一致。大树 after 前两个样本实际发生更多 Gen2 收集（2 对 1），因此少分配不自动意味着更少 GC 或更短耗时。run-01..03 的复制更新阶段同样存在 after 更慢的实际记录；本报告保留该事实。run-03→04 的微样本变化也不能独立归因到具体类型读取，因为轮次环境和调度没有控制。

性能语义文件的摘要涵盖全部保留根并与底本相同，但原树 JSON 本身未逐根另存，摘要复算需要重新执行探针；本审查只复算已保存文件的字节哈希，不重新执行它。该有限输入的工程收益不外推正式会话，更不换算五日覆盖耗时。

## 需明确记录的限制

structured-engineering/README.md 仍写“尚未运行”和“已准备，未执行”，与已存在 run-01..04 产物不符，建议主代理同步更新状态并明确首轮兼容失败及后续修复。run.py 先完成双方及全部性能进程后才比较原字节，因此 README 的“有差异……停止”是末尾发现差异后的停止，不是首个差异即时停止。

本审查属于已有工程证据的只读复核，没有执行一级、二级或三级验收。人物行为、剧情机制、五日时域、UI、碰撞、实际玩家体验均未验证；后续正式窗口须由新的独立上下文审查。本次写入仅为这份报告和哈希复算明细，既有原始证据与生产文件保持原样。
