# R01 范围门独立复验 round02

结论：冻结版范围门、中性回归和两个实际独立 clone 的范围检查均通过。可将下列冻结提交提交给控制器的最终集成流程；本结论只提供范围检查通过的证据，不签发生产写权限。

## 冻结输入

| 输入 | 冻结值 |
| --- | --- |
| guard SHA256 | `cf81c1bcb5c59f7cbea9d000bb1a6ca2fe7283e596c4bb0c60c1d77d8dfd017e` |
| 共同 base | `7489db56a714724eb7cb735958dd879dc662d238` |
| M01 branch | `agent/M01-remove-old-case-run01` |
| M01 tip | `249471e53a8c1208fc80e98835ea0d0ce6e15e00` |
| M01 lock rev4 SHA256 | `43e9d5ac1c69a0b4c0a5d40281bee93f45ff099e35f1029c7130cb92717a9b9e` |
| H01a branch | `agent/H01a-line-provenance-run01` |
| H01a tip | `dc63af04213ac0d418b1b598bcd9e025d657eafe` |
| H01a lock rev2 SHA256 | `ac81be3f6169690d9e72f7ae62907a272e0d0906a61a2d5a4d7a5a5d290cead3` |

开始前、各实际运行开始时和完成后核对 guard；值一致。实际运行采用封存的 guard 与锁字节，运行后再次确认控制器锁字节未变。未修改 worker clone、控制器锁、生产代码或旧测试。

## 独立复验结果

1. 旧34项预期分类全部匹配，`34/34`，不匹配数0。使用现有 `check_hardened_guard.py` 的用例枚举与预期，将证据输出重定向到本轮新目录；原 fixture 未改。期望文件先保存并哈希，完成后复查未变。`25_self_signed_lock` 和 `29_read_not_enforced` 仍按预期 PASS，分别证明锁身份认证和读权限不由范围门实现。
2. 新建两组仅含中性 ASCII 字节的独立仓库。精确 grant `allowed/[v].txt` 修改真实同名字面路径时 PASS，记录中确实出现该路径的 modify；同一 grant 下创建 `allowed/v.txt` 时 DENY，明确报告 `ungranted create`。`[]` 合法字面路径接受，grant 不扩展成 glob。期望先保存并哈希。指定位置没有 `check_bracket_guard.py`，因此本轮使用独立补充用例。
3. 实际 M01 和 H01a clone 均 origin 已移除、非 shallow、152条历史、共同 base 是冻结 tip 的祖先、missing objects=0、严格完整 fsck 通过。两个普通 `.git` 目录的37/35个文件不存在共享文件标识或硬链接；没有修改 clone。
4. 实际最终范围门均退出0、decision=PASS、problems为空，每个 worker 审核1个 post-base 提交。M01 观察4164文件，213个精确声明的生成文件；H01a 观察4271文件，318个精确声明的生成文件。各自8个 `godot_csharp/tools/__pycache__/` 文件按控制器冻结锁声明处理。旧34中的受保护生成目录与 tracked 生成目录反例仍 DENY，声明目录没有豁免受保护路径或 tracked 内容。
5. 逐项核对实际 diff 的完整路径、操作、普通文件模式和对象 ID。M01 仅创建自己的报告、修改 `csharp/AGENTS.md`、删除 `csharp/HallCausalityRules.cs` 与 `csharp/HallReplayProgram.cs`；H01a 仅创建自己的报告、修改 `godot_csharp/scenes/Shared/Story/WorldBehavior.cs`。全部符合锁的精确授权；受保护路径和旧测试变化均为0。没有显示或审阅被删文件的具体内容。范围门同时审核所有 post-base 提交、index、tracked/untracked/ignored 文件，起止观察无变化。

| 实际 gate | snapshot SHA256 |
| --- | --- |
| M01 | `02f03abee90cfa1889c338190f8dfe5fd7f1acb89f8d6353b554e467125134fd` |
| H01a | `d2e353ab6f3a8d575164d35001d19b2ab17d0d416572df00b0f8124ebb301fbf` |

## 证据与完整性

- [旧34结果](old34/results.json)、[运行前预期](old34/expectations-before-run.json)、[逐命令记录](old34/commands.jsonl) 与 `old34/raw/` 保留所有 Git/guard 原始 stdout/stderr 字节和对应 SHA256。
- [方括号结果](bracket-results.json)、[运行前预期](bracket-expectations-before-run.json) 和 [中性仓库归档完整性](neutral-archive-integrity.json)。`neutral-repos.zip` 保存65个中性 fixture 文件，包括完整 Git 元数据；逐文件读回归档核对 SHA256。残留 `neutralrepos/` 仅由本目录 `.gitignore` 忽略，未递归删除。
- [M01实际结果](M01-actual-results.json)、[H01a实际结果](H01a-actual-results.json)、[clone独立性](clone-independence-results.json)、[逐命令记录](commands.jsonl) 与 `raw/` 保留最终范围门和前置核查的完整输出。原始字节用局部 `.gitattributes` 的 `-text -diff` 保护。
- `frozen-hardened_scope_guard.py`、`frozen-check_hardened_guard.py`、`frozen-M01.scope.lock.json`、`frozen-H01a.scope.lock.json` 保存本轮输入字节。[证据完整性清单](evidence-integrity.json) 对本轮交付文件逐项记录字节数与 SHA256；中性残留工作目录由已经验证的归档单独覆盖。

本轮没有查阅设计、剧本、谜底、黄金内容或旧试玩。实际 gate 对文件和 Git blob 只做原始字节哈希处理，没有把内容作为验证判断依据。

## 可集成建议与外部限制

从本轮范围证据可接受上述两个冻结提交进入集成步骤。控制器应使用完整 tip SHA，并以封存锁、guard、snapshot 和其他独立审核证据核对最终合并身份；任何 tip、锁、工作副本或合并对象变化都需要重新核对。

范围门为 `DETECTION_ONLY`。OS 读/写 ACL、用户/审批身份、锁身份认证、语义审核、最终合并身份和持续不可变快照均不由它落实；运行后的写入及观察间未被观察到的竞态属于外部隔离限制。`production_write_authorized=false`。本轮实际锁哈希匹配来自控制器给定的冻结值，不构成范围门内的身份认证。

本轮机制结论仅为范围门 PASS。构建与 Core 测试按任务要求不重复，未在本轮验证；人物行为合理性、剧情和游玩体验未验证。一级、二级、三级游戏验收均未执行，本范围结论不代替任何游戏验收级别。失败项0；上述外部门继续由控制器承担。
