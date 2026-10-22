# taskgraph生产默认关闭

基线767d46a6；分支codex/taskgraph-default-off。未读取设计文件，未执行正式会话或AI调度，未改taskgraph算法。

60规范化配置、30两个缺省读取、原生CLI新开局和来源投影缺省均改为opt_taskgraph=false；显式on实验与保存配置恢复保留。统一FLAG_DEFAULTS供run_debug参数和矩阵使用，storyflow/stateindex和其他既有开关仍默认on。帮助与工程文档同步。

validation显式传taskgraph off。独立消融相对生产默认翻转：taskgraph off→on，其他on→off；拒绝taskgraph非false的S1锚点。旧all-on标签改为production-defaults-or-single-toggle。测量保留生产默认清单，run_config为实际配置证据；Markdown直接列出result.run_config中的opt_*，缺失时注明而不由场景名补推。taskgraph机制夹具显式opt-in，不再依赖默认。

有限验证：当前20–60经build_session生成临时程序并Release构建，没有打开会话。探针实际调用dvg_run_config和dvg_configure_actor_policies，核对运行配置与主体policy一致，实际输出：

```
default opt_taskgraph=false
on opt_taskgraph=true
off opt_taskgraph=false
```

首次临时探针误用带命名空间的DVProgram，出现CS0234；按真实生成的全局类型修正后，Release构建无警告、无错误，生产源码未因此变化。工具硬化检查完成，新增实际argparse默认off/显式on/off、storyflow仍on，以及矩阵validation off和双向消融参数检查；既有来源/文件/参数机制未报错。

未验证整局行为、Standard/Foolish效果、原生CLI界面或性能。临时输出仅在本分支`.scratch/taskgraph-default-probe`。自动审批策略拒绝了两次原生PowerShell递归删除（首次先核对工作区内路径，第二次已单独核对绝对目录和Directory属性后使用固定LiteralPath）；未改用其他解释器绕过拒绝，清理未完成，需主代理后续处理。该目录被忽略，不进入提交。
