# DV 编辑器集成维护

本目录继承 [dv_core/AGENTS.md](../../../dv_core/AGENTS.md)。客户端只传递源缓冲区和显示编译器诊断；语言语义统一由 [10-compiler.dv](../../../dv_core/compiler/10-compiler.dv) 实现，不维护第二份解析器或数值检查器。

`mcp/editor_lsp/check_dv.py` 读取最近的 `dv-check.json`，通过 C3 的 `--check` 模式检查编译组，不执行内容。当前配置分别选取编译器、生产闭包与独立检查闭包；生产组优先于 checks，避免向生产编辑上下文混入 oracle。各文件保留 define 作用域、源位置及独立范围。`mcp/editor_lsp/dv_lsp.py` 提供标准输入/输出协议，处理未保存缓冲区与过期诊断；当前文件之外的模块从磁盘读取。

VS Code 和 Visual Studio 2022 客户端都继续维护，Godot 插件直接调用同一个 JSON 工程诊断入口。客户端独立于游戏运行，不因生产未引用就删除。VS Code 在 `vscode/` 运行 `npm ci`、`npm run package`，发布包为 `artifacts/releases/dv/DuelView.VSCode.vsix`；打包复制共用 Python LSP，node_modules 与生成 server 保持未跟踪。Visual Studio 使用其项目文件构建 Release，再运行 `visualstudio/package.py --out <vsix>`。

工程修改先集中完成并静态审读，再按当前任务范围检查协议或真实编辑器。集成入口可在隔离扩展宿主中打开和编辑缓冲区；记录实际诊断或启动失败，不用协议测试宣称实际安装/呈现完成。构建产物在 bin/obj，安装包在 artifacts/releases；插件检查、游戏机制与游玩验收分别报告。当前请求没有授权运行的游戏或流程回归不得由插件测试间接启动。
