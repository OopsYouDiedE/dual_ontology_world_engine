# DuelView DV for VS Code

打开 `.dv` 文件后，扩展通过共用的 DV 语言服务调用真实编译器。未保存的修改也会检查；错误显示在编辑位置和 Problems 面板中。编译组由最近的 `dv-check.json` 决定，`define` 别名仍按各文件独立处理。

## 安装和使用

在 VS Code 扩展面板选择 **Install from VSIX…**，安装 `DuelView.VSCode.vsix`。打开本仓库中的 `.dv` 文件即可开始检查。需要 Python 3 和项目要求的 .NET SDK；首次检查会构建并缓存 C3 编译器。

设置 `dv.pythonPath` 可指定 Python 可执行文件。编辑仓库之外的文件时，将 `dv.checkerPath` 设置为本仓库 `mcp/editor_lsp/check_dv.py` 的绝对路径。设置改变后服务自动重启，也可以执行 **DV: Restart Compiler Checker**。输出面板的 **DV compiler** 记录服务错误。

当前只提供编译诊断，不提供证明状态、自动补全或定义导航。当前文件使用编辑缓冲区，同组其他文件使用磁盘内容，因此跨文件修改需要先保存。源码检查不执行剧情，也不证明角色行为或剧情正确。扩展仅在受信任工作区运行。

## 构建

在本目录运行：

```powershell
npm ci
npm run package
```

安装包生成到仓库 `artifacts/releases/dv/DuelView.VSCode.vsix`。打包时从 `mcp/editor_lsp/dv_lsp.py` 复制服务器，不维护另一份语言检查逻辑。客户端采用 [VS Code 官方 LSP 客户端方案](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide)。
