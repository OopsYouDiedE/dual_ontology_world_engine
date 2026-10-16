# 数值 ABI 静态审读（2026-10-16）

范围：只读审查 compiler/Runtime.cs、compiler/compiler.dv、compiler/seed/Program.cs，core/00-data.dv、50-response.dv、98-debug.dv，verification/session/16-personal-choice.dv、20-speech.dv、30-runner.dv，game/campaign.dv 与直接供应相关字段的内容声明。未读取设计文档，未运行构建、编译、测试、Godot 或会话。审查期间其他代理正在修改源码；以下定位以函数和接口为主。

## 已证实的调用合同错误

- `R.is_number(object)` 调用 `Text(s)` 后解析字符串。`dvp_owned_quantity` 首先对 declaration 调用它，而 chapter1-content 的 deep-resonance 明确供应 `{"policy_key": ...}` 记录；相同函数还对 policy 的 double 值调用它。两者均在解析前抛出 `Expected Text`。同文件 owner_requirement_refs 对这些记录调用它，erosion_update 对 chapter2 `erosion_exposure` 的 `dv_field(0)` 数值调用它，具有相同错误。应使用运行值谓词，不通过文本序列化迂回。
- `dv_validate_expression_policy` 使用 `rt_is_number(rt_json_stringify(weight[1]))`，不会因参数非 Text 抛错，但 Rational 的 JSON converter 写成 JSON 字符串，使合法精确数被拒。改为 `rt_is_numeric_value(weight[1])` 与后续 `dv_dot` 相容：乘法、加法、比较已具 Rational 分支；数字文本、bool、record 继续被拒。

## 新数值谓词的接线审读

`R.is_numeric_value` 只接收有限 double 或正分母 Rational；bool 不满足任一 C# 类型模式。Rational 是 sealed record，数值字段只有 get，构造器拒绝零分母、统一分母为正并约分，因此现有可构造 Rational 均有效。`Rational.From(bool)` 会落到 `Text` 并拒绝，不会隐式把布尔转成数。

compiler.dv 的 arity 表为新名字登记单参数；`ck_type_raw` 将返回类型标为 Bool。seed/Program.cs 对任意 `rt_` 调用机械生成 `R.<后缀>`，不存在需补的第二份 builtin 白名单。自举/Session 工具复制同一 Runtime.cs；本次未通过执行验证生成产物。

## 投影、规范收据与成功账本

`dv_debug_event` 原有非 record action 分支直接 `rt_show(raw_action)`；Runtime.show 只处理 string、Rational、double、bool，普通 list 需要 `rt_json_stringify`。先判断 `rt_is_list` 后采用 JSON 的建议与 ABI 相容，且只改变只读投影。未将此静态边界称为本轮会话已触发的错误。

`c2_norm_actions` 明确供应 record 形状的 norm_receipt 与 norm_decision；`dvp_normative_ready` 在选择前检查该主体实际 proposal 的 source/parse 及 norm 一致性。`dvs_complete_norm` 只在 applied 后执行，读规范还要求实际 observation 事件及本人 content 为真；来源字段与 norm 声明的消费形状吻合。`30-runner` 只在 execution.status 为 applied 时追加 completed_actions；资格侧读取的是 action_id 列表。campaign 扩展保留 live actors，再经 owned state update 替换外壳，未见将成功账本转成其他形状的调用。

该范围内未发现新增的、具有实际供应值和调用证据的确定 ABI 错误。这个结论仅为静态审读；实际动态分支、人物合理性、两章完成、画面与几何均未验证。用户音乐文件未操作。
