# F02 字面方括号路径修正

本报告是当前 `hardened_scope_guard.py` 的修正版记录；此前 [F02-report.md](F02-report.md)、[hardening-report.md](hardening-report.md)、`gate-decision.json` 及 `run-hardened-20261010b/` 保留为旧 SHA 阶段快照，不覆盖旧验收记录。R01-M01 的独立报告未读取、未修改；新版独立复验由主控安排。

最终冻结 SHA256：`cf81c1bcb5c59f7cbea9d000bb1a6ca2fe7283e596c4bb0c60c1d77d8dfd017e`。

## 修正与边界

独立复验指出合法的字面方括号文件名会被 `normalized_path` 当作 wildcard 拒绝。本轮只修改 F02 自身守卫中的一个字符集合表达式：

```diff
-    if not pattern and any(any(char in piece for char in '*?[]') for piece in pieces):
+    if not pattern and any(any(char in piece for char in '*?<>"|') for piece in pieces):
```

精确路径允许 Windows 合法的字面 `[`、`]`，继续拒绝 `*`、`?`，明确拒绝 `<`、`>`、双引号和 `|`；冒号、路径跳转、反斜杠、控制字符、尾随点/空格和设备名的原有检查保留。`allowed_writes` 仍以 casefold 后的字面路径字典查询，不对 grants 执行 glob。保护模式的匹配逻辑没有改动。

修正前守卫与修正后守卫逐字节核对，仅有上述单处替换。原 provided-pack 守卫及模板 SHA256 未变；未修改任何已有锁、生产文件或真正仓库 refs。测试锁均是新的中性 fixture 锁。

## 本轮证据

全部测试先写入预期再运行，均为 **IMPLEMENTATION_SMOKE_ONLY**，不替代全新 Agent 独立复验。

| 测试 | 预期 | 实际 | 原始证据 |
| --- | --- | --- | --- |
| 修正前 B01：修改 `allowed/neutral[value].txt`，授予该字面路径 modify | PASS | DENY / 2，wildcard in exact path；原始假阳性保留 | [run-bracket-before-20261010/results.json](run-bracket-before-20261010/results.json)、[原始字节](run-bracket-before-20261010/raw.zip)，命令 0013 |
| 修正后 B01：相同中性正例 | PASS | PASS / 0 | [run-bracket-after-20261010/results.json](run-bracket-after-20261010/results.json)、[原始字节](run-bracket-after-20261010/raw.zip)，命令 0013 |
| 修正后 B02：只授予 `neutral[value].txt`，实际修改 `neutralv.txt` | DENY | DENY / 1；包含 `ungranted modify: allowed/neutralv.txt`，不是前置验证失败 | 同上，命令 0026 |
| 原有 34 项整体自检重跑 | 34 项各自预封存预期 | 34/34 符合 | [run-hardened-20261010c/results.json](run-hardened-20261010c/results.json)、[完整命令账本](run-hardened-20261010c/commands.jsonl)、[原始字节](run-hardened-20261010c/raw.zip) |
| 路径验证器约束保留 | 14 项允许/拒绝预期 | 14/14 符合 | [run-bracket-validator-20261010/results.json](run-bracket-validator-20261010/results.json)、[命令及返回码](run-bracket-validator-20261010/commands.json)、原始 stdout/stderr 同目录 |

路径验证器自检覆盖字面方括号正例，以及 `*`、`?`、冒号、`../`、控制字符、`<`、`>`、双引号、`|`、反斜杠、尾随点和 NUL 设备名的拒绝。原有 34 项仍包含逐提交、index/工作树、ignored/untracked、大小写、Git mode、junction、隐藏 index flags 与构建目录例外。

修正前快照 [run-bracket-before-20261010/guard-under-test.py](run-bracket-before-20261010/guard-under-test.py) 的 SHA256 为 `ac6a34ef73bf70d5b4bb2b4bb8ded5cf0441c3184021f567402f46cfc9cbc6c1`。修正后中性回归与整体自检的 `guard-under-test.py` 均与最终交付代码字节一致。ZIP 成员已按 SHA256 和原始文件核对，预封存预期哈希保持不变。

汇总完整性检查见 [bracket-fix-verification.json](bracket-fix-verification.json)。可重复执行新的中性回归：

```powershell
python bracket_regression.py --phase after --run-name run-bracket-<新的名字>
python check_hardened_guard.py run-hardened-<新的名字>
```

脚本拒绝覆盖旧 run 目录。旧 `--phase before` 运行记录保留在修正前快照目录；不能对当前修正源码使用该模式来生成或冒充旧失败。

## 门结论

本轮修正实现及自检完成；当前新 SHA 的独立复验待另一全新 Agent 执行，旧 R01 结论不能自动移作本版本的通过证明。分类继续为 **DETECTION_ONLY**；读取 ACL、自签锁真实性、审批真实性、可信 merge 身份与不可变评审快照的外部限制保持不变。没有生产写入，本工具仍不签发生产权限。
