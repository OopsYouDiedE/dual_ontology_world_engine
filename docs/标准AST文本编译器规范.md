> 本文保留迁移前的接口说明。当前 Essence–Projection Core 请参阅 [当前使用示例](core-essence-projection-usage-zh.md) 和 [迁移验收说明](core-migration-acceptance-zh.md)。

# 标准 AST 文本编译器技术规范

## 1. 概述

标准 AST 文本编译器负责将人类可读的领域特定语言（DSL）文本源码编译转换为强类型的内存抽象语法树（Abstract Syntax Tree, AST）。
完整的编译管线包含三个核心阶段：词法分析（Lexing）、语法分析（Parsing）以及符号解析与类型校验（Symbol Resolution & Validation）。

## 2. 编译处理管线

```
[DSL 文本源码]
       │
       ▼ (词法分析器 Lexer)
 [Token 记号流]
       │
       ▼ (语法解析器 Parser)
[原始语法树 AST]
       │
       ▼ (符号解析器 & 语义分析器 Resolver)
[已绑定的运行时强类型规则对象 Rule]
```

## 3. 核心阶段技术规范

### 3.1 词法分析（Lexical Analysis）

词法分析器将源文本字符流扫描切分为离散的词法记号（Token）。每个 Token 包含词类（TokenType）、字面文本（Lexeme）及源码行列坐标（SourceSpan）：
* **关键字（Keywords）**：`RULE`、`THEN`、`REQUIRE`、`FORBID`、`PERMIT`、`PREFER`。
* **字面量（Literals）**：整型数、浮点数、双引号字符串、布尔值（`true` / `false`）。
* **操作符与分隔符**：`(`、`)`、`,`、`=>`、`$`（槽位前缀）、`@`（实体前缀）。
* **标识符（Identifiers）**：以字母或下划线起始的名称序列，用于表示谓词操作符（如 `state`、`tag_at_least`）与动作原语（如 `move`、`open`）。

### 3.2 语法分析（Syntax Analysis）

解析器消费 Token 流并基于形式文法（EBNF）构建树状数据结构。标准文法定义如下：

```ebnf
RuleDeclaration ::= "RULE" ConditionExpression "=>" DirectiveList
ConditionExpression ::= Expression
DirectiveList       ::= Directive ( "," Directive )*
Directive           ::= DirectiveMode EffectExpression
DirectiveMode       ::= "REQUIRE" | "FORBID" | "PERMIT" | "PREFER"
Expression          ::= Identifier "(" [ ArgumentList ] ")"
EffectExpression    ::= Identifier "(" [ ArgumentList ] ")"
ArgumentList        ::= Argument ( "," Argument )*
Argument            ::= Expression | EffectExpression | Slot | EntityReference | Literal
Slot                ::= "$" Identifier
EntityReference     ::= "@" Identifier
```

解析策略通常采用确定性递归下降解析法（Recursive Descent Parsing）或 Pratt 算符优先级解析法。

### 3.3 符号解析与语义检查（Symbol Resolution & Binding）

语法树生成后，必须经过语义分析阶段以桥接文本与运行时环境：
1. **符号解析（Symbol Resolution）**：
   * 将 `@Name` 格式的实体引用在当前世界环境的符号表（Symbol Table）中进行查找，替换为真实的运行时对象引用。
   * 若符号未定义，产生语义分析错误（Unresolved Symbol Error）。
2. **槽位验证（Slot Validation）**：
   * 校验 `$Actor`、`$Target`、`$Tool`、`$Scene` 是否属于合法的上下文槽位枚举。
3. **参数基数与形态校验（Arity & Shape Checking）**：
   * 校验特定操作符的参数数量与类型约束（例如 `tag_at_least` 必须接受目标实体、标签名与整数门槛三个参数）。

## 4. 诊断与容错机制

* **精确位置报告**：语法或词法错误发生时，编译器必须返回具体的行号、列号与期望的 Token 类型。
* **不可变输出**：编译成功后产出的 AST 节点应当使用不可变记录类型（Immutable Record），保证规则在并发求值与长周期缓存中的引用安全性。
