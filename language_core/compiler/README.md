# DV 编译器

`compiler.dv` 实现 S 表达式启动子集的扫描、解析、名称与元数处理及 C# 输出。`seed/` 是启动编译器 C0，C1及后续阶段使用生成的C#与Runtime.cs。新的书写格式见[SYNTAX.md](../SYNTAX.md)。

## 构建与使用

```powershell
python language_core/tools/bootstrap_compiler.py --out artifacts/partial-bootstrap-core/compiler-<new-run>
dotnet artifacts/partial-bootstrap-core/compiler-<new-run>/c3/bin/Release/net8.0/Program.dll input.dv output.cs
```

构建工具依次生成C1、C2、C3。生成的output.cs与Runtime.cs组成.NET 8程序。

## 当前语法

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

函数支持前向引用、递归和相互递归。let从左到右初始化，参数按顺序求值，if求值选中的分支，while返回false。局部赋值使用set。

值域为Number、Text、Bool、List；Number为有限double，List为可变容器。rt_eq对基本值比较值，对List比较身份。

标识符采用ASCII字母/下划线开头，后续可含数字；rt_为平台函数前缀。注释从分号延续到行尾。字符串支持基本转义和Unicode文本。AST节点为`[kind, payload, offset]`，位置为UTF-16偏移。

## 公理与平台

`(axiom Name Version (Parameters...) Premise Effects)`展开为返回dv_axiom数据的具名函数。领域算法由core/*.dv提供。

Runtime.cs提供数值、Bool、文本、列表、文件、命令行、JSON交换和Block调用。compiler.dv负责语法、作用域和代码生成。surface-v1将在DV中增加新的前端和统一IR。
