# 人物动作图集 v2

2026-10-02 生活翼四人（`detective`、`engineer`、`chef`、`musician`）及无外衣变体 `detective_shirt` 改由原生像素配方 `godot_csharp/tools/first_awakening_art/cast.py` 生成：头部按人物与朝向逐像素手绘，身体、服装与四肢按骨架逐帧摆姿，约 1:3.5 半Q头身比，服装与发色以主控规范第一部分 §5 为准。图集结构、帧数与锚点不变，原 `.tres` 继续有效；`detective_shirt.tres` 为同结构副本。重建：在 `godot_csharp/tools` 下运行 `python -m first_awakening_art.cast`，会校验帧数、二值 alpha、格边留白与脚底行，并把生产源写入 `AssetsSource/Internal/Characters/NativeCast/`。下文 imagegen 导入流程仅适用于 `placeholder` 与早餐专用表演等遗留资源。

2026-10-04 其余十二名候选人同样改由 `cast.py` 原生配方生成，身份对照 `../characters-21-front-master.png`，服装为 1900 年前后的平民职业装，默认空手站姿。`publish()` 现同时写出 PNG、`.tres`（结构与原五人逐字节一致）和 `NativeCast/` 生产源。原 imagegen 版 `poet`、`thief`、`lawyer`、`naturalist`、`painter` 运行图集已被原生版覆盖（imagegen 源稿与 `DemoAnimation/approved-atlas/thief.png` 保留但不再是当前运行图）。

| 键 | 人物 | 键 | 人物 |
| --- | --- | --- | --- |
| `detective` / `detective_shirt` | 侦探（外套 / 衬衫马甲） | `engineer` | 工程师 |
| `chef` | 厨师 | `musician` | 音乐家 |
| `poet` | 卢西安（诗人） | `archaeologist` | 顾平章（考古学家） |
| `thief` | 雀儿（盗贼） | `lawyer` | 沃德（律师） |
| `naturalist` | 赛尔玛（自然学家） | `chessplayer` | 阮清越（棋手） |
| `dancer` | 伊莲娜（舞者） | `painter` | 莫兰（画家） |
| `chemist` | 维罗妮卡（化学家） | `marksman` | 贝尔特朗（神射手） |
| `architect` | 玛格丽特（建筑师） | `doctor` | 茉拉（医生） |

2026-09-25 早餐专用表演另使用本目录`breakfast_exchange.png`（512×128，8列×2行，16帧），由`BreakfastPerformance.gd`读取，保持原有九人的通用图集不变。制作源、提示词与重建命令见[早餐动作生产说明](../../../../AssetsSource/Internal/Characters/DemoAnimation/breakfast_exchange.md)。它是源稿缩制的专用坐姿表演，不能用于替代全方向行走图集；完整日常片段的验证与限制见[日常三幕记录](../../../../../artifacts/first-three-scenes/daily-montage.md)。

当前有侦探及宣传剧情打样使用的八名角色：音乐家、自然学家、工程师、厨师、诗人、盗贼、律师、画家。每人 PNG 为 512×1536，配套 `.tres` 可赋给 Godot `AnimatedSprite2D.sprite_frames`。

前三场演示的角色名单：

| 场景 | 出场角色 |
| --- | --- |
| 中央厅清晨 | 侦探、音乐家 |
| 后勤院早餐 | 侦探、厨师、自然学家、工程师 |
| 公共娱乐室围谈 | 侦探、诗人、盗贼、律师、画家 |

## 图集结构

图集为 8 列×24 行，每格 64×64，无格线或间隔。行 0–7 为前向、8–15 为后向、16–23 为左向；每组依次是 idle、walk、talk、inspect、interact、sit、break、cast。有效帧数依次为 1、8、3、4、4、4、6、7，其余格透明。每人共 111 个帧引用和 24 个动画；右向镜像左向。

地图基础格为 32×32。64×64 是透明留白的动作承载格，不表示人物放大或地面占位扩大。运行时锚点为 (32,55)，即最后一个脚底像素在 y=54；居中 `AnimatedSprite2D` 偏移 (0,-23)，角色根节点位于脚底。目标人形高度约 43px。碰撞盒为 16×12，中心相对锚点 (0,-6)，遮挡排序使用角色根节点 Y。

idle 和 walk 循环，walk 为 10fps，其余动作 7fps 单次。sit 动画资源提供坐下帧序列；保持末帧与逆序起身由使用它的场景逻辑负责。通用 interact 不包含持物、端盘、倒水等专用表演，也不代替正式规则结算。

2026-09-24 Story 呈现调整：`StoryActor.gd` 增加原像素尺度的脚底接触阴影，`sit_at` 固定座垫高度与坐姿末帧，交谈时不切换成站立动画。房间脚本按实际交互对象转向，站立听众看向说话者；坐姿人物保持座椅朝向，早餐与围谈使用各自的休息朝向。音乐家侧耳按钟的位置镜像，避免朝反方向听钟。服装与发型沿用已确认源稿。画家的完整源图行距不齐，旧切片把上一行动作的鞋底碎片算入下一帧，导致头顶悬空黑线及错误缩放；导入器现在对画家的完整图按主连续非透明行段提取边界，再按同一方向的站姿定尺度。背向 override 保持原样；111帧重新导入，源图未覆盖。

## 源稿与重建

图集来自 imagegen 源稿的比例整理和确定性导入，不是原生逐像素手绘。源稿保存在 `godot_csharp/AssetsSource/Internal/Characters/DemoAnimation/`，导入报告保存在 `artifacts/demo-animation/`。源像素按每个方向 idle 的高度统一缩放到约 43px，动作帧保持等比例；导入按 alpha≥192 取轮廓、注册脚底、最近邻采样并阈值化透明度。

原有 `godot_csharp/tools/build_animation_atlas.ps1` 读取 `godot_csharp/AssetsSource/Internal/Characters/AnimationV2/`，负责原侦探与占位人物图集。新增角色使用 `godot_csharp/tools/build_demo_animation_atlas.ps1 -Actor <角色名...>`，输出到本目录。

新入口逐方向优先读取 `godot_csharp/AssetsSource/Internal/Characters/DemoAnimation/<角色>-front.png`、`<角色>-back.png`、`<角色>-left.png`。当前音乐家、自然学家、工程师、厨师、律师、画家有背向 override；诗人提供前、后、左三向 override。方向源按 8 列×5 行解释：第 0 行包含 idle、talk、inspect，第 1 行是 walk，第 2 行是 interact、sit，第 3 行是 break，第 4 行是 cast；行界在理论五等分附近寻找透明最长间隔。某方向源不存在时，回退读取 `godot_csharp/AssetsSource/Internal/Characters/DemoAnimation/<角色>.png` 中对应方向的 8×24 完整图集行。缺少该方向源和完整图集会报错。每次导入在 `artifacts/demo-animation/<角色>-bounds.txt` 写出源轮廓、目标边界和结构验证结果。

当前九张运行图集的结构检查结果见 `artifacts/demo-animation/validation.txt`：均为 512×1536、24 个动画、每个动画帧数符合规格、共 111 个帧引用；有效格非空、闲置格透明、alpha 为二值且轮廓没有接触 64×64 格边缘。五套最近更新的背向资源已纳入本次复查。这只说明文件结构及边界检查通过，不代表动作连贯性、比例观感、表演质量或实际场景效果已经验收。

盗贼原始生图仍保存在 `godot_csharp/AssetsSource/Internal/Characters/DemoAnimation/thief.png`，但它不能重建当前采用的运行时图集。当前采用的图集已逐字节保存为 `godot_csharp/AssetsSource/Internal/Characters/DemoAnimation/approved-atlas/thief.png`；这是从既有运行时成品收录的制作母版，不是原始生图的派生拷贝。默认重建入口优先读取这份已采用图集，原样复制 PNG 并重新生成 24 个动画、111 个帧引用。来源关系见母版目录的 README，结构结果见 `artifacts/demo-animation/thief-bounds.txt`。
