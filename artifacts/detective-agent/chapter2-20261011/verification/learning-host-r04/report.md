# Core 决策经历迁移的独立宿主验证

日期：2026-10-11。执行者：全新独立验证上下文 `verify_learning_host`。

本轮宿主构建、控制器、策略集成、控制器编译、跨进程保存及两章流程自动回归通过。验证只读取维护约束、相关源码和中性夹具；未读取设计规范、正式 `.story` 正文、历史试玩记录或其他报告。正式编译内容仅由既有回归入口加载；最后通过编译器只读检查生成物一致性。

## 本轮证据

| 入口 | 最终结论 | 本轮证据 |
| --- | --- | --- |
| 宿主构建 | 通过 | `host-build-attempt-05/` |
| PlayerControllerCheck，含 RouteLearningCases | 通过 | `player-controller-attempt-03/` |
| StrategyIntegrationCheck，含新增中性经历夹具 | 通过 | `strategy-integration-attempt-05/` |
| 控制器策略编译合同 | 通过 | `strategy-compiler-attempt-01/` |
| Godot 跨进程空闲保存 | 通过 | `save-attempt-02/`，保存产物原始字节在 `saved-state/` |
| 第一章 flow | 完整通过 | `flow-attempt-01/` |
| 第二章 flow2 | 完整通过 | `flow2-attempt-01/` |
| `compile_story.py --check` | 生成物一致，未生成或修改内容 | `story-consistency-attempt-01/` |

Godot 步骤沿用 `tools/verify.py` 的完整结束记录与脚本/引擎错误扫描。上述实际 Godot 运行均有成功结束记录，未触发完成后的退出崩溃或缺失结束记录重试；没有把局部 PASS 当作完成。

最终宿主构建、控制器及策略集成运行的源码前后哈希一致。flow2 期间唯一哈希变化是本目录的 `run_step.py` 增补证据哈希范围；该调整不改变命令或通过判定，生产及测试源码未变。最终源哈希见 `final-source-sha256.json`；证据文件字节哈希见 `evidence-sha256.json`。各阶段保留独立源快照，不覆盖首次失败。

## 机制结论

- U 形路线仍选择公开的相关路标，并保留 `follow_observed_route_to_needed_place` 和目标的 `expected_information_at_remembered_goal`。路标不叠加实际检视、普通地面新颖性或直线方向奖励。到达后优先交互；变更路标不会刷新目标的新颖性；目标检视耗尽后，路标不会重新开启调查奖励；新采信材料仍可重新开启相关交互和返程。
- 中性 WorldSession 先真实执行不可触及物件的 Take，并收到失败回执。该主体随后按经历改选可行替代方法，替代方法实际完成；另一主体没有获得这份经历。普通空闲保存恢复后，失败经历和方法评分一致，恢复会话继续选择同样的替代方法。
- 中性 WorldSession 终局真实取消待执行动作，记录中断而不记成功，清空宿主 `_executions` 和活动动作，未提交取消动作的物理效果。清理输出后保存切点可用，恢复保留中断经历；重复终局不重复反馈。
- 中性晨间日常夹具从 Index=0 起步，按评分选择较高权重的另一候选。真实 DailyBlock 反馈使下一次选择改为替代候选，并保留无进展历史。尚未进入执行的计划被 DailySuspend 后记录中断。departure 的顺序门控仍优先当前步骤，即使后续步骤权重更高。
- 正式宿主 StrategyAction 枚举开始时写入尝试；在实际枚举被 Dispose 时写入中断，没有伪记成功。经历读取使用公开 `ActionLearning.Capture` 合并各方法/情境的独立认知观察。
- 既有控制器公开令牌边界、角色预算、主体隔离、策略来源/亲见边界、当前可行性与冷却合同保留；默认集成入口仍使用真实 Python 编译器编译中性 `controller-wire.story` 后经 C# 读取评分。

新增夹具位于 `godot_csharp/scenes/Shared/check/StrategyIntegrationCheck/LearningHostCases.cs`，接入既有集成入口；另只更新 PlayerControllerCheck 的 U 路线合同断言。验证 Agent 未修改生产代码或预置章节答案。

## 首次失败与修正记录

| 快照 | 原因与处理 |
| --- | --- |
| `host-build-attempt-01/` | GameLoop 的 double 分数传给 int 边界导致编译失败。主代理修正通用 `ShouldReconsider` 类型边界，后续构建通过。 |
| `player-controller-attempt-01/` | 验证夹具误把目标预期信息价值当作路标直接调查奖励。实际 U 路 token 正确；按主代理明确的合同保留预期目标价值，并补充禁止直接调查及其他奖励叠加，未放宽路线选择或目标耗尽行为。后续回归通过。 |
| `host-build-attempt-04/` | 第二章取消接线访问 ActionExecution 未公开的 Soul，编译失败。主代理改为使用 People 的实际主体，后续构建通过。 |
| `save-attempt-01/` | PATH 无法发现可运行的 Godot .NET 控制台，实际保存检查尚未启动。保留启动前源哈希与工具响应文本；该文本明确不冒充原始子进程通道字节。改用现有控制台程序的完整路径后实际保存回归通过。 |

实际构建及回归的 stdout/stderr 保留原始字节，局部 `.gitattributes` 禁用日志与保存产物的换行转换。中性失败/取消保存字节分别封存在策略集成阶段目录，供复查恢复经历。

## 验收边界与退出状态

本轮属于自动机制回归。一级全程剧情/UI 对齐、二级渲染 UI 专项及三级真实游玩均未执行，不能以这些回归替代。人物行为是否符合设计、文学质量、游戏呈现和游玩体验未验证；独立验证上下文没有读取设计依据，不对它们作结论。

最后回归结束时仅保留用户编辑器 PID 23084，任务 Godot 均已退出；已通知主代理可以唯一恢复 FullTest。本 Agent 未运行 FullTest、未提交或推送，也不会在释放后另开 Godot。
