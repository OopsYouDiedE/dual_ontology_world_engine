# 07 有限几何/UI探针（仅已准备，尚未运行）

`probe.gd` 是外部 SceneTree 脚本，直接实例化生产 CampusMapView、living_floor 场景、StoryActor 和 FrameArchive。不会加载 Main、GameEngine、DV 或正式会话。历史脚点 `(2049.26440429688,1089.23413085938)` 和 blocker 均为明确标注的隔离几何夹具；这些身体位置不代表任何正式世界状态、玩家路线或进度。

运行须等待主代理空闲窗口，并在07统一 build/import 后，以07工程为 `--path`，本脚本的绝对路径为 `--script`，追加 `-- --out=<全新绝对目录>`。不要用 `--headless`，因为需要真实渲染截图；脚本45现实秒硬截止。不修改运行工程，不注入正式状态或答案，不开 Main 作为隔离失败的回退。

预备覆盖：开放地板相邻短步、原身体 test_move、非相邻目标、错误源房、现有墙端点以及明确添加的动态碰撞身体。每次记录真实格坐标/房间/可行走性/test_move/proof。调用仅查询，不执行正式过门。三语连续通行提示使用生产 phase-to-text helper 的独立UI夹具字典，不提交宿主，也不代表输入、模态抑制或实际通行已通过。

预备产物为至少两张 PNG（开放边界查询与动态身体阻挡）及 FrameArchive `probe.dvlog` 二进制输入/结果；完成前保留 `.partial`。原始记录不落 JSON。保留脚本错误、超时或失败，不在同一目录覆盖重跑。引用任务 `carry-forward-03/carry-forward.dvlog` 与 `blind-04-open-boundary-closed-evidence.dvlog` 为外部只读证据链。若复测，先新建独占目录复制前轮所有原始记录并校验。

限制：未运行，脚本语法/资源/隔离接口尚未实际验证。原地图采用场景 authored geometry，fixture snapshot 为空，不重建正式 DV 几何绑定或候选；source/target 声明只是历史几何查询参数。该探针不能替代正式 Prepare → 真实碰撞过门 → Complete 验收，也不能作为盲玩或性能结果。首次运行如不能隔离则保留错误、停止并报告静态范围，不启动正式主会话。

脚本保存并在完成/硬超时恢复进程原 locale，记录 language.cfg/settings.cfg 前后 SHA-256；进程内切换语言不等于修改偏好。FixtureHost 的20px position_occupied 只是隔离宿主简化，不是生产动态占位算法；结果分别记录真实 CharacterBody2D.test_move。不能把每一种拒绝归为生产动态占位实现。
