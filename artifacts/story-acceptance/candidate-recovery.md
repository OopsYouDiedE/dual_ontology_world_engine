# 候选分支恢复审计（2026-10-23）

只读核对 Git、已有正式记录及源码差异；未读取设计文件、未运行游戏/测试、未修改分支。此报告不是新会话验收。

## 结论

现有候选中没有可以凭现存证据直接宣告开仓、成功救治或 S5 通过的版本。主 acceptance HEAD 78c74a37 在 a2db464d 上只新增 CLI 捕获/显式观察者固定与审计工具，没有并入这些故事候选。所有旧候选基于 767d46a6 或更早分支；整体合并/覆盖会带回 taskgraph 默认开启及旧工具版本，必须选择性迁移差异。

## granary2：b40e5332，工作区源码干净

分支独有提交：cec8898c（重检与撤回），92384dc1（留在现场及重试请求），3790f0be（条件发言与真实撤回回执分离），b40e5332（失败记录）。只涉及 50 内容；较 a2 的其他倒退属于缺少 a2 更新，不应合入。

最新实际证据仍是 cec8898c；50 SHA256 379e45e4e15d52588f8f942f1f7b9f5cfff4a18ea2b9a0e195e3664fb4866562，当前 HEAD 文件为 41d8239ebedfee09cf0eb1495a2ce9eb9bea30cfe22c4024803406ba8a027282，二者不同。后两项内容修正尚无对应正式复测。

固定证据：C:/Users/iii/Desktop/dual_ontology_granary2/artifacts/perf-opt/aggressive/branch-granary-candidate。
旧局清册拼合 @2171，但无沃德撤回、雀儿接收、真实开仓。分支审计记录 e13987@2174 为 Ward speech_unheard/no-audibility-grant；e11384@2136、e11471@2137 的 joined_inventory 仍 false。实际章末 e37547@6333；完整封存。引擎218.141秒，后处理99.640秒，分支记录整命令373.673秒（重叠诊断）。

建议：后续 92384dc1/3790f0be 可作为待验证修复移植；不能以旧局章末证明开仓已修好。此前删除受阻导致未重跑，与源码修复是否有效是两件事。

## survival_candidate：767d46a6 + 未提交 50 修改

25行未提交新增内容：茉拉实际读取记录、两位医务人员接触检视/留在配药地点目标、维罗妮卡自行中和抽屉。正式 Standard/Foolish 两局确实运行了这份未提交文件，50 SHA256均为863958bbd89597fcfde98c7e4be8136648324e7349de4324a36a44ca88d51ac0，与当前文件匹配。不是纯767d46a6测试。

证据根：C:/Users/iii/Desktop/dual_ontology_survival_candidate/artifacts/perf-opt/aggressive/branch-survival-candidate。

|实际步骤|Standard|Foolish|
|---|---|---|
|读取R07|e13211 @2162|e12763 @2162|
|出示旧疤|e14432 @2182|e14161 @2182|
|道歉交钥匙|e16623 @2223|e16759 @2223|
|备药|e17537 @2240|e17839 @2240|
|制成药|e18649 @2259|e19117 @2259|
|实际取药|e19326 @2278|e20007 @2278|
|给苏用药|e19982 @2283 **rejected**|e20649 @2283 **rejected**|
|实际侦探章末|e37318 @6333|e38249 @6333|

以上 action_execution 和 stderr 已读取核对。两局完整封存、source_unchanged=true；结束汇总所有人的 alive 均 true，没有两个死者。整命令224.868/226.644秒，引擎150.219/152.500秒，后处理57.125/56.859秒；并发诊断，wrapper返回1。不能把制成药/取到药当作成功治疗。两种策略都成功制药还意味着所需策略因果差异需要另查。

建议：这份未提交修改有明确局部行为进展，值得迁移作候选，但仍须修给药拒绝并检查对照因果，不能按“救治已成功”合并宣布完成。应先保存补丁及指纹，避免清理工作区丢失它。

## replay：02831e36 + 未提交30及planning_evidence_check修改

02831e36修防导航重复领取已完成目标。未提交修改继续排除navigation_intention、按原始知识判断完成、保留当前位置任务意向/maintain_presence、移动本身不取收益；这些属于仍在推进的实验任务图。

现存 branch-taskgraph 记录：引擎300.484秒，后处理100.297秒；未封存完整，仅raw-events.dvdag.partial，stop_reasons为空。最后stderr e44573，gu.c2.sample.meter @3721，无侦探章末。测试30指纹c60f2ec0742bad92ef6fb8180b36c050c2c6ded19d28a1ccd99237354d6fa5d8，与当前未提交文件不同，因此不能把该局结果归到最新未提交代码，也没有新代码成功证据。

建议：保持任务图默认关闭；不选为验收基线。保留未提交差异，避免误清理。

## selectivefast：6f10a2f1，源码干净

f77f18bd引入独立 opt-selectivefast（原生默认off，但其旧run_debug默认on，存在入口差异）；6f10a2f1记录对照。工程仅优化归一化key构建/必要条件短路，无20/40/50内容变化。

selective-candidate/report.md记录：on/off两局均6333到末检视、37060事件、实际action stderr及完整可见正文逐字节一致。整命令377.340/229.785秒、引擎206.765/143.656秒，负载/构建复用不同，不支持速度收益。两局只读causal check 30秒超时，wrapper返回1。没有UI/剧情独立验收。

建议：不作为速度胜出方案；保留关闭。即使迁移也必须同时统一入口默认值并保留a2的taskgraph-off和工具更新。

## 移植顺序建议

优先保存并审阅 survival 未提交补丁及 granary 后续提交，按文件块迁移到新验收分支；二者都改50，需要人工整合。先做剧情实测再谈性能。replay/selectivefast保持隔离，不要让优化实验改变当前验收基线。当前恢复工作没有提供截图或渲染通过证据，也没有代替实际图形游戏与LLM盲玩。

## 追加：2cf698eb 开仓撤回机制源码复核

本节仅审阅已整合候选与通用 Complete/投影机制，未执行测试。

**未发现失败执行仍发布撤回、或跨房间自动授予撤回事实的问题。**

- 50:2796–2797 的物理法则要求 Ward 在 document_workshop、存活、真实 joined_inventory=true、lock_no_pry=true，才写入本人 restriction_withdrawn=true。50:2300将该 premise作为 authority_precondition；通用动作包装50:1247再补 c2_can_act（未昏迷/木僵）。50:2284安装具名法则。
- 30:8039之后先检验宿主applied、真实authority_precondition、本人speech可说性，再正式执行法则。dv_true speech只解除“尚未撤回却要求先知道已经撤回”的自锁，并不会单独产生撤回事实。
- 30:8241和8243均严格要求execution.status=applied，才分别发布现场公共观察和speech/public_grants。失败只记录action_execution rejected；30:8848也要求applied and visible才投影surface。
- 30:1894 visible_action_grants要求实际同房及visual_identity_when；本动作不是move，因此不走离场例外。30:8351 public_grants要求listeners内且dvs_speech_hearing；30:3903为同房或显式hearing_grants，而非campaign_people无条件全局广播。成功后取真实physical字段，不从speech dv_true推导“已撤回”。
- 50:2395–2405 的贡献后留场/重复检视/撤回目标使用本人completed_actions与本人知识；尚未见静态必然不可达。调度是否能实际选中与听见仍需新会话证明。

**发现一处明确呈现缺陷：撤回台词被外层surface遮蔽。**
50:2299 speech.surface是“我在现场重新核对了……我撤回指控……”，但50:2300 campaign_surface_action另设action.surface为“当面复核清册和锁具；材料核清后更正指控记录。”；30:8849优先action.surface，只有不存在时才用speech.surface。因此标准会话presentation成功时仍显示条件式描述（且因有speech被标成dialogue），真正明确的撤回句不在主surface显示。底层撤回与现场事实可能已成立，玩家却只能看到“核清后更正”的含糊话。建议将成功action.surface与成功撤回台词对齐（失败已经由通用applied gate抑制），或显式区分动作描述和成功speech显示。这是具体显示缺陷，不是失败泄漏，也不等于已验证运行阻塞。
