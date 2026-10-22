# 当前整合版 Standard 正式会话独立审查

审查对象：`S1`，任务标识 179340bd；只读当前输出、事件索引和封存摘要，未读取设计正文、世界源码或原始 DAG，未执行新会话。执行器为 formal-model，geometry=false；以下不含 Godot 几何、碰撞、输入或 UI 验收。

## 后续复核更正

本报告关于“完整正确推演不能确证”的判断已被独立复核纠正：e2295（history-case-1）为完整 declared-plan，七项观察全部 match、24阶段完成，实际现实时间5.815793秒。详见 [重演复核](replay-regression-review.md)。下面保留原审查过程以说明错误来源；其余第二章未到达、未值守和未治疗结论不因此改变。

## 结论

本轮第一章四问都实际被接受，636 分钟实际跨出隔离门。第二章到世界时域 7200 为止，没有侦探本人章末行动。实际状态中苏泠由既存侵蚀病进展为严重虚弱、运动障碍和重伤；16 名具有生命状态的参与者均存活，无已记录死亡。莫兰没有一直留守：5588 离开起居厅、5590 到仪器阵，随后反复采样；未记录进入罕见色层夹道、夹道伤亡或被值守者拦回。侦探提出并采纳复测约定属实，但莫兰实际检视回执 watched=false，不能把说过“我愿值守”写成值守已经生效。

## 第一章：推演、检视、答辩与交接

现实耗时直接取 raw-event-index.dvlog 的 process_elapsed_ms / 1000，是本进程实测累计值；不由游戏时间换算，不含先前构建。

|节点|事件|游戏分钟|现实秒|
|---|---|---:|---:|
|首次实际检视：风衣|e541|483|3.248717|
|首次关键痕迹检视：石板|e852|488|3.600181|
|第一问接受：闩门的是我|e1547|501|4.266511|
|第二问接受：墙内与门框管道漏音|e2161|556|5.210540|
|单项重演解释石板第一行|e2292|591|5.785222|
|第三问接受：原话是闩门禁开|e2347|605|5.982753|
|第四问接受：因早已相熟而信霍铎|e2591|629|6.486394|
|实际跨出隔离门|e2758|636|6.979957|

四个 answer_accepted 都记录 accepted=true、literal_truth=true、epistemic_support=true；不是仅有候选或打开答题界面。e2758 为 detective.departure.cross-out、status=applied，先有 e2716 拔内闩、e2731 开门。此为逻辑现场完成，不证明宿主真实碰撞。

推演需限制措辞：591 的 e2292 仅在“石板第一行刻痕”这一特征上 explained / declared-plan-complete / plan_complete=true。随后同假说的全特征比较 e2293 为 inconsistent / declared-plan-incomplete：内闩、靴印、湿布、第二行、乐谱有 contradiction。它不能证明完整正确重演。其余 mind_simulation 如下：

|事件|假说|结果|停止原因|计划完成|
|---|---|---|---|---|
|e2292|history-case-0|explained|declared-plan-complete|True|
|e2293|history-case-0|inconsistent|declared-plan-incomplete|False|
|e2295|history-case-1|explained|declared-plan-complete|True|
|e2297|history-case-2|inconsistent|declared-plan-incomplete|False|
|e2299|history-case-3|unknown|budget-exhausted|False|

玩家可见正文在09:51写“已有解释与现有观察相符”，但小证据同时保留上述不一致与预算耗尽结果。完整正确推演节点本轮不能据此确证；四问正式接受是另一条已经发生的证据。

## 实际伤亡链

debug_end.stop_evidence.injury_facts 给出苏泠 active_erosion_disease=true，时间0；这项是既存状态，不能归因于本轮外出。e30499 在4320（第4日00:00，现实92.760791秒）提交 physiology.su.weakness；e32523 在5040（第4日12:00，现实99.980268秒）提交 physiology.su.impairment。结束状态 serious_injury=true、motor_impairment=true（均5040），severe_weakness=true（4320），anti_erosion_treated=false，respiratory_failure=false，in_coma=false，stone_stupor=false，alive=true、dead=false，erosion_exposure=0。

这是已记录的既存病程恶化链；本轮没有把重伤写成新增夹道暴露的证据。苏泠5392采纳复测约定后，5394从起居厅去中央厅的行动 e34323 status=rejected；它未成功改变位置。仅凭该回执不能进一步断言拒绝的全部机制。结束时除苏泠外未记录其他人的 serious_injury=true；16名生命状态已知的参与者均 alive=true、dead=false，代管者生命状态未知。

## 莫兰是否留守，侦探有没有实际值守

|游戏分钟|事件|实际行为|
|---:|---|---|
|5378–5381|e34094 / e34137 / e34147 / e34158|侦探提出、张贴、阅读、赞成震后复测与返程约定|
|5383–5390|e34170 / e34187 / e34206 / e34215 / e34234|侦探去仪器阵采样后返回起居厅|
|5581|e35055|莫兰提出沿夹道外出写生|
|5582|e35197 / e35198|莫兰查看门内值守者，自己的实测观察 watched=false|
|5583–5586|e35244 / e35309 / e35324 / e35354|莫兰检视、阅读、赞成复测约定并观察天气|
|5588 / 5590|e35374 / e35384|莫兰离开起居厅，经中央厅到仪器阵|
|5591|e35393|莫兰开始实测源质计|
|6122 / 6124|e36011 / e36062|侦探离开起居厅到中央厅、仪器阵|
|6125–6152|e36097–e36991|侦探连续28次实测源质计|
|6232|e38477|莫兰最后一次实测源质计|

莫兰离开起居厅后的实际路线到仪器阵。该段不支持“莫兰留在起居厅”，也不支持“他进入夹道后被救回”。侦探有提议、公告、赞成和实际采样，却没有本轮实际值守生效回执；watched=false是莫兰在5582实际取得的观察，不是审查者按站位推断。莫兰这段每步规划记录 selective-first-supported-task / selective-current-task，5585看天气评分4.19，5586与5588移动评分0.98；5590选择采样评分1.04，5591选择等待为 selective-declared-wait。未把未展开后继解释为完整规划。

## 章末为什么本人没去：已记录与未知

可直接观察的行为是侦探6124进入仪器阵，6125至6152连续采样；加上5386一次，本轮共29次采样。e36083（6124）起选择采样：score=12.590000000000002，plan_expansions=1，stop=selective-first-supported-task，root_best.complete=false，completed_layers=0。到最后一次选择 e36974（6151）仍为同一停止原因、同一评分、complete=false；其后 e36991（6152，现实117.382599秒）采样成功。后面没有新的侦探行动。

6330 的 e39186 议会关闭和 e39187 地下震动已发生（现实124.114718 / 124.115396秒）。结束摘要 council_closed=true、council_ended=false，侦探 signed=false、voted=false；不能把世界事件或别人投票当作侦探本人章末到达。停止时侦探 action_quota=0、scheduled_action=false、can_act=true，reason_budget=281、model_budget=0、planning_limit=320；莫兰 action_quota同为0，scheduled_action=false、can_act=true。

据此可确认“反复采样直到侦探没有动作配额，未记录本人章末动作”。这里不能替未记录的决策补出“他选择拒绝章末”“章末没有价值”“路线不可达”等原因，也不能证明完整搜索曾比较并淘汰章末路线。配额0是实际终态限制，采样的选择性停止回执是先前实际规划证据；二者不自动解释每一个未执行行动的因果。

## 结束与证据范围

result.dvlog 报告执行134.141秒，exit_code=0，source_unchanged=true，raw_sealing_complete=true，40672个原始事件，raw_committed_events_not_yet_projected=0；debug_end在7200、active=false、action_bound_reached=false。stop_reasons同时列 objective-chapter-two-closing-record 与 world-horizon，但本人没有closing行动，因此不能按停止原因字符串宣布本人章末达成。原始封存完成与剧情达成分开。

本轮后处理 causal_check_status=timeout、main_causal_supported=false，不能宣布权威因果检查通过。审查只用小型事件投影与索引交叉核对，没有展开原始大DAG；其完整字段、设计符合性、实现代码根因、真实碰撞和界面效果未在本审查范围。
