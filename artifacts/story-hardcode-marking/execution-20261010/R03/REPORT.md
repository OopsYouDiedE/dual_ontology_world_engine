# R03 黄金与预期隔离首批盘点

结论：首批元数据盘点完成，状态为 `DETECTION_ONLY`。生产写入 0，游戏/Tests 运行 0，旧预期改动 0，新 golden 生成/采样/审批 0。此报告不确认操作系统级保密，不确认预期隔离已实现，也不把固定路线列为 V02 证据。

基线 HEAD：`7489db56a714724eb7cb735958dd879dc662d238`。主工作区存在并发实施；扫描使用的源字节逐文件绑定到 `source_snapshot_sha256`。与 HEAD 不同的已观察文件为 `godot_csharp/tools/PlayerTranscript/Program.cs` 和 `godot_csharp/tools/check/test_flat_world_independent_01.py`。R03 未写这些源文件。行号应结合扫描哈希使用，不声称它们全部等于 HEAD。

## 范围与证据

- `issue-map.json` 仅投影 S17/S18 的编号、路径、类型、范围和定位字段，共 37 条；未导出 snippet、设计依据、方案、结论或答案字段。
- 仅通过 Git tracked 文件名索引、22 个 csproj 的编译/引用入口、3 个 README 的入口命令和 `tools/verify.py` 入口代码确定测试面。
- 对 51 个允许的测试/工具源文件执行结构标识搜索，仅保存类别和行号；不保存代码片段、字面量参数、具体状态或断言答案。结构匹配可包含诊断信息，均按候选项记录。
- `expectation-inventory.json` 保存细项；`hash-manifest.json` 保存 SHA-256 和 HEAD 字节对比；`read-scope.json` 保存读取/写入边界。实际或疑似预期文件仅列路径与哈希，不复制内容。

## 固定路线与控制器

| 对象 | 位置证据 | 判断与使用边界 |
| --- | --- | --- |
| 统一自动回归 | `tools/verify.py:16–17`，入口配置与 `env/cwd` 在 108、110 行 | 文件自身明确为注入状态自动检查。可作机制回归，不能作 V02 自治或独立游玩证据。 |
| PlayerTranscript | S17：CSH-011/012/013；固定字面量动作与比较结构见 inventory 的当前字节行号 | 确定性固定路线的记录生成工具。保留作回归/回放输入；不作为自治控制器或 V02。 |
| Awakening/Chapter2 flow 与 fixture | S17 的 AWK-053、CH2-016/017/018/019、CH2-020；fixture、固定动作、断言类别逐行列入 inventory | 注入状态与固定检查路径。保留既有断言；未读取或重建其答案。 |
| DetectiveSimulation | S17：AWK-051/052；可见选项与随机/评分结构，同时存在字面量动作/筛选 | 混合控制器候选。出现随机或可见选项不证明自治；未取得 V02 资格。 |
| chapter_autoplay | `godot_csharp/scenes/Shared/check/chapter_autoplay.gd` 的视图/选项结构 | 视图驱动自动推进候选，未确认无具体内容筛选、独立会话或预期访问隔离；未取得 V02 资格。 |
| UI capture/replay 工具 | S18：CSH-024/025/026/027/006 | 渲染采集或记录回放用途；不当作玩家控制器或真实游玩证据。 |

## 预期文件与缺失控制

Git tracked 文件名索引未定位到独立、明确命名的游戏 golden/oracle/expected 文件。内嵌断言与 expected 集合候选分布于 32 个文件，详见 inventory；只保留路径、候选行号和源哈希。审核者需使用单独授权的预期审阅上下文；V00/V02 或自动玩家上下文不得接收这些内容。

`artifacts/story-hardcode-marking/baseline.json`、历史审计 fixture 与日志仅按文件名记为审计基线候选，并做 HASH_ONLY；未读取内容，未把它们认定为游戏黄金样本。正式三份记录和两份回放 JSONL 的现行引用路径，在本次路径检查中均不存在；未生成替代文件，也未将旧证据当成当前预期。

| 控制 | 本次观察 | 结论 |
| --- | --- | --- |
| 上下文继承 | 未在允许入口中定位到预期 denylist 或子上下文过滤执行机制 | 未验证。独立 Agent 的授权输入边界需要另行保证；本盘点不能证明不可读取。 |
| 进程环境继承 | `tools/verify.py:108` 拷贝现有环境，110 行使用仓库 cwd | 观察到共享环境/目录，未发现操作系统级访问隔离证据。 |
| 并发控制 | 96、141 行使用时间戳/PID 原始日志目录；39 行仍为共享 gallery 路径；相关测试引用 `user://` 状态 | 日志目录减少碰撞，未在入口定位到 mutex/文件锁/隔离控制；本轮并发源变动另有哈希记录。 |
| 黄金更新审批 | 允许的文件名索引、入口说明和 verifier 未定位到 golden 更新及审批记录 | 仅能报“授权范围内未定位”；不推断仓库其他位置不存在。未采样、替换或批准新 golden。 |

## 验收边界

本轮为只读盘点，不执行一/二/三级验收。构建、机制结果、人物行为合理性与游玩体验均未验证。R03 未修改工作流程/架构或生产源码，不新增 AGENTS 内容。主代理可使用这些元数据安排独立预期审阅；不能向 V00/V02 或自动玩家转发预期内容。


## 换行与初始哈希核对更正

本节更正并覆盖上文“并发实施／并发源变动”的推断。原始 raw SHA 与 HEAD blob 的差异仍保存在哈希清单，不能据此推断源码被修改。本次检查发现两处差异均来自既有换行字节；没有观察到源内容变动。

- 51 个扫描源文件的当前 raw SHA-256 全部等于 `execution-20261010/baseline.json` 的初始 raw 哈希，缺失记录 0，差异 0。
- 51 个扫描源文件经 CRLF→LF 规范化后的 SHA-256 全部等于 HEAD blob，差异 0。
- `godot_csharp/tools/PlayerTranscript/Program.cs` 有 446 个 CRLF；`godot_csharp/tools/check/test_flat_world_independent_01.py` 有 1 个 CRLF、34 个 LF。两者 Git 属性均为 `text=auto`、`eol=lf`，`working-tree-encoding` 未指定。
- 两者 `git hash-object --path` 的规范化 blob 都等于 HEAD，Git diff 都为空。因此此前“可能来自并发实施”的解释已撤回；R03 的读取行号对应初始工作区内容。

本轮仍只有 R03 四份盘点产物写入，生产写入 0。`DETECTION_ONLY`、固定路线不可作为 V02、未验证操作系统级预期隔离的边界保持有效。
