# r7 隔离错误堆栈诊断：通用回应表达类型错误

该工程诊断保持r7全部领域源、原输入、调度、行动代价与预算字节不变。唯一差异是compiler.dv及启动seed的错误输出Message改ToString；由真实C0至C3链生成C#，未手改生成物。具体差异与指纹在隔离源diagnostic-change.json。该编译链固定点只对应此诊断链，不是完整Core或游戏验收。

主体实际248.547现实秒exit1，仍在e750侦探重演game787（累计307分钟）之后发生Expected List，无最终trace。原r7正式失败证据不覆盖，这次只提供完整调用栈。

调用链为R.Items/R.count → dv_dot(features,weights) → dv_expression(appraisal,expression_policy) → dv_respond → dv_respond_adopted → dvs_sim_response → dvs_model_hypothesis → dvs_run。具体失配位于回应表达向量列表接口；已交模型/内容owner核实weights或features数据类型并修DV真源。没有据此改变剧情条件或成本，没有把诊断程序当正式完成记录。
