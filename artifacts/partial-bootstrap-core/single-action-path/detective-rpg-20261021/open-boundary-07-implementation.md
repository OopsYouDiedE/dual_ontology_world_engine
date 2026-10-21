# 开放边界与通行提示：07 根源码批次

本批仅静态改根源码和工程合同；没有启动 Godot、构建、正式会话或流程回归，没有改 candidate-06 冻结工程。未读取设计。前批与历史证据沿用本任务目录和主历史 carry-forward-03；本批关闭后核验结果独占保存于 `blind-04-open-boundary-closed-evidence.dvlog`。

## 关闭后证据

blind-04 关闭清单 complete=true、full audit、无 pending_action/errors 后才核原始输入与正式记录。7 次 select、5 次 prepared move 中都没有 corridor.hall；源房 awaiting_input 实有该可用候选。214704ms 持续 A 5000ms、239390ms 持续 A 4000ms，相关松键存在；220–240s 身体持续位于 `(2049.26440429688,1089.23413085938)`。这支持宿主边界准入漏接，不能把未提交动作当 DV 拒绝。

已载入 zone_yi authored geometry 的 corridor 西边界 x=2048，y=1056..1120 两侧都是 `.`，没有 D。示例 `(2032,1072)/(2032,1104)` 属 hall，`(2064,1072)/(2064,1104)` 属 corridor。原 `_step` 限制未正式准入跨房，但 `_request_continuous` 又强制 `doorway_rooms`，后者只接受 D，造成开放边界无法请求。

## 已实施

- `CampusMapView.passage_at` 是只读几何查询，要求身体当前 source、真实 departure/entered source 身份、attempted entered target、两个 authored 地板格四向相邻、短步最多一个现有 32px 地形格、原 walkability 及真实身体 `test_move` 无碰撞。返回真实 source/target/landing；不制造 D、站点或路线，不移动身体。
- WorldView 在原 D 分支之前查询该开放边界；只匹配当前可用、同图、唯一目的房动作。键盘边界 `_step` 传本次真实 attempted 点；近距 E 和鼠标边界查询复用同一几何。远距鼠标行走仍靠真实局部路线接近，在实际边界才请求；歧义动作不取首个。
- 请求保存 `passage_kind`、`boundary_source`、`boundary_target` 诊断值。其后仍由 Main 独立匹配 Prepared 身份，实际 `_move_body` 碰撞、真实到室和 Complete 决定结果。该证据不自行授予逻辑位置或观察。
- UI 提示优先投影当前真实连续请求阶段/取消标记，显示准备、通行、取消、核对、返回源处三语短句；不依赖 can_submit，不新增动作/对白/认知，不宣称抵达。模态/对白/关闭及隐藏场抑制，清除请求后恢复现场原提示。

## 相机边距单列提案，未实施

当前 camera_point 与 RoomStage 均根据真实身体/真实 authored region，不是旧 DV snapshot 房间。corridor bounds 为 `[2048,992]..[2752,1152]`；1280px viewport/2倍 zoom 可见宽640，左 clamp 对应世界 x=2048。脚点距左边仅1.2644px时，精灵自然被屏幕裁切，这与边界不提交是独立问题。

最小候选是读取本人当前可见 AnimatedSprite2D 的 `get_rect()`，经真实 sprite/actor 到 field 的变换取得绘制 bounds；仅在 bounds 将越过 viewport 时，按其真实溢出修正 camera focus，不改 room/stage_bounds/视野/碰撞。没有可见 sprite 则保留原 clamp，不能虚设角色半宽。

本批不实施该外扩：SightMask 对未知室内格遮挡，但非室内墙格不一定遮挡；RoomStage veil 是显示效果，不是已证明的知识屏障。平移相机可能露出原屏外未知墙格/相邻建筑轮廓，目前无法静态保证不泄漏。需先证明新增边带完全由原遮挡覆盖，或建立只显示本人溢出部分的独立呈现方案，再做有限截图核验。不得扩大 sight 或借改逻辑房间解决裁切。

## 验证边界

仅执行差异空白检查与人工源码审读。尚未编译、运行或验证按键/E/点击、物理通行、取消及截图；后续根07统一构建/冻结后分别核开放边界、实体墙、无资格/歧义动作、松键取消与真实到室。跨图中途失败后的通用实际位置协调仍未修复，本批不宣称解决它。
