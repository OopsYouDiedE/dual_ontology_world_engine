# F02 检测型集成门修正交付

交付代码：[hardened_scope_guard.py](hardened_scope_guard.py)。原 provided-pack 守卫及模板保持字节不变。完整实验、逐项原始证据和门结论见 [F02-report.md](F02-report.md) 与 [gate-decision.json](gate-decision.json)。

最终代码 SHA256：`ac6a34ef73bf70d5b4bb2b4bb8ded5cf0441c3184021f567402f46cfc9cbc6c1`。

## 已实现的检测修正

| 原版缺口 | 修正 |
| --- | --- |
| 越域中间提交后恢复空净补丁 | 每次 base..frozen HEAD 提交分别审计 paths/operations；禁止 merge。 |
| 暂存区与工作树互相遮蔽 | HEAD/index 与 index/原始工作树分别比较；始终要求可集成的 clean 状态。 |
| ignored/untracked 漏检 | 不依赖 Git ignore/status；全量扫描实际文件、目录及空目录。 |
| 保护路径大小写 | protected/grants/build dirs casefold；拒绝重复、碰撞或授权冲突。 |
| 文件系统链接与 Windows junction | 仓库祖先、工作树和 Git metadata 拒绝 symlink/junction/reparse。 |
| Windows 普通物化文件遮蔽 Git symlink | base、每次提交及 index 模式检查，拒绝 120000、160000 和非普通 blob。 |
| skip-worktree / assume-unchanged | 拒绝隐藏 index 标记，再用 blob 原始字节哈希核对实际文件。 |
| metadata exclude / 配置视图干扰 | 拒绝活跃 .git/info/exclude 与 info/attributes、共享/alternate/graft/replace metadata；屏蔽 Git 环境和全局配置，拒绝可执行/间接配置。 |
| 错误保护 glob 静默接受 | 所有 protected 模式均先验证相对路径段，再匹配。 |
| 对象库篡改 / 被观察到的并发变化 | full strict fsck；前后重复观察 tip/index/磁盘/metadata 并绑定快照哈希。 |

控制器可以在锁的 `build_output_dirs` 中显式列出精确相对构建产物目录；例外仅允许未跟踪内容，不能豁免 tracked/protected/link/reparse。没有字段即没有例外；目录不能带 wildcard 或路径跳转。

## 运行接口与前提

```powershell
python hardened_scope_guard.py --repo <独立clone目录> --lock <scope-lock.v1锁> --head <冻结完整SHA> --json
```

返回码 0：观测到的补丁范围和原始文件快照检测通过；1：观测到范围/clean 问题；2：前置条件、锁、mode、metadata 或运行错误拒绝。

工具接受普通独立 clone 的 .git 目录，拒绝 linked/shared worktree；tracked 文件须 raw-byte-clean，禁用换行转换和内容过滤。严格前提造成的拒绝应通过控制器准备合适 clone 解决，不应让 worker 改工具或锁来自行放宽。

最终自检 [run-hardened-20261010b/results.json](run-hardened-20261010b/results.json)：34/34 符合封存预期。实际 filesystem symlink 创建受 WinError 1314 限制，未执行；实际 junction 和 index mode 120000 均已检验。此处是实现自检，另一全新 Agent 的独立复验仍须单独记录。

## 没有声称解决的独立限制

共享宿主凭据和 unrestricted tools 下，本实现只有 **DETECTION_ONLY**。无法限制 worker 的读取或首次越域写入，也无法认证一个写着 SIGNED 的锁、批准引用或 merge 身份。检测还不能冻结并发 actor 或阻止检查返回后再修改文件。

所有输出明确列出 read/write ACL 为 NOT_ENFORCED，锁/审批/merge 身份与语义审核为 NOT_VERIFIED，`production_write_authorized:false`。该字段表示工具本身没有签发权限，不是否定控制器在用户允许的独立 clone + 检测型退化路线中作出独立决定。

独立复验通过后，具体迁移还须满足任务锁、被冻结的实际补丁范围、语义审核，以及控制器在可信集成环境重新核对并决定是否集成。不能把本轮中性 fixture 检测结果当生产补丁通过，也不能把没有 ACL 笼统解释为所有迁移永久不可行。
