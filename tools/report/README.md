# 代码分析报告

在项目根目录运行：

```powershell
python tools/report/generate_code_analysis_report.py
node tools/report/validate_report.mjs
```

生成器使用 Python 3.10+ 标准库；浏览器验证使用 Node.js 22+（内置 WebSocket）与本机 Chrome。Chrome 路径可通过 `REPORT_CHROME` 指定，不修改全局环境。验证启动独立无头浏览器，配置放入生成目录，退出时关闭。

输出均位于 `artifacts/code-analysis/`：

- `index.html`：可离线打开的单文件报告，包含源码、函数详情、工程引用图和搜索。
- `analysis.json`：相同分析数据，便于后续复查。
- `preview.png`：浏览器实际渲染的报告预览，不是游戏实机截图。
- `validation.json`：实际浏览器检查结果。
- `module-analysis.json`：16 个测试单元的边界、完整文件归属、接口依赖、测试契约与实施顺序。
- `module-preview.png`：模块拆分页面的实际浏览器预览。
- `system-design.json`：五个 UI、八个场景系统及二级验收条件、共享契约、内置能力和官方资料。
- `system-preview.png`：功能系统设计页面的实际浏览器预览，非游戏实机。
- `core-design.json`：11 个生活基础、7 个特色扩展、能力组合与回退验收契约。
- `core-preview.png`：Core 页面实际浏览器预览，非游戏实机。

Core 设计维护于 `tools/report/core_systems.json`，说明见 `docs/35 Core生活模拟基础与特色能力拆分.md`，页面入口 `index.html#core`。能力预设只切换报告显示，不修改游戏配置。生成器校验设计依赖无环、基础无反向特色依赖、源码路径及预设闭包；不代表运行模块已经解耦。

功能设计维护于 `tools/report/functional_systems.json`，设计说明见 `docs/34 UI与场景系统拆分设计.md`。打开 `index.html#systems` 查看主功能划分；旧 16 模块作为代码归属参考。生成器检查系统/二级标识、依赖、代码路径和官方来源；不把设计用例记为游戏验收通过。

模块定义保存在 `tools/report/module_boundaries.json`。生成器按最长字面路径前缀确定唯一归属，漏分、同优先级冲突、未知依赖或依赖环都会报错。该清单定义逻辑边界，不修改现有程序集或场景结构。每个模块有两个验收用例定义；它们尚未执行，不是游戏测试通过记录。

扫描当前工作树中的 C#、GDScript、Python、JS/MJS、HTML、Godot Shader/include，另列 DV、场景、资源、工程及配置引用。跳过缓存、虚拟环境、构建产物、符号链接与 `artifacts/`。Git 跟踪清单用于交叉检查漏扫。

Python 函数通过 AST 提取；其余语言使用词法屏蔽、括号和缩进边界。命名函数清单不保证包含所有局部函数、属性存取器、匿名函数和动态生成代码。报告保留完整文件原文。

函数连接是名称/类型名匹配候选，不是编译器语义绑定。关键函数的机制说明在 `KNOWN` 中维护；其他说明来自正文结构提取，明确标记来源。本工具不启动或验证游戏，不产生玩法回执，也不替代正式 CLI/Godot MCP 测试。
