# 《安眠者有福》重构评审及多Agent控制包

**状态：仅为实施方案和示例工具；没有修改游戏仓库。** 主要资料来自用户提供的2026-10-09 `solution-review(1).md`（静态审查版本`b608f5b6`）和 Astra 评议 `txt.txt`，本包没有原项目的最新源码与完整293条独立证据清单。

文件说明：

- `solution-review.md`：**主文件**。21组裁决、游戏引擎分层、权限、42张任务定义、分支/clone/worktree流程、审批和验收。
- `subagent-task-manifest.json`：待验证任务DAG、S组映射、候选读取/写入范围。所有`active_write_paths`为空，不授予权限。
- `agent-scope-guard.py`：示范性质的Git补丁文件范围校验器；需要可信控制器提供不可由Agent修改的scope锁。脚本不是操作系统权限沙箱，不负责语义审核和审批真实性。
- `scope-lock.example.json`：**故意无效**的控制锁模板；填入当前HEAD/分支/精确文件并由控制器签发后才可使用。
- `task-card.example.md`：任务M01的下发模板。
- `build_manifest.py`：从定义重建任务清单的脚本；仅由可信控制器使用。

建议启动方式：Sol XHigh 主Agent先执行F00、F01、F02（审计、能力盘点、隔离控制）。在没有真实权限保护时只执行只读研究；等控制层建好后按任务卡发布分支/任务。**不要**因 manifest 中出现候选写入路径就开工，也不要让 sub-agent 修改manifest来批准自己。

使用范围校验器的示例命令（参数应来自可信控制器；`scope-lock.example.json`原样运行必然拒绝）：

```bash
python agent-scope-guard.py --repo /absolute/worker/clone --lock /protected/M01.scope.lock.json --require-clean --head <frozen_full_commit_SHA>
```

对照在一个临时 Git 仓库进行的基础烟测：合法已提交文件修改通过，保护文件修改拒绝，越域新建文件拒绝，未提交改动在`--require-clean`下拒绝，错误任务分支拒绝。该烟测不是对正式代码/外部权限机制的保证。
