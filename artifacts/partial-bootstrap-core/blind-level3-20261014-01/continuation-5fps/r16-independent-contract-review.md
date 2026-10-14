# R16 独立合同静态审查

日期：2026-10-15。审查 `protected-change-r16.diff`，补读现役 Main、WorldView、AwakeningMap、动作构造、Complete 观察路径及 E1 既有几何声明。未读取设计规范或私有会话轨迹；差异中的文档只用于核对机制说明。未运行游戏、测试或构建，未修改机制源码。

## 需要修复的记录问题

**WorldView._daily_report 可能把上一行动的现场证据合并到本次失败回执。** 成功移动后 `_daily[who]` 保留上一 job，并标记 reported。下一行动若在建立新 job 之前因目标站点不存在或无可走路径失败，传入 details 已含新站点；但 `_daily_report` 无条件从 `_daily[who]` 取旧 job，以覆盖方式写入旧 station、approach_point、station_point、radius 及位置误差。结果是本次 serial/action 配上上次几何证据。

建议只在 job.serial 与当前 serial、job.action 与当前 action 匹配时合并 job 证据；否则保留本次 details，不引用旧 job。受阻计时分支也应传入该 job 的 action，避免记录默认 rest。此问题不会把 blocked 变成 applied，也未发现由它授予回忆，但会污染原始失败证据，需在新正式会话前修正。

## 接触动作与观察边界

- 新动作明确声明本人门厅位置、本人已获物件事实及本轮尚未接触。物理法则前提仍独立检查本人地点及 contact=false。
- 世界初始化 contact=false；主体初始化仅本人 contact=false，没有初始化 recalled 字段、完整取回或价格选择。预测法则只写 contact=true，不写回忆结果；同一预测分支之后不再满足 contact=false。
- 正式动作使用 move 费用模型，不增额度或改表。原 public_method 将移动标为 repeatable，但 contact=false 前提仍阻止成功后的重复执行。
- 显式站点到达事件先经 Main 核对主体、序号、动作、站点、房间、到达状态及目标点误差；拒绝回执沿 runner rejected 路径，不执行接触法则、不产生声明观察。只有实际 execution.status=applied 后才读取 grants。
- contact 观察单列 perception/own-executed-contact；回忆字段使用 recall 来源，仅授予行动本人。没有直接写 trust_reason 或正式答案，也没有 public_grants 向其他主体传播这些回忆。
- 显示标签只提出“走近并触碰插闩”，未在选择前陈述未获得的回忆。成功后的 surface 仍经原 applied/visible 过滤。

## 几何执行与同地点移动

WorldView 已把 station 与 execution_room/room 分开；from/to 相同不会触发快捷成功，仍经过站点解析、导航候选、寻路、碰撞和到达检测。site:E1 使用原热点：reach=56，接近半径为46，原到达容差4；正常成功终点应在原热点范围内。回执保留实际终点、目标点、站点点位、半径、距离与误差，可供实测核对。

Main 当前直接检查目标点误差，站点范围一致性由 WorldView 构造目标点保证；独立验收仍需核对原始坐标与截图。此路径证明走到交互范围并执行 DV 接触法则，不证明存在手部动画或独立手部碰撞。

原跨图出口显式 execution_room 保留原先实际执行房间，避免分离字段后改变该处几何目标；尚未实际复测该出口。

## 请求签名与重演预算

请求专用输入签名保存 ref/known/value/conflicted 和整个前提实际 truth，去掉 cell 的观察时间及事件历史。目标和主题明确保留；远程分支同时记录方法前提与频道前提。不改变仿真、论证或真实收闻。相同语义下有意催问、回到同一地点重试等没有另立生命周期，本次修改不能称为完整候答修复。频道 basis 仍保留在上下文，若未来动态更换该依据，仍可能改变签名。

重演资格只新增总 model_budget>0，没有使用上次方法余额拒绝可重新付费的方法；现有缓存读取路径不受此资格限制。结束提示只在没有有效完整解释且总预算耗尽时说明资源限制，未增加预算、退费或预知权威接受。

## 结论与未验证项

除上述失败回执几何记录串用问题外，本次静态审查未发现新增的真实知识注入、失败回执授予回忆或同地点跳过寻路路径。建议先修复记录问题并封存对应追加差异，再开启新指纹的正式会话。

草稿不在本差异中；本审查未修改或暂存草稿，也未单独完成其历史 SHA 对比。浅瞬内容是否足以支持授予命题由主代理核对来源，静态机制审查不能代替内容裁定。未验证实际可达性、碰撞、截图、可见回忆、推知链或通关，未执行任何验收级别。
