# 私有 stage 对象表索引

基线 main 7703d6341，分支 opt/stateindex。未读设计文件，无正式会话或全局 AI 测试。

热路径依据：20 的 dv_apply_effects、dv_apply_infer_effects、dv_update_mind 均从独立 clone 开始，经 dv_stage_store_owned 写入。既有 cow 路径避免了 DV 下标遍历带来的无关分支 thaw，但 Runtime.stage_store_cow 的 TryRawField(objects,id) 和结尾更新循环仍每次扫描完整对象表。既有 record slots/FrozenIndex/MutableIndex 已存在，所以不另建缓存或世界索引。

实现：默认 DV_OPT_STATEINDEX!=0 在 ValueContext 采样，对严格二元 pair、唯一文本键的正常表复用 record slots，私有 stage 写入定位唯一对象，只替换其一条 pair。cold 形状检查/索引构造仍扫描一次；确认只改该 slot 的 value 后继承旧索引。元数据只记严格 pair 形状，所有普通写入及祖先 Invalidate 均清除它。新对象 pair 冻结，后续读时沿原 Thaw 获得独立可变分支。引用、主体准入、法则与费用/事件仍由原 DV 调用链处理。

重复对象键、非文本键、不规则 ABI 结构回退既有 COW 实现；cow和stateindex均关闭时走原 DV 实现。未知 cell、额外字段和精确分数是普通不透明值，未拆字段或转浮点。新增 builtin rt_stateindex_enabled，未新增执行器。CLI/policy/env传递由 guard agent 整合，本提交不写30/50/60/65/公共CLI。

有限验证：

- 独立 .NET 8 C# harness 直接链接本轮 Runtime.cs，以真实 List/Frozen ABI 输入对照 stateindex on/off；源码保存 AbiCheck.cs。512 次连续更新的结构结果相同；源快照不变、兄弟复制隔离、插入字段、键改名使slot失效、重复键回退、未知cell/额外嵌套字段、缺失对象和不规则pair回退均覆盖。最初harness错误地用R.number解析分数字符串，得到FormatException；随后改为原生Rational构造，最终运行退出0。该错误没有改动运行时数字语义。
- 最终同一微负载10次独立context，每次128对象、2048次私有stage写入：off 540.1ms，on 260.7ms。只说明该重复写入负载，非正式Prepare、整局、Godot或故事表现结论。首次有限运行off587.7/on276.1亦只属开发采样，不合并成统计。
- check_dv.py --format json dv_core/modules/20-core.dv 使用新C3：success=true、diagnostics=[]、退出0；只证明语法、名字、调用与已实施类型约束。
- 临时 .scratch/stateindex 项目的递归清理由自动审批策略拒绝（blocked by policy），已核对目标绝对路径仅为本检出下的本轮项目；仍留在被忽略的 .scratch/stateindex，未修改其他临时工作。该拒绝未提供更具体理由。

未覆盖：正式Prepare耗时、真实会话输入序列、完整状态封存、Godot/UI、病例/人物行为。没有声称该改变带来故事改善或整局速度收益。
