# S03 执行报告（2026-10-10）

## 改动与依据

- 按本轮裁定的收缩范围实现；只查阅 `.story`、现有源码与 S03 审查材料，未读取设计文件。
- `Chapter2Bindings` 移除封闭物件字段表及公告板、废纸篓名单，按稳定实体 ID 从会话登记表取得实际引用并校验类型。
- `Chapter2Entities` 从声明构造地点与新增物件；空间属性直接指向同一会话的地点对象。既有物件机制继续持有原对象，并登记到同一表。
- 人物、地点、物件、通路、群体及宿舍集合从声明或声明生成的边界产生；少数具名机制保留原枚举访问。公告板、废纸篓集合从物件声明筛选。
- 第一章人物/地点兼容编号、重演通路，以及六项变量的取值 ID、显示文字和语义绑定迁入 `.story`；追加域不要求增加 C# 枚举。
- 保持现有枚举次序；六项变量检查旧 ID 对应原索引，拒绝重排、重复取值、未知绑定。稳定 ID 不得因数字后缀被误认为旧枚举名称。
- 新增独立编译及运行时夹具检查；同步 `godot_csharp/AGENTS.md` 记录登记与验证接口。未修改 Core 或 `scenes/UI/`，未改 UI 布局/样式。

## 验证

每轮由全新上下文的独立测试代理执行，未读取设计文件；未修改既有测试预期。

结论：临时生成状态下构建通过，本批登记与取值域机制合同通过；整章机制未完成全部验证，人物行为合理性与真实游玩体验未验证。

| 检查 | 本轮结果 |
| --- | --- |
| 实体编译/登记最终复测 | Python 4 项通过；Release 构建 0 警告/错误；运行 31 项通过，含六域扩展真实重演与原六数值字段序列化 |
| 临时生成后 `compile_story.py --check` | 通过，7 个输出一致 |
| `verify.py --only core scope cognition host --keep-going` | 退出 0；core 构建通过，scope 27 项、cognition 372 项通过，host 构建通过 |
| 第一章 `ReplayCheck` Release 构建/运行 | 构建 0 警告/错误，91 项通过；39 组时序扰动无错误组合吻合 |
| 第二章 `Prototype -- check` | 未完成，605.23 秒后中止；已抛关闭通路异常，无检查汇总；不能记为通过 |
| `AccessLifetimeCheck` / `PresentationCheck` | Release 构建均 0 警告/错误；运行分别 10 项、8 项通过 |
| 剧情隔离扫描 | adapter 减少 114；其余代码范围未增加；门禁仍为原有 FAIL 69 |

- 宿主构建使用 `-m:1 --disable-build-servers`，有 2 条 NU1900 警告（NuGet 漏洞数据源无法访问），0 编译错误。
- 重演时序扰动中 37/39 组重现真值；另外 2 组保留既有时序限制。此项不是盲玩或时长验收。
- 开发中第一轮夹具误读 `spatial`，正式属性为 `space`；已修复实现与新测试的读取，第二轮实体编译 3 项、运行 26 项通过。
- 第三轮数字地点夹具因缺少完整通路声明被编译器明确拒绝；保留原通行约束，数字兼容边界改用独立运行时登记检查，继续使用原新地点/物件夹具验证正常编译。
- 最终复测重跑 cognition 372 项和 host，均通过。Python 负例仅接受 `StoryError/ScriptError`；运行时拒绝合同仅接受 `InvalidDataException`，不以任意异常当作通过。
- 原始日志保留在 `tmp/s03-validation/` 至 `tmp/s03-validation-4/`。关键日志为第 2 轮 `06-replay-runtime.log`、第 3 轮 `05-prototype-run.stderr.log`、第 4 轮 `04-entity-runtime-check.log` 与 `07-story-leak-final-report.log`。

## Prototype 异常的基线对照

- 基线提交 `5cfbb1a9` 导出的 44 份源码/JSON 独立编译；现版对照引用既有 Release DLL，两份临时项目构建均通过。
- 两版按基线 `Bots.Play` 的 investigate 策略自然推进 18 时段至 `CaseDay=5` 清晨。第一件证物均经 `HostExamine → HostMoveDet` 抛相同关闭廊门 `PlayerError`；六件读取结果均为五件失败、一件可读。
- 两版完整 access 数据相同。`story/续篇/00_通行.story:13/17/21` 声明廊道初始关闭；`Bots.cs` 无 `HostCorridor/HostDormOut` 调用。`Bots.Read:60/Play:132` 与 `Host.cs:22/33` 未被本轮修改。
- 因而该闭廊读取失败路径是既有问题。本轮未改测试/驱动来绕过它；完整 Prototype 的其余断言与性能断言仍未验证，交整合主代理裁决。
- 对照证据：`tmp/s03-baseline/baseline-advance.log`、`current-advance.log`、`static-comparison.log`、`declaration-source.log`；对照程序捕获异常后退出 0 不等于原回归通过。

## 剧情隔离计数

| 范围 | 修改前 | 修改后 |
| --- | ---: | ---: |
| core | 0 | 0 |
| ui | 54 | 54 |
| shared-ui | 15 | 15 |
| interface | 0 | 0 |
| host | 0 | 0 |
| adapter | 2205 | 2091 |

交付状态执行 `python tools/check_story_leak.py --report`：进程退出 0，但门禁结果是 FAIL 69。原有 ui/shared-ui 命中未增加，不能宣称隔离门禁整体通过。

## 未完成项与风险

- 正式生成物按任务分工交由整合主代理重新生成。本轮临时生成用于构建与 `--check` 后，已将 7 个输出恢复为原始字节；交付源码因此需要整合后先运行 `compile_story.py`，再 `--check`，然后构建。
- 未运行 Godot；`verify.py --skip gallery --keep-going` 的 save/flow/flow2 留给整合主代理执行。本轮只能确认已执行的 dotnet 部分。
- 本轮未执行一级三份记录、二级 UI 专项或三级盲玩验收；人物行为合理性、文学质量及真实游玩体验未验证。
- 存量 `P`、`Place`、`Group` 及具名机制字段仍存在。新增通用地点可以登记/绑定/枚举；旧具名通行、移动和调查接口仍有枚举边界，其进一步通用化不在本批范围。
- 六变量结构未通用化；新增域须绑定现有机制。新增机制或变量结构属于后续任务。
- 保留进入工作区时已有的三份 `.translation` 改动；本轮不提交、不推送。
