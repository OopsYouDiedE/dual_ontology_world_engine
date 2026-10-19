# Structured runtime candidate — 2026-10-19

本目录是隔离候选，尚未集成生产。Runtime.cs 取自同级任务目录的 Runtime.stage02.cs.txt；Session.cs 取自复制时的 language_core/Session/Session.cs。底本原字节副本与来源 SHA-256 见 BASELINE-SHA256.json。无第三批数值缓存迁移。

候选只实现数组支撑的真正只读 FrozenList 与相关平台接线。它不实现持久向量，不宣称路径更新 O(log n)，不改变 DV 世界、人物或动作规则。

Save/WriteSave/WriteTrace 暂沿用 System.Text.Json 默认集合识别：FrozenList 实现 IReadOnlyList<object>，预期序列化为有序数组；SnapshotOptions 的 Rational 转换器仍输出 $dv_rational 标签，TraceOptions 仍采用 Rational 的字符串转换器。这一默认集合识别、私有嵌套类型以及嵌套 Rational 的实际组合均待主代理集中工程验证，当前不是已验证事实。Trace() 走 Runtime 的严格列表 JSON 写入器。HostValue 与 SnapshotValue 显式识别两类 DV 列表。

本子任务仅做文本实现与静态差异审读，没有构建、探针、会话或性能测量。集成需统一重建使用 Items 返回签名的程序集及自举产物，并由主代理同步生产工程参考、AGENTS.md 和 Session 边界说明。不要把此目录源码与旧程序集混搭。

完整改动、保留语义及风险见 CHANGE-NOTES.md。
