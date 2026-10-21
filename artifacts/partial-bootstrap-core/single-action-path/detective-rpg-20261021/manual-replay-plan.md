# 人工选择重演：实现与静态复核

2026-10-21。主代理先授权仅准备方案，后在隔离性能测量后授权落地共享源码。未读取项目设计文件；设计取舍与 V6 开放规则来自主代理明确消息。保留原有工作区改动。未构建、未运行正式会话、未运行 Godot 或流程回归。

## 已实施接口

- 继续使用唯一 Prepare → 现场执行/回执 → Complete。没有新增常驻 actions，没有改动 AI 候选数量、模型预算、重演深度或法则。
- 非重演动作继续传裸 action ID；人工重演传 JSON 字符串 `{action_id,hypothesis_id}`。JSON 仅作进程内传输，原始输入仍由现有二进制证据记录。
- InvestigationUI 只从 DV 提供的公开变量、标签、候选 ID 与赋值关系构造表单。各下拉框第一项为“未选择”，提交按钮待全部显式选择后启用。已有具名假设使用独立下拉框，同样不预选。
- UISystem 和 WorldView.spatial.select 从封装提取原 action_id，对当前已提供选项、可用性、自身/现场与接触资格重核；排队和 `_submit` 保留完整封装。Main 与 GameEngine 原有纯字符串转交链足够，未改这两个文件。
- Session.Prepare 只在字符串以 `{` 开头时解析为 DV 纯数据，仍调用同一 SelectPlayer。DV 再从本人 policy.hypotheses 查找候选，重核 topic、非空 basis、available_when、manual_available_when，再以原 action.id 的临时副本携带 replay_hypothesis_id。
- Complete 对所选候选再次重核。重演期间资格变化时记录 manual_replay_unavailable，不执行旧候选。人工分支不运行全域 factor_prune、全组 coverage 或其他候选；AI 无 replay_hypothesis_id，继续原批量分支。
- 同一动作不同假设使用独立尝试键；人工签名带本人认识修订，重复同一输入不会封死其他假设。投影通过同一动作准入过滤已尝试候选，DV 提交仍再次核验。

## V6 边界

主代理裁定：前五变量初始隐含“四人素不相识”；这是一项尚未查明的初始假设，不是已知事实，也不代表正确答案。

内容层 `c1_manual_history_gate` 使用本人 familiar 真/假命题可判定，或 cups_preferences、starter_layers、recalled_names 中至少两项已知为真开放 V6。没有读取物理世界或 oracle。V6 未开放时仅投影 V1–V5，候选 metadata 仅含 familiar=false 的既有候选并去掉 V6 显式赋值，界面说明“当前暂按彼此陌生推演，尚未查明”。V6 开放后显示两种可选假设值，仍从“未选择”开始。

旧具名 V6_0/V6_1 也附加相同人工开放条件，避免从具名候选绕过变量门。所有这些条件只约束人工投影/人工选取，不改 AI 原 available_when。

## 涉及文件及分工

- language_core/30-session.dv：人工封装/候选核验助手、候选专用重演签名、Complete 单选分支。保留另一代理新增的 manual_contact_retries 与接触失败处理。
- language_core/40-chapter1.dv：公开变量字典、V6 门、候选人工条件。保留比较投影代理新增的 feature 公开标签与白名单。
- language_core/60-game.dv：Prepare 选项的 replay_input 元数据、公开标题助手、最新人工重演优先展示。保留比较代理新增的 readable_comparisons、rows.comparisons 与 observation_action_ids。
- language_core/Session/Session.cs：Prepare 边界解析。
- godot_csharp/scenes/UI/InvestigationUI.gd：显式假设与变量表单。
- godot_csharp/scenes/UI/UISystem.gd：以原动作重核、保留完整 payload。
- godot_csharp/scenes/Shared/Field/WorldView.gd：仅 spatial.select / _submit 的封装传递；保留宿主代理的近门输入、提示与热点修改。

工程合同文档由主代理统一整合，本代理未修改 AGENTS/README。manual-replay.patch 是最初未应用提案；manual-replay-v2.patch 记录落地初版，之后有纯静态修正。当前源码及本报告为现行状态，两个 patch 不是最终源码快照。

## 已核对与尚未覆盖

已逐段静态复读整个选择链；确认 Main 将 choice_id 原字符串交 WorldView，GameEngine 原样交 Prepare；确认正式 action.id、现场回执标识与费用模型不变。已静态确认候选元数据投影不运行模型、不开 oracle、不写本人认识，并避免按每个候选再次线性扫描全候选的二次查找。`git diff --check` 在本轮涉及文件未报告空白错误；Git 另提示 60-game.dv 的 CRLF 将按仓库策略归一为 LF。

未验证编译器/GDScript 语法、控件尺寸与遮挡、实际点击、真实付费与观察结果、存档恢复及正式完整会话。新的人工控制方式与跳过全域裁剪改变了人工操作机制，不能作为 AI 同条件纯计算提速结论。重复重演、认识更新后重演、非法候选、过期候选及 V6 前后开放的实际行为需在主代理授权的集中工程排错和新会话中核查。

## 本批冻结时的具体边界

主代理要求本批停止生产修改并统一构建；本代理已停止。以下为静态发现，保留给主代理决定后续修复批次：

- 现役六域是 3×3×2×2×3×2，共 216 种组合；V6 尚隐含 false 时最多 108 个 cases，每个保留五项公开赋值；开放后最多 216 个 cases，每个六项。实际投影还受 topic、候选与动作准入、尝试签名过滤。没有创建对应数量的 actions，但元数据仍增加 JSON 编码、Godot 物化、字典复制与驻留内存；没有实测大小或耗时。
- `replay_input` 位于 awaiting_input intent.options，snapshot.pending 保存同一 intent。因此 Session 虽可复用同一次转换内的 DV intent，宿主仍会把这些 cases 作为 intent 与 snapshot 的一部分独立物化；UISystem 还对 options 和弹窗数据做深复制。候选准入投影避免逐候选再次查找全表，但每个候选仍执行公共 eligibility。不能把“无模拟”表述为“无投影成本”。
- 表单生成时提供中/英/日“未选择”和支付按钮文案；这些新 OptionButton 项与 Button 没有登记既有 `_ui_texts` 动态刷新集合。已打开表单期间切语言不会即时刷新它们，重新打开会按当前语言生成。内容提供的假设与变量标签保持原始中文正文，不应由宿主改写。
- 普通提交仍经 `_choose` 的 content_serial 和 can_interact 校验；先调用 close() 锁控件，再发 action_chosen，UISystem 等 modal_cycle 与屏幕过渡释放后只发送一次 pending_action；WorldView 再查当前现场并设置 submitted。关闭、内容重建或过期回调路径沿用现有机制，没有新增行动分派入口。
- 新 OptionButton 自带 PopupMenu 的焦点、父弹窗关闭时的退场、Escape、语言切换期间已展开菜单、重复点击和输入穿透尚无 UI 实测。重新打开表单会清空临时所选值；本批没有持久化草稿选择。
- 裸 ID 对非重演动作兼容；人工裸 replay ID 现在要求先选假设并被 DV 拒绝。AI 继续由自身选择动作，不调用这条人工选择入口。
