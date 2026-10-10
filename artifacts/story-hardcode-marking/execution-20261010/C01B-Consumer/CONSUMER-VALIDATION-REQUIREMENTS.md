# C01/B 消费方后续独立验证要求

此文件只列合同/消费卡的验证建议，没有运行任何测试。方法应调用正式 Observation、Ast、DecisionSystem、SocialSystem、Reasoner、SimulationSession 与正式 codec，不另造决策器、AST 求值器或替代执行器。压力结果需记录环境、种子、规模、操作数、时间/内存与原始异常；没有既定性能门槛时只报告测量，不自设通过阈值。中性压力与正式 3–5 日压力分别封存，40 日不纳入当前达成声明。

| Case | 正式入口与操作 | 最低观察要求 | 归属 |
|---|---|---|---|
| CB-V01 | 一批修改 Parent/Tags/Relations/DeniedRelations/Rules/Procedure，capture 同时进行 | 捕获只能是完整旧图或完整新图；成功非空一批 revision 只增1；无异主体/异世界引用 | foundation |
| CB-V02 | 批内最后一项使用非法/跨世界引用或父循环，前项合法 | 无部分变化、无 revision 增加、无私有 engine binding 残留；保留失败日志 | foundation |
| CB-V03 | Changed 回调 capture，并进入新的认知输入；另一回调抛异常 | 第一回调只見完整图及对应 binding；新增提交独立 revision；回调异常不能伪称原提交未生效，具体结果按合同固定 | foundation |
| CB-V04 | capture 后 Rename/Forget/重认，改变所有图字段与规则权重/接受 | 旧 snapshot、AST、主体可见来源与历史逐字段不变；新句柄不复用；tombstone/audit仍可审计，当前subject不重新获知 | foundation/history |
| CB-V05 | 一个真实 Assess 同时遍历多 Rule、Request、Permit、Forbid、Context与History；写者在中间更新 | 整轮分数/禁令/选择必须属于一个完整版本，不能只有每条 AST 各自一致；返回凭证与同版本一致 | C06消费 |
| CB-V06 | 正式 Social EvaluateSender 与 Evaluate 使用相同固定图 | sender trust/cooperation/compatibility与主体历史按同语义；Forget 后不从audit补事件，预览不写正式认知 | C06消费 |
| CB-V07 | Rule/Procedure/Say.Request/Wait/Think 的嵌套 AST 含 Reference 与 Slot；codec 二阶段往返 | 节点类型、参数、Repeated reference、slot语义及结构等价不丢；request槽保持接收者后续绑定；未知节点/坏id明确拒绝 | foundation/AST adapter |
| CB-V08 | 合法旧凭证提交前发生认知/词典变更；合法提交后分别 Forget actor/target/tool/scene | 前者按stale规则拒绝/显式重新评价；后者身体绑定按获批连续性，客观删除可失败；actor现状单独复现，不用target例替代 | 执行后续卡 |
| CB-V09 | 两个hidden现实不同，主体snapshot/model/显式假设完全相同；反向变换隐藏值 | 主体解释/评分/推演相同；结果不带objective反查通道。若模型依隐真生成则先报告上游污染，不用同method签名判通过 | 主体消费/推演 |
| CB-V10 | codec 重复ID/键、缺引用、异常枚举、未知major/必要能力/AST/property、超限数值 | 在写入/发布前拒绝；当前已运行会话不变；原始输入/错误均保留 | foundation codec |
| CB-V11 | 多实体×多Facet×多主体增减；遗忘与同名重建；跨进程codec恢复并重复载入 | 身份域不交叉、不按Name重绑、不复用ID；恢复epoch与版本凭证明晰；不能重复提交effects/回执 | foundation/完整save |
| CB-V12 | _active空但仍有时效层/_effects、自主wakeups、host pending choice/presentation/fiber | 如phase1不支持则精确UnsupportedSavePoint；完整save卡按全部provider保存或拒绝，不清零/重启/重跑初态 | B保存后续卡 |

阶段结论应分别标注 foundation、C06迁移、执行连续性、完整save、正式游戏stress。前一阶段通过不替后四项，原有27项失败仍独立追踪。任何测试读取域由主代理另行精确授权，不由本建议扩域。
