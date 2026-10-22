# 工具中心维护

本目录继承仓库根目录 AGENTS。用户当前执行限制优先；只要求实现时，工具源码和路由接线不触发构建、检查、采集或探针。

测试、采集和诊断使用本任务固定输出目录。每次新测试前按根 AGENTS 核对路径并递归清理上一轮输出，新测试替换旧测试；不复制历史记录或改用递增目录积累证据。独占写入与失败 partial 保留仅约束当前轮，下一轮开始前一并清理。

`verification/run_cli_ai_demo.py` 仅在用户明确授权 AI 决策运行及现实秒数时运行；固定内置 ai 与侦探检阅者，只请求 next 或限时 advance 批量自主推进；每项选择仍由引擎本人规划，工具不选择行动或答案。硬计时从进程启动开始，包含开局、在途动作和封存；`--seconds` 为停止请求新自主推进的时点，`--grace` 是硬期限内预留的关闭时间，不能叠加到授权时长之外。默认 `--seconds 41 --grace 4 --hard-timeout 45`，最后 250 毫秒留给操作系统终止与回收，独立 watchdog 保持在 44.75 秒终止仍在运行的被测 CLI 进程，保留失败或部分文件。该外部超时不代表规划器内部已实现墙钟硬期限。此工具不扫描源码、不运行静态验证、不生成 UI 回放；来源由调用者提供的 revision 与程序集原生记录标明。

- `tools.json` 是公开操作、命令路径、函数归属和运行模式的唯一目录。`server.py` 从 `routes` 生成公开枚举，`tool_actions.py` 只执行通用路由，不维护第二份操作名单。
- 新增可独立调用的工具时，同步添加实际路由与参数说明。共享库、场景组件、编译桥和编辑器回调通过其所属入口使用，不另造空 handler。
- `tools/list` 只读取静态目录。MCP/LSP 初始化可调度 `build/prewarm.py` 的后台编译器预热；请求线程不等待编译，其他管线在调用时导入。所有子进程输出进入响应、日志或 stderr，MCP stdout 仅承载协议。
- Godot 工具使用 `godot_workspace.py` 的临时工程。纯地图、纯 UI Mock 和资产适配器不创建 C# 项目；显式 C# 采集工程才编入 `FrameArchive.cs`。工具 `res://addons` 和 `res://tools` 路径只在该工程中映射。
- 长驻服务中的临时工程使用 `workspace()` 作用域及时释放。工具生成预览放 `sandboxes/generated`；原始素材放 `asset_pipeline/sources`；成品放 `godot_csharp/ShareAsset`；日志和发行文件放 `artifacts`。
- 二进制证据完整性、领域分析是否完成、主因果是否获得支持分别报告。负面领域判断可以是已完成分析；缺少前提或无效轨迹不能标成已完成。

CLI 的 JPG 由已经记录的同一画布离线导出，覆盖实际聚焦、确认、完成和正文续页；去重索引必须保留每个原始帧的覆盖关系。导出不另开 AI 会话，不计作额外游戏推进。当前任务遵守每轮启动至退出 45 秒上限，全部修改结束后才构建和测试；每轮导出后由子代理逐图对照设计文档，完成审查再替换当前轮输出。用户本轮明确要求逐图对照设计，优先于旧 AGENTS 中独立盲审不得读取设计的通用规则；本轮审查子代理须读取设计，不套用盲审限制。

`render_terminal_export.py` 按真实字体 Unicode cmap 选择字形，并按原 SVG 格宽和基线分段绘制；`✕`、`✓` 等原始符号保持原文。Windows 可使用实际 Segoe UI Symbol，额外字体用可重复的 `--fallback-font` 指定；缺少字形明确失败，不输出缺字方框或替换符号。成功报告列明实际字体，失败控制台截图也沿用同一回退。最新正文全宽优先、选中项短评分和字体回退仅为本批源码实施，尚未取得下一轮逐图验收；记录停止不是剧情完成。

性能流与 live 世界 DAG 记录器分开：当前 CLI 没有打开 live `RawDagWriter`，最近实测 `world_cut` 未生效，完整历史仍保留。报告不得把未接线的压缩或尚未实现的规划器墙钟硬期限列为已完成优化；既有实测与评估集中见 `../docs/cli-optimization.md`。

两章 CLI 激进优化入口及开关合同见 `../docs/cli-optimization.md` 的 2026-10-22 节；实际运行窗口以用户本轮明确授权为准，不从历史窗口或默认参数推定。原始 DAG 事件封包可附进程单调耗时，旧证据不补造时间。

激进优化诊断可用 aggressive_report.py 只读汇总固定输出目录；它不启动 AI、不展开原始 DAG。--profile 的 DV_PROFILE_NATIVE 条件编译细分 Runtime 热点，无插桩 Release 不包含这些探针。判定只读视图与字段 cell 缓存均按 CLI 显式开关采样于执行上下文，不读取人物策略档。

run_debug 的新增优化开关默认 on；mask/dirty verify 默认 off，仅作排错。prune 深度、时间桶和核验间隔写入配置指纹与恢复一致性检查。主会话耗时与构建、离线领域核验耗时分别记录，不以进程后处理时间替代游戏计时。

只读因果检查可独立关闭 causal-index；检查器开关和时限记录在 provenance.verification_config，不混入人物策略。索引只加速查找，不删检查，实际命中与关闭路径须保持等价。

矩阵工具 `run_aggressive_matrix.py` 的默认会话进程窗口为300秒，默认值不构成AI测试授权。baseline 保持 selective on并关闭统一 FLAGS 中全部优化；precise-baseline 单列策略条件。FLAGS 同时供 run_debug 与矩阵使用，新增 replaycache/runtimespeed 分别由 DV 策略及 CLR 环境消费者接线，配置、环境与复用元数据均留证。

复用程序要求 v2 完整来源/生成源文件/Release程序集集合及插桩、编译器、入口合同一致，复制后再次校验；旧v1记录须重新构建，不补造指纹。只读原始事件索引可保留原始 parent_ids 和 host_action_event，章末汇总必须以这些索引、实际执行投影及completion互相核验，不以非空摘要代替达成。

`--opt-causalbuildcache on|off` 默认 on，属于 TOOL_FLAGS 的只读工具消融项并写入 verification_config 及其 SHA-256，不进入人物决策配置。仅从 `--reuse-program` 同级 causal-program 读取独立 compiled-causal-program.json，以 v2 完整合同和 causal-check-70 入口校验后复制；缺少 manifest 正常构建并封存，已有 manifest 失配明确拒绝。每局仍重新生成本轮 intake 并实际执行完整因果核验；超时也保留 causal_stages 的构建/执行耗时及具体阶段。不得把复用或执行超时当作核验通过。

矩阵会话及本工具域读者按固定场景持有 OS lease，清理等待lease和旧进程结束后，在同一原生PowerShell内核对路径及reparse point并删除固定目录。外层运行由Windows Job Object管理全树；超时/中断/清理失败保留 matrix-running.lock，人工核对在途进程、读者及部分证据后才可恢复，不自动删锁重试。构建、会话执行、后处理、命令墙钟及含清理的场景墙钟分别报告。实现合同及有限合成检查见 `../docs/cli-optimization.md`。

`opt-evidence-index` 同时控制二进制 payload 原字节校验复用与只读节点缓存；所有帧仍完整解码、校验类型、帧哈希及有序链。节点缓存按 8192 项和 16 MiB 原始帧字节双限驱逐（Python 对象实际内存另有开销），不得省略原字段或未投影尾部报告。有限合成读取差分不构成正式会话或整局后处理性能验收。

性能报告分别列会话与整命令180秒达标；命令实测缺失保持未知。矩阵并发字段仅描述自身，不能作为跨检出没有并行工作的证明。
证据投影在 evidence-index 下复用不可变来源的绝对路径，索引记录直接编码二进制，不经临时 JSON 往返。run_debug 的 postprocess_stages 分别记录原始证据核验/索引、stdout 正文/索引、因果构建/检查及两次来源核验；失败仍保留各段已发生现实耗时。
