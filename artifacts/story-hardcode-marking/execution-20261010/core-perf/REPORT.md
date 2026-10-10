# core-perf 执行报告

## 改动与合同

- 提交仍在同一 gate 内完成准备、校验和原子发布，失败不修改状态/历史/审计；非空成功批次只增一版。
- 内部前态已经闭合：校验本批输入与受影响的完整 parent 链；遗忘仍扫描剩余图，删除关系、AST、来源历史及 LastObservation 的结构依赖。
- 未变 Frozen 节点、AST、关系数组共享；只准备触及节点，旧运行时历史用 ImmutableList 追加，审计也改用持久化 ImmutableList。合同 DTO 保持原有数组类型及完整变化前后态。
- 审计只比较触及/遗忘节点，保持语义相同观察的空状态差分；按精确版本重建仍完整验证，不从审计补回主体认知。
- Frozen AST 直接遍历句柄，不为找依赖解冻；覆盖 Wait.Until、Say.Request 等嵌套节点。
- 首次求值才从内部已验证快照构建读图，每版独立对象壳，同版本复用读图与原引用映射；一次评分固定同一版本。快照仍不含源 Projection、Essence 或回调。
- 公开 Rebind、外部快照求值和审计重建仍走完整校验；没有放宽未知 AST、跨域引用、缺引用、默认集合等输入合同。
- 同步更新 csharp/AGENTS.md；新增两种父拓扑的 10 项中性回归，既有 202 项断言保留。

## 测量方法与热点

环境：同机 Windows、.NET 8 目标、Godot 4.6.1 mono；CLI 为 Release，Godot 宿主为 Debug。
旧 Core 对照为 core-ab 合并第一父提交 `c877be1911525b88c5481ea85fe322d062391cbb`，当前起点为 `f60bf266`。
只从 git archive 提取 csharp，不切换工作区；Godot 对照仅替换 Core DLL/PDB，finally 原始字节核验恢复。
临时计时仅加在 tmp 下 Core 副本，真实耗时用无插桩 DLL；正式源码/最终宿主不含计时代码。

主样本：正式第二章 Game 全时段运行，并在每时段用真实主体认知执行社交只读预览，共 7,200 次预览、2,396 次提交、281 次 Assess。
下表为嵌套计时扣除子区间后的独占时间及占全部已计时区间的比例，含计时/JIT/GC/负载影响，不能当作整个进程 CPU 占比。

| 热点 | 改前 ms / 占比 | 改后 ms / 占比 |
| --- | ---: | ---: |
| 整图校验、读图构建 | 6,368.8 / 41.88% | 706.7 / 27.85% |
| Frozen AST 冻结/解冻/重绑 | 4,519.6 / 29.72% | 423.8 / 16.71% |
| 审计前后态比较/追加 | 1,713.7 / 11.27% | 85.9 / 3.39% |
| 提交输入准备、校验、快照增量 | 1,082.1 / 7.12% | 497.2 / 19.60% |
| 旧引用准备、发布 | 1,034.9 / 6.81% | 170.9 / 6.73% |
| CaptureSnapshot 与原引用映射 | 141.8 / 0.93% | 6.4 / 0.25% |
| Assess 内部评分 | 178.3 / 1.17% | 393.1 / 15.50% |
| 社交内部评分 | 85.8 / 0.56% | 159.0 / 6.27% |
| 观察适配 | 77.7 / 0.51% | 89.7 / 3.54% |
| 通知队列/交付 | 4.5 / 0.03% | 4.4 / 0.17% |

计时区间合计 15,207.1 → 2,537.0 ms；样本 wall 17.54 → 4.97 s。
AST 解冻调用 21,349,368 → 844,370；读图构建 2,396 → 634。评分代码未修改，单项计时波动不用于速度承诺。
无插桩官方 CLI `slots 0` 的同机连续样本：合入前 2.69 s、优化前 14.64 s、优化后 4.16 s（较优化前减少 71.6%，仍为合入前 1.55 倍）。
CLI 样本不是 flow/flow2 达标证据，也不是游玩时长。

## Godot 对照与最终结果

所有 Godot 运行把 APPDATA/LOCALAPPDATA 指向工作区 tmp/appdata，跳过 gallery，未执行 import。

| 版本 | flow | flow2 | 断言 |
| --- | ---: | ---: | --- |
| 优化前 Core | 320 s | 240 s | 135/136；109/109 |
| 合入前 Core 同机对照 | 176 s | 99 s（脚本 98.014 s） | 136/136；109/109 |
| 最终优化 Core | 181.891 s | 104.968 s（脚本 104.225 s） | 136/136；109/109 |

按旧 Core verify 输出的秒数计算：最终 flow 为 1.034 倍、flow2 为 1.060 倍，均低于 1.15；flow2 脚本计时比值为 1.063。
优化前 flow 有另一 Godot 并行负载，该轮耗时不作严格收益归因。旧 Core 对照 Agent 当时未观察到额外进程；最终 Agent 观察到额外 Godot 组，沙箱拒绝读取命令行，负载来源未能确认。这里报告同机实测比值，没有宣称严格隔离的 CPU 基准。
优化前唯一断言失败为 R-06 挂钩文字检查，旧 Core 对照及最终优化均未重现；未判断言过时、未改预期。
三轮均有 `Failed to read the root certificate store`，原 verify 错误扫描因此判 FAIL（exit 1）。最终每项仅此一条错误，无其他引擎/脚本错误、缺 .ctex 或退出泄漏错误。

## 验证、未完成项与风险

- 全新上下文独立 Core 终验：Scope 24/24；Cognition 212/212（原 202 + 新增 10）；Reasoner 38/38；最后再跑 Scope 仍为 24 项，未误启动 Cognition。全部退出 0，显式构建零警告/错误。
- 命令：Scope Release build/run --no-build、Cognition Release run、Reasoner Release build/run --no-build；build 用 -m:1 --disable-build-servers。Core 终验日志：tmp/core-perf/core-final-validation/。
- 最终另一全新上下文 Agent：宿主 build 25.563 s、exit 0、0 错误/2 条 NU1900 警告；原 verify 的 flow/flow2 断言全过且性能达标，总 wall 286.937 s。严格统一回归仍因上述沙箱证书错误失败，未把它报告为全绿。精确计时：tmp/core-perf/host-final-validation/{host.timing.json,verify.steps.json,verify.timing.json}；原始通道日志：该目录下 verify-logs/20261010-023731-099222-10748/。
- 优化前宿主 build 成功，有两条 NU1900 漏洞数据访问警告；Prototype 首次并行 build 无具体错误地失败，单节点重试成功。
- 官方 CLI `host` 优化前抛 PlayerError（Host.cs:22、:33，Program.cs:49），无法完整计时，改用 `slots 0` 及同一 Game 的社交预览驱动，未改原 CLI 或测试。
- 剩余成本：每次提交仍复制合同的 Projections/VisibleHistory 数组；每版首次读图仍分配独立对象壳、重绑当前 AST 与历史；遗忘仍扫描依赖；外部快照入口仍完整校验。CLI 相对旧 Core 的开销尚未全部消除。
- 本轮是自动机制回归与性能测量；一级剧情/引擎、二级 UI、三级盲玩均未执行。人物行为合理性、剧情审美与游玩体验未验证。
- 未读取设计文件，未修改 UI/样式/布局、剧本或其生成物，未提交/推送。交付仅代码、测试、对应 AGENTS.md 更新和本报告；原始计时与回归日志保留 tmp/core-perf/。
