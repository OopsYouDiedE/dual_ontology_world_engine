# Neutral compiler-to-controller contract fixture; no chapter content or route.
章节：neutral_controller_wire
前置章节：无

阵营
    - NeutralGroup | Life
体型
    - NeutralBuild | Medium | neutral

地点 NeutralRoom
    编号：room.DormLife

人物 NeutralActor
    编号：neutral.actor
    所在：NeutralRoom
    阵营：NeutralGroup
    宿舍：NeutralRoom
    体型：NeutralBuild
    性别：男
    速度：1
    欲望：neutral
    魔音台词：neutral
    操控者：代理
    操控规则：
        - 偏好 菜单 17 文案 "neutral-target"
        - 禁止 菜单 10 文案 "neutral-veto"
        - 偏好 菜单 1000 文案 "neutral-veto"
        - 放行 菜单 9 文案 "neutral-low"
        - 放行 菜单 10 文案 "neutral-release"
    推理预算：深度 4 展开 512 假设 3 论证 13 求知 7
