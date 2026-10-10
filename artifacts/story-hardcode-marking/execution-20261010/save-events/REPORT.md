# save-events 执行报告（2026-10-10）

## 改动与原因

- 工作目录为 `dowe-cx-saveev`，分支 `codex/save-events`；开始时工作区干净。未访问旧 worktree 或设计文件，未改 UI、布局、剧本及生成产物，未提交。
- 基线 CognitionRegression 在 37 项通过后于审计重建中异常退出：`MalformedData / commit / 审计变化与完整重建结果不符`。原因是旧 codec 丢弃了 core-d 的 `ObservationChange.Event`、可见历史/最近观察的事件，以及 `Say.Claim/Topic`；重建输入因此与原变化不同。
- `CognitionEventCodec.cs` 对行动、观察、证词、传播、呈现五类事件使用闭合 JSON 分支，保存全部版本、身份、时间、阶段/结果、引用、命题、来源、感知者与因果字段；未知事件种类/字段/枚举明确拒绝，不反射恢复类型。
- `CognitionCodec.cs` 将事件加入主体图、审计输入/变化/历史和 AST codec，并声明 `core.typed-events/1`；完整校验后返回候选。事件集合纳入历史限额。
- `RuntimeSave.cs` 保存有序权威事件记录；分配所有活跃/退休实体并完成绑定后重建事件引用及 ID 登记。重复 ID、缺失/前向因果、缺实体拒绝；声明类型化事件却缺少权威数组也拒绝。恢复不发布观察、不执行效果。
- `Observation.cs` 将前置可见事件的传递来源依赖加入后继记录；`SnapshotGraph.cs` 校验可见因果及依赖闭合。遗忘前置内容会清除其可见后继，权威审计保持完整；恢复不会从审计补回已忘内容。
- 保留原 core-save 270 项及 core-d 52 项断言，新增 `EventSaveRegression` 50 项：两个中性世界的五类事件 wire/审计往返、权限过滤、退休引用、明确拒绝、真实子进程恢复、重复登记拒绝和因果遗忘。原始拒绝输入与实际错误保存在 `tmp/save-events-wire/`。
- 宿主保存检查追加事件字段/因果保持、重复载入不重复登记的断言；沿用正式保存 API 和 headless 第一章连续推进检查。同目录 AGENTS.md 与 Core 项目测试排除项同步更新。无临时 DEBUG 打印。

## 验证结果

验证由全新独立上下文代理执行；只做构建与自动机制回归，不属于一级三份记录、二级 UI 或三级盲玩。

`python tools/verify.py --skip gallery --keep-going` **退出 0，七步全通过**，总墙钟 463.84 s。下表为统一入口记录的秒级墙钟。

| 检查 | 结果 | 耗时 |
| --- | --- | --- |
| Core / ScopeRegression Release 构建 | 0 警告、0 错误 | 21 s |
| ScopeRegression `--no-build` | 27/27 | 4 s |
| CognitionRegression Release | 372/372，0 警告、0 错误 | 33 s |
| 宿主 solution `-m:1 --disable-build-servers` | 0 错误，2 条 NU1900 | 18 s |
| 真实 Godot save / 新进程恢复 | 写入 22 项＋读取 1 项；连续推进结果相同 | 13 s |
| flow | 136/136，0 失败 | 231 s |
| flow2 | 109/109，0 失败 | 145 s |

- 最终源码的独立认知补验退出 0，372/372（新增 event-save 50 项），31.78 s，0 警告/错误；flat/nested 各 7 个 graph 与 6 个 authority 拒绝 JSON 均有实际错误文件，两份新进程恢复日志完整。
- `python tools/test_verify.py` 退出 0，7/7，通过；外层耗时 3.27 s。真实 Godot 本轮无缺汇总重试、退出崩溃或引擎错误；gallery 按要求跳过。
- flow2 同环境对照（无其他 Godot 并发）：整合 HEAD 基线 **137.476 s → 当前 136.938 s（-0.39%）**，两轮均 109/109、退出 0、无引擎错误/崩溃/重试；未见本次修改导致减速。只切换 Core 编译输出，两份当前 dll/pdb 最终按原字节恢复。
- flow 耗时参考前份 core-save 报告 **219.94 s → 本轮 231 s（+5.0%）**；参考版本不同，本轮全量运行也存在其他 Godot 并发，该差值不能作为本次修改的独占性能结论。当前整合 HEAD 的 flow 没有单独对照样本。

## 环境、未完成项与风险

- Godot 运行前 `APPDATA` / `LOCALAPPDATA` 指向工作区 `tmp/appdata`；使用正常的既有 NuGet 缓存。初次验证另设 `DOTNET_CLI_HOME` 后未解析到已有 Godot SDK，构建失败；恢复正常缓存后宿主成功。
- NuGet 漏洞数据查询因网络限制产生 `NU1900`，未抑制警告。首轮认知的 CS8602 已修正，最终 Core/认知均无源码告警。
- 临时 `godot_csharp/override.cfg` 指定工作区内的有效 certifi CA 包，避开旧报告中的 Windows 根证书存储访问错误；未禁用 TLS、过滤错误或放宽检查标准。该设置采用 Godot 官方的 [certificate_bundle_override](https://docs.godotengine.org/en/4.6/classes/class_projectsettings.html#class-projectsettings-property-network-tls-certificate-bundle-override)。测试结束后已删除临时配置，副本与证书保留 `tmp/save-events-env/`；主代理在同一沙箱复现时可复制该副本至 `godot_csharp/override.cfg`，结束后清理。
- 当前保存切点仍限于既有 WorldSession/第一章空闲点；活动行动、时效层、wakeup、选择、呈现回执、fiber 和其他未支持章节仍明确拒绝，本任务未扩大切点。
- 不声明旧存档迁移兼容；人物、游玩体验、UI 与正式游戏 stress 未验证。

## 证据

- 失败基线：`tmp/save-events-baseline/`。
- 首轮 370 项通过及 SDK 解析失败：`tmp/save-events-first-validation/`；宿主诊断成功输出：`tmp/save-events-final-validation/`。
- 最终统一回归：`tmp/save-events-regression/20261010T053534397311Z-30900/verify.summary.json`；原始逐步 stdout/stderr：`artifacts/project-audit/verify-logs/20261010-053534-672616-33256/`，实际返回码同前目录保存。
- 最终认知与拒绝输入证据核验：`tmp/save-events-evidence-validation/`；两个真实子进程及拒绝数据：`tmp/save-events-wire/{flat,nested}/`。
- HEAD 基线与当前 Core 的同环境 flow2 对照：`tmp/save-events-performance/`；只替换编译输出，结束后恢复当前库，不改源码或断言。
- `git diff --check` 通过；交付为代码、测试与本报告，无未完成的本任务功能项。
