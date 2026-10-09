# STORY-HARDCODE 标记独立验证报告

基线：`2384076f065acf32c95aad6e1732bd06a94f1bf3`。本轮只核验源码注释、分片清单、翻译锁、构建和 GDScript 语法；未读取设计文件、未运行游戏、未运行编辑器导入。

## 源码与清单

- 源码文件：206/206；源注释标记 196 条，清单标记 196 条。
- 三分片路径集合、marker 的 id/path/kind/scope/reason、ID唯一性、生成物 `generated_excluded` 例外、手写文件 `read_mode=full` 与 `unverified=[]` 均通过。
- 移除本轮完整标记注释行后，184 个文件与基线逐字节相等；另 22 个文件仅在 CRLF/LF 换行转换后相等；其余 0 个文件存在其他字节差异。逐文件基线与清理后 SHA-256、原始比较和换行归一化比较见 `source-audit.log`。
- 三份既有 translation 的工作区 SHA-256 与 baseline.json 一致，Git index blob 与锁定值一致；未触碰翻译。
- `git diff --check` exit 0；Git 对若干 CRLF 文件输出将来规范化为 LF 的提示，不是 whitespace error。
- 源码核验命令：`python tmp/verify_story_marks.py`，exit 0。

## 构建

- `dotnet build csharp/Core.sln --no-restore -m:1 -nr:false`：exit 0，0 warning / 0 error。原始输出：`build-core.log`。
- `dotnet build godot_csharp/DualOntologyWorldEngine.sln --no-restore -m:1 -nr:false`：exit 0，0 warning / 0 error。原始输出：`build-godot.log`。

## GDScript 语法检查

- Godot 4.6.1 console `--headless --path godot_csharp --check-only --script` 检查 27 个有改动脚本：26 成功，1 失败。每项命令输出分别保存在 `gd/` 下，汇总见 `gd-summary.json`。
- 唯一失败：`scenes/Chapter2/Game/check_gallery.gd` 预载 `res://scenes/Awakening/AwakeningEngine.cs` 不存在。使用基线版本的脚本另行执行相同检查，仍因同一路径缺失而失败；基线复现日志为 `gd/check_gallery-baseline.log`，临时基线副本为 `baseline-check_gallery.gd`。这是已存在的依赖错误，与本轮注释无关。
- `scenes/Shared/check/ui_modules_check.gd` 在恢复意外 BOM 后重新检查 exit 0，日志为 `gd/ui_modules_check-final.log`。

## 结论与未覆盖

- 移除所有新增完整标记注释行后，206/206 源文件均与基线相同；其中 22 个发生换行风格转换，除此之外没有程序内容差异。源码/清单和 translation 锁均通过。
- Core 与 Godot solution 构建通过。GDScript 语法检查 26/27 通过，唯一失败为基线可复现的缺失 preload。
- 未执行运行时剧情、回归套件、UI 验收或盲玩；这些项目不属于本次注释无行为变化核验。

## 场景与资源清单补充核对

- `resource-review.json` 的 59 个资源路径与 `rg --files godot_csharp/scenes -g '*.tscn' -g '*.tres'` 当前枚举精确一致；所有条目的 `read_mode` 均为 `structure_and_content_fields`，`unverified` 为空。
- 97 个 `RES-###` 索引 ID 全部唯一，路径均属于枚举资源，行号均落在对应文件的实际物理行范围内且行非空；类型和值域检查通过。
- 59 个资源逐个与基线 `2384076f065acf32c95aad6e1732bd06a94f1bf3` 的原始字节相同；仅换行归一化比较的文件为 0，其他差异为 0。资源未改动，条目仅为外部索引。逐资源 SHA-256、逐条索引行号核验和结果见 `resource-audit.json`，汇总日志见 `resource-audit.log`。
- 本补充核对不增加或混算源码标记数：源码仍为 196 条 `STORY-HARDCODE` 注释标记；97 条 `RES-###` 是资源审阅索引条目。
