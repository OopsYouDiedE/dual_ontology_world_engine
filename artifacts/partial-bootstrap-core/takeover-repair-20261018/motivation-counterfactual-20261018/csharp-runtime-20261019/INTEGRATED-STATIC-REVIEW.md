# 本批独立静态联审（2026-10-19）

审查身份：独立上下文只读源码审查。已读取根、language_core、verification 的 AGENTS 工程约束；未读取设计文档，未构建、运行探针或启动任何会话。仅新增本报告，未改生产源码。范围为当前 Git diff 中 chapter2/content.dv、dialogue.dv、homecoming.dv、session/10-subjective.dv、30-runner.dv，以及这些改动的直接公共调用。

## 结论

最终收尾复核：可以进入集中工程排错。主代理补入 rank2 的6120时序条件与 Bertrand 真实自述后，本报告发现的即时夜间记忆回退、Mora给药来源缺口已分别在相应窗口/分支接线；先前发现历史保留如下。未发现这些收尾改动的新确定性工程阻塞。此为静态结论，不是编译成功或任何级别游戏验收。

## 修复前发现：认知恢复阻塞（历史，见末尾收尾复核）

**rank2 将第二章可执行主体换回第一章的完整 mind。** 30-runner.dv:162 只在 session_open 调用 dvs_initialize_cognition；10-subjective.dv:796–820 将当时 actor.mind 保存为 first_day_mind。正式入口 game/entry.dv:293 在第一章开会话，302 的第二章扩展沿 live 继续；content.dv:1703 向当前 mind 安装 c2 methods，1747 返回新 mind，但没有同步或建立程序性能力恢复合同。10-subjective.dv:851–855 在 rank>=2 时直接以 first_day_mind 替换全部 mind（仅覆盖 time/revision），还清除实际收闻列表等。

危险到达夹道后 exposure+8；when 只拦 rank3/4，rank2(at4)仍立即成立。因此 Bertrand 下一调度触发此分支，虽然不再 terminal，仍丢失 c2 移动/救援 predict 采纳与本章本人字段、旧位置及观察也回到第一章底本。不能据“还能行动”宣称能救起或逐段搬运。该问题同样不限于 Bertrand，任何达到 rank2 的第二章主体均受影响。首次快照的建立时刻与章节方法安装顺序已静态核实，未运行复现。

需明确记忆截断与程序性能力、当前真实感知的独立合同，并从真实已有来源重建相应范围。不要通过降低剂量、给救援者豁免、替换成预知现场状态或绕过正式行为来掩盖。本报告末次指纹仍覆盖修复前源码；后续修复需另行记录并更新审查范围。
## 修复前发现：资格来源缺口（历史，见末尾收尾复核）

**Bertrand 给药的非目击分支缺少本人可知 illness 来源。** content.dv:954、1553、2010 的 illness/need 只接受 in_coma、fever、rescue_exposed。当前写入中，Bertrand 的 rescue_exposed 仅由实际夹道救起动作设置；其给药前没有 fever 或 in_coma 的真值写入。content.dv:930 的 examine 字段不含 rescue_exposed；10-subjective.dv:1233–1277 的同处观察只取得位置。因此未在夹道目击救起 Complete 的 Mora，后来即使现场查看 Bertrand 的生命/意识/木僵状态，也无法因此知道给药 need。已向主代理报告。可建立真实陈述或可观察症状及其收据；不要通过远端物理读取或把救援历史伪装成可见体征消除缺口。此分支不能据两个给药 action 的存在宣称双方均可治疗。

## 已核对的正式调用与边界

- 给药领取和逐人给药均经 c2_actor_actions 安装；c2_install_actor_laws_into 对应安装 predict/enforce。领取要求实际 white_tower_safe 与已配药，给药要求本人携带、存活病人与真实同处；成功 Complete 才发布各自 anti_erosion_treated。未声明库存或剂量数量，不能推出足量治疗两人或治愈。主代理最后补入的 anti_erosion_mixed 同时存在于 c2i_actions 的 places 与 c2i_goals 的 evidence_places（content.dv:1976、2047），允许未见配药者经现场检视取得来源；没有借此授予远端药物事实。
- Bertrand 的救起只建立 carrying/carried_by；返回为四个正式 move，分别改变两人位置，终段才清空携带关系。普通 Bertrand 移动要求 carrying 为空，两个已知路线函数在携带时取 carry action。目标、动作与方法安装均存在。路径与救援是否实际选择、是否有知情来源触发赴夹道仍须看新轨迹。
- Moran 的新写生 law 同时要求本人可行动、第四夜、实际 outdoor_alley、evening_choice=go；目标也要求 go 并声明 outdoor_alley 导航。没有从谈话或候选直接置完成。
- 危险进入夹道的 Complete 在观察前调用 dvs_environment（30-runner.dv:634）。world_processes 先处理 outdoor-danger，再处理 acute_collapse；危险前提成立时先 +8 后昏迷。下一准备会由 incapacity_when 排除行动。因此这种分支不应被标为已完成随后独立的 sketch experiment。主代理明确要求保留此真实未知，不补画稿旗标。非危险分支是否完成另待轨迹。
- threshold.when 仅在原剂量与 rank 条件成立后以 physical 真值限定新等级；未声明 when 保持原行为。incapacity_when 未知不冒充失能；已声明条件用于真实调度资格。终末/死亡阶段及失能均被 dvs_actor_can_act 排除。
- c2d_action 透传显式 listeners 与 completed_action_requirement；c2d_goals 和通用 dvp 资格均核对已完成动作。真实 received_speech_requirement 仍走实际收闻记录。Mora 证言要求实际 examine.moran 完成及本人死亡命题，不再声称隐含工程剂量或死因；Ward 只承接实际 Mora 证言，没有由这段话直接产生监禁/剥夺效果。
- 印章以 c2.seal holder/location 表示权威保管与藏置；取走、藏琴、取琴、沉水的 effects 同步相应派生标记。取琴 enforce 另核实 holder 为空与 location 为钢琴，现场检查按各 room 拆分，不用一处检查泄露其他藏处。具体行进与发现仍未验证。
- homecoming 的个人表态、动议、汇总与执行分离。c2h_motion_passed_condition 要求真实 consent 标记及当前在场资格，不把缺席自动算同意。c2h_balance_condition 编码 calculators>guardians；最初曾误与旧 summary 的保护门比较，主代理已明确此处为冷酷接管背景门，因此撤回该方向疑点。这是主代理给定接口意图，本审查未读取设计作内容裁定。
- dvs_session_close 现返回完整 actors（含 simulations），没有删去真实模型证据。

## 明确保留的范围与证据限制

动态三槽、完整 A/B/C 材料与危险链、死手、押送及实际离岛尚未实现。死手 action 列表为空；残存 law 声明不等于实际行动发生。方案 A 仅请求、B 的物理材料链与 C 的警报传播后续不能由结果字段证明完整实现。新药物领取与给予 receipt 不等于库存/剂量合同或症状恢复。既有 shura/ascension 相关残存定义也不作为本轮链条完成证据。

本轮未运行一级、二级、三级验收；不对人物合理性、全章完成、五日覆盖、时间改善、Godot 几何/输入/画面作结论。新会话须分别检查前置信息、本人选择、实际执行、现场收据、后续症状和死亡/木僵，不以声明、汇总或耗时替代。

## 首轮审查源码指纹（SHA-256，修复前历史）

| 文件 | SHA-256 |
| --- | --- |
| chapter2/content.dv | 6427F6F175C43CEE34A31EBDD83C0B45C7E6128C3ED9173837F36F03336F0650 |
| chapter2/dialogue.dv | 13D830B427FAD8B87F82DFACC62F54F98038C33F4B8267A65FF500692E61EA28 |
| chapter2/homecoming.dv | B7CF7EAE621FDE085291A68931CDF5140D5612F059EA6134536A2B0DB45A8369 |
| session/10-subjective.dv | 7D90AE570A43BC2DB6D80C97F6E4F9BD786ABD4F01254905644A8B4D6D1F732F |
| session/30-runner.dv | 99E03065F5B5CCA67CDF2E8EF2912B70ECBB16EF44CF7E994BA9404A87CD29A1 |

以上 chapter2 路径位于 language_core/verification/scenarios/；session 路径位于 language_core/verification/。后续生产源码若有修改，须按新指纹重新限定本报告覆盖范围。


## 收尾修复的最终静态复核

已复读最终 content.dv:2109–2113 与 dialogue.dv:165–169 及其 action/goal、真实聆听调用。没有再启动工程检查或会话。

1. rank2 仍要求 at=4，但另要求物理 c2_clock>=6120；rank1仍即时，rank3/4仍核实际 stone_stupor/dead。dvs_erosion_update 的通用 when 分支实际读取 physical，参数传入与 law 构造匹配。因此第四夜危险接触不会立即触发 rank2 的整脑回退；独立 acute_collapse 仍可使 Moran 当场失能，Bertrand 没有个人豁免。主代理将此明确裁定为本批临床时序行为条件修正，不是纯计算等价、更不是生理参数校准或性能证据。
2. 6120以后 rank2 的 first_day_mind 整体回退语义仍然存在；本次没有建立通用程序性能力/当前感知保留机制。它不再阻断6120以前的救援窗口，但不能据本次修复宣称次晨后所有认知或行动继续正常。若新轨迹触及该阶段，须核实际失能、记忆与可执行方法；受药物保护也不自动取消记忆截断。
3. Bertrand 的新 rescue.report 在 living_hall，literal=rescue_exposed，when=c2_can_act；要求 Mora 真实同处、listeners仅Mora，并要求已完成 rescue.moran_from_alley。c2d_goals同时核literal、when和completed_action_requirement；c2d_action透传后通用资格再核完成记录。话语没有用 effects 直接写 Mora 认识。20-speech 的真实 listener_parse 从正向 equals literal生成 testimony，再按本人信任与观察门槛处理，Mora才可能获得给药need。因而原“未目击夹道救起的Mora无信息来源”已有实际自述路径；是否发言、真实听见和采信仍待新轨迹，不能写成必然给药。
4. 该自述没有声称精确吸入剂量、病理诊断或治愈；examine仍不公开救援历史。尚未实现的支线、危险写生是否完成、药品库存/剂量合同及所有验收限制继续保留。

最终建议：进入集中语法、构建及必要中性工程排错；之后按用户授权冻结正式窗口。没有依据本次静读宣称剧情达成、时间改善或完整游戏验收。

### 收尾末次指纹（SHA-256）

| 文件 | SHA-256 |
| --- | --- |
| chapter2/content.dv | FD2F5C550E8233EDE501CFEBFDD172A1E0517BB960EC4514EDCE573F1624A8DC |
| chapter2/dialogue.dv | 71ADEB76342D6B385B3694BE08A5B30D0B6F189E99FB0887B4B8BDBFFA870608 |
| chapter2/homecoming.dv | B7CF7EAE621FDE085291A68931CDF5140D5612F059EA6134536A2B0DB45A8369 |
| session/10-subjective.dv | 7D90AE570A43BC2DB6D80C97F6E4F9BD786ABD4F01254905644A8B4D6D1F732F |
| session/30-runner.dv | 99E03065F5B5CCA67CDF2E8EF2912B70ECBB16EF44CF7E994BA9404A87CD29A1 |
