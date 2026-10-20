# 成熟参考与数学边界

核对日期：2026-10-12。

- [GCC 的构建与多阶段自举](https://gcc.gnu.org/install/build.html)：阶段构建与比较可作为 DV 编译器自编译链参考。DV 先比较固定条件下生成的 C# 源码，并另核对实际语义；固定点不等于形式化证明。
- [Roslyn 官方仓库](https://github.com/dotnet/roslyn)：第一目标 C# 的外部工具链。DV 的解析、类型、IR 与 C# 输出算法最终须在 `.dv` 中实现，不能转交 Roslyn 代替 DV 语义。

- [MAL 项目](https://github.com/kanaka/mal)：提供分阶段的 Lisp 实现及自身语言实现。参考固定到 `2bbfaa54cca4908efc90b4173b1406e260788e8a`；重点阅读 [C# stepA](https://github.com/kanaka/mal/blob/2bbfaa54cca4908efc90b4173b1406e260788e8a/impls/cs/stepA_mal.cs)、[self implementation](https://github.com/kanaka/mal/blob/2bbfaa54cca4908efc90b4173b1406e260788e8a/impls/mal/stepA_mal.mal)。本目录是新实现，参考其自举结构，不是 vendored MAL；MAL 自身采用 MPL 2.0。
- [SWI-Prolog 的合一说明](https://www.swi-prolog.org/pldoc/man?section=compare)：occurs check 避免构造包含自身的循环有限项。本原型在语言库中实现它；尚未实现变量子句求解所需的 standardization apart，不称为完整 Prolog。
- [An Introduction to Logics of Knowledge and Belief](https://arxiv.org/abs/1503.00806)：多主体知识与信念的形式框架。知识事实性与可错信念须区分；一个主体快照本身不足以宣称完整 Kripke 可达关系模型已建立。
- [WebPPL conditioning](https://webppl.readthedocs.io/en/master/inference/conditioning.html)：condition、observe、factor 是在推断模型中使用的条件化操作。启发式分数不是自动得到的条件似然。
- [ProbLog API 与 semiring](https://problog.readthedocs.io/en/latest/api.html)：概率逻辑的 term、clause、公式与半环接口可供未来设计参考。概率解释重叠、独立性、归一化与模型语义仍需处理；实现一个加乘接口不等于已经证明概率计算正确。
- [Lean 的类型系统与证明](https://lean-lang.org/doc/reference/latest/The-Type-System/)：可借鉴小核/外层分离。Lean 能在假设与公理下陈述理论，不能简单说成所有命题都无条件地全宇宙为真；本引擎本轮目标也不是 Lean 证明器或量子概率系统。

参考用于技术分析，不作为剧情、人物或玩法设定依据。本语言尚未声称对应某一完整概率逻辑体系的已验证实现。
