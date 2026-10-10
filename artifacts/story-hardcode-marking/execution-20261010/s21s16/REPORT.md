# s21s16 执行报告

## 改动

- S21：续篇新增 `00_通行.story`，声明稳定边界/通路 ID、初始开关、出发地、目的地、可见范围、初始到场位置及召集消息。开放时点继续由已有剧本流程声明；编译器校验身份与流程引用。
- 边界开关经 Core `Effect.Set` 提交，实际连接从声明与权威边界状态派生；删除写死的宿舍释放集合和走廊分组。
- `HostDormOut` 只开门。原来依赖人物到场的流程改为邀约，由既有社交评估、自主决策和 Travel 提交产生移动；个人已接受的禁止规则可以阻止应邀出行。
- 通路认知只在当地可见复查、收到明确消息或实际 Travel 失败时更新；远处人物保留旧认知。Core `FailedEffect` 提供当前失败效果的直接引用，避免把尚未执行的 Travel 当作失败尝试。
- 夜间快照用单一 `AccessStates` 按边界 ID 保存开关，另存各人的通路认知，避免多道边界覆盖同一房间时互相覆盖状态。
- S16：规则与场面触发器支持 `world/chapter/interaction` 有效期；元数据随认知冻结、重绑定及修订保留，与实体/候选 scope 区分。
- 场面选择入口同样拒绝过期分支，拒绝时不消费其他仍有效的选项。
- 退出只移除匹配有效期和 owner 的规则，同时撤销同范围实体规则源；持久习惯、接受状态、记忆、欲望和对象引用保留。旧物场景习惯在 `.story` 声明为本章局部，不再统一清空习惯投影。
- 连续世界移交检查所有已有人物的未完成动作；重复移交返回原活世界。重复加载继续明确拒绝，拒绝前不撤销规则、不改状态；重复结束不追加效果。
- 移除 `CH2-009`、`AWK-060` 标记；同步模块 AGENTS 架构和验证命令。

## 验证

全新独立 Agent 执行自动检查；没有读取 `docs/design-standard.md`，没有更改既有测试预期。

| 检查 | 结果 |
| --- | --- |
| 剧本临时生成后 `compile_story.py --check` | 7 个输出一致 |
| 编译合同 `test_access_lifetimes.py` | 6 项通过 |
| ScopeRegression Release 构建/运行 | 构建通过；27 项通过 |
| CognitionRegression | 202 项通过 |
| AccessLifetimeCheck | 最终代码 10 项通过；Release 构建 0 警告/错误 |
| 宿主 `-m:1 --disable-build-servers` | 完整回归与最终代码重建均通过；0 错误、2 条 NU1900 警告 |
| `verify.py --skip gallery --keep-going` | 退出码 0；core/scope/cognition/host/flow/flow2 全通过，gallery 按要求跳过 |
| 最后快照/分支修正 `--only host flow2` | 退出码 0；host 通过，flow2 109/109，无 Godot ERROR/泄漏 |

定向检查覆盖：更换边界 ID 与分组、限制通路起点、远处旧认知、实际失败后更新、前置条件失败不冒充走路、开门不搬人、应邀实际到场、个人规则拒绝邀约、多道边界与旧认知恢复、混合有效期规则保留、未完成动作移交拒绝、重复退出/加载、触发器和选项分支到期。

初轮统一回归退出码 1：flow 136/136、flow2 109/109 游戏断言通过，但两项各记录一次 `Failed to read the root certificate store`，验证器据此判失败。未过滤 ERROR。验证环境临时使用已安装 certifi 的公共根证书 PEM，按 [Godot 官方证书配置](https://docs.godotengine.org/en/4.6/tutorials/networking/ssl_certificates.html) 替代系统证书库读取；TLS 验证保持开启，短启动检查退出 0 且无 ERROR。原失败日志保留。

宿主 NU1900 警告来自沙箱无法访问 NuGet 漏洞数据服务，不是编译错误。

完整回归最终记录 flow 136/136、flow2 109/109，无 Godot ERROR、脚本错误或资源泄漏。此完整回归在最后快照/分支修正之前构建；其后仅第二章发生改动，已由新独立 Agent 重建最终宿主并复测 flow2，全通过。

原始日志：`tmp/s21s16-validation/`（首次环境失败）、`tmp/s21s16-final-validation/`（完整回归）、`tmp/s21s16-completion-validation/`（最终代码定向及受影响模块复测）。统一入口的完整回归原始通道目录为 `artifacts/project-audit/verify-logs/20261010-025552-487678-30484/`，最终 host/flow2 为 `20261010-030524-235299-24136/`。

## 交付与限制

- 本轮只做自动机制回归；一级三份全程记录、二级 UI 专项、三级盲玩均未执行。人物的认知与行动机制有定向证据；文学、最终人物行为合理性和游玩体验未评判。
- 未修改 `godot_csharp/scenes/UI/`、布局/样式资源，未提交或推送。
- 为验证暂时生成过正式编译物；已恢复其原始字节，正式生成仍由主代理负责。主代理合入源改动后须先运行 `python godot_csharp/tools/compile_story.py`，再运行 `--check` 和构建；交付时生成物尚未反映新声明。
- 验证用 `godot_csharp/override.cfg` 已移除；证书包和原始日志在 `tmp/` 留作本机排查，未交付环境配置。
- 召集仍经人物决策，拒绝或尚在执行任务的人不会被强制移动；当前自动机制检查不代替真实现场行走和 UI 呈现验收。
