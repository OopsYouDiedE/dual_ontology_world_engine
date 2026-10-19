# Structured runtime neutral engineering tool

准备日期：2026-10-19。当前仅编写与静态审读，尚未运行、构建或取得差分结论。

后续实际执行记录：run-01观察到空冻结集合枚举器异常差异，整体比较退出失败；其execution-status只覆盖子进程，不代表最终比较结果。修复后run-02，及批量解冻/具体冻结索引优化后的run-03、run-04均保留独立目录。原生保存、恢复、Trace、HostJSON和DAG原字节可在comparison.json核对。有限样本分配下降，CPU结果有波动，不宣称稳定提速；独立审查见上级STRUCTURED-ENGINEERING-INDEPENDENT-REVIEW.md。所有历史输入与失败原样保留。

从仓库根执行：

```powershell
python artifacts/partial-bootstrap-core/takeover-repair-20261018/motivation-counterfactual-20261018/csharp-runtime-20261019/structured-engineering/run.py --out artifacts/partial-bootstrap-core/takeover-repair-20261018/motivation-counterfactual-20261018/csharp-runtime-20261019/structured-engineering/run-01
```

入口要求全新输出目录。先复制stage02 Runtime、Session原底本和当前生产Runtime/Session，再记录指纹，然后串行构建/运行两份隔离工程。工具不修改生产源码，不载入DV内容或设计文件，不启动Godot或正式游戏。中性DVProgram stub仅为实际Session.cs提供F_入口，不能验证任何游戏执行机制。

证据保留完整stdout/stderr、源码、原生Save/流式Save/Restore后Save、Trace/流式Trace、HostJson、原始DAG及失败partial、异常类型/消息和逐项行为观察。comparison.json对这些文件比较原字节；有差异保留双方实际记录并停止，不转换成统计通过率。构建或probe失败保留execution-status和日志，不继续冒充完成。

覆盖严格拒绝数组及ReadOnlyCollection、冻结拒写和索引首错、有序重复key三种行为、坏pair/非文本key、copy分叉、多父失效和循环失效遍历、索引失效、memo返回复制及存活规范身份、generic/nongeneric枚举与Reset/结束状态、精确数标签与文本、原始DAG节点/引用/重复事件失败。冻结表示本身不同，不比较其CLR类名。

Save标签与Trace文本有明确中性检查；默认System.Text.Json是否识别冻结只读类型由原生Session输出直接观察。观察与有限检查仅说明所覆盖的工程边界，不是正式一级、画面或游戏验收，也不说明性能收益。工程运行需要主代理集中排错授权后执行。本工具没有耗时收益基准。


## 有限中性性能样本（已准备，未执行）

每版本另开三个独立进程，原样运行同一输入，分别保存performance-1至performance-3。每进程预先建立2500个三字段小树、256个128字段大树；每个树的每个叶分支带唯一文本，避免把重复树驻留命中误当全新冻结成本。输入构造不计入冻结阶段，少量预热根也持续保留。

分别记录唯一小树冻结、唯一大树冻结、1500次独立copy分叉/分支读/追加及单字段record_with的线程分配字节、Stopwatch耗时和GC收集次数。所有冻结返回根、独占copy根与稀疏更新根一直保留到样本结束，不主动GC，不因弱身份消失而使两版本采用不同身份样本。这个有限规模是工程样本，不能推算正式五日或游戏窗口收益。

语义摘要在计时之外按有序JSON逐根SHA-256，分别涵盖小树、大树在copy之后的原值、copy分支和稀疏更新。performance-semantics.json加入before/after逐字比较；performance-metrics.json和汇总performance-observations.json仅保留观察，不加入一致性判定。三进程重复便于观察噪声，不把一次微样本结果宣称为稳定收益。样本使用中性文本和容器，不围绕数值缓存构造输入。
