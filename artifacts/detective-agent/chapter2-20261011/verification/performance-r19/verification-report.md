# Performance r19 独立工程验证

本轮由全新独立上下文执行。未读取设计规范、剧本、生成剧情内容、历史试玩轨迹或历史报告。只修改中性 StrategyCheck/Program.cs；未提交。

## 实际结果

- 正式 Debug 宿主构建：通过，0 warnings / 0 errors，退出码 0。证据 build-debug.log。
- Godot 4.6.1 headless editor import：通过，退出码 0。证据 godot-import.log。
- FullTest.gd --check-only：通过，退出码 0。证据 fulltest-check-only.log。此命令仅语法/装载检查，不是实际场景运行验收。
- Release StrategyCheck：85 passed / 0 failed，退出码 0。证据 strategy-check-release.log。初次83项通过日志保留为 strategy-check-release-initial.log，随后补充两项生命周期作用域测试。
- 本轮测试文件 git diff --check：通过。

## 中性机制覆盖

新增28项断言：分别使用平层与嵌套主体夹具，检查等价但重新构造的 AST 重复绑定（时间变化）不增加认知版本、历史或替换最后观察；优先级、说明、方法可用性、效果、生命周期 owner/scope、接受状态变化正常发布；同版本缓存复用且无认知写入；门槛认知变化重新计算；ActionLearning 失败反馈使评分降低且缓存失效；其他主体独立评分与经历；已有缓存仍拒绝 foreign actor；存档恢复产生新主体对象与新评分列表，同时保留实际学习评分。

原有测试继续覆盖撤回方法、阈值边界、来源获取/遗忘、foreign effect/condition、无副作用、闭合wire以及隐真变形。

## 缓存边界审查

StrategyPolicy 比较规则条件与效果的结构等价，并比较模式、权重、接受状态、时效和公开说明；观察时刻不同本身不产生新内省。缓存使用实际主体投影引用作为key及本人的 KnowledgeRevision；调用入口先校验 self 属于传入世界。恢复后的投影对象不同，不能复用旧会话结果。MorningStrategies 以会话实例字段持有缓存，只把本人 Soul.World 与 Self 传入评分。

本轮验证覆盖现有顺序调度；没有执行并发写入压力测试。缓存外壳不额外提供跨线程同步合同，仍依赖宿主当前顺序调用约定。

## 范围和未验证项

本轮为自动构建/中性机制回归，不属于一级、二级或三级验收；三者均未验证。人物行为、画面显示、游玩体验、帧率改善和真实性能均不能由以上通过结论推出。FullTest真实性能会话由主代理另行执行并单独报告。

开始与结束 Godot 检查时均只保留编辑器 PID 23084；本轮临时进程结束后立即通知主代理 Godot 已释放。
