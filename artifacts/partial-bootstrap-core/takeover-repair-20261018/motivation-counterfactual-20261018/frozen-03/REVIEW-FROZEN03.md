# 冻结03独立证据审查

审查日期：2026-10-18。审查对象仅为 frozen-03/standard-yi 与 foolish-yi 的本轮一级 formal-model 输出。审查未读取设计文档，未构建、启动会话或修改源码；只读检查 stdout.jsonl 的事件包、actual-event-index.jsonl、玩家可见正文、result.json 及冻结 source 中相关 DV 声明。以下是执行事实与源码条件的核对，不构成人物合理性的设计裁定，也不与旧版本作同条件速度比较。

## 证据范围与停止

两配置均 seed=1、zone_yi、su_initial_room=R08、private_execution_benefit_multiplier=1、homecoming_negotiation=false。各 result.json 记载源码一致且运行期间未变化。standard 执行180.188现实秒，foolish执行180.219现实秒；均在 logical-session 阶段超时，logical_loop_finished=false、world_horizon_observed=false、raw_sealing_complete=false。raw-events.dag.jsonl.partial 仍是部分文件，不能视为完整会话封存或终态存档。

standard 最后可解码 stdout 事件为 e14279（第2日12:00，2160分）的 observation；foolish 为 e14254（第2日12:02，2162分）的 observation。仅引用已提交可解码前缀，不从超时推断人物停住、剧情失败或五日终点。raw索引覆盖与stdout包末尾可能不同，应保留各自来源，不能把索引总量当完整性证明。

## 首章实际差异

两配置侦探均在594、605、631、654分收到 answer_accepted；standard事件为e2662/e2761/e3057/e3310，foolish为e2697/e2794/e3078/e3322。两者均实际执行658分拉内闩、659分开门、661分cross-out；对应standard e3440/e3456/e3486，foolish e3448/e3463/e3492。此处记录的是formal-model回执，不能声称Godot几何跨门已验收。

首章反事实并非仅改配置名：standard在874分先执行night.inspect-evidence（e4658），875分night.defer-judgment（e4668），正文说明封口与新撕毛边后暂缓判断；foolish在874分先执行night.provisional-assent（e4657），875分才检视（e4705），正文是“暂且附和先限制行动”。standard另有第1日14:50的暂缓核对回应及21:02搭档邀请正文，foolish相应正文未出现。前段也有检视排序差异：standard10:36触觉闪回、10:41杯子、10:42酵种；foolish先10:35杯子、10:36酵种，10:42才触觉闪回。不得把这些实际差异扩大成第二章最终优劣。

## 私下动作确有执行与本人观察

冻结 chapter2/content.dv:2346起声明地点、未完成证据前提、本人欲求与代价。执行并非只出现候选：

| 行动 | standard实际事件/时刻 | foolish实际事件/时刻 | 本人随后观察 |
| --- | --- | --- | --- |
| lucian堵管、擦部分算式 | e9220 / 第2日09:43（2023） | e9087 / 同时刻 | e9221/e9088：lucian的pipe_blocked、formula_partial为true |
| ward藏清册 | e10288 / 第2日10:11（2051） | e10606 / 第2日10:17（2057） | e10289/e10607：ward的inventory_hidden、blue_ink为true |
| moran藏底稿 | e14009 / 第2日11:55（2155） | e13882 / 同时刻 | e14010/e13883：moran的draft_hidden、misleading_sketch为true |
| veronica蚀抽屉、换标签 | e14013 / 第2日11:55（2155） | e13886 / 同时刻 | e14014/e13887：veronica的drawer_etched、r07_label_changed为true |

上述action_execution均status=applied，随后observation的subject均为行动本人。侦探可见正文未含上述四条私下动作的独白文本，不能用调试轨迹中可见的全局信息声称侦探已知。此次未逐条展开全部权威世界DAG字段，结论限于执行事件及本人观察。

ruan.c2.motive.leave-latch 在冻结声明中要求第3日18:00—21:00且主仓锁闭；两窗口只到第2日中午，未出现该行动不能判为接线失败。反制、收回清册、清管、回收底稿和中和抽屉等后续未据此窗口验收。

## 讲堂移动、等待与后续

冻结dialogue.dv:394—395的study条件为第2日10:00—11:30且北廊可达，目标带maintain_presence。实际不是全员瞬移或只生成到场候选：人物沿中央厅/讲堂移动，期间仍有提案、读告示及其他行动。demon于2020分实际进入讲堂（standard e9194，foolish e9061）；2041分讲守则第四条，2102—2105分依次teach.lesson11/12/13/14，2106分lesson11.close（standard e11692—e11977，foolish e11565—e11850）。课程本轮有真实执行，但不是一到10:00即完成全部授课。

侦探在讲堂于2109分完成60分钟等待，随后2110—2112分检视、阅读并采纳veronica告示，再等待至2127/2128/2129/2130；ward于2063分进入讲堂，等待至2123、2128、2129、2130。lucian于2046分进入讲堂，完成提案/采纳/张贴/阅读，随后等待到2110、2125、2130。moran则2113进入、2115离开、2118再进入，说明窗口内仍有折返，不能宣称在场目标消除了所有重复移动。

2132分，侦探及多名人物实际从讲堂回中央厅。此为study窗口11:30结束后的移动回执，而不是仅位置投影。此后侦探/lucian/ruan回起居厅检视日程；moran走共鸣厅—沉静室并藏底稿；veronica走后勤院—白塔安全端并蚀抽屉；ward仍在讲堂等待到2160，foolish随后2162回中央厅。这些后续行动显示窗口关闭后推进恢复，但本轮仍未覆盖所有后续案件与反制。

## 正文仍可见的问题与未验证内容

玩家正文大量保留“未具名说话者/未具名主体”，包括开场自报姓名、讲堂往返等段落；从正文单独阅读仍难定位某些人物。调试事件actor能辅助审查，不等于玩家已得到相同信息。讲堂前后多段相同移动文案连续出现，结合moran实际折返，应继续检查是否造成阅读重复与目的不明。

本轮只审查一级无窗口逻辑输出；未启动或激活Godot，没有UI、碰撞、真实输入或画面结论。未完成五日时域、完整剧情终局或整局体验验收。工程生成记录、事件数量及较远的游戏时钟均不代替这些结论。
