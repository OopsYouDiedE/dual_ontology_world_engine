# S19：示例内容外置、作者预览数据中性化（非 UI 部分）

## 改动与原因

- `ExampleWorld` 移除 C# 中的具体人物、物件、画像、补给和剧情构造，改为加载指定的正式 `.story` 编译 JSON，再创建 `WorldSession`；文档缺失、空文档或加载失败均明确拒绝，不补入默认故事。
- 原两份 `.world` 示例迁为 `WorldScript/chapter_01_arrival.story`、`chapter_02_morning.story`，使用中性的机制样本演示声明、主观候选、策略变化、对象及认知继承；文字只在 `.story`。反应补齐当前正式编译器要求的 `intent`。
- `ExampleScript` 仅引用 `ChapterRuntime`。示例统一使用正式 `tools/story_script.py` 编译入口，不另造解析器。
- `WorldSessionEngine` 只导出已成功加载的声明，位置从同一运行时对象引用读取，包含携带对象所在房间；不提前导出后续章节角色。
- `WorldScriptPreview` 动态枚举角色、所在地与当地物件，可选择不同角色和对象。场景删除固定实体元数据及具体预览文字，布局、尺寸、颜色、主题与样式资源未改。
- 两份检查从声明取得实体、候选、章节、状态标签和对白，不再写入具体案件绑定；新增缺失文档无回退及对白来源断言。预览检查可指定临时编译物，不截图，不冒充 UI 验收。
- 已删除 `CSH-008、CSH-017、CSH-018、CSH-038` 注释，移除 `RES-094–097` 固定资源绑定；同步更新 `godot_csharp/AGENTS.md` 的示例路径、入口和检查方式。

## 验证

测试由全新独立上下文 Agent 执行，未读取设计文件。详细临时日志位于 `tmp/s19-validation/`。

| 项目 | 结果 |
| --- | --- |
| 正式 `story_script.py` 编译及 `--check` | 基础例、改名夹具、新增声明夹具均通过 |
| `WorldScriptCheck` Release 构建 | 通过，0 警告、0 错误 |
| `WorldScriptCheck` 运行 | 基础例 16/16；所有章节/实体/候选 ID 改名后仍为 16/16 |
| Core Release 构建、作用域回归 | 通过，0 警告/错误；24/24 |
| 宿主构建 `-m:1 --disable-build-servers` | 通过，0 错误；NU1900 警告为 NuGet 漏洞服务网络不可达 |
| 作者预览传输检查 | 基础例和第二章新增第三角色/地点/物件夹具均退出 0、断言通过；启动有证书环境 ERROR，不能判为零引擎错误通过 |
| 统一回归 `--skip gallery --keep-going` | 退出 1；core/scope/host 通过，flow 136/136、flow2 109/109 断言通过，但两项各有证书 ERROR，被原验证器判失败；gallery 按要求跳过 |
| 修改范围及 `git diff --check` | 无 `scenes/UI/` 或 Core 改动；差异检查通过 |

保留的原断言包括适用性、主观策略、未知反应、对象/灵魂/自我引用继承、状态不覆盖、重复/缺依赖/非法引用/非法动作原子性、选择令牌过期与去重、三值逻辑、错误认知不偷看现实，以及世界候选跨章存续。未删除失败断言或放宽数值预期。

统一回归通过临时启动器调用原 `verify.py`，仅为宿主命令追加规定的构建参数、将日志输出改至临时目录；扫描规则及断言未改。原始通道日志位于 `tmp/s19-validation/verify-logs/20261010-020357-284297-13176/`，汇总为 `verify.log`，完整错误扫描为 `log-inspection-final.log`。四份 Godot 日志（基础预览、扩展预览、flow、flow2）除各一条证书错误外，无其它错误或资源泄漏。

原“章节名及实体 ID 等于固定文本”改为两章依赖与声明引用合同，并以一致改名的 `.story` 夹具复核；依据为本轮两份 `.story` 的声明、反应、扩展与候选，机制预期保持不变。

先前失败日志均保留：初稿缩进、角色画像必填、反应 `intent` 必填已按正式入口修正；一次非单进程 MSBuild 仅报告失败而无诊断，改用规定的单进程无构建服务器命令后成功。

## 留给 UI

| 标记 | 文件与事项 |
| --- | --- |
| CSH-014 | `scenes/UI/Panels/Notebook/Notebook.gd`：笔记编辑器样例 |
| CSH-021 | `scenes/UI/Panels/CampusMap/CampusMap.gd`：地图预览节点及连线 |
| RES-089 | `scenes/UI/Workbench/UIWorkbench.tscn`：预览问题 |
| RES-090 | 同上：预览作答指引 |
| RES-091 | `scenes/Shared/UI/ChapterDialogue.tscn`：旧对白预览说话人 |
| RES-092 | 同上：旧对白预览正文 |
| RES-093 | 同上：旧对白预览人物文字 |
| 其它 UI | `ChapterDialogue.gd` 及 UI 布局、样式、正式界面的样例回退均未修改，留给 UI 对话 |

## 未完成项与风险

- 未提交；正式生成物按用户分工留主代理重生，现有 `generated_world.json` 未改，默认预览须在重生后复核。测试均使用本轮临时编译结果，未以旧生成物冒充证据。
- 重生示例：`python godot_csharp/tools/story_script.py godot_csharp/scenes/Shared/Story/Examples/WorldScript/chapter_01_arrival.story godot_csharp/scenes/Shared/Story/Examples/WorldScript/chapter_02_morning.story -o godot_csharp/scenes/Shared/Story/Examples/WorldScript/generated_world.json`。
- `ExampleWorld` 不再实现第二章专用 `IWorldScript`；旧外部使用者需改用 `ExampleWorld.Load(path).CreateSession()`，不再用第二章 `--world-script` 加载该示例 DLL。仓库内相关示例检查已同步，正式两章入口未改。
- Godot 报 `Failed to read the root certificate store`（Windows `os_windows.cpp:2570`）。保留原扫描规则，未过滤环境错误；需主代理在证书可读取环境复核。
- 本轮仅为构建与自动机制/预览传输回归。一级三份记录、二级 UI 呈现、三级盲玩均未执行；人物合理性及完整游玩体验未验证，不作整体游戏验收结论。
