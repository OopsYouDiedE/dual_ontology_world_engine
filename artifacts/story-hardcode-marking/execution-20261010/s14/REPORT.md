# s14：地图文字迁移执行报告

## 改动与原因

- T1a：九项环境旁白逐字追加到 `story/第一章/热点与布局.story` 的 `家具旁白` 表；六个房间标签复用既有 `ROOM_NAMES`。`build_awakening_map.py` 按明确的窗口、地毯、拖鞋槽键读编译文字，移除 `cx > 600`、`fw[1] == ...` 选句。
- T1b：十四个室内标题、四十个场点标签逐字追加到 `story/续篇/07_家具与文本.story` 的 `文本` 块。`rooms.py` 只保留 `title_key` / `label_key`；生成器从正式 Chapter2 `design.text` 解析，向现有场景和 manifest 输出原来的 `title` / `label` 结构。
- 两章新增 `--text-only`。先完整校验文字、重复键、目标节点与场点集合，再统一发布候选；仅替换字符串 token，保留其他序列化字节，值未变时不写文件。第二章仅接受计划指定的十四个室内地图键。
- `--sites-only` 的几何仍来自 recipe，标签改读同一正式编译文字表；仍保留从 `Sites` 到文件尾重写的既有用途。
- 新增共用 `tools/map_text.py` 和隔离自检 `tools/check/check_map_text.py`；同步 `godot_csharp/AGENTS.md` 第四节管线说明。
- 未修改 Core、C#、UI、布局/样式资源或绘图几何；未读取设计文件，未提交。工作区原有三份 translation 修改原样保留。

## 执行与验证

验证由全新独立上下文代理执行；临时目录修复后的自检另由新代理复测。

| 命令/核对项 | 结果 |
| --- | --- |
| `python godot_csharp/tools/compile_story.py` | 通过，正式生成七份输出 |
| `python godot_csharp/tools/build_awakening_map.py --text-only` | 通过，0 文件变化 |
| `python godot_csharp/tools/build_chapter2_scenes.py --text-only living_hall zone_jia zone_yi zone_bing zone_ding central_hall lecture archive counterweight resonance clinic logistics granary greenhouse` | 通过，0 文件变化 |
| `python godot_csharp/tools/compile_story.py --check` | 通过，七份输出均为最新 |
| `python godot_csharp/tools/check/check_map_text.py` | 9/9 测试通过，28 个显式子用例；测试耗时 1.218 秒 |
| `dotnet build csharp/ScopeRegression.csproj -c Release` | 通过，0 警告、0 错误 |
| `dotnet run --project csharp/ScopeRegression.csproj -c Release --no-build` | 24/24 检查通过 |
| `dotnet build godot_csharp/DualOntologyWorldEngine.sln` | 默认构建 exit 1，摘要 0 警告、0 错误；诊断仅定位到 Restore 的 MSBuild 子任务失败，未给出明确末端原因 |
| `dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1` | 通过，0 错误、2 个 NU1900：nuget.org 漏洞数据索引不可达 |
| `python tools/verify.py --only flow flow2 --keep-going --godot <用户指定程序>` | 两项均在无法打开 `user://logs` 后 signal 11 崩溃，exit 3221225477；未进入断言 |

自检覆盖缺键、重复 JSON 键/绑定/场点、未知地图或场点、错误字符串类型；后续目标校验失败时整批不写。另核验成功更新只改文字、引号/换行/反斜杠转义、原字节保留、重复执行零写入、移动几何不改变选句，以及 `--sites-only` 使用编译文字。所有夹具均为中性内容，不修改测试预期。

首次自检被系统 Temp 写权限阻断；移入仓库后，`TemporaryDirectory` 的 0700 目录权限仍阻断写入。现改为仓库 `tmp/` 下独立 UUID 目录，以普通 `Path.mkdir()` 继承权限，清理前校验其父目录；随后独立复测通过。失败原始日志保留。

## 字节与内容核对

- 九项旁白、五十四项续篇文字逐项匹配计划绑定的 UTF-8 摘要；两份 `.story` 的原有字节前缀完整保留。
- `generated_awakening_layout.json` 仅新增九个 `furniture_asides` 键；`generated_chapter_02.json` 仅新增五十四个 `design.text` 键。移除新增键后，两份完整数据与修改前一致；其他五份正式编译输出原字节一致。
- 52 个 `scenes/` 场景与本轮原始字节一致；55 个既有 ShareAsset 场景与 HEAD 原字节一致，共 107 个 `.tscn` 无变化。`map_layout.tres`、manifest、已有 translation 和第一章地图 PNG 均原字节一致。
- 十四个室内 recipe（四十个场点）解析文字后完整等于旧模块结果；第一章纯 layout 结果也完整等于旧 recipe 与现资源。
- `git diff --stat` 显示两份编译 JSON 分别为 9 行新增和 56 行差异（54 键及末项逗号）；地图产物专项 `git diff --stat` 输出为空。全量 stat 为 11 个已跟踪文件、508 行新增、71 行删除，包含开始前已有的三份 translation 差异；新工具、自检和本报告尚未跟踪。
- 独立核对脚本最初将 52 个 `scenes/` 文件误作全部场景，导致一项覆盖断言失败；补充核对其余 55 个 ShareAsset 场景后通过。这是验证范围修正，未改项目断言或实现。

## 窗户坐标调查

当前原中心为 `(x0 + 3) * 32`，前三窗 `x0=1/8/15`；镜像宽度为 `36*32=1152`，第一章偏移为 `4*32=128`。

| 槽 | 原中心 | 镜像后 | 第一章偏移后/现资源 |
| --- | ---: | ---: | ---: |
| EN | 128 | 1024 | 1152 |
| R05 | 352 | 800 | 928 |
| R08 | 576 | 576 | 704 |
| hall | 832 | 320 | 448 |

公式、`rooms.zone`、第一章纯 `plan` 与现 `map_layout` 一致。计划列出的前三窗 1184/960/736 与当前源码实际计算不符，其数值等价于将前三窗误按 `(x0 + 2) * 32` 推导；未发现实际生成几何差异。本轮未修改几何。

## 未完成项与风险

- Godot 的 AppData 日志写入被沙箱阻断，无法核对既有 flow 24 项、flow2 3 项失败是否保持；不宣称“不新增失败”已通过。
- 默认宿主构建失败原因未完全定位；已确认单节点构建通过。NuGet 漏洞索引联网警告仍存在。
- 未执行全量绘图重生、Godot import、正式 `--sites-only` 资产更新；该入口文字来源已在隔离夹具验证。固定旁白槽序和几何若后续改变，需要重新核对槽关联。
- 本轮为自动回归及资源一致性验证；一级、二级、三级验收均未执行。构建：上述范围通过；文字机制：通过；人物行为合理性、游玩体验：未评价。

过程日志位于 `tmp/s14/verify-*.log`。关键证据：`verify-map-text-final.log`、`verify-08c-godot-build-single.log`、`verify-11-independent-invariants.log`、`verify-11b-scenes-coverage.log`、`verify-10-flow-flow2.log`；Godot 原始分通道日志保存在 `artifacts/project-audit/verify-logs/20261010-005909-891532-7724/`。
