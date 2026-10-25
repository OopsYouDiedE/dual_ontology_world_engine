# 工具中心维护

本目录继承仓库根目录 AGENTS。用户当前执行限制优先；只要求实现时，工具源码和路由接线不触发构建、检查、采集或探针。

测试、采集和诊断使用本任务固定输出目录。每次新测试前按根 AGENTS 核对路径并递归清理上一轮输出，新测试替换旧测试；不复制历史记录或改用递增目录积累证据。独占写入与失败 partial 保留仅约束当前轮，下一轮开始前一并清理。

`verification/run_cli_ai_demo.py` 仅在用户明确授权 AI 决策运行及现实秒数时运行；固定内置 ai 与侦探检阅者，只请求 next 或限时 advance 批量自主推进；每项选择仍由引擎本人规划，工具不选择行动或答案。硬计时从进程启动开始，包含开局、在途动作和封存；`--seconds` 为停止请求新自主推进的时点，`--grace` 是硬期限内预留的关闭时间，不能叠加到授权时长之外。默认 `--seconds 41 --grace 4 --hard-timeout 45`，最后 250 毫秒留给操作系统终止与回收，独立 watchdog 保持在 44.75 秒终止仍在运行的被测 CLI 进程，保留失败或部分文件。该外部超时不代表规划器内部已实现墙钟硬期限。此工具不扫描源码、不运行静态验证、不生成 UI 回放；来源由调用者提供的 revision 与程序集原生记录标明。

- `tools.json` 是公开操作、命令路径、函数归属和运行模式的唯一目录。`server.py` 从 `routes` 生成公开枚举，`tool_actions.py` 只执行通用路由，不维护第二份操作名单。
- `asset_pipeline/modern-stage` 由`publish_astra_stage.py`切分用户认可的Astra素材，发布128px宽地面和透明人物/家具；只做裁切、透明分离、镜像及有记录的尺寸对齐，不重绘，不改32px碰撞尺度。`godot_tools/capture-modern-stage`仅观察真实舞台的材质候选、绕桌落座和拐杖姿态，不启动Core/AI。输入、身体采样与结果以DVTILE01头、长度、SHA-256及Godot原生Variant二进制封装到`.dvlog`；它是此工具的呈现证据格式，不冒充正式会话日志。
- 有限舞台采集的Python包装器只复用生产`.godot/imported`资源缓存，临时工程仍独立；更新源素材必须正常重新导入。`modern-stage-report`读取固定输出目录，拒绝早于本轮指纹的截图、源码失配和损坏流，生成HTML而不启动会话；检查完整性不等同于画面或游戏验收。
- 新增可独立调用的工具时，同步添加实际路由与参数说明。共享库、场景组件、编译桥和编辑器回调通过其所属入口使用，不另造空 handler。
- `asset_pipeline/highlight-stages` 只发布保留原稿的高光道具与人物 atlas；其裁切参数、比例和来源哈希写入成品清单。场景执行、画面观察和正式 Core 会话分别授权、分别记录。
- `asset_pipeline/highlight-room-skins` 将可复用地形接到指定现役房间，只新增呈现节点与资源引用，使用已有 terrain_rows；章节地图再生复用相同的 attach_skin 函数。既有场景几何、门口、站点、脚点和楼层语义不在此发布器修改。
- `asset_pipeline/highlight-terrain` 将明确的材质格或47/16/3×3图集发布为32格 TileSet，原始规格、衍生拓扑和透明边分别留证，纹理引用外部 PNG。`asset_pipeline/highlight-report` 只将当前内容和证据索引生成本地 HTML；该入口不启动游戏、不执行验收。
- `asset_pipeline/astra-terrain` 只按显式源坐标裁切已有画廊边饰并旋转拼接，邻接表来自 `rpg_adapter/data/autotile_tables.gd`，不恢复退役的材质缩小/遮罩转换器。`capture-astra-terrain` 复制有限依赖到临时工程采集，检查错误日志与二进制封存；不得把邻接样片当作正式会话或全地图验收。
- `asset_pipeline/document-textures` 只生成设计文档对应的 SVG 材质、透明徽记、平铺纹样和样板；制作补全维护在该管线 `sources/Internal/Textures/DocumentSystems/DESIGN.md`，不回写原设计案、不修改游戏几何。受限配色通过素材清单的 scope 区分。
- `tools/list` 只读取静态目录。MCP/LSP 初始化可调度 `build/prewarm.py` 的后台编译器预热；请求线程不等待编译，其他管线在调用时导入。所有子进程输出进入响应、日志或 stderr，MCP stdout 仅承载协议。
- Godot 工具使用 `godot_workspace.py` 的临时工程。纯地图、纯 UI Mock 和资产适配器不创建 C# 项目；显式 C# 采集工程才编入 `FrameArchive.cs`。工具 `res://addons` 和 `res://tools` 路径只在该工程中映射。
- 长驻服务中的临时工程使用 `workspace()` 作用域及时释放。工具生成预览放 `sandboxes/generated`；原始素材放 `asset_pipeline/sources`；成品放 `godot_csharp/ShareAsset`；日志和发行文件放 `artifacts`。
- 二进制证据完整性、领域分析是否完成、主因果是否获得支持分别报告。负面领域判断可以是已完成分析；缺少前提或无效轨迹不能标成已完成。

CLI 的 JPG 由已经记录的同一画布离线导出，覆盖实际聚焦、确认、完成和正文续页；去重索引必须保留每个原始帧的覆盖关系。导出不另开 AI 会话，不计作额外游戏推进。实际导出、截图审查范围与运行时限按当前用户指令执行；历史任务的时限和审查授权不适用于新任务。

`render_terminal_export.py` 按真实字体 Unicode cmap 选择字形，并按原 SVG 格宽和基线分段绘制；`✕`、`✓` 等原始符号保持原文。Windows 可使用实际 Segoe UI Symbol，额外字体用可重复的 `--fallback-font` 指定；缺少字形明确失败，不输出缺字方框或替换符号。成功报告列明实际字体，失败控制台截图也沿用同一回退。最新正文全宽优先、选中项短评分和字体回退仅为本批源码实施，尚未取得下一轮逐图验收；记录停止不是剧情完成。

性能流与 live 世界 DAG 记录器分开：当前 CLI 没有打开 live `RawDagWriter`，最近实测 `world_cut` 未生效，完整历史仍保留。报告不得把未接线的压缩或尚未实现的规划器墙钟硬期限列为已完成优化；既有实测与评估集中见 `../docs/cli-optimization.md`。

两章 CLI 激进优化入口及开关合同见 `../docs/cli-optimization.md` 的 2026-10-22 节；实际运行窗口以用户本轮明确授权为准，不从历史窗口或默认参数推定。原始 DAG 事件封包可附进程单调耗时，旧证据不补造时间。

激进优化诊断可用 aggressive_report.py 只读汇总固定输出目录；它不启动 AI、不展开原始 DAG。--profile 的 DV_PROFILE_NATIVE 条件编译细分 Runtime 热点，无插桩 Release 不包含这些探针。判定只读视图与字段 cell 缓存均按 CLI 显式开关采样于执行上下文，不读取人物策略档。

run_debug 的优化开关除实验taskgraph默认off外默认 on；mask/dirty verify 默认 off，仅作排错。prune 深度、时间桶和核验间隔写入配置指纹与恢复一致性检查。主会话耗时与构建、离线领域核验耗时分别记录，不以进程后处理时间替代游戏计时。

只读因果检查可独立关闭 causal-index；检查器开关和时限记录在 provenance.verification_config，不混入人物策略。索引只加速查找，不删检查，实际命中与关闭路径须保持等价。

矩阵工具 `run_aggressive_matrix.py` 的默认会话进程窗口为300秒，默认值不构成AI测试授权。baseline 保持 selective on并关闭统一 FLAGS 中全部优化；precise-baseline 单列策略条件。FLAGS 同时供 run_debug 与矩阵使用，新增 replaycache/runtimespeed 分别由 DV 策略及 CLR 环境消费者接线，配置、环境与复用元数据均留证。

复用程序要求 v2 完整来源/生成源文件/Release程序集集合及插桩、编译器、入口合同一致，复制后再次校验；旧v1记录须重新构建，不补造指纹。只读原始事件索引可保留原始 parent_ids 和 host_action_event，章末汇总必须以这些索引、实际执行投影及completion互相核验，不以非空摘要代替达成。

`--opt-causalbuildcache on|off` 默认 on，属于 TOOL_FLAGS 的只读工具消融项并写入 verification_config 及其 SHA-256，不进入人物决策配置。仅从 `--reuse-program` 同级 causal-program 读取独立 compiled-causal-program.json，以 v2 完整合同和 causal-check-70 入口校验后复制；缺少 manifest 正常构建并封存，已有 manifest 失配明确拒绝。每局仍重新生成本轮 intake 并实际执行完整因果核验；超时也保留 causal_stages 的构建/执行耗时及具体阶段。不得把复用或执行超时当作核验通过。

矩阵会话及本工具域读者按固定场景持有 OS lease，清理等待lease和旧进程结束后，在同一原生PowerShell内核对路径及reparse point并删除固定目录。外层运行由Windows Job Object管理全树；超时/中断/清理失败保留 matrix-running.lock，人工核对在途进程、读者及部分证据后才可恢复，不自动删锁重试。构建、会话执行、后处理、命令墙钟及含清理的场景墙钟分别报告。实现合同及有限合成检查见 `../docs/cli-optimization.md`。

`opt-evidence-index` 同时控制二进制 payload 原字节校验复用与只读节点缓存；所有帧仍完整解码、校验类型、帧哈希及有序链。节点缓存按 8192 项和 16 MiB 原始帧字节双限驱逐（Python 对象实际内存另有开销），不得省略原字段或未投影尾部报告。有限合成读取差分不构成正式会话或整局后处理性能验收。

性能报告分别列会话与整命令180秒达标；命令实测缺失保持未知。矩阵并发字段仅描述自身，不能作为跨检出没有并行工作的证明。
证据投影在 evidence-index 下复用不可变来源的绝对路径，索引记录直接编码二进制，不经临时 JSON 往返。run_debug 的 postprocess_stages 分别记录原始证据核验/索引、stdout 正文/索引、因果构建/检查及两次来源核验；失败仍保留各段已发生现实耗时。

run_debug显式将opt_plancache/opt_declcache分别写入DV_OPT_PLANCACHE/DV_OPT_DECLCACHE；完整run_config与runtime_environment继续留证。两者控制核心显式缓存组，不禁用全局memo，不代替各自dirty/replaycache等开关。
evidence-index 的二进制读取可使用游标解码及精确固定形状节点封包快路；快路必须逐字节核对全部固定标签、长度、键与顺序，变量仍完整解码，异常回退参照解码器。通用格式、损坏格式、整数/浮点/UTF-8/重复键/深度与提交校验不得因快路省略。关闭开关使用原解码器；stdout 投影优化也由相同开关回退。


统一FLAGS含taskgraph/storyflow/stateindex三项，taskgraph默认off、其余默认on，并参与全关/独立消融。run_debug明确设置DV_OPT_STATEINDEX并留证，65核对实际环境与配置。compiled-program v2运行时捕获合同同时要求runtimespeed/stateindex；新增开关不能静默忽略。策略近似与内容重写边界见../docs/cli-optimization.md。

矩阵validation显式关闭taskgraph，独立消融相对生产默认翻转开关；ablation-taskgraph为显式on实验，须有opt_taskgraph=false的完整S1锚点，不能复用旧默认on锚点冒充同条件对照。

## 真实 CLI 窗口取证接口

用户要求真实 CLI 窗口取证时，JPG 来自实际控制台窗口的原生像素截图；SVG、离线画布及文字栅格导出须标明各自来源。运行范围、时限、是否并行与独立审查按当前用户授权执行，本接口说明不授权启动 AI 或盲玩。等待正常输入提示属于命令同步，不表示剧情完成。

`verification/cli_window.py` 以显式 `conhost.exe -ForceNoHandoff --` 启动每例独立控制台、进程、存档和输出目录；不修改系统默认终端。会话使用 120×42 的真实 viewport。后续输入由独立 helper 按 PID AttachConsole、打开 CONIN$ 并发送 WriteConsoleInputW 键盘事件；不使用共享焦点、全局 SendKeys、状态注入或调试入口。窗口 JPG 由 PrintWindow 获取；玩家可见正文由 CONOUT$/ReadConsoleOutputW 原样读取，二者均保留。

公开 `cli-window` 路由将 arguments 的首项作为子命令：`start`、`command`、`capture`、`autonext` 或 `stop`。start 显式提供 --repo、--session、--source-revision、--stage ai|player、--controller、--profile、--zone、--viewer。command 提供 --session、--command-id 和单行 --text；capture 仅取证，可用 --wait-ready 等待已有输入返回正常 dv> 提示。autonext 仅在 stage=ai 的实例运行，显式提供 --run-id 与 --count，每步只发送 next，绝不代选路线或答案。stop 通过正常 quit 封存，保留退出后的实际窗口截图后再释放窗口。

阶段一由内置 AI 自主决策，工具只请求 next。阶段二的盲玩家只依据正常 CLI 可见正文与规则作出输入；不读设计文件、源码内部状态或隐藏结局选择行动；工具只执行 Agent 已选择的正常输入，不另行替玩家作 AI 决策。不同案例不共享进程、stdin、焦点、存档或证据写者。

每次输入前后均保存真实 JPG、实际 stdin、玩家可见 viewport、PID/HWND、现实时间、source_revision 与程序集 SHA-256。每个操作使用单独 RecordWriter 创建的 DVLOG2 文件；不通过重新打开已有 xb 记录器追加。JSON 只保存工程会话清单、路由和进程元数据，不作为新剧情证据。真实事件编号与完成判断由 `verification/audit_cli_evidence.py` 只读核对封存后的原始 CLI trace，不从截图猜测或补造事件编号。

command-id 在同实例内至多投递一次。输入投递异常或等待新 dv> 超时后，保留 partial、pending 与原进程；不重新发送原输入，也不另开替代案例掩盖失败。先使用 capture 等待并记录该在途输入的实际结果，再决定下一条正常输入。每例自身独占锁阻止该实例发生两个并发写入；不同实例不共用此锁。quit 封存超时不自动强杀，也不重复投递 quit。

CLI 原生截图在捕获线程临时启用 per-monitor-v2 DPI awareness，将 GetWindowRect 与 PrintWindow 统一到物理像素坐标，完成后恢复调用线程上下文；不得通过缩放、补画或文字重绘修补裁切。截图元数据记录坐标空间，每个操作另记实际桥接工具 SHA-256，启动清单保留原启动版本。接口与排错见 `../docs/cli-window.md`。

控制台快照允许有限只读重采样，以应对重绘时缓冲区更换；不能重投 stdin。必须在元数据中留下 read_retries/read_stable，持续失败仍报告并保留 pending。

`--opt-evidence-stream on|off` 默认 on，属于 TOOL_FLAGS，只影响离线 raw DAG 读取，并留在 verification_config。开启时将平坦 node items 的解码与原子/向后引用验证合并；精确固定形状以外（含 rational）回退原解码与验证，保留损坏输入的既有错误。帧哈希、有序链、节点编号、事件、checkpoint 与 seal 仍独立核对。已验证连续节点的 offset 索引用八字节无符号数组替代每节点 SQL 行，常驻索引内存为节点数乘八字节；事件唯一性仍由 SQLite 保证，节点正文缓存沿用原双限。关闭 evidence-index 时同时停用此路径。
`dv_level3_capture.gd` 的决策截图只监听正式 dv_action 并在下一真实渲染帧取证；decision-frames.dvlog 保留意图与时间映射，截图不代替 Complete 或章节达成证据。

## 家具样片采集

capture-furnished-rooms打包正式地图的最小资源闭包，避免导入无关UI和翻译资源。只采集地图与独立碰撞夹具；motion.dvlog保留路径、实际终点和校验，不创建正式会话，不授予事实或推进游戏时钟。工具结果与主代理逐图对照分别报告。

## E区域美术库

E扩展采用art_style_trials/build_e_library.py与e_outdoors/extract.py从现役陈设/几何提取，独立Forward+采集由capture_e_library.py执行，固定artifacts/style-e/library-render在旧进程退出后替换。记录来源SHA256与DVSTYLE1二进制观察，不执行Core。各区域模块不制作门；RoomKnowledgeView仅消费本人认知与最后所见静态快照，不读取隐藏实时房间。接口及正式Host未接线范围见../docs/style-e-library.md。
