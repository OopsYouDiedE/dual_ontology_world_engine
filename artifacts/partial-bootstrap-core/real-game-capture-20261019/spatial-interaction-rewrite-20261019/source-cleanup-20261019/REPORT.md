# DV与Python接口清理

本轮清理退役源码入口及其配置，不调整现役游戏算法、内容或资源。来源提交、逐文件删除原因、删除前SHA-256和生产/调试include闭包见同目录cleanup-manifest.json。

移除的DV包括未实施的surface-v1语法提案、旧语义回归模块、仅由旧编排调用的三个main包装、固定比较配置fixture与未接线回忆fixture。移除的Python包括旧多会话文件计划编排、重复Core自举/独立构建包装、依赖scratch/test-build-3的语义回归包装，以及写入退役Awakening场景的地图构建器；相应两个旧JSON计划一并清理。

保留生产game/entry.dv与debug-run.dv、共享编译器/Session构建链、check_dv/dv_lsp编辑接口、run_debug及恢复/证据依赖。独立checks算法、oracle、可读投影、编辑器仍使用的main-causal入口、机制示例和被现役文档引用的未实现草案也保留。共享素材生成器及现役MCP接口不因未被游戏直接import而删除。

模块AGENTS与参考文档已同步；旧Awakening README改为迁移导航，移除失效场景、回归和重建命令。旧内容源码可从Git恢复，原始artifacts、保护清单、.story与译文没有改动。

静态记录：生产和调试入口include闭包内的文件均仍存在，字节摘要与清理前一致；现役源码/文档中未发现已删除完整路径的字面引用。此核对只涉及依赖与文件字节，不证明动态调用、编译或游戏行为。按用户先暂停验证的要求，没有运行构建、探针、回归或正式会话，各级游戏验收仍未验证。

原有godot_csharp/project.godot工作区修改继续保留，不纳入提交。
