# UI prefab 独立自动验收首轮

日期：2026-10-05。执行者：独立新上下文 Agent。未读取项目设计文稿、README、AGENTS 或历史验收结论。本轮属于自动局部验收，不属于盲玩或完整游戏流程验收。

使用 Godot 4.6.1 mono，真实 `--editor --script`，OpenGL 3.3 NVIDIA RTX 3070。脚本直接记录 `Engine.is_editor_hint() == true`。截图来自实际编辑器上下文内的 SubViewport 渲染纹理，并非 headless 截图或运行时冒充编辑器。没有通过鼠标操作 Inspector；即时修改以脚本在同一真实编辑器上下文设置 exported properties，保存临时 PackedScene 到 artifacts 后重载。

## 通过的局部断言

- PaperModal 导出标题、尺寸、边距、遮罩颜色更改立即更新实际子节点。
- 改动的 PaperModal PackedScene 仅保存到本任务 artifacts，保存成功；重载后标题、尺寸、边距保持。
- FieldUI 编辑器预览标题、时间立即更新。
- ReplayManuscript 编辑器标题、页边距立即更新。
- 报告枚举41个prefab条目，JSON断言 failures=[]；这不能作为全部场景加载干净的结论，原因见下述日志错误。
- 渲染输出了Workbench标题、对白、纸页弹窗、重演手稿、一章HUD与二章HUD；另外直接场景捕获对白/手稿各一张1280×960 PNG。

## 发现的问题与限制

1. 实际编辑器渲染中 RichTextLabel 继承编辑器黑色 normal 背景：对白出现大块黑底；重演手稿墨色标题与正文在黑底上难以辨认。已有截图可直接复核。已向主代理报告，由主代理修复。
2. 首轮测试期间 NotebookGrid 场景正在移除；日志出现两章 NotebookGrid.tscn 不能打开错误。Workbook资源当时仍引用这些路径。JSON断言并未拦截ResourceLoader后台日志，因此不能宣称全部prefab通过。主代理已收到问题。
3. 首轮进程启动日志出现旧 `FirstAwakening/LocaleSettings.gd` autoload找不到。随后同路径同参数的单独截图进程不再出现此错误。验收Agent未修改project.godot，不对首轮旧路径来源作无证据归因。
4. `--editor --script`在退出时出现编辑器扫描中止、EditorSettings未实例化、RID/ObjectDB/resource泄露等诊断；截图有效，但本轮日志不干净，不据此宣称正常编辑器完整生命周期无错误。
5. 按主代理最新要求停止修复后重测，后续必须由新独立Agent执行。尚未进行两章启动交互、正常游戏外观与git HEAD截图对照、完整游戏游玩或Core测试。

## 文件

- `independent-editor.log`：首轮全部局部断言与Workbench截图日志。
- `independent-editor-report.json`：首轮断言记录。
- `editor-capture.log`：对白/手稿直接场景渲染日志，明确EDITOR_HINT=true。
- `editor-page-01.png` / `editor-page-04.png`：Workbench对白/手稿首轮截图。
- `editor-dialogue.png` / `editor-manuscript.png`：直接场景首轮截图。
- `editor-modal-fixture.tscn`：保存重载用临时场景，不属运行时生产资产。

生产场景和脚本未由验收Agent修改。仅新增 `godot_csharp/scenes/Shared/check/ui_prefab_check.gd` 与 `ui_editor_capture.gd` 两个明确自动验收入口，以及本任务artifacts输出。
