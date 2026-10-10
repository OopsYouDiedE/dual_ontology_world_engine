# 边界探索中性机制验证

本轮为独立上下文 Agent 的自动回归与源码检查，不属于一级、二级或三级验收。未读取设计规范、剧本正文、正式游玩轨迹；未运行 FullTest。只修改现有两份中性检查文件，未修改生产代码或 `csharp/Core`。

被测生产补丁位于 `godot_csharp/scenes/Host/PilotNavigation.gd` 和 `godot_csharp/scenes/Shared/Story/PlayerController.cs`，属于 Godot 宿主及其共享游戏模块，不属于独立 Core 类库。

| 检查 | 结果 | 原始证据 |
| --- | --- | --- |
| 宿主构建 | 通过；0 警告、0 错误 | `host-build.stdout.log`、`host-build.stderr.log`、`host-build.result.json` |
| PlayerControllerCheck 构建 | 通过；0 警告、0 错误 | `controller-build.stdout.log`、`controller-build.stderr.log`、`controller-build.result.json` |
| PlayerControllerCheck 运行 | 58 项通过；退出码 0 | `controller-run.stdout.log`、`controller-run.stderr.log`、`controller-run.result.json` |
| Godot 中性导航检查 | 32 项通过、0 失败；stderr 为空；退出码 0 | `godot-navigation-final.stdout.log`、`godot-navigation-final.stderr.log`、`godot-navigation-final.result.json` |
| 两份检查文件差异检查 | 通过 | `test-diff-check.stdout.log`、`test-diff-check.stderr.log`、`test-diff-check.result.json` |

## 新增覆盖

- 在 `(16,16)` 先尝试普通移动，再提供同位置 frontier，后者仍有未尝试和边界探索评分；该坐标在 32/64 像素格中都属于第 0 格，能单独验证历史类别隔离。
- `(16,16)` 与 `(48,16)` 属于同一个 64 像素格，但属于相邻两个 32 像素格；第二个 frontier 保持独立新鲜度。重复同一 frontier 则不再获得新鲜度。
- 先尝试 frontier 后，同位置普通移动仍保持独立尝试历史。
- 中性走廊的已见边界中心为 `(144,144)`；近处提供 `(176,144)` 普通移动输入，远处仍先提供已见边界 `(144,144)`。
- 在已见范围不变的两格门槛上，脚下位置 `(176,144)` 与 `(208,144)` 均继续提供八个 32 像素 frontier 短步，向前分别为 `(208,144)` 与 `(240,144)`；生成短步没有查询这些向前落点的通行几何。
- 空已见集合时，八个既有短步全部具有 frontier 标记，通行及路线查询计数为零。
- 已见地板四周均为墙时，边界识别不产生 frontier 候选。

未知几何结论限定于普通短步生成分支。已有 `frontier()` 边界识别仍查询已见格的相邻通行和路线；本报告不宣称整个 `offers()` 无未知几何查询。

## 失败记录及修复

首次 Godot 检查发现新增测试 lambda 的换行语法错误，已仅修改测试文件修复；原日志为 `godot-navigation.*`。

第二次发现生产 `PilotNavigation.gd:12` 的动态表达式无法推断 `beyond_seen` 类型。主代理改为显式 `bool` 后复测通过；原日志为 `godot-navigation-rerun.*`。因脚本初始化错误未自动退出，验证 Agent 结束了本次控制台进程 PID 31504 及其明确关联的 Godot 子进程 PID 12248；结果码保留，不作为通过证据。

最终检查控制台进程 PID 9492 正常退出。结束后仅剩任务开始前已有的用户 Godot 进程 PID 23084，未操作该进程。主代理已收到所有新增检查 Godot 退出的通知，可以继续实机。

所有 stdout/stderr 均由二进制文件句柄直接接收；本目录 `.gitattributes` 对 `*.log` 设置 `-text -diff`。开始与最终源码 SHA-256 分别保存于 `source-hashes.json`、`source-hashes-final.json`，原日志哈希保存在 `log-hashes.json`。

## 结论与未覆盖项

构建通过，所要求的中性尝试历史、边界外延、多格门槛和墙面排除机制通过。未执行一级、二级或三级验收；实际地图、碰撞运行、UI、人物行为合理性和游玩体验均未验证。本轮测试不作为剧情或游玩通关证据。

两份测试修改和本目录证据交由主代理合并提交；验证 Agent 没有提交或推送。
