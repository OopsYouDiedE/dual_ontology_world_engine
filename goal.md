# 当前制作目标

将 Gemini 的前五日剧情与现有 C# Core 接入既有正式 Godot 场景与界面，使玩家从标题新局开始，能移动、观察、交谈、执行行动并连续推进，多个人物独立行动。不得另建临时房间界面替代正式游戏。以代码依赖树为整体重构依据，可玩性优先于动作接触和表现精度。

正式入口为 `scenes/main/Main.tscn`。`config/coregodot-debug.dv` 指向 `assets/data/scenarios/campaign.dv`；后者引用五份单日源内容。客观事实与人物认知由同一个 `RuntimeSession` 保留，Godot 负责空间呈现、输入和真实执行回执。CLI 供逻辑回归，不能替代 Godot 可玩性验证。

正式宿主固定为 `scenes/main/CharacterSessionHost.gd`。五日剧情仍需补齐该宿主所需的空间、角色初始布置及动作表现映射；缺少映射时明确报告，不切换到替代界面。代码依赖与重构边界见 `docs/代码结构树.md`。

HTML 演示与临时 Compact 界面已撤除。历史阶段起点、旧截图与旧检查数量不作为当前完成证据。当前实施及未完成项统一记入 `docs/33 实施与验收.md`。
