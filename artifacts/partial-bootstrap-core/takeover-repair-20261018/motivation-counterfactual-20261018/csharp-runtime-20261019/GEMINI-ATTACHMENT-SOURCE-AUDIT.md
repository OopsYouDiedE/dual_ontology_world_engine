# 附件所指第二章源码只读审计

审计时间：2026-10-19 07:57 +08:00。分支：`codex/partial-bootstrap-core`。审计者为独立上下文子代理；未读取设计文档，未修改源码，未构建、运行探针或会话。唯一新增文件为本报告。附件是审计问题清单，不是设计依据；是否符合设计由主代理核对。

工作区已有其他修改（含 Runtime.cs、工程文档及本报告所指三文件），不回退、不覆盖。附件未给出其源码包指纹，因此不能把附件描述归责于当前版本或断言某次提交已修复。下文“当前已存在”仅表示读取版本的静态事实。

## 读取版本

三文件在首轮与末轮读取时 SHA-256 相同；这只约束本次读取区间，后续 Antigravity 修改不在本报告结论内。

| 文件（相对仓库） | SHA-256 |
| --- | --- |
| language_core/verification/scenarios/chapter2/content.dv | 9CC86DFD0462D241786721AAB2BF864D712C7729020CEF4FA9FA6DC05FCA309E |
| language_core/verification/scenarios/chapter2/dialogue.dv | 2BDBF1CC3B1C4882FE19A099CB632057E91770E7C4226AAFB96862BE618F845D |
| language_core/verification/scenarios/chapter2/homecoming.dv | 5E6E0990C7FD5C15D1811B23B9686E7BAFEC8281FE6F0DAFE2C9A21E45A91865 |

以下行号均对应这些读取版本；简称 content、dialogue、homecoming。

## 生产接线

`language_core/game/entry.dv:302` 的扩章入口实际组合 `c2_extend_scene → c2d_install_scene → c2daily_install_scene → c2h_install_scene`，并配置主体政策；`:328` 刷新 dialogue/homecoming 目标。content:1688–1708 组装实体、主体、bindings 和 world_processes。content:987–996 的主体动作组合确实包含英雄、伤亡、非常规、死手和 c2m 动作；content:1052–1202 安装权威/预测法则。不能仅因名称不同判作未接生产。

环境过程由 `verification/session/30-runner.dv:999–1028` 的 `dvs_environment` 依列表顺序读取物理前提并真实 enforce；它不会直接提供人物知识。静态声明可达与人物实际获知、选择、执行是不同问题。

## 伤亡与空间病理

**附件中“莫兰在起居厅原地执行死亡动作”不再描述当前定义。** content:763–776 将急性昏迷设为室外 `outdoor_alley`、alive、`erosion_exposure >= 8`、尚未昏迷；效果为 `in_coma=true` 与 `exposure_terminal=true`。次晨死亡另要求 clock>=6120、alive、terminal 与尚未给药匙；石雕结算要求 rescue_exposed。content:874–902 保留旧新动作名/别名，莫兰动作空间前提已改室外。content:2025–2026 的 outdoor danger 根据真实室外位置、震后与危险空气给暴露剂量，亦安装为环境过程。content:1199–1201、1743–1745 把急性昏迷、死亡及石雕接入 world_processes。

**仍存在会影响链路的明确冲突：** content:570 的通用 terminal law 在 alive 且 exposure_terminal=true 时立即写 alive=false；该过程已给每个人安装并列于急性昏迷之前（1738附近）。急性昏迷后，下次环境轮转可先清除莫兰 alive；而次晨死亡结算（773）仍要求 alive=true。由此可能无法写入 `dead`、`c2.casualty.moran_dead` 和 `casualty_occurred`。这是源码条件冲突；本审计不凭静态阅读声称实际发生时刻。

**救援空间仍不闭合：** content:769–770 与891–902 的贝尔特朗救援仅要求本人存活、第四夜、莫兰昏迷，没有要求贝尔特朗已到夹道、莫兰仍在夹道或真实相遇。效果直接把二人位置改为 living_hall，并加8暴露和 rescue_exposed。三个救援/石雕动作使用同一类前提，且若干动作自带 effects，不能只因安装法则 ID 有别名差异断定它们不执行。当前实现尚不能证明真实冲入毒雾及拖回路径；还需审查重复非repeatable动作之间能否反复施加救援效果。

**已有驱动：** content:1510、1512 给莫兰暴露目标及贝尔特朗 rescue_exposed 目标；1957–1959 给莫兰真实外出目标与 evening_choice。附件“完全没有目标、收益必为0”不可沿用。本人何时知道仪表、昏迷、路径及救援机会，必须以正式轨迹核验；目标存在不保证选择。

## 英雄补位

当前确实定义 `c2_hero_substitutions`（842）且 `c2_hero_actions`（871）转发；函数名存在不等于语义完整。四类动作现有 personal_choice，promotes 包含 solidarity/safety 等，亦有 content:1502–1508 的对应目标，故附件“无选择/无目标”已不适用。

但动作/权威法则（751–761、842–869）仍主要检查房间、alive、夜次与对应成果尚未完成；未检查侦探当前失职、技术方案失败，也未检查真实气闸险情、水阀破裂、滑轮失稳、枕木受压等事件。`airlock_hazard` 虽初始化为 false（233），这些英雄前提未使用它。文本声称冒险爬外墙/抵住险情，效果只是写入完成旗标；不能据此证明文本所述过程发生。需主代理依据设计决定应加入哪些可知危机与竞争条件。

## row.literal 自指与对话驱动

`c2d_action`（328–391）仍把 `row.literal` 加入本人前提，这是共同话语资格合同，不应无区别删除。当前 comfort（182–205）、demon rebuke（208–224）、trial/shura（226–291）改用 `dv_true()`，并在执行法则效果中写自己的完成字段，已避开附件指出的“必须先知道自己已完成才能说”的特定自指。

`c2d_row_law`（313–321）实际写 spoke mark 与 row.effects；`c2d_install_scene`（675以后）为本人安装动作/预测和物理 enforce；`c2d_goals`（539–630）逐行产生 communicate 目标，并核实际请求、机会、收闻等条件；因此不能只因这些行没有显式 personal_choice 就断言无收益或死代码。comfort 还有显式 personal_choice。条件字段何时被本人看见仍待执行证据；`dv_true()` 也仅证明空字面量资格，不证明表面台词叙述的具体事实真实。

## 恶魔背刺、铜口与公审来源

当前有 `c2d_demon_trial_dialogues`（226）及 `c2d_demon_incite_guard`（250），并由 shura rows 组合。后者主体确为 demon；侦探 intercom 台词是另外几行，不能互相替代认定主语。

背刺前提（230）现在除 casualty 外还要求 detective 的 `detective.c2.color.encourage` spoke mark；该 mark 可由 content:788、929 的正式动作效果写入，故附件“只看伤亡、不查操纵”已不适用。但是当前校验的是动作旗标，没有逐条匹配 `player_influence_history`、原话、实际 listener_parse 或恶魔获知原话的来源；方案D动作只声明 kind=talk、surface、listeners 与 effects，未在此构造标准 speech 记录。背刺正文说“那夜在走廊”，而该鼓动动作要求 living_hall；本审计不能证明正文地点或私聊来源成立。`sketch_lured` 在全DV引用搜索中只见初始化及该动作写入，未见它参与莫兰实际外出选择条件。

公审链确实是背刺→ruan indictment→mora witness/ward verdict，后者直接写监禁。阮正文“十九场死活推演”没有对应推演计数/记录前提；茉拉正文声称二人病历/创面，前提仅 indictment，未核二人对应伤情；沃德正文声称言论/动线/死因闭环，前提仍仅 indictment。恶魔 incite 行直接写 guard_abandoned_post，未要求某守卫真实听见、选择并离岗。因此特定自指已调整，但内容来源与他人选择仍不能由当前旗标链证明。

## guardian balance、放逐与 trauma imprint

`c2h_guardian_balance(physical)`（208–229）动态数六位 guardians、三位 calculators 的 alive 并返回多数/fate，但全DV调用搜索仅见 summary:263。实际放逐门是另一套逻辑：`c2h_humanity_barrier_law`（100–101）只看 casualty 与 Bertrand 不alive，再写 barrier_broken；ruan 行（85）读 casualty+barrier；ward 行（90–91）读 ruan 宣告并直接写动议通过和侦探被放逐，没有数票。故“动态人数天平未控制实际放逐”仍成立。

“放逐全 manual_only”已不符合当前版本：安装器139–143恰对三条 exile 行不加 manual_only。但这些行没有声明 personal_choice，`c2h_refresh_actor`（177–205）只为归乡提议/同意/暂缓补自动沟通目标；不能由解除 manual_only 证明 AI 会自动发动放逐。需要实际候选/评分证据。

`c2h_trauma_imprints`（231–247）现已接收 physical，根据 moran_dead、bertrand_stupor、earthquake_recent、detective.hungry 返回分支文案，附件“无参数固定数组”已过时；不过它仍是 summary投影，未见向各主体认知写印记，也未调用通用 dv_imprint_*。全无条件分支时还返回“全员生还誓约”等固定描述，缺少对应执行事实验证。需要区分设计中的潜意识创伤与 Core 的数值认知刻画是否真是同一合同，不能凭同名要求强行接 `dv_imprint_solve`。

## 三槽设局、夜方案与死手

**印章链已经有声明和生产接线。** content:2682–2688 有卢西安黑板诗、沃德失印、雀儿藏钢琴、阮沉水窖及三段侦探破案；c2m_actions（2746）为各行构造 personal_choice；c2m_goals（2811）构造本人目标并接入 c2_personal_goals（1935），c2m_install（2735）安装法则。不能判为“只有 leave-latch”。但主体/房间/次序是静态行，未见扫描全场未解矛盾、动态选A/B/C候选及替换槽位机制。失印动作可直接设 seal_lost；钢琴与水窖字段是累积布尔，转移不清旧位置；三重破案动作主要按前一旗标/物件旗标直接写 solved，未在这些行核推理成果。`c2m_fields`2692还把这组字段统一置于 document_workshop 的检视对象，与动作各自 living_hall/resonance_hall/logistics 的空间表述需主代理核对。

**ABC/D 已声明且进入动作与法则。** content:905–933 的A/B/C有personal_choice；A会真实写intervened，故“全图无写入者”已过时。但A前提未要求伊莲娜受伤/被安排在气闸、条款实际触发或恶魔选择，直接写airlock_held；B无铁楔/焊具或锁芯过程，直接写airlock_held与ward_impeached；C直接写fake_alarm和airlock_held，未核他人确实被拖住两小时。全DV检索 airlock_held 未见它限制莫兰外出/伤害过程，因而“关门保命结果”尚无完整因果接线证据。

**死手已有动作，不是完全缺失。** content:935–967 有四人的动作与effects并接入主体动作组合；安装法则也有对应声明/不同别名。伊莲娜与贝尔特朗前提分别要求 elena_betrayed、community_covenant_failed；全DV搜索未找到这两字段的true生产写入者，后者初始false，所以这两分支当前没有找到启动因果。维罗妮卡前提是“侦探被放逐”的全局动议，不是她本人遭驱逐；阮前提为granary.destroyed。若干结局只写acid_gas_released、traitor_executed、sedative_administered，未见这些旗标进一步造成具体受害者损伤/死亡，文本所述目标和代价不能据旗标认定。四类动作未在此补显式personal_choice或对应专门goal，自动选择仍无执行证据。

## 性能与验收边界

当前 content:1669 确实设置第二章 plan_depth=1；文档存在更早“恢复两层”的描述不改变该源码事实。此审计不评价应设何值，也不把快慢归因为死代码，更不采信附件的232.50秒/7200分钟为本轮测量。

已静态确认若干附件断言与当前实现不符，同时定位真实未闭合条件与来源。没有运行一级、二级或三级验收；没有判断剧情达成、人物行为合理性、游戏体验或同条件提速。当前文件未变指纹仅说明本次审读自洽，后续修改需重新核对相关结论。
