# V00 自动玩家公开接口骨架

结论：**V00 合成边界测试通过，模式为 `DETECTION_ONLY`。未连接生产引擎，未进行正式自治通关，没有文件系统隔离。**

本目录只建立可复用接口、可见输出投影、子进程传输、失败关闭及原始记录。所有测试内容都是中性的合成字符串、运行时生成的候选 token，既不是章节路线，也不是谜底。控制策略按当前可见标签排序选候选，没有固定 token、答案或操作路线。

## 读取范围与基线

- 指定基线：`7489db56a714724eb7cb735958dd879dc662d238`。实际执行 HEAD 记录在 `results/run-context.json`。
- 公开契约：`godot_csharp/scenes/Shared/Story/ChapterProtocol.cs`；`WorldBehavior.cs:30–44` 的 `WorldInput` / `WorldOutput` 声明；`InteractionPacket.cs:6–13` 的交互 DTO 声明。
- 能力清单：`../F01/capability-manifest.json` 的 `F01-C010`、`F01-C019`。它们是审计事实，不是新能力的批准凭证。
- 初次 Agent 阅读发生范围偏差：上下文检索读取了 `WorldBehavior.cs:26–172`，并读取了 `InteractionPacket.cs` 全文件，超出了 DTO 声明，包含相邻通用机制实现。未读取设计、`.story`、谜底、黄金、固定玩家路线或历史试玩。此偏差不能省略，也不能把本次上下文称为只看过 DTO。最终提取器仅导入授权行范围内的精确 DTO 声明，来源见 `results/dto-provenance.json`。
- 仅写本 V00 目录；未修改生产、任何 AGENTS、设计、S18、黄金。提交与推送由主任务统一执行。

## 可复用连接面

`ICandidatePort` 仅有一个方法：`Submit(SelectCandidate) -> IReadOnlyList<WorldOutput>`。它没有根会话、`Snapshot`、`IChapterProbe`、任意 `WorldInput` 或状态写入方法。该 port 由可信主程序持有；controller 子进程只接收 JSON 字节，连 port 对象也不会收到。

`PublicPlayerDriver.TurnAsync` 是主程序连接点。主程序提供正式输出 DTO；`CandidateBoundary` 显式按正式类型序列化并由父进程 observer 保存原始输出，再生成严格可见投影发送给 `ControllerProcess`。子进程返回的唯一游戏动作格式为：

```json
{"type":"SelectCandidate","token":"当前正式候选提供的opaque token"}
```

无候选时可以返回 JSON `null`，它只表示本轮没有输入，不能创建任意 `WorldInput`。父进程收到有效选择后才构造正式 `SelectCandidate`，调用窄 port。

可见投影仅包括当前 token、标签、正文的 speaker/mood/text，以及可见证据卡 title/facts/source/notes 和完成标志。候选 `Id`、行 `LineId/SourceId`、presentation id、资源路径、整个 state/snapshot、运行时对象或 observer 路径都不会进入 wire。这里仍要求 V02 核验每个字段是否确为玩家可见；V00 没有读取实际 UI 或设计。

当前只接受 `CandidatesAvailable`、`SemanticDialogue`、`NoApplicableCandidate` 三种正式 `WorldOutput`。其他类型明确失败，不能笼统序列化进控制器。`InteractionPacket` 可以提供可见正文或卡片；带菜单 choice 或 `WorldCommand` 的包在本骨架中失败。菜单 `Id` 不会伪装成候选 token，V02 必须提供经核验的正式接线。

每个新 observation 使旧候选失效；单次提交使整批候选失效，正式 port 抛错也不重试。重复、过期、未知或重新发行 token，重复 JSON 字段，隐藏或未知字段，未知输入类型都失败并关闭 boundary。默认上限为 8 次决策、32 次 observation、单回复 16 KiB、单观察 64 KiB、256 条候选或正文；controller 逐轮超时，transport 失败通过 driver 同时关闭主 boundary。V02 应依据真实生命周期设置有界预算，不能通过重启 controller 延续失败会话。

## 执行与证据

在仓库根目录运行：

```powershell
python artifacts/story-hardcode-marking/execution-20261010/V00/verify_v00.py
```

该命令从授权源精确提取 DTO 声明生成 `PublicDtos.g.cs`，构建独立 .NET 8 工程，并运行 18 条 Python 合约断言和 23 条 .NET/API/子进程边界用例。提取物是**声明编译夹具**：类型声明来自正式源，生产引擎实现并未编译或调用。V02 必须引用真实程序集并排除该夹具，不能用本次编译替代生产编译。

本次结果 41 条用例通过、0 失败。独立观察器断言验证正式输出含中性隐藏标记，而发送给 controller 的原始字节没有这些标记；反射检查窄 port 和 wire DTO 不带 observer、port 或委托。恶意子进程测试实际检查未知输入、隐藏 state、重复字段、超长输出、超时及失效 token，没有转交任何非法选择。

- `results/summary.json`：机器可读结论与未验证项。
- `results/csharp-results.json`、`results/python-results.json`：逐项结果。
- 各用例 `results/*.jsonl`：正式 DTO 序列化、完整决策、主/子进程输入输出、拒绝与退出记录。wire 字节使用 Base64 保留；超长或超时读取先记录已读取前缀，进程终止后的 stdout 余量另行记录，不作为第二个动作。
- `results/observer-separation.json`：独立于控制器的日志核对结果。
- `results/wire-log-integrity.json`：核对超长回复的全部 20,002 字节（本次 Windows CRLF）分段记录均已保存，且没有形成正式输入。
- `results/permission-detection.json` 和 `permission-probe.*.log`：权限实测。
- `results/build.stdout.log`：最终构建 0 警告、0 错误。首轮 `CS8669` 失败原始日志保留在 `build.previous-1.stdout.log`；生成器已补充 `#nullable enable`。初次 Windows 完全空环境启动失败摘要见 `failed-attempts.json`，其 traceback 只存在原 tool 输出，未声称该摘要是原字节日志。依赖失败步骤当时未运行。

局部 `.gitattributes` 对 JSONL 与运行日志设 `-text -diff`，防止提交时改写原始字节。重复构建前保存上一轮 stdout/stderr 原字节；后续重新执行会先把当时所有结果文件复制到独立 `results/archived-attempt-*`，并立即标记新一轮 RUNNING，失败时标 FAILED，避免使用旧 PASS 作为新证据。`archived-attempt-1` 保存加入此机制前的 integrity 失败状态，其 summary 是之前留存的 PASS，不是该失败轮的合格结论，见 `failed-attempts.json`。归档机制加入之前的多次合成调试没有逐轮完整封存，已保留的失败与最终证据不补造。`bin/obj` 不交付。

## API 边界与进程权限

API/对象/序列化边界已在合成数据上测试：controller 没有根会话句柄，没有 observer 参数，只能回复窄选择协议。

进程权限**没有隔离**：测试启动同用户子进程，在空 cwd 及最少环境下成功读到了 cwd 外的 V00 自有无敏感 sentinel。这是正向证据，表明工作目录、环境清理、Python `-I/-S` 均不能阻止读取仓库、历史日志或 observer 文件。没有尝试读取这些受限内容，也没有验证网络、其他进程内存或操作系统权限限制。现状只能称为 `DETECTION_ONLY`，不能称为可信盲玩或安全自治。

## V02 缺口与最小验收

1. 用真实程序集替换 DTO 编译夹具，在可信宿主实现 `ICandidatePort.Submit(SelectCandidate)`；controller 不接收 `WorldSession`、`IChapterSession` 或任何 engine 对象。明确核对候选 actor 与正式 token 的归属。
2. 核验正式输出至可见投影的逐字段映射，并处理真实 action start/finish、候选刷新、感知、presentation 完成和时间调度。后者由可信宿主按正式流程驱动，不成为 controller 的任意注入接口。未核验类型继续 fail closed。
3. 若现实章节菜单暂不提供正式候选 token，明确报告接口缺口；不得把菜单 id、预写答案、探针或 state 注入作为替代通关。
4. 另行落实可证明的操作系统权限限制，实测限制进程无法读仓库设计、源码、黄金、历史试玩与 observer，且只可访问控制器最小依赖。普通子进程模式不能升格为该证明。限制能力未完成时继续标 `DETECTION_ONLY`。
5. 在真实正式接口上复测有效/过期/消费/刷新 token、未知字段、port 失败、超时、输出上限、有限总预算与失败会话不能继续；保留全部原始输入输出。V00 的 41 条用例不能代替这些生产接线测试。
6. 仅在明确授权正式验收时，用全新独立上下文进行真实游玩，记录从新会话开始的全部玩家可见信息及输入，并依据该验收要求评价。不能把本合成测试当作自治通关、盲玩或真实时长证据。

本轮验收结论：独立 DTO 骨架构建通过；骨架 API/协议边界机制通过；生产机制、人物行为合理性及游戏体验未验证；一级、二级、三级均未执行。
