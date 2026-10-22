# 预测移动产生自检收益循环：通用修复

后续补正：7703d634正式S1仍出现循环，证明本报告夹具未覆盖实际触发。限定节点核对发现另一个实际根因：未来minimum_time=5370在2329被反复奖励，详见taskgraph-future-evidence-fix.md。以下来源保留修复只保留其有限机制结论，不能用来声称当前实际循环已解决。

日期：2026-10-23。独立检出 dual_ontology_replay，合并主线225ab40d后实施，保留此前重演证据报告。未读取设计文件，未运行正式会话；没有角色白名单、预算或剧情变更。

## 根因与改动

30-session 的 dvp_forecast 使用正式 DV 预测，效果通过 dv_stage 写为假设 cell，不携带实际 perception claims。预测移动写本人位置后，dvp_value_context 原先只检查后继 cell 是否具有 perception；已经实际获得的位置观察因预测替换丢掉 claims，被重新算作未满足的 evidence，向同一自检 action 生成 information=1。选择性最近任务与既有路线重核都消费该估值，因此反复移动和自检可以被错误维持为有利任务。

现在 dvp_actor_view 首次进入 prediction 域时保留只读 planning_evidence_mind，后继预测沿用同一真实证据起点。dvp_value_context 的 evidence 来源检查读取该起点；动作资格、未来可用条件、位置、个人价值与正式效果仍读取本节点 mind。已知证据不会仅因预测写值而被清空或重新奖励。真实 actor 不保存该字段，下一次正式规划从最新实际 mind 开始；真正的未知输入、真实值变更导致旧 perception 不支持当前值、或新的 minimum_time 要求仍可产生检视价值。

该字段是瞬态规划视图的内部只读依赖，不发布观察，不执行动作，不复制预测结果进真实 mind，不降低正式资格。对所有主体与两种规划路径统一适用。source claim、minimum_time 与声明条件未放宽。

## 有限验证

- dotnet build dv_core/base/DuelView.Core.csproj -c Release --disable-build-servers -p:NuGetAudit=false：成功，0 warning、0 error。
- python mcp/verification/check_planning_evidence.py：成功；只构造独立通用夹具，直接调用本轮生成的 DVKernel，不创建会话。
- 相同夹具用原 node-local 估值路径重现预测移动后 information=1；修复后 root、第一次预测和继续预测均 information=0。
- dvp_selective_followup 对 move→inspect 的既有路线返回 matched=false、committed-task-no-longer-valuable，防止原错误路线继续复用。
- 真实未知输入仍 information=1；实际变更但没有相应 perception 的输入仍 information=1；minimum_time 从10提升到20后旧证据仍失效，真实20时刻的新 perception 使 information回到0。
- 上述有限机制检查分别关闭/开启规划缓存执行，结果相同；原真实 actor 未增加 planning_evidence_mind。检查临时工程自动删除，.scratch为空。

## 范围与未覆盖

没有重新运行 S4 或 S1，没有声称实际往返、预算终态或章末效果已改善。未扫描原始大 DAG，本次定位依据主代理提供的 S4 症状与源码正式预测写值语义，有限夹具独立重现同类错误。新增检查位于mcp/verification，未添加公开MCP路由。未来工程合同应明确“预测后继的 evidence 满足度保留本次真实规划起点；正式状态变化后重新读取实际证据”，本报告记录该内部依赖与所有权边界。
