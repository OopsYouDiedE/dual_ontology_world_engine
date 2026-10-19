# 因果修复集成只读静态复核

2026-10-19，独立上下文审查。未读取设计文档，未构建、运行探针或会话，未改源码；本报告不评价设计取舍，不建立任何游戏验收结论。content尚待主代理冻结通知，本版先记录dialogue/homecoming及共同资格路径；未完成冻结审查前不得将本报告当作完整集成结论。

## 读取版本

| 文件 | 首读SHA-256 |
| --- | --- |
| chapter2/dialogue.dv | ED74AA29E4A34CA6C0F6F731D77CF283917C37401C7ADD185E4FCA646860CB0D |
| chapter2/homecoming.dv | B7CF7EAE621FDE085291A68931CDF5140D5612F059EA6134536A2B0DB45A8369 |

路径均相对 `language_core/verification/scenarios/`。后文行号对应读取版本。三文件最后指纹与content冻结审查待补。

## 共同speech与成功执行路径

dialogue:326–391 的 `c2d_action` 构造本人precondition（地点、when、literal、未说过），physical authority前提（地点、when），标准speech.literal_ast、surface、listeners、law与grants。completed_action_requirement现列入metadata；received_speech_requirement仍转发到动作，未只停留在目标声明。

session/10-subjective.dv:432–440 的收闻资格检查speaker/action_id且要求非空source_event/parse_event；`:503–530`在共同owner_requirements中检查收闻及completed_actions。dialogue:606–614的目标资格同样过滤收闻和成功动作，因此不是仅显示一个无法正式执行的目标。session/30-runner.dv:774–780只有execution.status=applied才追加completed_actions并传播public grants/speech；20-speech.dv:740–775由实际接收消息建立listener_parse和received_speeches。预测没有在这些路径写真实完成记录。

Complete（30-runner:614–628）再次核speech本人真值、权威话语约束、物理authority_precondition及owned_decision的原选择输入，再决定applied/rejected。它没有重新调用完整owner_requirements；收闻/成功先行行动的资格由Prepare保留。当前未找到这些只增成功记录在排队期间失效的实际反例，不把这一差异直接判成漏洞；需注意认知侵蚀可清received_speeches，与长期队列的共同合同需由主代理确认。

## dialogue当前修复事实与边界

背刺（226–234）不再直接把操纵flag当原话来源，要求恶魔实际收闻detective.c2.color.encourage；引用正文取共享c2_color_encouragement_text。后续实际选择、执行与伤亡因果明确保留待核，没有从先后顺序直接宣布杀人。伤亡前提是莫兰dead或贝尔特朗stone_stupor，尚需content的动作、listeners和共享文本定义冻结后交叉核实。

Mora证言（241–244）字面命题要求莫兰dead及erosion_exposure>=8，且本人真实完成mora.c2.examine.moran，已替换单凭起诉flag宣称病历的逻辑。Ward证言（246–248）使用同一字面命题并要求实际收到Mora证言；20-speech的实际解析可把该literal的认识传入，听到全文与采信程度仍由共同传播决定。

铜口挑拨（252–258）已停止发言一次直接写他人擅离岗。它仍依赖detective_imprisoned=true。当前全DV只检到该字段初始化false与消费者，未找到生产true写入者；因此当前保留的监禁后对话不能证明有启动链。把不可靠定罪动作撤掉避免伪结果，是来源边界修正，不能称公审、监禁和越狱全功能已补齐。

## homecoming当前修复事实与边界

实际放逐链已区分阮主张→Ward提议→每人consent/reject→Ward记录一致决议。每项声明都走标准speech和Complete，提议/投票具有personal_choice；投票的owned_decision按本人context避免无变化重投。c2h_refresh_actor:280–287给exile动作生成自动沟通目标，不再只解除manual_only。作用域与函数参数从这些调用处看一致：c2h_exile_choice四参、c2h_exile_decision二参、c2h_at_least_conditions三参及递归调用匹配。

`c2h_balance_condition`（144–162）把人数条件编成普通命题，实际进入提议与一致决议前提，summary调用同一条件；这与旧版只读计数装饰不同。计数对象是central_hall中alive、非stone_stupor、非in_coma者；没有把永久失能自动写成死亡。`c2h_motion_passed_condition`（164–175）要求每人死亡/石雕排除，或在场有能力且consent表态与成功发言mark；缺席的活人不会被当成默认同意。暂时昏迷者既不能投票也不排除，结果是未决而非死亡；是否应有另一流程是设计取舍，本审查不补设。

本人可知来源仍有覆盖限制：检票action（177–180）要求双方同场，观测真实alive/stone_stupor/in_coma/stance/执行mark；Ward刷新为成员添加观察目标（300–314）。离场死亡或石雕成员如何被Ward实际确认，未在此模块找到专门远端来源；未知会阻止本人资格，不能靠physical真实多数直接赋给Ward认知。该模块目前没有找到语法层作用域或参数数量的具体错误，但仅静态阅读不能确认编译器接受。

一致记录仅写exile_motion_passed与ration_deprivation_decided，没有写实际exiled或位置；summary明确exile_execution=not-implemented。全DV当前检索ration_deprivation_decided、protection_refused未见进入配给领取/防护资源操作的消费者。故已补本人决议链，但尚不能证明实际剥夺配给、防护撤回或押送放逐。

归乡departed与trauma_imprints_sealed只剩初始false及summary读取，未找到成功执行写入者；summary已标negotiation-only。创伤函数（349–364）只投影事件来源候选且标contact-unverified，没有宣称写入具体人物或完成记忆封存。真实归乡、封存与个人印记仍是缺链，不作为本轮已经完成。

## 待冻结交叉审查

待核content的共同c2_can_act/认知失能、Mora examine成功后的grants、鼓动speech与共享原话、室外暴露/搬运/次晨结算、夜方案与英雄的实际作用消费者、死手启动者与真实损害、三槽物件位置及真假解题结果。读取区间源码正在由其他代理修改，本报告不以行号相近或名称一致替代冻结指纹。
