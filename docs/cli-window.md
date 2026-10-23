# 真实 CLI 窗口截图

`python mcp/verification/cli_window.py` 管理独立 conhost 实例，以 PID 投递正常控制台输入，以 HWND 获取真实窗口像素。具体输入、封存和并行合同见 `../mcp/AGENTS.md`。

截图将捕获线程临时设为 per-monitor-v2 DPI aware；GetWindowRect 的物理宽高与 PrintWindow 输出一致，随后恢复原线程 DPI 上下文。否则桌面缩放会使 GetWindowRect 返回较小的逻辑尺寸，导致右侧及底部被裁掉。截图不缩放或补绘。图像元数据包含 coordinate_space 和 dpi_awareness；每份操作证据记录 operation_bridge_sha256，启动时的 bridge_sha256 仍独立保留。

有限渲染检查可用 stage=player、controller=llm 启动到输入提示，只截图并正常 quit；不请求 next、选择或确认行动，因此不构成全局 AI 验收。检查必须实际查看 JPG，确认窗口边界、右侧页码和底部 dv> 提示均存在。完整剧情仍须独立运行核验。

读取快照在控制台重绘或替换缓冲区时最多重读三次，只处理 Win32 1/6/87 的瞬时读错误；失败继续向上报告。读取前后缓冲区几何与光标必须一致才认可 ready。每份快照记录 read_retries/read_stable；重读绝不重投用户输入。

stage=player 强制 controller=llm，启动加 --map subjective --no-scores。桥在投递前拒绝 view/views、inspect/tp/time、未显式 subjective 的 map，以及非 off 的 scores；AI 审查阶段不受这些视角约束。此边界只覆盖该桥入口，不宣称原 CLI 已删除这些能力；盲玩必须只经桥投递命令。
