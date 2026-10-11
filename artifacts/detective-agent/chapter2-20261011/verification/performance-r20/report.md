# r20 独立工程验证

范围：后台只读案件准备、并发单次发布及活会话隔离。未读取设计规范、剧本、生成剧情数据或历史试玩记录；运行时按生产入口自行加载嵌入数据，诊断不输出内容。未修改生产文件，未提交。编辑器 PID 23084 保留。

## 结果

- 正式 Debug 宿主解决方案构建成功：0 警告、0 错误，退出 0。证据 build.log。
- Godot 4.6.1 .NET headless editor import 成功，退出 0。证据 import.log。
- FullTest.gd --check-only 成功，退出 0。证据 fulltest-check.log。仅语法检查，不是实机流程验收。
- 独立新进程诊断成功，退出 0。证据 preparation.log；诊断源码 PreparationCheck.csproj、Program.cs。
- 创建正式 Chapter1Session 后确认共享模型尚未构建；首次 PrepareAsync 返回 3.278ms，此时任务仍未完成。32 个并发调用返回同一 Task；准备后 32 个读取返回同一 CaseModel。准备后的 100000 次成对 Model/Predictions 读取合计 3.456ms。
- 从程序启动（含活会话创建）到准备完成共 8754.712ms；此数不能解释为纯模型构建耗时，也不是帧率。
- 准备前后活会话 SubjectWorld、认知版本、词汇版本、投影及可见历史逐项保持一致，玩家时钟及阶段不变。
- 诊断初版使用不存在的 Nodes 字段导致编译失败，改为真实 Projections/VisibleHistory 后通过；原始失败日志 preparation-build-failed.log 已保留，属于诊断代码错误。

## 并发核查

AwakeningCase 的 Built 与 Preparation 均使用默认 Lazy 执行与发布模式；Preparation 只创建一个 Task.Run，Built 仅发布一次完整结果。GameSession.PrepareAsync 原样转接。CaseModel 的集合为 ImmutableArray / ImmutableDictionary，Prediction 与变量为 record，不含活会话引用。NightReplay 每次构造独立 WorldRuntime、SimulationSession 以及实例字典、列表和主体；静态共享路径包括只读 JSON、不可变通路表和标签，不存在此次新增的共享可变世界写入。

Chapter1Data 的 JsonDocument 由 Lazy 持有且未释放，访问为只读；ReplayDomains 使用独立克隆及不可变集合。后台不调用 Godot API。GameEngine._Ready 捕获 await 异常，未使用 ConfigureAwait(false)，因此保留宿主同步上下文续执行的标准路径；本轮没有故障注入或线程 ID 实测，不将主线程异常回报作为已动态验证项目。

边界：直接读取 Model/Predictions 早于准备完成时仍会同步等待；此次优化不保证所有提前读取都无阻塞，也不消除后台计算对 CPU 的竞争。诊断只验证当前流程的隔离与单次初始化，未宣称整个 WorldRuntime 跨线程安全。

## 验收结论

构建通过；上述初始化机制检查通过，未发现新增共享可变状态问题。一级、二级、三级验收均未执行；实际帧率、画面、人物行为合理性和游玩体验未验证，由主代理的本轮正式运行另行报告。Godot 检查结束后已通知主代理释放测试占用。
