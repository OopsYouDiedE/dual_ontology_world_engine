# 七文件 DV 导出工具

双击 `export_dv_package.bat`，会从当前仓库布局读取七个 DV 输入目录，并生成到 `artifacts/dv_consolidated_package_v2/`。原 `artifacts/dv_consolidated_package/` 七文件包保持保留。也可在命令行向批处理文件传递 `--check` 等参数。

脚本 `consolidate_dv.py` 支持：

- `--root <仓库目录>`：指定仓库根目录；默认根据脚本所在位置推导。
- `--out <输出目录>`：指定输出位置；默认是 `<root>/artifacts/dv_consolidated_package_v2`。
- `--check`：只检查输出是否与当前输入一致，不写文件；缺失或不一致时返回非零状态。

导出由七份分别对应原始输入目录的 `.dv` 源码审阅文件、`manifest.json` 和包内 `README.md` 组成。每个来源都记录相对路径、原始字节 SHA-256、源行数和合并文件中的正文行范围；稳定排序保证相同输入重复生成相同内容。原始 `define`、`main`、`include` 等文本均保留，文件边界通过来源标记辨认。七份合并文件用于源码审阅，不是可直接联编程序，也不代表全仓源码。

更新时，脚本先在输出目录旁完整生成临时文件，再仅替换 manifest 登记的七份 `.dv`、manifest 和 README。输出目录里的其他文件会保留；遇到同名但未由本工具登记的文件时会停止，避免覆盖。输出路径不得与输入源码目录重叠。脚本不会递归清理输出目录；清理临时目录前会确认其解析后的父目录和名称前缀符合预期。

输入范围：编译器目录只读取直接子级 `.dv`，不读取 `examples` 或其他子目录；Core、session、chapter1、chapter2、game 和 verification/checks 会递归读取 `.dv`，其中 checks 包含 report、fixtures、authority 子目录。完整包含与排除范围列于生成的 manifest。
