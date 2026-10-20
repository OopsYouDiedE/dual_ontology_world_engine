# 现役人物资源

正式地图人物由 `scenes/Shared/Field/WorldView.gd` 根据 DV 提供的角色键创建，身体与动画呈现由 `scenes/Story/StoryActor.gd` 承接。角色资源不提供主体决策、世界效果或知识。

| 目录 | 当前用途 | 制作入口 |
| --- | --- | --- |
| `PixelCast/` | 正式地图优先加载的像素动作图集，PNG 与 SpriteFrames `.tres` 配套 | `godot_csharp/tools/build_pixel_cast.py` |
| `AnimeCast/` | PixelCast 缺失时的现役回退图集，另保留上身裁切资源 | `godot_csharp/tools/build_anime_cast.py` |
| `DialogueCast/` | 对白上身立绘与六种表情的 AtlasTexture；当前 `DialogueUI.gd` 按角色键加载 calm 资源 | `godot_csharp/tools/build_dialogue_cast.py` |
| `voices/` | 已保留的声音素材；是否播放以宿主实际调用为准 | 素材不等同于已实现的对白语音功能 |

当前角色键为 `detective`、`engineer`、`chef`、`musician`、`poet`、`archaeologist`、`thief`、`lawyer`、`naturalist`、`chessplayer`、`dancer`、`painter`、`chemist`、`marksman`、`architect`、`doctor`。人物身份、名字与呈现绑定由正式 DV 内容提供，资源文件名不作为剧情依据。

PixelCast 基础画布为 32×48，运行时使用最近邻采样。具体角色可由制作参数声明其他画布尺寸；脚底、缩放和过滤参数取各 SpriteFrames 的 metadata。AnimeCast 从原图登记透明边界、对齐与显示尺度；DialogueCast 从原图登记 calm/thoughtful/worried/angry/warm/hooked 六种上身范围，不把图集制成证明当作角色动作或表情已在正式会话中使用的证明。

## 生产源与维护

这些制作工具从 `godot_csharp/AssetsSource/Internal/Characters/` 下的同名资产族读取源稿；PixelCast 还需要该资产族中每人的调色板 JSON。当前仓库快照没有包含这些源稿与调色板，必须先从原制作来源恢复输入才能重建。现有运行图集应保留，不能因工具仍在就假定缺失源稿能够重现。

在输入齐备后，从仓库根目录执行对应工具，例如 `python godot_csharp/tools/build_pixel_cast.py --only detective`；资源导入按 Godot 工程说明执行。更新图集同时检查角色键、PNG、`.tres`、metadata、透明边界、脚底锚点及导入侧车。开发整图截图工具 `scenes/Chapter2/Field/capture_maps.gd` 同样使用 PixelCast，避免开发截图仍显示另一套角色比例。

现役人物图集、立绘及 AnimeCast 回退保留独立用途，不能只按静态路径搜索移除动态加载的资源。此前静止身份图与旧动作制作过程从 Git 历史查阅，本说明只维护当前运行引用与实际重建前提。
