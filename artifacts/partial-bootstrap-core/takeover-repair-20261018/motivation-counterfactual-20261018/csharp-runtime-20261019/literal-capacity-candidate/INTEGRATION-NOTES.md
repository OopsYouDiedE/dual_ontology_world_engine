# 字面量容量候选集成记录（2026-10-19）

遵照主代理划定范围，本子任务只修改 `language_core/compiler/compiler.dv`、`language_core/core/00-data.dv` 并新增本记录；AGENTS 与 core/README 由主代理统一同步。未读取设计文档，未构建、运行中性探针或正式会话，未修改 Runtime、Session、UI 或历史候选文件。

## 来源核对

修改前生产两文件与候选对应 before 文件的 SHA-256 逐字节相同，且等于候选 BASELINE-SHA256.json：

| 文件 | 修改前 SHA-256 |
| --- | --- |
| compiler/compiler.dv | CEFD7FA52CFC8507AE3F540DD56B389996AE75E166333E355C9E655B0D981141 |
| core/00-data.dv | 0CA15989DDF92801E5FF9A9938C47BD3CFEF6825E4328028189E380E052BAA1A |

因此没有需要保留但与 before 不同的其他生产改动。实际使用定点补丁，仅引入候选两个差异块；未复制覆盖整文件。集成后按 CRLF/LF 规范化的文本与对应候选完全相同，git diff --no-index 未显示源码文本差异。生产工作树与候选的换行字节差异仍如实保留，未据此重写候选证据。

| 文件 | 当前工作树 SHA-256 |
| --- | --- |
| compiler/compiler.dv | AF22B71FFBFD7D48ED0C34FAB785CBD3538A2B57DB3F25A221172F06CF5B3F65 |
| core/00-data.dv | 07CBB35B58E034440F8BDCA5692C45BDA3F35857B62B44615835AAE28BCE69BC |

## 生产差异

`sf_emit_array` 以 AST payload 的已知元素数量发射 `new List<object>(N) { ... }`，保持元素生成顺序、分隔符与返回值。只处理定长字面量；comprehension 的未知输出长度不预估，不新增用户可见容量语法或 Runtime ABI。

`dv_pair`、`dv_single`、`dv_list3` 分别直接返回 `[a,b]`、`[a]`、`[a,b,c]`。参数在调用前依旧求值，结果是独立可变根并按原顺序保存原参数引用；没有克隆、冻结或共享结果根。它们沿现有字面量发射获得精确初始容量，移除新根上的逐次 push 及 list3 对 pair 的中间调用。

## 必须同步到工程参考的边界

主代理应在 core/README 与 language_core/AGENTS 说明：

- 这只调整 CLR 容器的分配和增长，不修改显式游戏费用、预算、资格、DV 值域或冻结/别名合同。
- 容量分配发生在首个元素表达式前。资源耗尽时 OOM 与元素自身错误/副作用的发生先后可能改变，不能宣称覆盖内存耗尽的所有异常次序完全等价。正常资源范围仍按原序求值元素；前项抛错后不执行后项。
- dv_list3 不再调用 dv_pair，插桩函数调用次数与包含/独占时间的归属会变化。诊断函数边界变化不是显式工作预算变化，也不要求 profile 调用统计逐字相同。
- 编译器生成文本会变化，候选应由源码重新自举并检查自己的固定点，再统一生成 Session；不得手改生成 C# 或混用旧程序集。

这些说明继承本目录上层 `LITERAL-CAPACITY-STATIC-REVIEW.md` 的静态审查，仍不能替代本轮中性输出、别名与通常错误顺序核对、自举工程检查或真实正式证据。未运行阶段保留未验证，不从减少分配推断五日覆盖或实机体验改善。
