# 最终独立自动回归

日期：2026-10-05。测试在全新独立 Agent 上下文进行；未读取 docs/、项目设计文件、历史测试或审查报告，未修改正式源码。仅执行指定命令并保存本轮日志。

这轮是自动回归，**不是盲玩**。流程断言和 120 帧默认场景运行不能证明实际玩家体验、有效游玩时长或推理质量。

## 结果

| 项目 | 结果 | 退出码 | 墙钟时长 |
| --- | --- | --- | --- |
| dotnet build | 成功，0 警告、0 错误 | 0 | 1.523 秒 |
| Godot headless import | 成功，无 stderr | 0 | 5.990 秒 |
| check_flow.gd | 流程断言失败 0；存在退出清理错误 | 0 | 5.933 秒 |
| 默认场景 120 帧 | 启动和指定运行完成；存在退出清理错误 | 0 | 1.759 秒 |

所有命令均设置 60 秒硬超时，均在限时内退出。运行监控每 200ms 检查 stderr，遇 SCRIPT ERROR、Parse Error 或 ERROR 将终止进程树；本轮 ERROR 发生在退出清理，进程已结束，随即报告主代理。

check_flow 输出确认 17 个区域载入与出生点可走，动态对白可编译，列出的剧本标题通过；第 2 日到夜、第 3 日清晨结算、重演步子吻合、对账及第 4 日议事的脚本断言均通过，脚本总结为“用时 4431 ms；失败 0”。

## 未通过的清洁退出检查

check_flow stderr：

```text
WARNING: ObjectDB instances leaked at exit (run with --verbose for details).
ERROR: 4 resources still in use at exit (run with --verbose for details).
```

默认场景 stderr：

```text
WARNING: ObjectDB instances leaked at exit (run with --verbose for details).
ERROR: 1 resources still in use at exit (run with --verbose for details).
```

因此可记录构建、导入、自动流程断言和默认场景启动通过，但不能记录为全程无错误通过。此报告不推断泄漏对象归属或错误是否为既有问题。

## 实际命令与日志

Godot：`C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe`。

```text
dotnet build godot_csharp/DualOntologyWorldEngine.sln
Godot --headless --path godot_csharp --editor --import
Godot --headless --path godot_csharp --script res://scenes/Chapter2/Game/check_flow.gd
Godot --headless --path godot_csharp --quit-after 120
```

日志目录：`tmp/claude-unify-merge/runtime-final/`。各命令分别保留 `build`、`import`、`check-flow`、`default-scene` 前缀的 `.stdout.log`、`.stderr.log`、`.result.json`，运行器为 `run-command.ps1`。
