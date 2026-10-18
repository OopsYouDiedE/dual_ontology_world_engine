# DV 文件整理与七文件导出

日期：2026-10-18。原始版本：`25ee9282`。四个 gpt-6-luna / low 子代理分别整理 Core、session/checks、章节内容和导出工具；主代理统一接线并核对。

## 交付

双击 `export/export_dv_package.bat`，从所在仓库读取七个目录，生成到 `artifacts/dv_consolidated_package_v2/`。可传 `--out <目录>` 或 `--check`。需要 Python 3；启动器先使用 `py -3`，再尝试 `python`。

用户提供的外部 `consolidate_dv.py` 保持原文件；新工具沿用其七个输出名称。旧 `artifacts/dv_consolidated_package/` 保留。来源路径与原脚本指纹见 `export-origin.json`。

| 源码职责 | 整理前 | 整理后 |
| --- | ---: | ---: |
| compiler 顶层 | 1 | 1 |
| core | 13 | 5 |
| session | 11 | 5 |
| 章节内容及专用检查素材 | 17 | 12 |
| game | 5 | 2 |
| checks 顶层 | 18 | 6 |
| report | 1 | 1 |
| 本轮范围合计 | 66 | 32 |

文件数是源码整理范围的盘点，不是测评结果。编译器示例、旧 surface 提案和其他范围外源码不计入。章节生产内容分别置于 chapter1、chapter2；oracle、recall、验证配置、cognitive-semantics 移入 checks 的 authority/fixtures 子目录，report 也归入 checks。生产入口继续隔离这些检查素材。

同职责的小模块合入现有锚点，未发现可直接删除且完全等价的重复函数，因而保留全部声明。七组导出的重复文件拼接逻辑统一为一个配置驱动的实现。函数签名、声明正文、字符串和既有来源证据标识保持原文；物理路径的迁移见 `path-map.json`。同步修改 include、配置列表、工程工具路径、AGENTS 与源码索引。

## 工程证据

- `verify_layout.py` 对 Git 原版本和当前文件作声明逐行比较，只忽略空行、整行注释和 include；覆盖 fn、axiom、mapping、acceptance、imprint 和启动 defn。合并目标的声明内容一致，生产 debug-run 依赖闭包的声明集合一致，编译器字节未改。详细指纹见 `structural-review.json`。
- 生产 debug-run 的编译器静态诊断未报告问题，见 `compiler-diagnostics.json`。Release Session 类库构建完成，见 `build.log`。
- 检查组最初静态诊断提示 oracle 使用的 `c1_list_4` 缺少声明。已将编辑器检查组配置改为加载生产 entry 的声明依赖，再加载隔离的检查与 oracle；生产入口不包含 oracle。原诊断与复查分别保存在 `checks-diagnostics.json`、`checks-diagnostics-final.json`，后者未报告诊断。
- 实际调用 BAT 导出，随后再次调用，生成字节一致；`--check` 未写入。`verify_package.py` 核对所有源 SHA-256、正文行范围及跨文件重复来源，见 `package-review.json`。
- 独立只读审查未发现生产引用或导出覆盖范围的阻断问题；未读取设计文件、未运行会话。

导出含七份 DV、来源 manifest 和说明。七份是源码审阅包，原 include、main 和文件边界保持可追溯，不作为可直接联编的替代入口。脚本拒绝缺失输入和未经登记的同名输出覆盖。

本轮没有运行剧情会话、流程回归或 Godot。编译及结构比对只说明工程接线与声明保持情况；五日推进、人物行为、几何与体验均不在本轮验证范围。旧会话导出配置仅更新迁移路径与重复列表，没有重跑旧流程。

原源码可从上述 Git 版本恢复；本轮提交保存映射和指纹，不将临时 baseline 副本作为第二套源码提交。
