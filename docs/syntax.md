# DuelView (DV) Core 现行完整语法与语义规范

本文档为仓库现行 DV Core 运行所采用的完整语法规范与语义参考，作为设计文档体系的第二个说明文件（与 [design-standard.md](design-standard.md) 并列）。本文档依据 `compiler/compiler.dv`、`compiler/Runtime.cs`、Core 运行时及会话执行逻辑的现行实现整理。

---

## 一、词法与源码结构

### 1.1 文件编码与标识符
- 源码使用 **UTF-8** 编码，文件后缀为 `.dv`。
- 标识符支持 Unicode 字母、数字及下划线，开头不可为数字，词法扫描时按 **NFC (Normalization Form C)** 归一化，区分大小写。
- 关键字及内置操作符以英文字符为主，支持中文标识符命名。

### 1.2 注释与空白
- **行注释**：支持以 `//` 或 `;` 开头的行注释，直到行尾。
- **空白与缩进**：
  - 语句通过换行符自然结束。在圆括号 `(...)`、方括号 `[...]`、大括号 `{...}` 内部允许跨多行书写。
  - 代码块以冒号 `:` 结尾，后接缩进更深的语句块。
  - 同一代码块内缩进空格数必须一致，禁止混用 Tab 字符（遇 Tab 报错）。

### 1.3 宏别名指令（define）
- `define <原词> <目标词>`：位于文件顶层声明之前，用于在词法层面进行标识符 token 的精确单次替换。
- 每个源文件拥有独立的别名作用域；不进行递归展开；重复定义同一别名将报错。
- 示例：
  ```dv
  define 函数 fn
  define 返回 return
  define 变量 var
  define 常量 let
  ```

### 1.4 模块与包含指令（#include）
- **模块声明**：`module <name>` 位于文件头部，当前在编译期被识别并登记。
- **文件包含**：
  ```dv
  #include <相对路径.dv>
  ```
  - `#include` 必须独立成行，路径相对于当前声明文件。
  - 依赖拓扑按拓扑排序展开，同一文件只包含一次，禁止循环包含。

---

## 二、基础语法与控制结构

### 2.1 函数声明
支持位置参数函数、可选名义类型注解以及单表达式或块状函数体：
1. **多行块状函数**：
   ```dv
   fn calculate_energy(mass, velocity):
       let v2 = velocity * velocity
       return mass * v2 / 2
   ```
2. **单表达式函数**：
   ```dv
   fn square(x: Number) -> Number = x * x
   ```
3. **前向引用与相互递归**：同一程序集内的函数支持任意顺序的前向引用与相互递归调用。

### 2.2 局部变量与绑定
- **不可变绑定**（`let`）：
  ```dv
  let actor_id = "detective"
  ```
  声明后禁止在同一作用域内被重新赋值。
- **可变变量**（`var`）与赋值：
  ```dv
  var counter = 0
  counter = counter + 1
  ```
  `var` 声明局部变量后，可以通过 `=` 进行再次赋值；禁止给未声明的变量赋值。

### 2.3 控制流语句
- **条件分支**（`if` / `elif` / `else`）：
  ```dv
  if score >= 90:
      return "优秀"
  elif score >= 60:
      return "及格"
  else:
      return "待考"
  ```
  `elif` 在前端降低为同级 `else` 内嵌套的 `if` 处理。
- **循环**（`while`）：
  ```dv
  var i = 0
  while i < rt_count(items):
      let item = items[i]
      process(item)
      i = i + 1
  ```
- **返回**（`return`）：从函数返回指定值；无返回值函数默认返回中性值 `false` 或空。

---

## 三、数据类型与表达式

### 3.1 字面量与基础类型
1. **数值（Number / Rational / Float）**：
   - 整数与浮点数：如 `0`, `42`, `3.14159`。
   - 底层支持精确有理数与双精度浮点运算，涉及逻辑判等与概率配平使用有理数（`$dv_rational`）。
2. **布尔（Boolean）**：
   - 常量：`true`, `false`。
   - 逻辑运算符：`and`, `or`, `not`，支持短路求值。
3. **文本（Text / String）**：
   - 双引号包裹：如 `"hello world"`。
   - 转义支持：`\n`, `\r`, `\t`, `\"`, `\\`, 以及 16 进制 Unicode 转义 `\uXXXX`。
4. **列表（List）**：
   - 字面量：`[1, 2, "three", true]`。
   - 下标访问：`items[index]`（0 起始，越界将触发运行时异常）。
   - 列表推导式：
     ```dv
     let active_actors = [a for a in actors if a.active]
     ```
5. **记录（Record / Map）**：
   - 字面量：`{"key": "value", count: 10}`。
   - 字段访问：`record.field`（展开至 `dv_get`，缺键报错；带回退值查询使用 `dv_find(record, key, fallback)`）。
   - 不可变更新：`dv_with(record, key, new_value)` 或 `dv_with_many(record, updates)`，创建新副本并保持结构共享。

### 3.2 运算符与优先级
按优先级从高到低排序：
1. 属性访问与下标：`a.b`, `a[i]`, 函数调用 `f(x)`
2. 一元运算符：`not`, `-`
3. 乘除模：`*`, `/`, `%`, `×`
4. 加减：`+`, `-`
5. 比较运算符：`==`, `!=`, `<`, `<=`, `>`, `>=`
6. 逻辑与：`and`
7. 逻辑或：`or`

---

## 四、领域声明（Domain Declarations）

DV 语言支持特定的领域公理与认知模式声明：

### 4.1 公理（axiom）
用于声明世界客观物理法则、行动效果、传播与因果推演规则：
```dv
axiom c1_boots_carbon_law:
    premise:
        equals(ref("detective", "physical", "location"), "living_hall")
    effects:
        assign(ref("detective", "evidence", "boots_fit"), true)
```

### 4.2 映射（mapping）
定义主体认知、信息通道与感知转换映射关系：
```dv
mapping topic_binding:
    domain: "dialogue"
    operator: "associate"
```

### 4.3 认同度与接纳（acceptance）
表达主体对假设、证言或观察证据的主观接纳程度与门槛：
```dv
acceptance subjective_degree:
    levels: ["reject", "suspect", "plausible", "accepted"]
    threshold: 0.75
```

### 4.4 有限域印刻（imprint）
用于有界假设搜索、有限变量域配平与最优解确认：
```dv
imprint solve_timeline:
    achieve: target_state
    vary: candidate_actions
    conserve using: energy_balance
    limit using: reasoning_budget
    minimize using: action_cost
```

---

## 五、平台 ABI 与核心运行时操作

DV Core 通过 `compiler/Runtime.cs` 提供的平台底层支持与基础语义函数交互：

### 5.1 基础容器与运行时（rt_*）
- `rt_count(container)`：返回列表或记录元素个数。
- `rt_at(list, index)`：根据索引返回列表元素。
- `rt_push(list, value)`：追加元素至列表末尾。
- `rt_slice(str_or_list, start, length)`：截取切片。
- `rt_cat(str1, str2)`：字符串拼接。
- `rt_length(str)`：返回字符串长度。
- `rt_has_value(list, value)`：判断列表中是否存在指定值。
- `rt_copy_value(value)`：对值进行独立深拷贝。
- `rt_json_stringify(value)` / `rt_json_parse(text)`：JSON 序列化与反序列化。
- `rt_json_print(value)`：输出结构化 JSON 投影。
- `rt_read(path)` / `rt_write(path, text)`：平台文件读写。
- `rt_fail(message)`：中止当前演算并记录错误。

### 5.2 数据与记录操作（dv_*）
- `dv_ref(object, facet, field)`：构造领域引用单元（Reference Tuple）。
- `dv_get(record, field)`：获取记录字段，不存在时报错。
- `dv_find(record, field, fallback)`：安全获取记录字段，不存在时返回默认值。
- `dv_with(record, key, val)`：返回包含单个更新的不可变副本。
- `dv_with_many(record, key_values)`：返回包含多个更新的不可变副本。
- `dv_has_key(record, key)`：判断记录中是否存在某键。
- `dv_store(target, key, val)`：修改目标容器内的对应键。
- `dv_fact(ref, val)`：生成关于特定引用的客观断言。
- `dv_require(cond, message)`：前置条件断言检查。

### 5.3 会话与主体决策（dvs_* / dvg_*）
- `dvs_session_open(config)`：初始化世界与十六位主体状态。
- `dvs_session_intent(state)`：提取当前等待执行的意图与候选操作。
- `dvs_owned_state_update(state, updates)`：所有权安全更新会话顶层字段。
- `dvs_session_close(state)`：完成会话并生成事件账本。
- `dvg_host_open(options)`：宿主配置与会话打开入口。
- `dvg_prepare(state)`：执行下一轮主体动机评估与行动报价。
- `dvg_complete(state, receipt)`：接收宿主或执行器的现场回执并裁定最终权威效果与认知传播。
