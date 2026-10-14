# R18 局部采用构造器备选审查

日期：2026-10-15。基于此前已只读核对的 dv_adopt、持久复制 API 及 c2_install_actor_laws，审查新增 checked/into/finish 提案。当前 R17 会话继续运行且源码冻结；本次未修改源码、构建或执行会话，未读取设计或私有轨迹。

## 结论

该备选比预构造整批更能保持现有首错顺序：保留每条原调用位置的法则构造，随即验证并追加，通过后再构造下一条。限定为 c2_install_actor_laws 内部独占构造器时，可保持最终采用内容、顺序、修订号和 world/mind 隔离，未发现必须依赖中间 world 的现役消费者。

## 分工与失败边界

`dv_checked_adoption(domain, adoptions, law, mode, confidence, basis)` 应逐字保持原先的验证顺序及错误：confidence 范围、physical 确定性、basis 非空、重复 law.name、domain/mode 相容，然后构造 active=true 的 adoption。现有 dv_adopt 调用该函数后仍走原持久复制路径，不能顺带改变它的返回结构或引用语义。

`dv_adopt_into` 必须先完成 checked（包括 adoption 构造）才 rt_push 新字段 pair；失败时当前条目不得追加。已通过前缀只存在函数局部 owned_adoptions，不发布到输入 world，不触发事件、观察或采用执行。异常继续抛出，不将前缀作为成功结果返回。

法则构造与验证继续交错后，后项构造错误不会抢在前项采用错误之前发生。这里保持的是正常语言异常的顺序；初始化独立复制的实际耗时不必与旧实现一致。

## 独占与 finish 的必要不变量

- owned_adoptions 必须来自本次输入 world.adoptions 的独立 dv_clone，不得直接别名原表，也不得供其他线程、主体或预测分支使用。
- 构造期间只允许追加 checked 产生的新条目，不修改、重排或删除旧前缀，不替换已有 law.name，不在构造器之外并行修改其 domain、基础 world 或修订号。
- 中间法则构造继续只读原输入与原生声明，不能开始依赖未提交的 world.revision 或 world.adoptions。当前安装函数满足这一条件；未来变更需重审。
- finish 以原 world 为基础调用原 dv_with_many，使最终 world 与构造器及输入 world 保持独立复制。完成后不得再以 builder 引用更改已返回 world。
- 新旧长度差必须非负，且等于成功追加数；revision=原 revision+差值，空构造器应返回原 world，以匹配零次采用。

长度差本身不能证明构造合法：如果未来调用者改写旧前缀、传入其他 world 的表或缩短表，一个无检查 finish 仍可能覆盖旧法则或递减修订号。因此这些 helper 只能作为明确标注 owned/append-only 的内部协议，不能被描述为可接收任意 adoptions 表的安全通用更新器。实现可在 finish 检查长度及旧前缀一致性，或通过局部构造结构严格限制调用点；不应为此放宽原重复或来源验证。

## 性能与预算

初始 clone 与最终 dv_with_many 仍有独立复制成本，checked 构造每条 adoption 也保留原复制。收益是消除每次追加时对整个增长世界和采用表的重复复制，不是取消持久隔离。

旧表/批内重复均由同一 checked 对当前 owned 表验证；保持追加次序与全部字段。未涉及主体计划预算、推演工作额度、真实费用或判答条件。其他安装保持原方式，可缩小本轮风险，但其后续复制成本仍然存在。

## 实施后核对范围

需要 exact diff 证明只有目标安装函数改用独占 builder、原 dv_adopt 仅提取共享验证且持久更新不变，以及构造调用次序和分支完整保留。应核对 duplicate、无来源、非法 mode/confidence、空序列及中途失败均不改变输入 world；这些是工程合同检查，不能代替新正式会话或以通过计数评价游戏。

本审查不证明实现已存在或性能已改善，不推定当前会话判答或章节状态。
