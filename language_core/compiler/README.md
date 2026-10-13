# DV 编译器启动子集

正式表层语法目标已于 2026-10-13 改为易读声明与代码块，见 [DESIGN 第 12 节](../DESIGN.md#12-易读表层语法2026-10-13)。本编译器目前仍只解析下述 S 表达式；新前端、格式选项、return lowering 与表层自举均未实现。

目标前端同时包含文件头 `define 别名 原名` 的中文 token 别名（DESIGN 12.8）。此指令及 Unicode 别名扫描当前未实现，不能送入本编译器或旧解释器执行。

完整v1方案以 [SYNTAX.md](../SYNTAX.md) 为准，包含领域操作、类型/能力、来源、评分分布、模块闭包和完整文法；当前编译器仍为v0启动子集。实施需由DV实现新前端、typed AST/IR及后端，重新产生v1自举证据；旧固定点不代替新前端能力。

`compiler.dv` 包含词法扫描、带 UTF-16 偏移的 AST 解析、函数签名收集、局部作用域/名称/调用元数检查和 C# 生成。它能编译自身。`seed/` 中的一次性 C# 编译器 C0 使用原 Reader 启动；C1 及以后只包含生成的 C# 与 `Runtime.cs`，没有 Kernel、Reader、Interpreter、原 Core 或 Godot 依赖。

这是通用计算启动子集的真实编译器自举。完整 DESIGN 中的类型系统、能力检查、目标无关 IR、闭包、模块和领域声明仍未实现；现有 `bootstrap/*.dv` 的 `def! / fn* / quote` 解释语法尚不能直接送入本编译器。两条链路明确分开，后续需要统一并迁移推演库。

## 重现

仓库根目录执行（输出目录必须是全新路径）：

```powershell
python language_core/tools/bootstrap_compiler.py --out artifacts/partial-bootstrap-core/compiler-<new-run>
```

脚本冻结源码原始字节及 SHA-256，记录 .NET 工具链，构建 C0，依次生成并构建 C1/C2/C3。比较 C2 与 C3 的 C# 源码字节；程序集包含路径等构建信息，因此分别记录程序集指纹，不要求程序集相同。每阶段 `.deps.json` 必须只有生成项目自身的依赖条目，平台依赖为 .NET 8。

随后用 C3 编译并运行新的递归/循环程序；修改编译器源码中的目标类名、编译该变体，再用变体编译样例，确认输出随编译器源变化。错误程序必须报告失败且不产生目标文件。所有步骤保存原始 stdout/stderr；错误即停止，不覆盖旧证据。固定点是可复现的自编译证据，不是编译器正确性的形式化证明。

使用已构建的编译器：

```powershell
dotnet artifacts/partial-bootstrap-core/compiler-<new-run>/c3/bin/Release/net8.0/Program.dll input.dv output.cs
```

将 `output.cs` 与本目录 `Runtime.cs` 放入独立 .NET 8 控制台项目构建；生成文件不能手改。C1 以后不再需要 C0。启动编译器只承担受信任的编译器源码启动工作，用户输入诊断以 DV 实现为准。

## 当前语法与语义

```text
Program = (defn Name (Name*) Expr)* (main Expr)
Expr = Number | String | true | false | Name
     | (Function Expr*)
     | (if Expr Expr Expr)
     | (let (Name Expr ...) Expr)
     | (set Name Expr)
     | (do Expr+)
     | (while Expr Expr)
```

`main` 可以位于声明序列中任意位置，但只能有一个。函数允许递归、相互递归与前向引用；参数和局部绑定具有词法作用域。`let` 从左到右初始化，禁止重复绑定及遮蔽外层变量；赋值只允许已有局部变量。`if` 只求值选中的分支；`while` 返回 false。普通函数参数从左到右求值。未提供一等函数、闭包或尾递归优化。

启动数据域为动态检查的 Number、Text、Bool、List；目标用 `object` 表示这个值域。Number 是有限 IEEE-754 double；溢出或除零导致非有限值时拒绝。条件必须是 Bool；类型错误在运行时诊断，不能称为已实现静态类型化 AST。List 是可变容器，`rt_eq` 对 List 比较身份，对基本值比较值；没有隐式结构相等。

标识符限 ASCII 字母/下划线开头，后续允许数字，函数名 `rt_` 前缀保留给平台原语。注释从 `;` 到行尾。字符串支持 `\n \r \t \" \\ \uXXXX`；实际 Unicode 文本保留，C# 特殊换行符由 DV 后端转义。输入限 1 Mi UTF-16 单元与 128 层列表嵌套。诊断位置目前是 UTF-16 偏移，部分词法错误仅给错误类别。

AST 是 `[kind, payload, offset]`，kind 为 `list / sym / num / str`。签名表与局部环境用于名称和调用检查；当前检查与 C# 表达式生成共用一次遍历，没有把生成文本冒充目标无关 IR。下一步应在保留自举固定点的条件下引入独立类型化 IR 与后端接口。

## 平台边界

`Runtime.cs` 只提供有限数运算、严格 Bool、文本切片/字符转换、列表容器、文件与命令行 I/O、错误抛出和普通 C# 闭包调用辅助 `Block`。`Block` 执行已经生成的委托，不读取 AST 或解释程序。所有语法识别、标识符规则、作用域检查、C# 转义和代码布局均在 DV 中。运行时没有任意 C# 注入、反射、动态编译或 eval 接口。

编译器当前是受信任的命令行程序，HostIO 尚未受静态能力检查约束。不能将这个启动子集当作不可信主体沙箱，也不能用它宣称全部推演核心迁移、旧 Core 等价或游戏验收完成。

## 本轮前端扩展

支持 `(axiom Name Version (Parameters...) Premise Effects)`；DV 前端展开为调用领域库 `dv_axiom` 的具名函数，继续执行名称/元数检查。实例化返回数据，不自动执行或证明命题。新增的 `rt_arg_count / rt_json_parse / rt_json_stringify` 仅为命令行与数据持久化 ABI；领域实现见 `../core/README.md`。
