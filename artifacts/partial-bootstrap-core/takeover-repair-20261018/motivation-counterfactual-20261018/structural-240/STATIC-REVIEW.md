# 独立静态审查（整合中）

审查范围：core/00-data.dv 的引用键、verification/session/15-action-planning.dv 的生产者与消费者、compiler/Runtime.cs 的 ABI 及 compiler/compiler.dv/seed 映射。只读源码，未读取设计文档，未构建、运行探针或启动会话。此记录不是游戏验收。

## 引用键与 ABI

当前未发现阻止整合的具体缺陷。

- 快路径仅接受精确字段顺序、四或五字段、type=ref、全部文本的标准引用；长度分帧和 R/J 域前缀避免字段分界或 JSON 回退键混淆。非标准排列、额外字段和非文本保留原 JSON 编码分组。
- UTF-16 代理单元和 U+FFFD 均回退 JSON，可避免 JSON 替换无效代理字符造成的旧键等价类被拆开。有效补充平面字符也回退，属于保守边界。
- dependency topology 与 freshness 的写入、读取均使用新函数；两处 memo 标签更换版本，避免读取旧键布局。25-simulation 的局部引用签名使用另一套数组语义，没有混用该索引。
- freshness 内部索引是函数中新建私有列表；插入前克隆 entries，发布只在 memo_put 后发生。当前读写范围没有暴露部分构建索引或可变 goal.ref 别名。
- rt_is_text 有运行时单参数实现、编译器 native arity 和 Bool 类型映射；seed 的 rt_ 前缀直接映射 R 方法，无额外白名单需要更新。
- Runtime 的 eq/same_value/has_value 改为共享装箱布尔值；底层比较与分支顺序保持原实现。DV 的值比较不观察装箱布尔身份。

## 待续

主代理尚未通知冻结；目标刷新、空闲唤醒、批量投影与真实 checkpoint 的最终整合审查待冻结后进行。唯一 240 秒正式一级会话尚未交付本审查代理，时域终点、剧情节点、尾部投影与封存完整性均未验证。

## 批量投影与阶段存档初审（尚未冻结）

已只读检查 game/debug-run.dv、tools/run_debug.py、debug_checkpoints.py、resume_debug.py、DebugCheckpointResume.cs，以及 raw_dag 的解码接口和编译器生成类声明。

- 每次正式 Complete 后立刻取得对应可见 presentation 并 snapshot；批次按原完成顺序输出。debug_events 延后输出但仍以事件 cursor 顺序扫描。仅 stdout 内调试包与正文包的交错排列发生变化。
- 正常退出 flush 剩余正文和事件；超时保留已打印正文，result 明确未刷新尾部未知，不声称正文完整。原始事件的提交不依赖该批次。
- 阶段 checkpoint 仅在真实 Complete 后、pending=false、跨绝对时间阈值时写入；保存完整实际 state 与事件尾、action_event、来源和配置。恢复入口没有重新初始化。
- export 使用 dv-tagged 保留精确数原值；恢复适配器通过 Rational.From 恢复。导出与恢复核对原 DAG、metadata、Save 哈希及原 checkpoint 的流式精确重展开哈希，防止换成其他存档。
- 恢复前核对原源码清单与配置，构建后及结束时重查源码和恢复输入；新增类由 compiler.dv 生成的 partial DVProgram 接入。
- 已向主代理提出合同核对：目前 provenance 提供源码 SHA-256 与 raw commit_frame，尚未观察到 Git 完整提交 SHA。若“完整提交标识”包含版本控制身份，应在冻结前补记录；不能把 raw frame 和 Git commit 混为同一身份。

以上为源码推导，尚无执行证明；不宣称恢复成功、正文已完整输出或五日覆盖。

## 守卫与目标声明静态复审

30-runner 的 idle 证书仅在真实 prepare 后主体输入未变、没有新增认知事件、选择无行动且无展开/时间阻塞时建立。证书保留 mind、预算、未知主体字段、bindings/costs/story；仅剔除管理游标及有上界等价归一化的剩余时间。新事件默认使证书失效；许可远端记录不属于 support/utterance 输入。同处集合、本人位置、可见资格单列比较。守卫只在时间推进且严格早于 until 时复用；同刻外部输入变化仍可重新取得 ready 资格，到期行动仍优先分派。缺少可证明时间条件时不建立证书，horizon/规划最大费用边界提前停止复用。未发现具体遗漏唤醒；此判断仍限源码审读。

目标改动属于闭合声明构造复用，不是完整 Goal Refresh Guard：c2d 的静态 head/tail、row 声明，以及 c2m row/observation 模板可复用；动态本人资格、private completion、实际导航/BFS 等仍按原调用计算。head/tail 维持原先目标顺序，动态 row 循环仍位于两者之间；row 缓存包含整行、主体及已经计算出的条件，c2m 还包含 completion/travel。缓存接口复制返回值，构造结果没有直接发布可变共享实例。不能据此宣称全面跳过目标刷新。

主代理澄清“完整提交标识”指原 DAG 有效 commit_frame 与实际源码 SHA-256；Git HEAD/dirty 仅用于溯源。先前提出的 Git 身份问题不构成当前合同缺陷。
