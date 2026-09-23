# 人物动作图集 v2

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

## 源稿与重建

图集来自 imagegen 源稿的比例整理和确定性导入，不是原生逐像素手绘。源稿及导入报告保存在 `artifacts/demo-animation/`。源像素按每个方向 idle 的高度统一缩放到约 43px，动作帧保持等比例；导入按 alpha≥192 取轮廓、注册脚底、最近邻采样并阈值化透明度。

原有 `tools/build_animation_atlas.ps1` 读取 `artifacts/character-animation-v2/source/`，负责原侦探与占位人物图集。新增角色使用 `tools/build_demo_animation_atlas.ps1 -Actor <角色名...>`，输出到本目录。

新入口逐方向优先读取 `artifacts/demo-animation/source/<角色>-front.png`、`<角色>-back.png`、`<角色>-left.png`。当前音乐家、自然学家、工程师、厨师、律师、画家有背向 override；诗人提供前、后、左三向 override。方向源按 8 列×5 行解释：第 0 行包含 idle、talk、inspect，第 1 行是 walk，第 2 行是 interact、sit，第 3 行是 break，第 4 行是 cast；行界在理论五等分附近寻找透明最长间隔。某方向源不存在时，回退读取 `artifacts/demo-animation/source/<角色>.png` 中对应方向的 8×24 完整图集行。缺少该方向源和完整图集会报错。每次导入在 `artifacts/demo-animation/<角色>-bounds.txt` 写出源轮廓、目标边界和结构验证结果。

当前九张运行图集的结构检查结果见 `artifacts/demo-animation/validation.txt`：均为 512×1536、24 个动画、每个动画帧数符合规格、共 111 个帧引用；有效格非空、闲置格透明、alpha 为二值且轮廓没有接触 64×64 格边缘。五套最近更新的背向资源已纳入本次复查。这只说明文件结构及边界检查通过，不代表动作连贯性、比例观感、表演质量或实际场景效果已经验收。
