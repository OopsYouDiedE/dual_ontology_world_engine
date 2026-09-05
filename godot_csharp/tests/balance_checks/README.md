# AI 言灵、规则与契约平衡检查

独立 .NET 8 控制台项目，仅引用 Core。测试中的 AI 指确定性候选生成、预算预测与效用选择，没有调用外部大模型，不需要密钥。规则候选直接调用 Core.RuleTemplate.SampleCandidateRules；言灵按强度和成本自动组合实际配方；交换契约根据双方公开效用选择后交由 PatternBook 执行。

## 执行

从仓库根目录运行快速检查：

```powershell
dotnet run --project godot_csharp/tests/balance_checks -c Release
```

运行三档时长、三档价格对照，并保存报告：

```powershell
dotnet run --project godot_csharp/tests/balance_checks -c Release -- --long --output docs/ai-construction-balance-results.md
```

`--output` 只写调用方指定的文件，目录须已存在。默认只输出控制台。重新执行上面的命令会更新本次测试报告，不改写学院历史轨迹。

上线门槛检查：

```powershell
dotnet run --project godot_csharp/tests/balance_checks -c Release -- --strict
```

退出码0表示受限候选的不变量通过；退出码1表示至少一项不变量或探针执行失败；严格模式下退出码2表示仍有开放式创建风险。目前仍有 F01、F04，严格模式应返回2。

## 覆盖与口径

48组言灵由4档库存、4档价格和3档强度组成，检查预测不改状态、失败不残留支付及真实产出。另验证嵌套/拆分支付、自缚折扣及破约恢复、互利交换和单次结算、6组期限预算/履约组合、隐藏硬禁令以及规则去重与采信。

长程运行通过 Soul.InstallStep 自动构造候选，Session 基于心智视图过滤，效用选择可支付的最大进展方案，经声明式行动提交。施法的 Changes 从配方 Cost/Effect 提取；不宣称已支持全部多阶段仪式、槽位或持续配方。底层 Kotodama.Invoke 的支付一致性由独立矩阵检查覆盖。

每个长程场景初始资源8，每100 AV供应2，行动成本10 AV；供给和注册顺序相同，仅改变单位价格。时长为1,000/10,000/100,000 AV，价格为0.75/1/1.25。检查库存与流量守恒、收益付费、实际达到目标时间、活动队列规模，并比较配对价格变化是否影响有效进展。

未引入随机数，矩阵样本不是相互独立的随机实验，不计算置信区间。报告中的耗时为单次诊断，受机器和预热影响，不是性能结论。经济进展是测试产出，不代表人物已成长；单主体循环也不能代替多人文学评审。

## 风险探针与当前结论

- F01：可信配方工厂可直接生成无成本资源赋值。
- 已修复 F02：PatternBook 使用真实变化预算；独立 consequence_budget_checks 检查预测与实际收费。
- 已修复 F03：异常规则候选仅依据已观察事实，不读取隐藏 Danger。
- F04：直接 Add 可安装硬条款，AI创建所需授权与双方同意并非工厂职责。

探针复现风险，不把“漏洞必须存在”写成断言。风险修复后条目应消失；随后应扩展真实 AI 创建入口的端到端用例。默认测试通过不能用来宣称开放式 AI 已平衡。

详细设计见[长期模拟方案](../../../docs/long-term-simulation-plan-zh.md)。正式回归仍应运行原有 literary_checks 与 runner，新增检查没有替代它们。
