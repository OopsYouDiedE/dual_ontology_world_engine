# engineering-20261012-r1 独立机制审查

审查日期：2026-10-12。审查者使用全新上下文，仅读取根维护规则、`language_core/` 的冻结实现，以及 `engineering-20261012-r1/` 的本轮原始证据。未读取项目设计规范、任何剧本、旧 Core、旧报告或前一审查报告；未运行游戏或 flow 系列入口。新增的 `language_core/DESIGN.md` 未读取内容。

本轮结论是：**部分解释自举确有实际实现，但当前权威执行存在可复现的别名资源重复消费和禁令绕行；主体预测存在把未知条件当作确定跳过的问题；远距检查仍泄露对象存在性。不能据此认定完整 Core 迁移、编译自举或正式游戏验收。**

## 冻结与证据范围

开始审查时，`engineering-20261012-r1/source-sha256.json` 中的文件与工作区逐字节 SHA-256 一致；封存的程序集也一致：

| 程序集 | SHA-256 |
| --- | --- |
| `language_core/Cli/bin/Release/net8.0/Language.Cli.dll` | `fc1d82f13aab0aeb1a30b4e28b62db8fcdf2e33eb132a78f17a38bdda57b666d` |
| `language_core/Cli/bin/Release/net8.0/Language.Core.dll` | `9d5de0b4108452bac73a31fa2688589da82d0f6c9566ede5720467928c25aca0` |

独立 CLI 直接运行上述冻结 DLL；临时 C# harness 通过程序集引用同一个冻结 `Language.Core.dll`，没有重新构建或修改生产源码。输入在 [review-harness/Program.cs](review-harness/Program.cs) 和 [review-harness/run_review.py](review-harness/run_review.py)，命令/退出记录在 [review-harness/execution.json](review-harness/execution.json) 和 [review-harness/harness-execution.json](review-harness/harness-execution.json)。这些文件是通用机制审查工具，不是生产实现或游玩证据。

结束时再次比对，所有原 `.cs`、`.owl`、项目文件、示例和原程序集仍与 r1 指纹一致。**完整目录不能再称“完全未变”**：主代理在审查期间更新了 `README.md` 与 `AGENTS.md`，并新增 `DESIGN.md`。只记录新增文件名，没有读取其内容。文档变化与可执行实现分别记录在 [review-harness/fingerprint-final.json](review-harness/fingerprint-final.json)；开始指纹在 [review-harness/fingerprint-before.json](review-harness/fingerprint-before.json)。本报告关于 README 的判断针对最初读取的 r1 版本，不是对更新后设计的审评。

临时 harness 首次编译因单个 `params Observation[]` 调用中的目标类型 `new()` 写法产生 `CS8752`；这是审查工具自身错误，日志保留为 `harness-build.stdout.txt`。改为显式 `new Observation(...)` 后，工具编译完成，新增日志为 `harness-build-r2.stdout.txt`，没有覆盖前一次失败。原始 stdout/stderr 按字节保存，局部 `.gitattributes` 禁用其换行转换。此编译事实只证明审查工具可以运行。

## 必须修复的具体缺陷

### P1：同一权威实体的别名可以重复消费资源，序列写入没有正确累积

通用输入：一个实体的权威 `stock=3`，同一主体通过 `object` 与 `facet` 两个已观察句柄指向它。两个句柄都报告 `stock=3`，使用合法可达的本人 actor。

```lisp
(seq
  (spend (ref "object") stock 2)
  (spend (ref "facet") stock 2))
```

实际公开 API 输出：`Succeeded=True, Failure=<none>, stock=1`。一次事务请求累计消费 4，却在仅有 3 的情况下提交；第二次消费没有读取第一次的阶段结果。另一个新世界执行两个别名各 `change +1`，从 3 只到 4，未得到顺序累积的 5。[原始输出第 2–3 行](review-harness/harness.stdout.txt#L2)。

源码原因：`WorldCore.Prepare` 在 `WorldCore.cs:290–298` 把每个句柄分别编码为权威副本；`20-world.owl:106–117` 按句柄保存阶段修改。回收阶段结果时 `WorldCore.cs:330–333` 只拒绝同一实体的**不同最终写值**。两个别名都算出同样的 `stock=1` 或 `stock=4` 时，该检查放行，无法证明资源顺序正确。

必要修复边界：权威事务必须按实体身份共享阶段状态，或明确拒绝这种别名组合；不能只比较最后写值。主体快照仍应使用本地句柄，不向它暴露权威实体 ID。

### P1：已接受禁令可经另一个实体别名绕行

独立新世界中，主体接受一条权重 5 的 `Forbid`，动作是 `(change (ref "object") stock 1)`，没有 Permit。随后对同一权威实体执行：

```lisp
(change (ref "facet") stock 1)
```

实际输出：`Succeeded=True, Failure=<none>, stock=4`。[原始输出第 4 行](review-harness/harness.stdout.txt#L4)。

源码原因：`20-world.owl:69–91` 绑定名词后仍按句柄结构比较动作；`WorldCore.cs:286–288` 先按这些结构做禁令检查，后续权威映射没有重新识别同一实体的操作。实测事实是同一权威写入可以由另一个句柄提交。应明确对象同一性对禁令的作用，并在执行边界消除这个绕行入口；不能把主体未知的权威身份写进主体事实来补救。

### P1：未知 when guard 被预测为确定不执行，后继因此获得无依据的确定收益

主体知道 `flag=0, progress=0, stock=3`，没有观察 `unseen`。首步偏好分 1：

```lisp
(seq
  (when (tagged (slot target) unseen 1)
    (set (slot target) flag 1))
  (change (slot target) progress 1))
```

后继偏好分 10 的动作 `(set (slot target) stock 8)`，条件是：

```lisp
(all
  (tagged (slot target) progress 1)
  (not (tagged (slot target) flag 1)))
```

`WorldCore.Plan(depth=2, expansions=10)` 实际返回上述首步和后继，`score=11, depth=2, expanded=1, stop=depth`，每条边带相应接受规则 ID。[原始输出第 7–8 行](review-harness/harness.stdout.txt#L7)。直接 CLI seed/self 对未知 guard 也都返回 `ok`，保留 `flag=0`：见 [cli-unknown-when-eval.stdout.txt](review-harness/cli-unknown-when-eval.stdout.txt)、[cli-unknown-when-self.stdout.txt](review-harness/cli-unknown-when-self.stdout.txt)。

源码原因：`20-world.owl:124–125` 只在 guard 为 `true` 时执行 body，`false` 与 `unknown` 都返回原事实；`50-planning.owl:5–17` 把这个 `ok` 结果当作确定后继。未知 body 本来可能修改已知 `flag`，规划却保留其旧值并用它激活后续条件。预测保留在私有节点、不回写真实快照是正确的隔离事实，但不能弥补预测本身的确定化。

本反例没有提交该首步，不宣称已经发生真实世界失败。证据证明的是主体没有得到 guard 结果时，规划已经把“不执行 body”作为确定事实使用。修复应保持未知、显式分支或拒绝依赖此确定结果的预测。

未知数值缺省 0 的旧危险入口没有在本轮复现：seed/self 的缺失 tag `change` 返回 `(failed unknown-state)`；公开 Plan 对缺失 observed `stock` 返回 `no-action`，权威 `stock` 仍为 3。[原始输出第 5–6 行](review-harness/harness.stdout.txt#L5)。该窄结果不能扩张成所有未知预测都安全。

### P2：不可达对象的存在性可由 Prepare 失败原因区分

同一主体只持有两个远距对象的句柄，二者观察的 `Existence` 都是 `Unknown`。权威中一个存在，另一个不存在。对存在的对象，以真实 secret 阈值真假分别调用 `require`，均返回 `out-of-reach`，没有从这组输入泄露 secret 真值。[原始输出第 25–26 行](review-harness/harness.stdout.txt#L25)。

但调用 `(require (exists (ref "remote")))` 返回 `out-of-reach`，对权威不存在的远距对象调用同样的 exists guard 返回 `missing-object`。[原始输出第 27–28 行](review-harness/harness.stdout.txt#L27)。

源码原因：`WorldCore.cs:300–305` 在可达性检查之前检查 `entities[entity].Exists`。因而 Prepare 的返回值仍是未观察远距存在性的 oracle。必须在不可达边界统一这些公开结果，不能只保证数值条件的真假难以区分。

## 预算的实际口径与限制

`WorldCore.Plan` 的输入是 `depth=1/2/3, expansions=1`，含两个可预测首步候选。它总是先生成两个根后继；深度 1 返回 `expanded=0, stop=depth`，深度 2 或 3 返回 `depth=1, expanded=0, stop=complete-layer-budget`。[原始输出第 9–11 行](review-harness/harness.stdout.txt#L9)。

源码 `50-planning.owl:38–45` 在预算检查前对全部 assessments 调用 `plan-child`；`27–33` 只对之后的 frontier 父节点展开扣预算。**当前 expansions 不约束首层后继的生成，也不是“生成边数”的总预算**；单个父节点产生的所有候选也按一次父节点展开扣费。后续层确实在 frontier 大于 remaining 时整体停止，没有看到该计数口径下的扣费超限。

因此不能笼统声称“全部搜索展开受 expansions 严格限制”。若合同预期包含根节点展开，当前实现违反它；若合同刻意只计根之后的父节点，应明确命名、文档及单独的首层工作上限。r1 README 只说展开预算是语义参数，没有充分说明这一例外。本轮没有扩大到大量候选或性能测量。

`expansions=0` 的 Plan 明确拒绝为 `Invalid expansion budget`。Prove 的预算口径不同：目标需要一条条件为 true 的 Infer 规则时，预算 1 返回 `Truth=Unknown, Stop=expansions, Expansions=1`，预算 2 返回 `Truth=True, Stop=complete, Expansions=2`；所需递归 goal 调用实际扣费。[原始输出第 12–14 行](review-harness/harness.stdout.txt#L12)。这是窄输入下的资源事实，不是复杂搜索的形式化证明。

## 部分自举与基础语义

源码与执行记录支持“部分解释自举”：`Bootstrap.cs:16–32` 用启动 Interpreter 载入语言写的 `10-evaluator.owl`，再由 `owl-load` 解读各语言模块，包括解释器自身；`InferenceProgram.Call` 在 `42–45` 从这个环境取函数并调用 `owl-apply`。`WorldCore` 的主体求值、推导、权重和规划都通过此 InferenceProgram。原 demo 还从新环境取出新载入的解释器并实际求值，输出 42，见 [mechanisms.stdout.txt](engineering-20261012-r1/mechanisms.stdout.txt)。

`Interpreter.cs:12–58` 的 native table 是数值、比较、列表、opaque function、cell 与 apply，没有 native eval、规划、规则求解或案例推断算法。C# 仍保留 Reader、启动求值器、预算与宿主权威边界。没有看到编译器、自编译产出的 C# 或语言自身实现的 parser；不能称完全自托管编译器。

独立 seed/self CLI 对 opaque function 给出同一输出 `(true false false false true 7)`：函数不是列表，不是 native，同一个函数保持身份，不同闭包不相等，apply 结果为 7。nil/false 的 if、symbol unknown 的普通 Lisp truthiness，以及三值函数给出同一输出 `(2 2 1 unknown unknown unknown)`；普通 Lisp `if` 不自动采用三值条件语义。畸形 let 和不支持的 quasiquote 均拒绝执行，错误文字不同，未发现这组输入的结果语义分歧。命令与原始输出在 [execution.json](review-harness/execution.json) 及相应 `cli-*.stdout/stderr.txt`。

## 主体、推导、权重与提交的窄证据

公开 API 拒绝外国 SubjectSnapshot、外国 PreparedAction 和把权威 ID 当主体 ref 使用。提交后旧快照对 `stock>=4` 仍为 False，说明它未被权威 stock=4 自动改写。[原始输出第 20–24 行](review-harness/harness.stdout.txt#L20)。`ReadAuthority/Define/Observe` 是受信任宿主接口；这不意味着把整个 WorldCore 暴露给主体也是安全沙箱。

Prepared token：第一次提交成功后，竞争 token 返回 `stale-world`，已用 token 返回 `already-committed`。另外 Prepare 后调用 `Conceive` 改认知版本，Commit 返回 `stale-cognition`，权威 stock 保持 4。[原始输出第 17–21 行](review-harness/harness.stdout.txt#L17)。本轮这些新实例支持世界版本、认知版本和一次性消费的实际检查；不替代线程调度、持久化或跨进程合同审查。

推导：同一条件同时由已接受 Infer 支持与否认时，输出 `Truth=Unknown, Contradicted=True`；显式假设未知条件时输出 `Truth=True, Hypothetical=True`。[原始输出第 15–16 行](review-harness/harness.stdout.txt#L15)。推导未插入真实观察，现有源码以显式规则、假设与停止原因组织结果；仍不是完整知识逻辑或变量子句求解器。

相同 testimony、相同假设模型，仅本人观察的 speaker Trust 从 3 改为 -3，h1 权重从 `0.7777777777777778` 变为 `0.5`；替换为 perception 后 h1 为 `0.8888888888888888`。[原始输出第 29–31 行](review-harness/harness.stdout.txt#L29)。这与 `40-belief.owl:15–21,44–51` 的来源折扣和归一化运算相符。r1 README 把这些值称作启发式证据权重、把显式 likelihood 的 bayes-update 分开，表述较为诚实；本轮不能证明其与旧 Reasoner 的全部行为一致，也没有验证相关证据独立性。

## README 的未完成边界与验收结论

最初读取的 r1 README 明确说不是旧 Core 完整替换、C# 启动与执行边界仍存在、变量 Horn 子句和完整概率逻辑尚未完成，且列出宿主接入、保存、物理行动和正式游戏未实现。其“部分自举”表述与实际结构吻合，其“完整迁移仍不能成立”的限制是必要且诚实的。

不过 README 对当前小执行子集“检查权威资源、主体规则”的说法被别名反例实质削弱，对未知保留和规划私有预测的表述也不足以说明 unknown when 的确定化；展开预算口径需要更精确。主代理告知后续目标加强为语言自身编译器及 C# 输出，本报告不以这一未来目标冒充当前能力，也不审评新增设计文档。

本轮只有工程与语言/宿主机制独立审查。未执行一级剧情与引擎验收、二级 UI 专项或三级游戏本体验收；人物行为、文学审美、真实游玩、显示、碰撞与性能均未验证。发现的权威资源、禁令、未知预测与远距观察缺陷需要修复后重新冻结源码，由新的独立上下文复评。当前报告和原始证据封存，不应在修复后覆盖或把本轮输出归为修复结果。
