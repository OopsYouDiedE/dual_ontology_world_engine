# 连续过门释放取消：宿主严格证明与人工重试方案

状态：仅静态方案，已与core_audit对齐；不改根源、candidate04/05或任何冻结副本，不构建、不启动会话。该方案针对blind03中Prepare迟到、人工输入已released、真实身体仍在源房而同move被rejected_signature封住的路径；不是一般失败移动位置同步。

## 证明形成位置

WorldView._finish_cancel_if_safe当前只证明_continuous已prepared且cancelled、真实adapter.departure_room(body.position)==from_room，随后发blocked。新增资格只允许明确reason=released，先不扩展turned-back/new-target/input-blocked/blocked/source_changed等原因，也不接受arrived/reconciling路径。

在该函数中先将phase置settling，再停止本次局部路线并将身体velocity置零、保留真实position；这样调用_stop_walk时_cancel_continuous遇settling直接返回，避免递归再完成。仅在同map、真实departure仍from_room且无在途切图、execution_transitions为空时发源侧取消证据。不能传送、不能把返回源房过程冒充到达目标，不能声明成功。

WorldView原生证据补充from_room、to_room、map、request_id、serial、action、actor、request_source_position、actual_position、transitions=[]、reason=released、连续模式标志；坐标直接读同一身体。该层不声明identity_verified，也不自造action_event。

## Main独立核验

原DvFieldExecutor在源身份核验后捕获的source_map/source_room/source_position仍作为权威宿主源证据。Main._accept_execution在当前_dv_intent/_dv_serial/_continuous_selection仍存在时独立核对：

- 本轮pending actor/action/action_event非空；action.kind=move且无station；request_id与独立保存的continuous selection一致。
- raw who/action/serial分别等pending actor/action及_dv_serial；raw map/source_map等请求map及当前adapter.map；from_room等executor source_room及独立请求from_room，to_room等pending destination.execution_room及请求to_room，from!=to。
- 本轮为人工玩家的同图连续模式，raw status=blocked，reason=released；actual房间raw room等from_room；raw transitions为空，当前adapter已停止切换、身体停止并真实departure_room等源房。
- raw source_position及actual_position均为有限二维数值；actual_position核对当前同一身体脚点。不得用旧snapshot、destination或request_source_position填实际停止点。

只有核验全部成立才由Main构造field.continuous_cancel并加identity_verified=true。核验失败则保留raw字段及原rejected，不签发重试证明，也不吞掉原错误。

```text
continuous_cancel = {
  version: "source-cancel-v1",
  action_event: pending.action_event,
  request_id, actor, action, serial, map, from_room, to_room,
  source_position: raw.source_position,
  actual_position: raw.actual_position,
  transitions: [], reason: "released", identity_verified: true
}
```

source_position是executor在Prepare现场开始、源身份核验后捕获的source_point；_continuous.source_position则是较早人工请求点。两者在准备等待期间允许不同，不能强求相等；额外request_source_position留作诊断，不替代source_position。source_position也不要求等actual_position，因为真实源侧局部走动或安全返回可以发生。core_audit已确认该区分并保持source-cancel-v1。

## DV资格和可见反馈

30按core_audit准备的continuous-cancel-retry-dv-proposal.patch核nested/raw/outer身份、源图/源房/坐标/空转交链等合同，只授带failure_class、event和本次rejected_signature/source_signature的manual_contact_retries token。保留旧rejected签名，不清全局禁用表，不扩大到AI自动动作。token仅对当前同签名、下一次明确manual_input选择同action有效；后继同动作Complete按现有规则覆盖。费用仍原动作单次结算，目标效果仍rejected，不创建世界位置或成功观察。

宿主在对应rejected Complete已经返回、确认该取消结果后，以现有show_aside显示短UI提示，例如“过门已取消，仍在原房间。重新输入方向可再次尝试。”并提供英日文。它不进入剧情对白、不发布世界观察、不代发select/dv_next，不伪称玩家已经过门；下一步必须来自真实人工新输入。若DV无法接受证明，提示具体失败/等待处理，不能承诺已获重试资格。

## 应单列验证的范围

新冻结后复现准备迟到→松手→真实源房blocked→Complete rejected→人工再次输入同move；确认重试获得新Prepare，原目标没有成功效果、没有移动/费用伪造、没有宿主自动重派。错serial/request/actor/action/source_room/map/transitions/reason或source_changed都不授token；AI与自动背景调度不消费该manual token。旧记录仅证明原故障，不冒充修补后验收。

该方案不解决已跨图、多图中途失败或无法真实回源的场景；这些仍遵循另列实际终点合同缺口。
