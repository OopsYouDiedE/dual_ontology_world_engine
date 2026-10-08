# 第一章 staging 独立验证

日期：2026-10-08。验证 Agent：`verify_staging`，独立上下文。未读取 `docs/design-standard.md`，未访问其他工作树，未修改产品实现，未提交。仅运行本任务编译、构建、连续协议与现场导航/动画开发自检；未运行全量 flow。

## 结论

- **构建：通过可行替代命令。** 剧本编译生成 7 项，初次和最终 `--check` 均通过。默认 `dotnet build godot_csharp/DualOntologyWorldEngine.sln` 和两次 `python tools/verify.py --only host` 均退出 1，只报告 0 个错误、0 个警告；诊断记录停在 restore 子目标失败，没有足够信息确定原因。最终源码使用 `dotnet build godot_csharp/DualOntologyWorldEngine.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:minimal` 成功，0 个错误、1 个 NU1900 网络警告。不能把替代构建成功记为原 `verify --only host` 通过。
- **目标机制：通过。** 正式 `Chapter1Session` 从全新实例开始，经醒来和连续相遇流程，正常场景及 5 个 `WalkOk=false` 场景全部通过；场面数为 2/6/4，运行时钟实际结算合计为 24/36/8 分钟，四人欲望各增加 1 且只发生一次。
- **现场导航/动画：目标开发自检通过。** 正式 WorldView/Awakening 中 13 段真实导航均可达、有成功回执、到点误差小于 4 像素；130 速度下最大单帧位移约 2.1667 像素。5 次 interact 和 Huo inspect 实际动画名正确，7 张 PNG 已逐一查看。
- **人物行为合理性、文学审评、真实游玩体验：未验证。** 独立验证未接触设计依据，也未盲玩。

## 协议证据

最终结果：`independent-protocol-smoke-final.log`。完整输入/输出记录：`protocol-normal.jsonl`，以及 5 份 `protocol-fail-*.jsonl`。临时验证源码位于 `scratch/ch1-staging-smoke/`，引用正式 `Chapter1Script.csproj`。

快速回执对白和 stage；Sleep 使用实际墙钟经过的秒数推进，正常场景用时约 4.90 秒；灶台失败约 3.27 秒，4 个领取柜失败场景各约 3.96–3.98 秒。这个耗时只证明动作等待，没有游玩时长含义。验证没有写入进度、欲望或答案；几何上下文及到达回执由夹具模拟，因此不是盲玩。

断言覆盖：

- 灶台 WalkTo → interact → 1.5 秒 → idle → `meeting:Chef` 返回 → `s2_tea.008`。
- 领取柜按 Shen、Huo、Su、Detective 顺序走位和 interact；各成功动作至少等待 0.8 秒，Huo 后续姿态为 inspect，其余 idle；返回各自 meeting 前缀锚点。
- 每个岗位命令在对应 `.004/.005/.006` 已回执之后发出，并先于下一条 `.005/.006/.007` lines 输出。
- 暂扣全部 3 个岗位走位回执时，仍可继续到 `.007` 和 investigate；此时 `posts_assigned=true`、岗位作业仍等待到达。旗标语义为已发出岗位分配，未要求全部到达。
- 5 处 stage_action 分别收到 `ok=false` 时均跳过 pose 和 Sleep，立即继续返回走位，连续流程不会卡住。
- 新锚点生成值：备餐灶台 `(394,558)`、领取柜 `(500,132)`。

验证夹具曾漏回执公告框 camera，并曾把走位前的通用 halt 错判为失败后动作，已修正；早期失败轨迹保留为 `protocol-harness-initial-missing-camera-ack.jsonl` 和 `*-harness2.jsonl`，相应日志保留。临时夹具的语法错误和崩溃进程占用造成的构建失败日志也保留，不归为产品失败。

## 现场渲染证据与观察

`independent-render-workspace-log.log`、`render/godot-runtime.log` 最终显示 `STAGING RENDER failures=0`，进程退出 0。实际检查了：

- `render/01-stove-interact.png`
- `render/02-locker-shen-interact.png`
- `render/02-locker-huo-interact.png`
- `render/02-locker-su-interact.png`
- `render/02-locker-detective-interact.png`
- `render/03-locker-huo-inspect.png`
- `render/04-posts.png`

人物、灶台及房间家具资源正常，无明显角色被家具遮没或错位。领取柜锚点画面位于墙上铜口正下方，没有明显单独柜体；这是视觉观察，未据此补资产或作设计结论。岗位总览受正式视野遮罩限制，不显示所有房间中的人物。

本项夹具注入初始站位并调用正式现场走位和动画，只是现场导航/动画开发自检，不是完整二级 UI 验收，也不是全程游玩。

## 环境限制与未覆盖

Godot import 完成、退出 0，但日志含根证书读取失败和不能写入 AppData/Roaming 下的 `editor_settings-4.6.tres`；原窗口启动因 `user://logs` 无法打开后 signal 11 崩溃，脚本未执行。失败记录保留在 `independent-godot-import.log`、`independent-render.log`。随后仅用 `--log-file` 将日志重定向到工作区，并加入 `--disable-crash-handler`，目标脚本成功运行。运行环境另有 OpenGL 回退 ANGLE、WASAPI 输出初始化失败并回退 dummy、根证书读取失败；本轮未验证声音。沙箱也拒绝 WMI 进程枚举；已仅清理本轮启动并崩溃的进程，未改环境配置。

正式一级三份全程记录、二级 UI 专项验收、三级独立盲玩均未执行。已知无关 flow 及旧开场选项故障未运行或修复。自动协议与截图不能替代以上正式验收。

其余主要证据：`independent-compile.log`、`independent-compile-check-final.log`、`independent-build-diag.log`、`independent-build-final-single-node.log`，默认 host 原始 stdout/stderr 已按字节复制到 `verify-host-raw-initial/` 和 `verify-host-raw-retry/`。
