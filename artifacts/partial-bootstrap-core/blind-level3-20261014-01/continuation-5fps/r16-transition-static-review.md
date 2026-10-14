# R16 长停顿静态审查

日期：2026-10-15。仅阅读现役 DV、生成的 Session/obj/dv/DVProgram.cs、被 Session 工程引用的 compiler/Runtime.cs，以及本轮 detective-r16-launch.log 末段。未读取设计规范、私有规划或其他会话；未修改保护源码、运行会话、构建或测试。本报告为静态排错，不是判答或章节验收。

## 可证事实与定位限制

读取日志时，末条记录为 detective.answer.trust 的 action_execution applied，game_time=752。runner 的这条 rt_print 位于观察更新、dvs_answer、answer_accepted/answer_rejected 写入及可能的章节扩展之前。因此该行只能证明行动执行已被记录，不能证明判答已接受，也不能证明代码已进入 dvg_extend。

dvg_complete 同步先执行 dvs_session_complete，再执行 dvg_extend。DvSession.Complete 只有两者全部返回后才生成呈现 JSON；GameEngine 在后台 Task 完成后才发布 state/dv_present。故渲染与采集仍可响应，但这次业务回执长期不交付，与后台任务长时间计算相容；这不是具体停顿栈的证据。

## 有限循环检查

dvg_chapter_two_ready 遍历固定阶段表；dvg_extend 本身没有反复扩展的循环。它只在 story.chapter=1 且真实完成条件成立时扩展，成功返回后 chapter=2。c2_extend_scene 和 c2d_install_scene 分别有 installed 标记。

检查的安装循环按有限 people、rooms、edges、objects、rows、actions、bindings 等列表及有限 day 区间推进索引。c2d 的 actions 重写通过替换保持长度，不在该循环追加动作；bindings 的追加不会扩大其 objects/actors 循环边界。未发现这些入口中的明显无界计数或自增容器遍历。

有限循环仍可能因每步复制的大树而极慢；静态有限不构成现实耗时上限或已经返回的证明。

## 已确认的高成本结构

1. **每次采用法则反复深拷贝增长的 world。** core/10-laws.dv 的 dv_adopt 先 dv_with 更新 adoptions，再通过嵌套 dv_with 更新 world.adoptions 和 revision。dv_with 会深复制未修改条目，因而一次采用不只是插入新法则。c2_extend_world 按人员、边、日期等批量安装，c2_install_actor_laws 又用于各主体 mind，反复复制增长中的法则表和世界对象。该结构具有随安装规模增长的累计复制成本。

2. **新增对象同样复制世界。** dv_add_object 采用嵌套 dv_with；c2_world_objects、主体共享对象与 c2d 对象安装反复调用。旧的真实对象、法则和来源没有被抛弃，复制量取决于继承状态大小。

3. **章节组合复制主体和整份事件历史。** dvg_extend 的 live 使用 dv_with_many 更新 story；c2_extend_scene、c2d_install_scene 及 chapter 赋值再次复制组合记录；最终 dv_with_many(state, ...) 对更新中的 events 也调用 dv_clone。此时 events 包含整局行动、前后认知、推演等历史载荷。这是源码中确实存在的整份历史复制路径。

4. **主体更新也复制已有负载。** c2_extend_actor_from、c2_refresh_actor_goals、c2d_refresh_actor_goals 等对 actor/policy/mind 使用 dv_with/dv_with_many。原主体保留的缓存、模型与历史相关字段因此参与复制。具体存量大小未在本审查中测量。

5. **动作列表循环重建容器。** c2d_install_scene 每个 action 都调用 dvs_replace；该函数只复制列表外壳并复用元素，并非整树深拷贝，但每次仍遍历整个 actions，构成重复列表分配。不能将它误报成与 dv_clone 相同的递归复制。

6. **实际 runtime 是递归逐项复制。** DuelView.Session.csproj 引用 compiler/Runtime.cs；DVProgram 中 F_dv_clone 直接调用 R.copy_value。copy_value 对每个 List 新建列表并递归复制子项，没有共享子树记忆化。当前生成的 F_dvg_extend 也保留上述调用链，没有编译器自动消除这些复制。

## 推测，尚未证实

多次大世界/主体复制、法则采用时的增长复制，以及最终 events 整树复制，是长停顿的具体静态候选；大量临时分配也可能带来垃圾回收或内存压力。没有线程栈、阶段耗时或分配采样，不能将八分钟停顿归因于其中某一个函数，也不能据此断言内存不足、死锁或无限循环。

action applied 与章节扩展之间仍包括观察、判答、接受转换与传播等工作；日志没有这些位置的入口/出口时间。因此还必须保留“尚停在 runner 内部、尚未进入扩章”的可能。

## 后续最小排错方向

当前正式会话保持源码冻结，不用第二会话或重新构建争抢资源。若能取得当前后台工作线程的被动采样，可区分停在判答、采用、复制或序列化；仅 CPU 活跃或窗口响应不足以定位。

若本轮结束后另立修复基线，先给阶段入口/出口记录实际时刻，区分 runner 返回、c2_extend_world、逐主体扩展、dialogue 安装、组合历史复制及呈现序列化。若随后优化复制，必须保留原状态隔离、别名行为、真实事件、前提与采用检查，不以删事件、重置主体、注入完成状态或跳过原接线换取返回。

截至本次审查，未获得实际判答回执、章节内容可用事件交付或真实跨门证据；均不能由源码推定已发生。
