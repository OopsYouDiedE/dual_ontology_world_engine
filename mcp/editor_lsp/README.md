# 统一 DV 编辑器服务

`dv_lsp.py` 提供 LSP 文档传输与诊断发布，`check_dv.py` 调用 `mcp/build/dv_toolchain.py` 的真实编译器。编译入口与 include 闭包由 `dv_core/dv-check.json` 决定。源文件的祖先目录用于发现仓库中的检查器，仓库外文件可用 `DV_CHECKER` 指定绝对路径。

`clients/vscode` 与 `clients/visualstudio` 打包同一份 `dv_lsp.py`，不复制语言规则。VSIX 输出位于 `artifacts/releases/dv`。Godot 插件位于 `godot/`，通过 `python mcp/godot_workspace.py --editor` 挂载到独立工具工程；`duelview_tools/repository` 提供源码和检查器根路径。

统一 MCP 的 `editor_tools` 提供 `check`、`lsp-diagnostics`、`lsp-config` 和两种客户端的准备/打包操作。`check` 的 `arguments` 是原命令行参数；`lsp-diagnostics` 接受 `{"path":"<源文件>","text":"<可选缓冲区>","version":1}`，直接使用同一 `Server.check` 与诊断发布器。`lsp-config` 返回实际 stdio 启动命令，长期编辑仍由标准 LSP 连接承载。

LSP 初始化从配置的 `DV_CHECKER` 或已打开的仓库发现 `mcp/build/prewarm.py`，调度共用后台编译器缓存。编辑器请求不等待预热；第一次实际诊断仍通过同一缓存锁获得编译器。MCP 列举不启动诊断，客户端准备和打包只在显式调用时执行。

生产游戏不加载语言服务或编辑器插件。实现、构建、协议观察和真实编辑器安装/显示分别记录；用户禁止执行时只修改源码。
