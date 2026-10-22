# S5 同源两局只读诊断（2026-10-23）

本审查没有读取设计文件、改源码、运行构建或正式会话，也没有扫描 raw-events.dvdag。先查玩家可见文本及调试索引，再逐帧读取现存 stdout.dvlog 的必要事件投影；小型 evidence-summary 与现存 survival-wiring-review 用于停止边界及原始病程核对来源。所有读取通过 with/生成器正常迭代结束，进程已经退出，无保留句柄。

证据目录：`C:/Users/iii/.codex/worktrees/aggressive-survival-fix/dual_ontology_world_engine/artifacts/perf-opt/aggressive/branch-survival/{Standard,Foolish}`。两局属于冻结73abc65d；已有报告记录source-hashes逐项一致、SHA-256为7d8c2becd0331051a1a7add5af683377ed53b187fa17a88a99f50fde45568c69。测试后HEAD 4b20e226的infer补丁不在这两局证据内。

## 伤亡与停止范围

已有原始DAG复核（survival代理及survival-wiring-review）确认：Standard e32770 / Foolish e32626，time=5040（第4日12:00），Su motor_impairment、serious_injury由false变true且applied；两局16人终态dead/in_coma/stone_stupor均false，Su alive=true，未有另一重伤病程事件。本次独立读取没有重复全DAG核验，因此伤亡字段原始核验属于明确引用的既有核验，不冒称本次新核验。Standard“一人本章重伤、无死”符合这一定义；Foolish“两死”未发生，两局实际全活。

两局debug_end game_time=6331（第5日09:31），停止原因均为objective-chapter-two-closing-record和actual-viewer-closing-inspection，action_bound_reached=false、active=true。它们不是7200时域终点，也不是所有未来危险都不可能发生的证明。

## 实际规程提案和侦探决定

共同applied提案：沈统一烹饪1805（S e5165 / F e4115）；Lucian查验前行动自由2019（e8018/e7123）；Ward公物登记2051（e9013/e8144）、夜间登记2057（e9414/e8549）、公物登记议案2063（e9611/e8746）；Gu封闭议案2051（e9052/e8183）；Veronica实物登记2116（e11275/e10499）；Ruan夜间补记2261（e17979/e16327）、按人配给2275（e18935/e17262）。Mora病人夜间用粮S2283 e19537、F2285 e18077。

直接不同：Foolish有Huo开放议案1990 e6605；Standard没有对应applied提案。因此Standard在2293虽检视open公告e20174，没有实际阅读/决定open；Foolish2294阅读e18611、2295拒绝e18666。

|侦探正式决定|Standard|Foolish|
|---|---|---|
|赞成夜间补记|2285 e19729|2285 e18020|
|公物登记|2289拒绝 e20040|2289赞成 e18404|
|拒绝夜间行动登记|2292 e20143|2292 e18540|
|拒绝封闭议案|2296 e20252|2298 e18762|
|赞成公物登记议案|2299 e20328|2301 e18915|

两局没有detective.c2.color.encourage、color-advice或guard系列的action_execution。规程决定已有差别，但这些差别没有在第四日晚间转成实际劝说或值守回执；不能把“条款不同”直接等同“已经改变夜间监督”。

## Moran本人信息及选择

|本人实际信息/动作|Standard|Foolish|
|---|---|---|
|源质计safe=false、severe_air=true|5343 e33667，执行e33666|5343 e33671，执行e33670|
|门内值守者watched=false|5581 e35145，执行e35144|5581 e35410，执行e35409|
|天气wind=false|5582 e35239，执行e35238|5582 e35516，执行e35515|
|选择stay.unsafe|5582 e35247|5582 e35528|
|实际stay.unsafe applied|5583 e35266|5583 e35552|

safe读数是本人moran.c2.sample.meter在outdoor_meter的perception/direct，time=5343，environment_epoch=2；不是作者世界字段自动授知。watched也是本人门内现场检视的perception/direct，time=5581；wind是本人现场天气观察的perception/direct，time=5582。旧safe=false距选择239分钟；最新wind=false和watched=false都已获得。共同结果不能解释为“莫兰不知道无人看守”。

两局第四日20:32均实际收到教师介绍色层（S e34877/F e35067，沿用现存事件索引定位）。这并没有使侦探同时听见、也没有自动形成监督。

选择前投影的本人评分：5581天气检视根路径4.19（S planning_choice e35187 / F e35457），stop=selective-first-supported-task、complete=false；5582 stay.unsafe根路径0.99（S e35248 / F e35529），stop=selective-current-task、complete=true、completed_layers=1、dependency_expansions=0、best_after_successor_layer=[]。前置planning_yield为S e35246/F e35524，score=.99、plan_expansions=1。选中事件plan_expansions=0；不能据此声称完整枚举go、stay、guard或完整多步比较。

本次现存投影没有给出go等所有未选候选的完整分项评分；未全扫DAG补齐。因此“stay比go具体高多少”尚无本次直接证据。survival静态诊断为stay.unsafe额外promotes measured_work，而go主要promotes research；这属于源码解释线索，不当成原始候选评分实测。

## 直接阻断结果分化的事件与条件

1. 侦探在两局均拒绝seal（e20252/e18762），Standard还缺少实际open提案。规程差异存在，但没有形成不同的夜间安全行动链。
2. 两局均无实际劝说、guard、encourage回执；Moran同在5581亲见watched=false，监督信息完全同向。policy_contrast只读源码核对指出guard导航依赖本人已知safety_notice，侦探未取得该notice；此项是协同源码诊断，尚非本次独立逐前提的原始因果证书。
3. 在共同safe=false、wind=false、watched=false的信息下，两局5582仍同选正值.99的stay.unsafe，5583都实际applied。selective-current-task在已有正值任务停止，不支持“已比较全部危险路线后仍同选”的结论。
4. 没有真实go/夜间写生/搜寻/搬运/实际给药链；急性暴露、昏迷/木僵、呼吸衰竭和死亡的上游危险实际执行未发生。改死亡结算本身无法解释这两局为何会出现两死差异。

因此当前直接观察到的共同断点在机会、通知、本人行动选择与正式现场执行之前。不能据这两局预定任何角色死亡，也不能按profile分配NPC行为。本次没有Godot、碰撞、UI、完整因果工具或新正式会话验收。
