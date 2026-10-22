# 独立规划分支诊断

分支 opt/fix-planning，从 77e31772 创建。仅修改 30 的通用搜索、60 配置和 CLI 默认值及工程合同；没有修改 40/50 内容、oracle、权威判定或人物信任参数。没有读取设计文件。两局采用 Standard、全部优化打开、统一 depth=3、Top-K=3、300 秒上限，并行运行，期间其他分支也有任务；不能作为独占耗时比较。

## 修改

1. --plan-depth 和 prune_depth 默认从 1 改为 3，所有人物使用同一上限，仍受本人原规划参数约束。不是人物白名单。
2. Top-K 入列前检查本人是否真的持有 active/predict 采纳。此前只要动作声明 predict_law 就先占据 Top-K 槽位，到 successor 才发现没有可执行预测；现在这些不可展开项不挤掉可展开的候选。顺序和评分仍是本人原值，正式后继预测与 Prepare/Complete 不变。

Release 解决方案构建成功，0 警告、0 错误，60.29 秒；git diff --check 无错误。

## 实际结果与限制

两局在 game_time=6330 发生议事截止/震动节点，并生成 debug_end 及封存 DAG。S2-bing 的 c2.daily.council-close 为 e33693，真实进程事件耗时 211.994 秒；S2-ding 为 e33619，213.969 秒。65 运行器在 council_closed 后主动停止，actual-chapter-two-closing-record 是该停止逻辑的标签，不能自证剧情结局。完整公约是否成立、玩家是否实际取得章末观察尚须分别核验，本报告不宣称两章验收成功。

但提高规划深度并没有修复两种第一章内容结果：

- 丙区：侦探只有身份提交接受 e1526@674。没有完整理由提交/接受；章间内容交接 e2537@1800。
- 丁区：侦探始终没有 answer_submitted/answer_accepted；章间内容交接 e2511@1800。

这是未解决项，不能写为本分支已经满足 S2。Standard 乙四问的既有 depth=2 证据见主任务 hypothesis-prune；本分支没有新跑乙局，不能借旧局声称当前分支乙场景已验收。

## 丙区实际阻断条件

本轮 e860（489）是伊莲娜实际听者解析。她确实收到侦探关于 gesture_examined 的证词，trust=0.45；原始 mind_after 明确为 gesture_examined: known=false, value=true, confidence=0.45, conflicted=false。e887/e945 随后停止。不是没有唤醒：认知重新经过，输入存在，但没有成为本人采纳的事实。

40 的 c1_other_truth_law(astra[l], elena_identified) 要求该主体本人已知前一步 gesture_examined；30 的正式资格仍调用 dv_truth 检查。增加深度不能把 known=false 改成 true。不能通过降低听者阈值或跳过此前提来宣布恢复，也不能把收到句子当成相信句子。需要内容层提供可正式执行的展示/共同检视或另一条实际可知来源，或由用户决定修改人物认知设定；本分支未自行补设。

## 丁区实际阻断条件

本轮 e790（487）侦探 mind_after 已有 veronica_explained=true 和 reason.wound_cleaning=true，confidence=0.55；因此其停顿不能归为“没推得救治用途”。但同一原始心智中 **不存在 material.physician_permission 单元格，也不存在 answer.veronica**，report_basis 在该时刻仍为 false。e793 的 admitted 候选仅为查看在场人物/重问公开题目，所有本人分数均 -0.01、forecasts=0；不是 Top-K 把一个合法高价值提交淘汰。

40 的 c1_other_material_identity(logistics, veronica) 明确同时要求 reported_taker、reported_reagent、acid_blisters 和 physician_permission=false。c1_other_install_material_actor 只把本人未开医嘱的事实放进 mora 的心智；报告动作的 materials=[]，其 speech literal 是 dv_true，未把该具体事实作为显式证词或 public_grants 交给侦探。侦探因此不能完成材料身份推导，也不能合法提交身份；有后续救治原因不替代身份提交的独立前提。

没有向侦探静默授予 physician_permission，也没有从展示文本擅自解析补真相。这个可知路径需要内容声明修复，不在本分支允许修改的 30/60/CLI 范围内。

## 基线与归因边界

上述阻断条件在主任务 depth=2 证据和本分支 depth=3 实际证据中一致。它们不是仅提高深度可修复的问题。但尚未对丙/丁执行全部优化关闭的同源正式基线，不能把它们直接标记成“基线也未达成”。本报告只给出实际缺失前提及声明来源，不越过这条证据边界。

原始证据全部留在本工作树 S2-bing/、S2-ding/，包含 stdout/stderr、带校验 DAG、索引、正文及来源指纹；不提交大文件。该目录的新输出没有覆盖或搬用主任务旧证据。

## 接续核对（2026-10-22）

接续时两份 run_debug 及只读因果检查进程仍在运行，未启动重复会话、未清理或覆盖其输出。接续审读确认 Top-K 的采纳检查读取当前搜索节点的 mind，与 dvp_selective_successor 的本人采纳条件一致；未新增角色特判或读取 detective_profile。

两份 raw-dag-summary.json 均记录 final checkpoint、seal、零残尾及 complete_sealing=true；丙区 33700 个已提交事件，丁区 33626 个。原始文件 SHA-256 分别为 49303c660fc9ba997aebf21ffafca84990a6e7771881accc82436203a1611745 与 6a13e2fd832e21a2553dc971fffe89ab7eac06fe3f33a9774c319a56cdd8672a。玩家可见完整正文和调试正文在各自目录保留。

最终进程已不在运行，但两份 causal-check-stderr.txt 都只到 domain-check-start，causal-check.dvlog 为零字节，未发现最终汇总文件。因此只读领域因果检查没有可用完成记录，原因未确认，不宣称整个 run_debug 成功退出或领域验收通过。原始正式会话封存完整与此检查未完成分别报告。未重复运行测试。

## 丙/丁正式获取路径只读定位

未读取设计文件，未修改剧情，未追加会话。丙区已有侦探检视完整草图动作（40:3865）；本轮 e805 实际执行、e806 观察。其声明只给侦探本人 grants=[gesture_examined]，没有 public_observers/public_grants。询问伊莲娜（40:3763附近）把前一步 gesture_examined 作为 speech.literal_ast，并明确 listeners=[elena]；e856 执行、e858 发言、e860 伊莲娜接收均发生。但提问没有声明共享检视，伊莲娜仅按证词信任0.45更新，仍 known=false。她自己的回想动作 law.premise 要求本人已知前一步（40:3838–3891），所以不能执行，随后 e887/e945 停止。不是通用接收漏调用，也不是将侦探本人已知误用为NPC已知。现有声明缺少目标本人对草图的直接检视/共同展示合同；把所有请求自动升为可靠观察会越过证词与感知边界。

丁区 physician_permission=false 仅初始化在客观材料（40:3420）和茉拉本人心智（40:3532），没有任何 row.materials 包含该字段，也无其他该字段的观察/证词声明。茉拉 report_basis（40:3474）实际在 e1427 执行，侦探 e1429 取得公共观察，说明通用公共观察接线有效；该行 materials=[]，生成 grants 只有 report_basis 与 clock，故不能取得医嘱事实。发言 e1433 的 literal=dv_true，listeners 也只含本区原成员，没有侦探。即使把侦探补入 listeners，仍只收到真命题，无法获得 physician_permission。此处是内容声明未表达正文里医嘱断言的缺口，而非漏传播现有明确事实。

最小建议：通用层保持当前显式 grants 和证词可信度合同；内容层需授权后为丙区定义真实展示/共同检视的对象、接触资格和字段，为丁区把现有医嘱断言明确表达为可收闻证词或可检视记录。不能通用地从自然正文、法则前提或作者客观状态推生这些事实；仅修30规划无法补足。上述是现有源码工程路径定位，不是设计修订授权。
