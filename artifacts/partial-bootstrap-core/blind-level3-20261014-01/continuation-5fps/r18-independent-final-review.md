# R18 最终独立静态审查

日期：2026-10-15。审查 `protected-change-r18.diff` 的采用构造器、新鲜度元数据与组合调用、Session 流式序列化及 Host 关闭错误路径。依据本轮已读现役复制、事件、预算、消费者合同核对，不读取设计或私有轨迹；未改源码、构建、测试或执行会话。

## 结论

未发现本次限定差异中的阻塞性机制越界、旧采用表别名污染、声明顺序改变或错误完整性宣称。可进行主代理安排的独立构建及中性工程检查；本结论不代替生成物检查或新正式会话验收。

## 采用验证与独占构造器

- dv_checked_adoption 保留有效 world 合同下原 dv_adopt 的 confidence、物理确定性、basis、重复与 domain/mode 检查次序、错误文本及 adoption 字段。
- 原 dv_adopt 只共享检查/构造步骤，返回仍采用原持久复制路径。
- c2_install_actor_laws 先独立复制 world.adoptions，每个原调用位置立即构造相同法则并 checked/append。各分支、循环、正反向及声明顺序未见增删；构造与验证仍交错，没有先构造后项再检查前项。
- dv_adopt_into 在 checked 完成后才追加新 pair；adoption 构造本身经原 dv_with 独立复制，未将传入 law/basis 作为可写共享负载发布。
- finish 检查长度不减及旧前缀结构一致，空追加返回原 world；新增条数决定 revision 增量，最终原 dv_with_many 保持返回 world 与旧 world/builder 的独立复制。中途异常只丢弃局部前缀，不更新输入世界。
- finish 仍是内部协议，不是任意表验证器：新增后缀必须来自 checked/into，不能绕过它直接追加未经验证的条目。本差异中的调用满足此限制。

## 证据新鲜度与元数据范围

只有原有 demon solved evidence 目标新增 refresh_until，各目标以自己的 solved=true 停止刷新；没有给其他目标、玩家或其他居民增加复查元数据。helper 只读 actor.mind，在新 result 中保留顺序；匹配目标通过 dv_with 返回新 goal，仅改变 minimum_time，cached departure_goals、旧观察与 mind 未被原地修改。

c2d 组合仅刷新这份现有目标缓存，再沿原组合逻辑处理，没有新增 grants、频道、动作、费用、预算或答案判定。本人 mind.time 不前进时仍可能无法使旧证据过期，且额外检视仍消耗原资源；不得据此静态宣称已解除释放阻塞。

## 流式格式与关闭副作用

WriteTrace 直接向新 FileStream 序列化一次 SessionClose 的值，保留 Runtime 的普通 Rational 字符串。WriteSave 单独使用 SnapshotExactConverter 输出既有 $dv_rational 标签，不再预先构造 SnapshotValue 整树；旧字符串兼容接口保留。两套 options 没有混用，未新增 HostValue 对象投影、引用元数据或数值近似。

Host 等待 work 后，先写输入/可见记录，随后分别 await 后台 WriteTrace/WriteSave。每个流 using 结束返回后才改名；写入或改名失败保留 .partial，不覆盖既有正式文件。生产关闭没有调用旧 Trace，也没有重试 WriteTrace，因此该路径只调用一次有追加 session_stopped 副作用的 SessionClose。

Trace 与 Save 各自 catch，Trace 出错后仍尝试 Save；错误阶段进入 close-result.json，complete 仅在错误列表为空时为真。目录已存在时拒绝继续，只有本次 ownsDirectory 才写结果文件。Godot 路径/API 在主线程，后台任务仅调用 Session 文件输出，await 后的 finally 发 TraceClosed，信号不表示游戏完成或证据完整。

## 错误路径及尚未验证的限制

- inputs 或 visible 写入失败会进入外层 catch，后续 dump 不再尝试；已有文件保留且结果为不完整。若以后要求每个阶段尽量独立抢救，可分别 catch，但当前差异未伪报成功。
- 目录创建失败或 session=null 时可能没有 close-result；结果缺失不能当作完整。结果文件自身写失败只输出错误，随后仍尝试关闭信号。
- OOM 时错误收集/日志本身也可能失败；全流程 finally 改善关闭等待释放，但不能保证任意进程资源故障可恢复。
- 流式格式等价、精确数恢复、写入失败证据与回调、builder 隔离/验证及新鲜度均仍需中性工程检查。本审查未核对新生成程序集。
- 采用表前缀检查和最终世界复制仍有成本，其他章节安装仍保留逐条复制；流式写入也未移除驻留状态和历史。实际耗时与内存改善须新会话测量，不能推算。

本轮没有执行任何游戏验收级别。R17 原始输出缺失保持缺失，不由本次修复补写；后续新证据应另立来源及程序集指纹，实际接受、交付、画面和跨门分别记录。
