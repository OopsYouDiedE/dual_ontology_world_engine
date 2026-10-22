# 等价因果切片共享：有限机制验证（2026-10-23）

本轮未新跑正式会话。03d5c802的history/证据保持原样，不用于新缓存效果宣称。候选原序、全部角色统一参数、320总模型预算与Replay_Depth保持。

## 定位与改动

旧history证据e2215/e2217/e2219都是相同石板第一行的小切片，各12单位；e2216/e2218/e2220完整候选仍分别执行。这种重复探测至少值得共享，但只读旧结果不能推断新预算足够覆盖全部216个竞争候选。

30新增一次付费重演内的显式model_probe_cache。缓存键包含主体、完整遮蔽初态（包括声明）、响应模型、深度、切片计划、假设、特征、prior、basis、起时。候选ID和与该切片无关的元数据不要求相同；mask规则只有在生成的完整初态相等时才可能等价。缓存保持声明模型值，不触发会话动作或授予本人观察。

仅plan_complete且无failures、非budget-exhausted的切片入缓存；命中还要求原计算量不超过当前工作额度。返回原预测及其来源，expanded=0、reused_work=原工作量，remaining按当前预算保留。新model_probe_reuse事件显式引用原计算事件，不把原hypothetical事件ID冒充新计算。局部缓存由同次Complete内各候选传递，结束即移除。完整失败/未知模型不共享、不放宽资格或当成反证。

这能节约等价组件；没有改变候选顺序或给正确模型保留专属预算，没有把未知陌生关系补为默认信任，也没有提高预算保证结果。

## 有限验证

Probe.cs通过生产DuelView.Core机制调用，没有正式会话/AI调度。命令：dotnet run --project artifacts/perf-opt/aggressive/branch-planning/probe-mechanism/Probe.csproj -c Release --disable-build-servers -p:NuGetAudit=false。

本轮退出0，实际输出依次确认：首个切片完成且比较相符；不同候选ID的等价切片复用且不二次扣费；原预测与当前预算保持；保留原事件引用；本人输入改变不命中；当前工作额度不足不导入较大缓存计算；预算耗尽比较不入完整缓存；不同owner不命中。测试未证明正式历史候选一定命中或完整排除全部竞争者。

LanguageCore Release先前整批构建成功（34.35秒，0警告错误）；其后增加“budget-exhausted不入缓存”守卫，最终有限验证命令重新构建了当前生产Core并运行成功。git diff --check通过。没有重新执行整局、因果检查或Godot。

## 仍不足

保留非唯一结论：既有history完整模型吻合，但竞争覆盖不足仍成立。新机制是否在历史候选真实运行中命中、节约多少单位，以及是否足够形成可采纳论证，均待未来授权新会话测量。由于陌生关系的响应输入仍未声明、部分竞争候选未完整计算，仅做等价共享不能先验承诺在320预算内证明唯一。

## 独立开关补齐（2026-10-23）

51a4d7b8最初未提供独立开关；本轮补--opt-replaycache on|off，默认on，不与行动规划plancache混用。run_debug注册后自动进入run_config和回放配置映射；60默认值、布尔校验和主体策略均接通；30在off时直接普通执行，完全绕过缓存查询/写入。没有Runtime全局开关，因为该缓存仅在DV会话Complete内持有。

有限Probe程序改为引用Session工程，实际验证dvg_run_config默认on/显式off及dvg_configure_actor_policies向主体传递off；随后在已有缓存存在时切换off，确认真正重新执行并扣原计算量。on/off的feature predictions、comparisons、status、plan_complete逐值一致；expanded、remaining和显式来源复用记录按实际计算差异报告。原有输入/主体/额度/截断负面验证继续通过。最终命令退出0，当前Core与Session均由ProjectReference重新构建。run_debug --help实际显示新开关；未新跑正式会话。

已与/root/sol61_audit_speed协调：其工具分支负责run_aggressive_matrix统一FLAGS、全关基线及消融项登记replaycache（本分支没有受跟踪matrix脚本，未复制主树未跟踪脚本）。run_debug各分支的开关名单合并时取并集，包含replaycache。矩阵工具提交由主代理整合，不能以本分支单独提交声称整个矩阵已运行。
