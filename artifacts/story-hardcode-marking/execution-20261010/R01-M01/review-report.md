# R01-M01 独立审核

## 当前结论

M01 的原始差异与构建/机制回归通过。F02 加固范围门的独立中性验收通过（23/23），冻结官方 smoke 复跑通过（34/34），另补充的一项缺失历史对象试验也正确拒绝。**F02 本轮整体验收未通过**：补足对象后的正式 M01 范围门 0412 仍 DENY，原因是脚本把基线中合法字体文件名的方括号误判为通配符。正式集成应等待 F02 修复与新一轮独立复验，不能用 smoke 通过替代真实仓库的门结果。

本卡分别验收两个维度：M01 删除范围及可构建性；F02 的检测能力与外部权限边界。检测门通过不代表 OS 读写权限或签名认证已实施。

## 独立性与冻结输入

- 全新独立 Agent 上下文，只读取获准范围：控制锁、冻结 guard/smoke、M01 Git 元数据与全部原始 diff、维护/包 README、项目与解决方案文件，以及旧类名的源码引用索引。未读取设计文件、`.story`、谜底、黄金资料或历史试玩。
- 基线：`7489db56a714724eb7cb735958dd879dc662d238`。
- 冻结实施 tip：`249471e53a8c1208fc80e98835ea0d0ce6e15e00`；分支 `agent/M01-remove-old-case-run01`。
- Guard SHA256：`ac6a34ef73bf70d5b4bb2b4bb8ded5cf0441c3184021f567402f46cfc9cbc6c1`。
- Smoke SHA256：`d7ad05c1e6cdec8f5be03760ea745576b6fad3b923e81cd7e8bb0a7e0cea59fd`。
- 执行前安全预期封存于 `expectations-before-run.json`，SHA256 `29e777261dcc63a50c057fde2ac9b6b9f9d9952423f766cb0478eb3884a2b141`。执行后核对未变。
- 实际正式执行封存的初始锁已是控制器 rev2，SHA256 `aa56371a288aaa52a7fbc1a013c6374ca4d5eb960f8b6f2e2e71604f9fa46564`。初次只读检查曾发现旧锁的 whole-host/build 冲突，但控制器在封存之前已更新；不能把预期原因写成旧锁的正式实测拒绝。0012、0018、0019 三次实际拒绝首因均为 shallow。
- 命令原始 stdout/stderr 为 `raw/*.bin`，命令、时间、退出码、输出哈希见 `commands.jsonl`。环境见 `environment.json`、0001/0002。Windows x64，.NET SDK 8.0.400、MSBuild 17.11.3、运行时 8.0.8。

## M01 差异与回归

原始 Git diff 仅有以下四项，只有一个基线后的提交，父提交就是基线（0007—0010）：

| 路径 | 审核结果 |
| --- | --- |
| `csharp/HallCausalityRules.cs` | 删除整份 210 行旧模块 |
| `csharp/HallReplayProgram.cs` | 删除整份 97 行旧模块 |
| `csharp/AGENTS.md` | 只删除上述模块条目的两行 |
| `artifacts/story-hardcode-marking/execution-20261010/M01/implementation-report.md` | 新增 39 行实施报告 |

没有替代类、新 Core 能力、公开替代接口、测试或预期修改，也没有其他生产改动。`git diff --check` 通过。冻结工作区的旧类名引用索引在 Core/Godot 的 `.cs`、项目、解决方案、维护文件及 README 中没有结果（0011，rg 的无匹配退出码为 1）。两次工作区状态均干净且 tip 未变（0005、0017）。删除公开 API 的仓库外源码/二进制兼容性仍不能由仓库构建证明。

| 实际命令 | 证据 | 结果 |
| --- | --- | --- |
| `dotnet build csharp/Core.sln` | 0013 | PASS，0 警告、0 错误 |
| `dotnet build godot_csharp/DualOntologyWorldEngine.sln` | 0014 | PASS，0 警告、0 错误 |
| `dotnet run --project csharp/ScopeRegression.csproj` | 0015 | PASS，24/24 |
| `dotnet run --project csharp/ReasonerRegression.csproj` | 0016 | PASS，38/38 |

实际宿主构建还写出 `godot_csharp/scenes/Chapter2/Script/bin`、`obj`；rev2 的构建目录许可漏了这两处。克隆初始 local config 另含 `extensions.worktreeconfig=true`（0006），加固门禁止 `extensions.*`。控制器随后更正这两项并取回历史；独立审核未修改该克隆的元数据、生产源码、测试、预期或提交。

rev3 锁 SHA256 `ffa8965fd7adad7a24df3ab124812529b2945e65d2168c54821f5569edd5d0c3`，执行前封存的预期为 PASS（`M01.scope.lock.rev3.expectation.json`）。正式调用 0386 实际 DENY：`git fsck --strict --full --no-reflogs --no-dangling` 返回 3，缺少 `8ad74f78d9c9b9b8f3d68772c6d5aa0cb3fe47e9`、`ecf9aac0ca82e095ab0119de414335ea3bd008fb` 两个历史 `.gitattributes` blob。这是未通过项，保留原始双输出，不降低检测门条件。

控制器随后完整重新取回对象，独立只读补验结果为 shallow=false（0407）、所有可达对象缺失数 0（0408、`M01-final-object-completeness.json`）、严格 fsck 成功（0409）、local config 无前述禁止配置且工作树干净/tip 不变（0410、0411）。未重跑已通过构建与回归。

同一 rev3 锁的最终调用 0412 仍 **DENY**，原始双输出保存在 `raw/0412.*.bin`，具体原因：

```
wildcard in exact path: 'godot_csharp/ShareAsset/External/MaterialSymbols/MaterialSymbolsOutlined[FILL,GRAD,opsz,wght].codepoints'
```

这是 guard 的真实仓库适用性假阳性。`normalized_path` 将 `[]` 与 `*?` 一同禁止，并将该验证器用于实际 Git tree/文件路径；方括号是 Windows 合法文件名字符，此基线资源未被 M01 修改。应由 F02 修复实际文件路径与通配许可的处理，再冻结新哈希；本 R01 不修改 guard、资源或预期以绕过拒绝。依据仓库每轮测试须由全新上下文 Agent 执行的要求，修订后的 F02 应使用新一轮独立审核。

## F02 独立复验

`independent-guard-results.json` 记录 23 个预先封存的独立中性病例，全部匹配。独立病例没有使用项目设计、剧情或黄金答案。

| 检测面 | 独立结果与命令 |
| --- | --- |
| 合法已提交修改；精确声明的构建输出 | PASS（0103、0114） |
| 保护内容和测试预期的隐藏中间提交，末端已恢复 | DENY，逐提交列出修改及恢复（0129、0144） |
| ignored 预期、metadata exclude、额外未声明输出 | DENY（0155、0228、0340） |
| 保护路径大小写变更 | DENY（0170） |
| assume-unchanged、skip-worktree | DENY，识别具体标志（0182、0194） |
| staged 保护修改；untracked 保护文件 | DENY（0206、0217） |
| Git `120000` 链接、实际 Windows junction、共享 worktree、alternate objects | DENY（0251、0263、0275、0286） |
| tip 与冻结值不符 | DENY（0296） |
| 构建目录覆盖保护根、tracked 内容或通配符 | DENY（0307、0318、0329） |
| tracked 预期仅 CRLF 原始字节变化 | DENY（0351） |
| self-signed 锁；能读取许可之外的中性文件 | PASS 且外部门仍未认证/未实施（0362、0372、0374） |

Windows 此会话不能创建普通 OS 符号链接（WinError 1314），因此不声称该种文件系统链接已实测；Git 链接模式及实际 junction 已实测拒绝。首轮 Windows 大小写目录改名的夹具创建失败，改为新目录中两步改名后完成。失败记录见 `harness-failure-01.md`、`harness-failure-02.md`，原测试输出未覆盖。

另有一项执行前标为只观察的硬链接病例：工作区 tracked 文件与夹具外普通文件共享 inode、`nlink=2` 时门仍 PASS（0385）。Guard 的明确范围是 symlink/reparse 检测，未声称阻断普通硬链接；该结果应作为剩余文件共享/快照隔离限制记录，不能把通过解释成完整文件系统隔离。

M01 的对象缺失触发了额外中性完整性试验。先封存 `promisor-expectation-before-run.json`，再从中性本地供体生成 filtered clone；保留 promisor markers、移除 origin 后，证明旧历史 blob `9d4d92e220acd9ad8bbe4dfd49233563e3fe8041` 缺失（0404）。正式 guard 正确 DENY（0406，fsck 返回 2），结果见 `promisor-supplement-result.json`。因此没有把 promisor 元数据怀疑当作已证实的检测绕过。

官方冻结 `check_hardened_guard.py` 只把输出路径重定向到 `f02-smoke/`，沿用其原函数与冻结输入夹具。结果为 34/34、0 不匹配，原始 68 次调用见 `f02-smoke/commands.jsonl` 和 `f02-smoke/raw/`。其分类仍是 `IMPLEMENTATION_SMOKE_ONLY`；本卡的独立病例另存。

中性夹具的 1324 个普通文件已逐字节封存并验证于 `neutral-fixtures.zip`；链接仅保留元数据而未跟随，ZIP SHA256 `d6df77bec705866f006f0d997f05ca82167c7282788a6f194fad390c43b52f3d`。清理状态与避免误提交嵌套仓库的方法见 `archive-cleanup-note.md`。

## 尚未实施的外部门及验收边界

所有 guard 输出保持 `classification=DETECTION_ONLY` 和 `production_write_authorized=false`：

- `read_acl`、`write_acl`：`NOT_ENFORCED`。
- 锁真实性、批准真实性、合并身份、语义审核：`NOT_VERIFIED`（语义删除范围由本独立报告另行审阅）。
- 不可变审核快照：`NOT_ENFORCED`；观测间/观测后的并发改写不能由检测脚本形成硬锁。

本轮执行独立静态审核与自动回归。构建通过；所运行的两套机制回归通过；人物行为合理性和游玩体验未验证；一级三份全程记录、二级 UI、三级盲玩均未执行且未验证。本轮不涉及 UI 改动，不把逻辑回归当作实机或剧情验收。

集成建议：M01 的删除实现可以接受；F02 本轮需要修复合法方括号路径的误判。暂不集成。修复后须先让新一轮独立审核的正式 M01 范围门通过，并由控制器核对同一 tip 的合并身份。不得把后续 PASS 写成 read/write ACL 或批准认证已完成。
