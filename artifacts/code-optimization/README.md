# 核心世界引擎与代码优化综合度量报告 (Code Optimization Audit)

> 本文档为 `artifacts/code-optimization/` 唯一权威报告，系统归纳整理了 2026-10-01 代码优化里程碑中关于 Core 独立引擎 AST 算法重构、GC 内存分配优化、Python MCP 推演工具分层、以及 Godot 宿主回归与会话隔离的测量数据。

---

## 一、 职责整理与重构全景

在保证语义与对外契约 100% 一致的前提下，对核心模块进行了职责分离与去冗余重构：

| 模块 / 路径 | 重构前行数 | 重构后行数 | 职责归属与优化要点 |
| :--- | ---: | ---: | :--- |
| `csharp/Ast.cs` | 420 | 280 | 名词与引用收集直接读取 AST，避免创建中间映射树；保持顺序、重复引用和对象身份不变。 |
| `godot_csharp/scenes/FirstAwakening/HallReplayBridge.cs` | 1,007 | 120 | 保留 Godot 绑定 API；解析、会话执行、翻译与状态投影解耦分离。 |
| `tools/deduction_mcp/engine.py` | 899 | 183 | 单向分层为 `engine → _analysis → _replay → _case_model`，存储与纯逻辑校验分离。 |

---

## 二、 Core 独立引擎 AST 内存与性能度量

通过 `GC.GetAllocatedBytesForCurrentThread()` 进行同线程高频基准测试（每项预热 2,000 次，强制 GC 后完整迭代 10,000 次），复杂嵌套树包含 141 个名词与 130 次投影引用：

| API / 遍历目标 | 基线分配字节 (10,000次) | 优化后分配字节 (10,000次) | 内存分配降幅 |
| :--- | ---: | ---: | ---: |
| **Nouns (effect)** | 248,960,000 | 43,040,000 | **-82.71%** |
| **References (effect)** | 250,080,000 | 44,160,000 | **-82.34%** |
| **Nouns (rule)** | 267,040,000 | 43,040,000 | **-83.88%** |
| **References (rule)** | 268,160,000 | 44,160,000 | **-83.53%** |
| **References (condition)** | 22,160,000 | 8,560,000 | **-61.37%** |

- **正确性验证**：52 项运行检查 100% 通过，覆盖 9 种 Condition 与 12 种 Effect 节点；异常类型与错误信息完全一致。

---

## 三、 Python MCP 推演工具分层验证

- **对照命令**：`python tmp/independent-code-optimization/verify_engine.py`（117 项动态基线对照全部通过）；
- **验证范围**：
  - 案例加载、列举、校验、可用性计算、审计、可识别性与补丁对比；
  - 有界搜索（depth=3, max_states=2000）状态数（745 状态）与路径决策完全等价；
  - 持久化 SHA-256 冲突检测与原子写入逻辑完全一致。
- **度量索引**：参见本目录 [`independent-engine-log.json`](independent-engine-log.json)。

---

## 四、 宿主绑定与会话状态隔离 (Session Isolation)

1. **同 Node 会话隔离度量**：
   - 针对单个桥接 Node，连续 25 轮混合调用 `CompileDraft` 与 `CompileEvents`，包含空输入、有效物理状态变迁、失败行动及非法输入，共计 **325 项检查全部通过**；
   - 验证了跨调用之间阀门、连接、人物位置与事件进度完全零污染。
2. **多语言与重演**：
   - 中文、English、日本語多语言切换、玩家名占位符替换与重演文案投影通过。

---

## 五、 历史演进与后继状态说明

- 本报告记录的 2026-10-01 优化主要聚焦于 Core 独立世界引擎与旧重演桥底层；
- 随后在 2026-10-02 第一章“未归的红筹”开发中，已彻底淘汰旧门案逻辑与纸面草稿系统（包含旧 `paper-notebook` 系列检查），基类进一步瘦身重构为 340 行轻量宿主；
- 当前第一章主线的最新实机运行与盲玩数据统一参见 [`artifacts/first-awakening/consolidated-audit-report.md`](../first-awakening/consolidated-audit-report.md)。
