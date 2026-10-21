# 工具中心维护

本目录继承仓库根目录 AGENTS。用户当前执行限制优先；只要求实现时，工具源码和路由接线不触发构建、检查、采集或探针。

- `tools.json` 是公开操作、命令路径、函数归属和运行模式的唯一目录。`server.py` 从 `routes` 生成公开枚举，`tool_actions.py` 只执行通用路由，不维护第二份操作名单。
- 新增可独立调用的工具时，同步添加实际路由与参数说明。共享库、场景组件、编译桥和编辑器回调通过其所属入口使用，不另造空 handler。
- `tools/list` 只读取静态目录。MCP/LSP 初始化可调度 `build/prewarm.py` 的后台编译器预热；请求线程不等待编译，其他管线在调用时导入。所有子进程输出进入响应、日志或 stderr，MCP stdout 仅承载协议。
- Godot 工具使用 `godot_workspace.py` 的临时工程。纯地图、纯 UI Mock 和资产适配器不创建 C# 项目；显式 C# 采集工程才编入 `FrameArchive.cs`。工具 `res://addons` 和 `res://tools` 路径只在该工程中映射。
- 长驻服务中的临时工程使用 `workspace()` 作用域及时释放。工具生成预览放 `sandboxes/generated`；原始素材放 `asset_pipeline/sources`；成品放 `godot_csharp/ShareAsset`；日志和发行文件放 `artifacts`。
- 二进制证据完整性、领域分析是否完成、主因果是否获得支持分别报告。负面领域判断可以是已完成分析；缺少前提或无效轨迹不能标成已完成。
