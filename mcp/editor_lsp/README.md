# 统一 DV 编辑器服务

`dv_lsp.py` 提供 LSP 文档传输与诊断发布，`check_dv.py` 调用 `mcp/build/dv_toolchain.py` 的真实编译器。编译入口与 include 闭包由 `dv_core/dv-check.json` 决定。源文件的祖先目录用于发现仓库中的检查器，仓库外文件可用 `DV_CHECKER` 指定绝对路径。

`clients/vscode` 与 `clients/visualstudio` 打包同一份 `dv_lsp.py`，不复制语言规则。VSIX 输出位于 `artifacts/releases/dv`。Godot 插件位于 `godot/`，通过 `python mcp/godot_workspace.py --editor` 挂载到独立工具工程；`duelview_tools/repository` 提供源码和检查器根路径。

生产游戏不加载语言服务或编辑器插件。此次实现阶段只修改与迁移源码，所有构建和检验均留待全部 Step 完成。
