# 第一章完整历史模型输入就绪修复（2026-10-23）

基于main 6ce6d133，包含主体隔离dirty推理/时钟队列。只修改30通用重演准入、评分和Complete暂缓，以及40历史候选external_inputs声明；未读取设计文件、未改正确答案/候选顺序、未增加320模型预算、未提高profile深度。own_alarm_anchor仍是候选明确假设，不报告为已获事实。

## 实施

候选必须有可执行timeline/predict_laws且外部输入在本人心智中已知无冲突，才进入执行模型。false与其他已知值同样通过已知性检查，不校验预期正确值。空候选目录的原合法factor-only路径保持。准入、人工候选与Complete复用检查；Complete时不再有可执行就绪模型则记replay_deferred，不消耗执行模型预算。未知输入仍进入本人调查探针，完整心智变化使就绪缓存失效；实际重演尝试签名仍禁止相同输入的无效重复。

历史模型明确9项外部refs：pipe_layout、heads_lost、bolt_down、ash_wedge、boots_toward_bolt、cloth_after_boots、slate_marks、slate_person、score_partial。假设和预测状态不回填这些外部收据。未知输入时不把重演评分为可完成验证；候选仍能提供缺失材料的调查需求。

Release LanguageCore构建成功：0警告、0错误，50.03秒。git diff --check通过。

## 本轮300秒Standard乙区正式会话

固定新子任务目录history/；命令为run_debug.py --out artifacts/perf-opt/aggressive/branch-planning/history --timeout 300 --causal-timeout 1 --detective-profile Standard --detective-zone zone_yi。仅一次新会话，未覆盖S2丙丁目录。正式进程169.5秒退出0；并行其他工作存在，不能当作独占180秒性能结论。

本轮只有一次detective.replay正式执行：e2213，游戏591（第1日09:51）。没有再执行V3_0/V3_1两个无计划条目。实际模型计算6个事件：三个切片探测和三个完整候选尝试。

完整历史候选history-case-1在e2218明确得到status=explained、plan_complete=true、stop_reason=declared-plan-complete、failures=[]。它是完整timeline执行，没有causal-refutation切片标签；不能以同轮e2215/e2217/e2219三个切片的explained冒充全模型。

e2218原始封包实测进程耗时7.1949427秒，游戏591；完整模型展开89单位，剩余90。输入来自本人实际收据：pipe_layout@489、heads_lost@546、bolt_down@538、ash_wedge@543、boots_toward_bolt@539、cloth_after_boots@540、slate_marks@488、slate_person@491、score_partial@562；原始actor_before中9项均known=true。未在缺少heads_lost时偷用事实。

七项实际比较全部match，prediction=true、observation=true、confidence=0.5：内闩、楔上烟灰、朝闩靴印、湿布覆盖靴印、石板第一行、石板第二行、乐谱残句。完整模型中社会响应经共享响应模型合法选择accept-huo；随后形成的预测性claim为Huo-relay-and-Su-report、Tube、stressed-tail-without-leading-qualification，置信度0.5。这些仍是模型内预测，未宣称直接恢复昨夜事实。

玩家实际正文第1日09:51显示“重演结果：已有解释与现有观察相符。”四问随后/此前仍由实际答辩接受：e1470@501、e2084@556、e2268@605、e2512@629。本次完整推演结论来自e2218及七项比较，未用四问答对替代。

## 仍不足及边界

不宣称已证明唯一历史。history-case-0（陌生关系）e2216仍因缺少关系响应上下文不完整且与部分观察矛盾；history-case-2在e2220耗尽剩余78单位，总模型预算最终为0。完整成功模型已保留，但竞争候选未完整覆盖，e2225 model_argument_unavailable列出inconsistent/incomplete、budget-exhausted和未计算竞争者。本轮没有model_argument_accepted，后续还反复投影argument_unavailable；未虚写为已建立可传播唯一论证。

本轮达到“一个声明完整、本人输入合法、七项实际比较吻合的历史推演”，尚未达到“全部候选完整排除或唯一解释证明”。未自行增加预算或改排序以隐藏此限制。陌生关系模型的缺失上下文保持未知，没有补造信任值。

侦探实际章末检视e36511、观察e36512@6331已发生；完整公约是否成立仍单独按实际正文判断，不由截止标签推断。

原始DAG有36514个已提交事件、final checkpoint及seal、零未刷尾部，evidence_complete=true、source_unchanged=true、run_config_matches=true、raw_sealing_complete=true。SHA-256=79664108381226ef65b370a651d9ef5099eb232e6039787abadec00ecd61f3ac。原始DAG、完整玩家正文、stdout/stderr、指纹、result.dvlog保留在history/且不提交大证据。

只读因果检查遵照显式causal-timeout=1超时，causal_check_complete=false，run_debug总退出码1；正式进程退出0与领域检查失败分列。尚待新的独立上下文审查；本轮不含Godot/UI/盲玩，也没有专门验证人工输入在排队期间失效的路径。
