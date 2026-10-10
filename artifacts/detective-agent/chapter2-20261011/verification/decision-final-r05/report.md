# Core 行动经历缓存与发布锁独立复验

本轮 Core Release 构建、SubjectiveDecisionRegression、SubjectiveReasoningRegression、CognitionRegression、决策 `--api-probe` 和追加中性探针均通过。验证对象是当前 `SubjectiveDecision.cs` 中按 Projection 弱引用、不可变 LastObservation 身份复用的解码 DTO，以及 Observe / Assess / Capture 共用的主体发布锁。

执行分支为 `codex/virtual-detective-chapter2-20261011`。在收到 `verify_learning_host` 明确释放 dotnet 后，所有构建和回归串行执行；完成后已向该代理与主代理明确释放 dotnet。本轮不修改生产源码，不提交或推送。

本代理使用新独立上下文，只读取 AGENTS、Core 源码与中性回归源码；未读取设计文件、正式剧本、历史试玩或既有报告，未启动 Godot 或 FullTest。

## 结果与原始证据

| 执行入口 | 结论 | 原始标准输出 |
| --- | --- | --- |
| Core.sln Release 构建 | 通过 | [core-build-attempt02.stdout.log](core-build-attempt02.stdout.log) |
| SubjectiveDecisionRegression | 通过 | [decision-regression.stdout.log](decision-regression.stdout.log) |
| SubjectiveReasoningRegression | 通过 | [reasoning-regression.stdout.log](reasoning-regression.stdout.log) |
| CognitionRegression | 通过 | [cognition-regression.stdout.log](cognition-regression.stdout.log) |
| SubjectiveDecisionRegression `--api-probe` | 完整结束，边界值与拒绝标志正确 | [decision-api-probe.stdout.log](decision-api-probe.stdout.log) |
| CachePublicationProbe | 通过 | [cache-publication-probe.stdout.log](cache-publication-probe.stdout.log) |

每次执行另有同名 `.stderr.log` 与 `.result.json`，后者记录实际参数、UTC 开始时间、耗时、退出码和输出 SHA256。[run_step.py](run_step.py) 用 subprocess 的原始 stdout/stderr 字节写入独占新文件；局部 [.gitattributes](.gitattributes) 为日志关闭文本转换和差分。失败证据未覆盖。

首次 Core 构建没有进入有效构建：PowerShell 将转发给 Python 的 `-m:1` 拆成 `-m:` 与 `1`，MSBuild 因无效 CPU 参数拒绝执行，见 [core-build.stdout.log](core-build.stdout.log)。将该参数明确引用后，第二次构建通过。该失败属于验证调用错误，不是生产构建错误。

## 缓存与发布锁机制

追加探针 [CachePublicationProbe.cs](CachePublicationProbe.cs) 只操作公开 Core API，使用独立中性主体与对象。

- 先以 Assess / Capture 预热，再对本人同一经历写入 Failed；Capture 保留原 Attempts 并新增 Failures，对应行动分数由 `0` 降为 `-80`。随后通过 Observation 发布不同计数的合法 Details，读取到新值，证实新 LastObservation 不沿用旧 DTO。
- 预热后发布未知版本 Details，Capture、Assess、重复 Assess 和同 key 的 Observe 都拒绝读取；拒绝期间认知图编码字节保持相同。再发布合法 Details，读取与评分恢复。
- 两个主体使用完全相同的经历 key，得到各自独立计数。第二主体发布非法 Details 后被拒绝，第一主体的计数和后续反馈保持正常。
- 先保存已预热主体图与运行时，再更新原主体经历。CognitionGraph.Rebind 和 WorldRuntime.Restore 创建与原图相同 SubjectId、不同对象身份的节点，均读取保存时计数。恢复节点再发布非法 Details 后被拒绝，原图与分离图的计数不受影响。
- 并发任务从尚无经历节点开始，交错调用同主体的 ActionLearning.Observe，并穿插 Assess / Capture。最终 Attempts 与 Failures 均为预期的 `240`，只有一个匹配经历节点；所有其他 outcome 计数保持为零。这证明本探针覆盖的 ActionLearning 公共入口保留累加，不声明整个 WorldRuntime 跨线程安全。
- 多次预热 Assess / Capture 后，认知图编码字节与 LastObservation 对象身份保持不变，支持评分与捕获只读的结论。

## 性能实测范围

既有 `--api-probe` 在每个新 fixture 中写入不同情境经历，先执行一次 Assess 预热，再测量连续 Assess；stdout 保留了经历规模、Details 字节、时间及当前线程分配。该入口不是旧版与新版的效率对照，不据此宣称提升。

追加探针以同一份快照含 `128` 个经历节点的图比较整次 Assess：原主体先预热 `12` 次，测量 `40` 次；另在计时前从同一快照建立分离图，每个分离图只测量首次 Assess。图构建、快照校验不在计时范围内；计时使用 Stopwatch，分配使用当前线程 GC 分配量。

| 读取状态 | 平均时间 | 平均当前线程分配 |
| --- | ---: | ---: |
| 同一主体图预热后的 Assess | 4.794 ms | 2,022,494 字节 |
| 分离恢复图首次 Assess | 7.281 ms | 2,664,029 字节 |

这是同一工作负载的整次 Assess 观察。恢复图的首次读取同时初始化快照读图等结构，因此不能把差值单独归因于经历 DTO 缓存，也不能据此评价正式宿主帧率。未读取既有性能报告作基线。

## 源码指纹与验收范围

[source-before.sha256.json](source-before.sha256.json) 与 [source-after.sha256.json](source-after.sha256.json) 记录 csharp 根层源码、项目文件、Core.sln 与根/Core AGENTS 的运行前后 SHA256；原有被记录文件没有变化。追加探针源与项目在初始指纹之后创建，列于运行后清单。

`SubjectiveDecision.cs` 前后均为：

```text
f906b4eff4543768f9822f5400bf604c1c0531d286ebc69cdc3213a1ebb3e79b
```

构建与本轮机制自动回归通过，没有发现生产失败项。人物行为合理性与游玩体验未验证，由主代理依设计规范判断。本轮只完成 Core 自动回归，一级剧情与引擎验收、二级 Godot UI 验收和三级真实游戏盲玩均未执行；本报告不能替代它们。弱引用回收时机与整个 WorldRuntime 的并发合同也不在本轮实测范围内。
