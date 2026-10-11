# 独立语言内核初审

日期：2026-10-12。审查者：全新上下文独立 Agent。

## 范围与证据边界

本次只读取 `language_core/Kernel`、`language_core/Cli`、`language_core/bootstrap` 的初始实现及根 `AGENTS.md` 的工作流程约束；未读取项目设计文件、剧本、旧 Core 源码或旧报告。未修改实现源码。执行的是独立 CLI 的 `demo`、`eval`、`self`，没有运行流程回归或游戏会话。一级、二级、三级游戏验收均未执行；人物合理性、剧情与游玩改善不在本报告结论范围。

初审期间主代理修改了共享目录。下列闭包反例与机制输出来自修改前实际执行的 CLI；报告按初始实现封存，不能用于宣称修改后仍有同一缺陷。未在执行前封存该初始源码及程序集 SHA-256，因而不把当前工作区指纹冒称为初始程序集指纹。最终实现需要另行冻结并独立复评。

## 可行性判断

初始实现确实提供了可运行的部分自举，而非把 `owl-eval` 直接接到一个宿主 `eval` primitive。`10-evaluator.owl` 用自身的列表、环境、递归函数实现读入 AST 后的解释；`Bootstrap.LoadSelf` 将全部 bootstrap 模块作为 AST 交给这个语言写的解释器。CLI `demo` 实际取出 self 环境中新加载的 `owl-eval`，经 `owl-apply` 调用后得到 `42`。这证明“解释器源码能由自身语言解释，再调用所得解释器”这一有限边界成立。

它不是完整自举或形式化验证。Reader、数值与列表 primitive、cell、函数调用执行、预算、类型化业务 AST、权威实体及提交边界仍在 C# 中。`self <expression>` 的默认入口调用 seed 环境中的 `owl-load`，将用户程序在 self 环境中执行；它没有默认切换到 demo 单独取出的新一层 `owl-eval`。初始 `WorldCore` 也只调用 seed 装载的规则函数。独立 `.csproj` 未引用旧 Core 或 Godot。

初始版本有可观察的 seed/self 语义差异，以及正式准备入口未执行禁令核验的缺口。它适合继续作为独立语言实验内核，但不能在修复与明确这些边界之前宣称 seed/self 等价、禁令由正式执行入口强制保证，或所有推演核心已完成语言内表达。

## 已复现的具体问题

### 不同闭包在 self 中被判为相等，环境可被程序读取

相同输入：

```lisp
(let* (f (fn* (x) x) g (fn* (x) x))
  (list (= f g) (list? f)))
```

实际输出：

```text
eval: (false false)
self: (true true)
```

seed 的 `Value.Closure` 以引用身份比较且不是列表；初始 self 的闭包为 `(owl-closure parameters body environment)` 普通列表。两个新闭包共享环境且内容相同，宿主结构相等因此返回 true。

相同输入：

```lisp
(let* (secret 42 f (fn* () secret))
  (get (deref (first (nth f 3))) 'secret nil))
```

实际输出：

```text
eval: Expected list.       [退出码 1]
self: 42                  [退出码 0]
```

这既是可观察类型/身份差异，也是初始闭包环境不透明性缺失。普通列表还可伪造这种 callable 编码；伪造可调用的结论来自 `owl-apply` 源码，未另行实际执行。主代理已告知随后会改为不透明函数包装；本报告不对该新实现作结论。

### Forbidden 结果并不约束初始 prepare/正式准备入口

实际输入：

```lisp
(let* (facts '(("x" ((exists true) (tags ((stock 2))) (relations ()))))
       context '((actor "x") (target "x") (realm authority))
       action '(change (slot target) stock 1)
       rules (list
         (list 'rule 'want '(const true) 'prefer action 4 true)
         (list 'rule 'ban '(const true) 'forbid action 5 true)))
  (list (assess rules facts context) (prepare action facts context)))
```

实际输出：

```text
((((change (ref "x") stock 1) 4 true (ban)))
 (ok (("x" ((tags ((stock 3))) (exists true) (relations ()))))))
```

`assess` 明确记录了 ban，但纯 `prepare` 接口没有 rules 参数，照常返回成功准备结果。这本身可以是“已经授予执行权限后的纯事务函数”边界；缺口在初始 `WorldCore.Prepare` 同样没有核验 subject 的规则或 `Assessment.Forbidden`，而 public `Commit` 只核验 token、世界 revision 与单次消费。调用者可以绕过候选选择直接提交这类动作。正式 C# API 的绕过结论来自完整源码审查，未另造 C# harness 实际执行。

### 多别名实体的普通写入可能被判为 conflicting-facets

这是源码推导，尚未实际执行：`Observe` 允许两个不同本地 handle 指向同一 Source；`Prepare` 为两个 handle 各构建相同权威状态。若 effect 仅修改其中一个 handle，pure prepare 返回另一 handle 的未改副本。C# 解码全部返回状态，再按 EntityId 合并，看到已改/未改 tags 不相等便返回 `conflicting-facets`。因此并非只有程序明确提出矛盾写入才会被拒绝，普通单次写入也可能被无变化的别名副本阻塞。应明确一个 Source 的多个认识 facet 是独立状态还是共同实体，并以该选择实现规范化；本次未读取设计文件，不替项目作此选择。

## 各机制的实际行为与边界

### Unknown 与 authority 域

输入：

```lisp
(eval-condition '(not (tagged (ref "x") missing 1))
  '(("x" ((exists true) (tags ()) (relations ())))) '())
```

`eval` 和 `self` 实际均输出 `unknown`。缺失观察没有经否定变成已知 true。demo 中 `WorldCore.Evaluate` 的未观察 tag 输出 `Unknown`；把权威 EntityId 字符串当本地 ref 使用被拒绝，报 `Reference is outside subject view.`。旧 `SubjectSnapshot` 在权威提交之后仍保留原有主观 stock=9 评价，demo 输出 `True`。

这支持“Evaluate 仅拿到复制的本地主观 facts，执行不自动改写认识”的有限隔离。权威读取 API、Observe 输入和 `Prepare` 都是宿主边界；它们不是让任意不可信调用者持有 WorldCore 时的安全隔离能力。

纯语言函数的 realm 只是调用者提供的数据，不是不可伪造 capability。以下输入在两种入口均实际输出 `true`：

```lisp
(eval-condition '(tagged (ref "missing") absent 0)
  '() '((realm authority)))
```

此结果没有读取任何真实世界；它显示纯 authority 模式把缺失 tag 默认为 0，即使 facts 中没有该对象。`WorldCore.Evaluate` 不给主观调用者这个 realm，但直接调用纯函数不能获得同样的防护保证。

另一个需要明确的边界来自源码：`MutatedNouns` 对 `require` 不产生对象，正式 Prepare 可以用 authority facts 判断任意已绑定对象的条件，即使该对象当前不在可达范围。提交失败原因可作为该条件的可观察结果。它是否应该是可允许的权威查询通道取决于机制要求；本次没有通过 C# API 执行此反例，也不把此结果冒称为未经观察的知识已写入主体。

### 规则、Permit 与选择

demo 的“prefer 4 / forbid 5 / permit 5”实际输出 `score=4, forbidden=False`。Permit 没有自己产生候选或提高分数。只有 acceptance=true 且 antecedent=true 的规则进入 active；Unknown antecedent 的 forbid 也会被排除。因此当前规则评估不提供“禁令前提未知时保守阻止执行”的保证。

实际输入：

```lisp
(assess
 '((rule want (const true) prefer
     (seq (change (slot target) stock 1) (set (slot target) mark 1)) 4 true)
   (rule ban (const true) forbid (change (slot target) stock 1) 5 true)
   (rule allow (const true) permit
     (seq (change (slot target) stock 1) (set (slot target) mark 1)) 5 true))
 '() '((target "x")))
```

实际输出：

```text
(((seq (change (ref "x") stock 1) (set (ref "x") mark 1)) 4 false ()))
```

与整个候选完全相同的 compound Permit 可以按权重解除其中匹配的子禁令。Permit 匹配候选整体；forbid 则匹配候选或 seq/when 内的语法子树。匹配依赖规范化后的结构相等，不比较两种 effect 的世界结果等价性。`Require` 规则仍是候选与评分贡献；不满足某 Required action 的其他候选只是被减分，没有因此自动 illegal。以上为当前明确机制边界，不能单凭名称把 Require 解释成硬性约束。

### 事务准备与提交

实际输入：

```lisp
(let* (facts '(("x" ((exists true) (tags ((stock 2))) (relations ()))))
       context '((target "x") (realm authority)))
  (list
    (prepare '(seq (set (slot target) stock 100)
                   (spend (slot target) stock 101)) facts context)
    facts))
```

实际输出：

```text
((failed resource)
 (("x" ((exists true) (tags ((stock 2))) (relations ()))))
```

输入状态未被事务中途改写。demo 的正式 API 同样记录失败后权威 stock=2；主体认为 stock=9 时花费3仍得到 `resource`。竞争 token 实际输出 `first=True, second=stale-world, repeated=already-committed`。

源码中的 token 对外不可构造，归属另一 WorldCore 的 token 被拒绝，Commit 受锁保护。每个成功 Commit 都递增世界 revision，任何世界变动都会使旧 token 失效。Subject revision 未纳入 token；接受新规则或观察更新而没有世界更新时，原 token 不因此失效。这是当前提交有效性边界，未实际执行其对应反例。

### 图闭合

demo 将对象 ParentHandle 指向未知本地 handle，实际得到 `Unclosed parent handle.`，随后记录 `subject revision unchanged`。源码先建立完整 proposed facts，再核验所有 parent/relation 端点，验证完成后才替换 bindings/facts；一批互相引用的观测可以一起接入，不会因先后顺序被错误要求逐条闭合。

权威 `Define` 只允许引用已定义实体；主观闭合只核验引用端点存在于 facts，不核验事实真伪、authority Parent 是否一致或主观 parent 是否成环。不能把引用闭合称为观察真实性证明或全图语义一致性证明。

### 推理与预算

实际输入 `(unify '(var x) '(f (var x)) '())` 输出 `nil`，发生了 occurs check 拒绝。实际输入 `(prove-ground '(p) '((clause (p) ((p)))) 8 '())` 输出 `(unknown cycle)`。demo 的缺失 Horn fact 输出 `(unknown no-clause)`，变量 Horn clause 被明确拒绝，报 `Only ground Horn clauses supported`。因此当前不是通用变量 Horn resolution；unify 函数的存在不能替代 standardization-apart 或通用解析器完成证据。

输入：

```lisp
(do (def! count-down
      (fn* (n) (if (< n 1) 0 (count-down (- n 1)))))
    (count-down 500))
```

`eval` 和 `self` 实际均输出 `0`。demo 的无界 tail recursion 在专用 Steps=1000 下实际得到 `Evaluation step budget exhausted.`。Reader 另有限制源长度、节点量与嵌套深度，Eval 有 steps/depth 限制。

这些预算不提供总内存或现实耗时上界：列表 `concat`、`cons`、结构相等与 Print 中的批量分配/递归工作没有按元素收费；已构造 Value AST 可以经公开 Evaluate API 绕过 Reader 的源限制；LoadSelf 对各模块分别创建预算，而非同一总预算。没有执行故意耗尽进程内存的输入。本次不声称这些未测极限导致了实际崩溃，也不把有界失败称为形式化终止/资源安全证明。

## 后续复评的必要输入

最终冻结版本应重放上述不同闭包相等与环境读取反例，实际验证正式 WorldCore 的禁止动作提交、规则更新后的 token、同实体多 handle 写入，以及 require 对远距对象的权威查询边界。记录该轮源码与程序集指纹，避免用本初审的修改前 CLI 输出替代最终实现证据。纯 prepare 是否故意限定在已经授予的状态上，应与正式执行入口的权限保证分别说明。

本次结论仅涉及独立语言机制与工程可运行性。没有读取设计依据，因此没有作人物、游戏或剧情合格判断。
