# Example TASK CARD — M01 删除旧案件专用执行器

> 状态：EXAMPLE / NOT AUTHORIZED。本卡展示下发结构，不授予任何文件修改权限。以下路径均来自2026-10-09静态审查，首次执行时由F00验证。

- **Main agent**: GPT-6.1 Sol XHigh；只负责调度、复核、集成
- **Task**: M01；原组S01；执行模式`SCOPED_WRITE`
- **Purpose**: 删除旧 Hall 案件专用计算和重演的生产路径，不保留替代入口
- **Prerequisites**: F00 验证实际 HEAD、引用及旧回归；F02 创建独立clone/分支、可信控制层和租约
- **Branch**: `agent/M01-remove-old-case`；base SHA 必须由F00从当前 integration 提取
- **Read**: 经控制器核定的两个旧文件、调用处、构建配置及必要的公开接口
- **Write**: `csharp/HallCausalityRules.cs` delete；`csharp/HallReplayProgram.cs` delete；**仅此两项，且须实际签发**
- **Forbidden**: 修改Core其他原语、变更任何`.story`、将案件类搬到工具/测试、删除失败断言、写回放采样器、扩大自身任务范围
- **Stop condition**: 当前代码存在不可删除的真实调用或公共接口兼容要求，提交证据，状态变为BLOCKED；不得顺手修改调用方
- **Acceptance**: 删除动作符合租约，现有接口与主流程构建可运行，正式权威执行不包含Hall专案路径；回归失败提供旧/新对照和修正意见
- **Evidence**: 基线SHA、git diff、全部提交SHA、CI测试输出、调用搜索结果、风险及遗留问题
- **Independent review**: 代码边界审查 + 功能回归审查；合并权限由可信集成控制器保留

**任务提示**：仅在你自己的隔离目录和分支工作。对任何不在租约中的问题，输出异常证据和新任务建议，不直接修改。不得将编译失败的补丁伪装为成功，也不得更新黄金预期。
