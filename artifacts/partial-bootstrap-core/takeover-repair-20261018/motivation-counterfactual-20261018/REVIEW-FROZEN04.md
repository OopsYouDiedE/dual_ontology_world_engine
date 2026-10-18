# 冻结04独立证据审查

审查日期：2026-10-18。对象为 frozen-04/standard-yi 的一级 formal-model 正式逻辑窗口，并与 frozen-03/standard-yi 已解码共同前缀比较。未读取设计文件，未编辑生产源码，未构建或运行游戏。源码来源在事件中的路径只是来源元数据，本审查未据此读取设计正文。本报告不作人物合理性的设计裁定。

## 结论与范围

180.297现实秒窗口的 stdout 可解码末事件为 e17536、2280分（第2日14:00）；冻结03同配置末事件为 e14279、2160分（第2日12:00），运行180.188现实秒。本次窗口实际多覆盖两小时游戏时间，但仍未到五日时域。不能把较远时钟或事件产出解释为剧情终局、体验改善或完整会话完成。

result.json 明确 logical-session 超时，logical_loop_finished=false、world_horizon_observed=false、evidence_complete=false、raw_sealing_complete=false。脚本正常退出不改变这些事实。raw DAG扫描记录末提交事件e17591，stdout投影仅至e17536，两者不能混为同一覆盖边界。raw-events.dag.jsonl.partial 有提交前缀、无final checkpoint及seal；无异常残尾不等于完整封存，未提交计算保持未知。

## 共同前缀的行为比较

读取两轮stdout.jsonl，每行解析为DV字段对，提取 type=debug_events 的batch.events并保持原顺序；仅去掉事件顶层raw_event_ref这一存储位置字段，逐事件比较其余完整导出字段，包含event_id、actor、game_time、parents、action_id、action_kind、label、purpose_sources及detail。冻结03全部可解码前缀至e14279与冻结04对应前缀完全相同。另对actual-event-index的行动、主体、时刻、父事件与执行状态交叉比较，未发现差异。

因此本轮未见原已记录窗口中的本人选择、实际执行、观察、规划得分或因果父事件因优化发生变化。这只是已解码共同前缀一致，未逐一展开detail内指向DAG的全部原始值，更不是整局等价。未将profile-03插桩窗口用作同条件基线。

首章侦探实际answer_accepted仍为594/605/631/654分的e2662/e2761/e3057/e3310，拉闩、开门、cross-out仍为658/659/661分的e3440/e3456/e3486。874检视、875暂缓判断，以及lucian堵管、ward藏清册、moran藏底稿、veronica蚀抽屉的执行与随后本人观察均处在相同前缀。此为formal-model回执，不含Godot几何证据。

## 新覆盖段的信息、选择、执行与后果

侦探在2160分选择detective.wait.first_night.120（e14281），规划e14282记录complete-depth，e14283排定due=2280、duration=120。2280分先有host_completion e17509，再有status=applied的action_execution e17510，e17511随后向侦探观察写入本人等待elapsed=1201及campaign.clock.minute=2280。由选择、排队到回执和本人观察形成实际链，不能把2160的候选或排队当成等待完成。它也说明新覆盖的两小时并非侦探持续调查两小时；期间本人等待，其他人物继续行动。

2165分ward与ruan分别实际检视台账，e14484/e14488只向对应本人给出直接感知的台账字段。2166分随后分别执行motive.inspect.c2.ledger（e14530/e14534），有本人观察后继。新增段还出现shen分配（2161/e14330）、moran取表样（2162/e14380）、ruan提出并采纳/张贴台账方案（2169—2172）、ward张贴与阅读方案。这些是明确行动回执，不能据此声称侦探知道全部私下信息。

本段有实际拒绝：mora.c2.count.initial于2165分e14475的status=rejected；本报告未解出其全部拒绝条件，不将其算作盘点完成。后段存在反复走动，正文可见沃德13:45到起居厅、13:47回中央厅、13:49再回起居厅，13:51/13:53/13:55/13:57/13:59继续往返。相关观察亦由真实移动回执产生。更远时域没有消除重复移动，阅读体验仍有需要主代理结合设计判断的问题。

玩家正文仍出现“未具名主体”，例如13:10、13:24、13:55的移动段，影响从正文单独辨认行动者。调试actor能辨认人物不等于玩家得到了相同信息。本窗口未验收第3日后动机、反制、最终案件或终局。

## 冻结、配置及未验证

result记载build_source_consistent=true、source_unchanged=true、run_config_matches=true。实际配置与冻结03相同：seed=1、Standard_Detective、zone_yi、R08、private_execution_benefit_multiplier=1、homecoming_negotiation=false；server GC及4堆请求亦相同。这里引用工具记录，未独立重跑构建或重新测量。本次只覆盖standard-yi，未运行的其他配置与区域保持未验证。

只进行了一级无窗口证据审查，没有Godot UI、碰撞、输入、画面或三级游戏体验结论。五日覆盖目标本窗口未达到；完整封存与剧情终局仍未验证。

## 审查输入指纹（SHA-256）

| 文件 | frozen-03/standard-yi | frozen-04/standard-yi |
| --- | --- | --- |
| stdout.jsonl | 51982e8382ae9b2acdcccbd3692ec90b7d3a4adab8f337ee34ea37875d27a76f | 8b9357590e4259438469d897b891a450cd032623bd31ace6b0400b8d3f59ad45 |
| actual-event-index.jsonl | d7fdb9c198261d256eedc4493bf915ddd05543448907738f2eead66c95b26b22 | 4231122ba0ea1c4c7f2d3077fa5acd1701952a9c91365ed8df0e411ce2800d52 |
| 02-引擎输出的玩家可见动作.txt | e7d93042a33a378e0675174ff4311641a69b6f0e57e25d79ba7c8f426af37f33 | 4ba7b4093289114240b357bb94248905f7d51cf63c5777cf2bc4679b01e5f1cd |
| result.json | 1bac424d7bdc3401d614a11f92c2ff4c8e3fbfb461f1607c0bf9cde481f22329 | 9d5f4e9ebd5794a63b87971cdef53d0d07bbb7be9478b374a4567620282c92d8 |

冻结04 raw DAG指纹（扫描摘要记录）：cddb8b6c9e6f1ac01cf855fc4ff06d9d22a3683c2258562c7d7943382abb2e82。
