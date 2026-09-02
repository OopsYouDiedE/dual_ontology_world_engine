"""学园世界与实体构建器 (World Builder)。

工作代码：负责解析静态配置数据并实例化核心引擎世界对象（WorldSpace, Entity, Passage,
Character, Soul, Recipe, Rule, Covenant 等）。
"""

from __future__ import annotations

from core.python.engine import (
    Character,
    Directive,
    E,
    Entity,
    F,
    InformationCarrier,
    Knowledge,
    Mode,
    Passage,
    Recipe,
    Rule,
    RulePublication,
    Soul,
)
from ..content.academy_data import (
    ACCIDENT_MEMORY_SPECS,
    ASPECT_PROFILES,
    CHARACTER_SPECS,
    COMPONENT_SPECS,
    CONNECTIONS,
    HAZARDOUS_SCENES,
    MENTOR_SPECS,
    OBJECTIVE_SPECS,
    RESTRICTED_SCENES,
    SCENE_GROUPS,
    STORY_ROLES,
    STORY_SPECS,
)


def build_scenes_and_passages(world):
    """构建场景拓扑与通道连通网络。

    遍历场景分组数据，根据限制和危险标签实例化 Entity；
    随后解析 CONNECTIONS 数据，创建带条件校验的 Passage 门禁通道。
    """
    scenes, passages = {}, []

    # 1. 批量创建场景 Entity
    for layer, names in SCENE_GROUPS.items():
        for name in names:
            tags = {"Scene": 1, "Discovered": 1}
            if name in RESTRICTED_SCENES:
                tags["Locked"] = 1
            if name in HAZARDOUS_SCENES:
                tags["Hazard"] = HAZARDOUS_SCENES[name]
            scenes[name] = Entity(name, world, tags, properties={"layer": layer})

    # 2. 批量创建连接各场景的 Passage 通道
    for index, raw in enumerate(CONNECTIONS, 1):
        left, right, cost, *gate_name = raw
        gate = scenes[gate_name[0]] if gate_name else None

        def make_condition(gate_scene):
            def condition(_from, _actor, env):
                return (
                    gate_scene is None
                    or not gate_scene.has("Locked")
                    or bool(env and env.evaluate(E("unlocked", gate_scene)))
                )
            return condition

        passage = Passage(
            f"通道{index}：{left}—{right}",
            scenes[left],
            scenes[right],
            cost,
            tags={"Restricted": 1} if gate else None,
            condition=make_condition(gate),
        )
        world.add_passage(passage)
        passages.append(passage)

    return scenes, passages


def build_characters_and_souls(world, env, scenes):
    """构建十六位学员 Character 及其绑定的 Soul 实体。

    初始化学员身份属性、职业行动、初始场景，并在环境 (env) 中注册名字与角色别名。
    """
    characters, souls = {}, {}

    for role, name, occult, home, action, operation, team_role in CHARACTER_SPECS:
        # 创建学员物理实体
        actor = Character(
            f"{name}·{role}",
            scenes[home],
            {"Student": 1, "Health": 6, "Panic": 1, "Occult": occult},
        )
        actor.properties.update(
            role=role,
            personal_name=name,
            home=scenes[home],
            profession_action=action,
            operation=operation,
            team_role=team_role,
        )

        # 创建并绑定心智灵魂 Soul
        aspects = ASPECT_PROFILES.get(role, {})
        soul = Soul(
            f"{name}的心识",
            world,
            {
                "Stability": 8,
                "Identity": 6,
                "Law": 1,
                "Name": 1,
                **aspects,
            },
        )
        actor.bind(soul)
        characters[role], souls[role] = actor, soul

        # 环境变量注册别名与初始位置事实
        env.alias(role, actor)
        env.alias(name, actor)
        env.assert_fact(E("located_at", actor, actor.parent))
        soul.observe(E("located_at", actor, actor.parent))

    return characters, souls


def build_faculty_and_demon(world, env, scenes, history_archive, rule_archive, words, characters):
    """构建四位导师遗存、恶魔代管者以及初始约束契约 (Covenant)。"""
    # 构建四位导师的历史遗痕
    mentors = [
        Entity(
            f"{name}·{discipline}",
            history_archive,
            {"Mentor": 1, "Vestige": 3, "Authority": 3},
            properties={
                "discipline": discipline,
                "duty": duty,
                "status": "牺牲或成为封印载体",
            },
        )
        for name, discipline, duty in MENTOR_SPECS
    ]

    # 构建恶魔代管者及其灵魂
    demon = Character(
        "校内代管者",
        scenes["核心隔离舱"],
        {"Demon": 1, "Authority": 5, "Name": 4, "Law": 4, "Desire": 4},
    )
    demon.designate("恶魔", 1)
    demon_soul = Soul("代管者意志", world, {"Stability": 12, "Mind": 4, "Law": 4})
    demon.bind(demon_soul)
    env.alias("恶魔", demon)
    env.assert_fact(E("confined", demon, world))

    # 构建禁闭游戏契约（三导师临终硬约束）
    anchors = tuple(
        scenes[name]
        for name in ("名录库", "阵法基座室", "机械联锁室", "学园长隔离室")
    )
    covenant = Entity(
        "禁闭游戏契约",
        rule_archive,
        {"Covenant": 1, "Active": 1, "DyingWord": 1},
        properties={
            "active": True,
            "parties": (demon, *mentors),
            "fired": True,
        },
    )

    # 声明硬约束规则：禁止恶魔离开、破坏封印、直伤/操纵学员或损坏锚点场景
    forbidden = [
        F("leave", demon, world),
        F("break_seal", demon, world),
        *(F(op, demon, actor) for actor in characters.values() for op in ("kill", "possess", "control")),
        *(F("damage", demon, anchor) for anchor in anchors),
    ]
    for effect in forbidden:
        covenant.attach(
            "HardConstraint",
            Rule(E("true"), (Directive(Mode.FORBID, effect),), "三导师临终契约"),
        )
    for mentor in mentors[:3]:
        covenant.relate("GuaranteedBy", mentor)

    words.contracts.append(covenant)
    return mentors, demon, demon_soul, covenant


def build_histories_and_memories(history_archive, scenes, characters):
    """构建四大封闭历史事件、隐藏记忆载体与事故终局记忆。"""
    history, memory_carriers = [], []

    # 1. 构建前四条日常事故故事线
    for story_index, spec in enumerate(STORY_SPECS):
        event = Entity(
            spec["title"],
            history_archive,
            {"History": 1, "Truth": 1, "SealedMemory": 1},
            properties={
                "scene": scenes[spec["scene"]],
                "summary": spec["summary"],
            },
        )
        event.relate("OccurredAt", scenes[spec["scene"]])
        history.append(event)

        carrier_scene = scenes["动力总站"] if story_index == 3 else scenes[spec["scene"]]
        for role, (material_name, medium, claim) in spec["memories"].items():
            actor = characters[role]
            memory = Knowledge(
                f"{actor.name}关于“{event.name}”的记忆",
                event,
                claim,
                subject=event,
                source=actor,
                tags={"Memory": 1, "Subjective": 1, "Sealed": 1},
            )
            memory.relate("Witness", actor)

            carrier = InformationCarrier(
                material_name,
                medium,
                carrier_scene,
                memory,
                creator=actor,
                tags={"MemoryCarrier": 1, "Vestige": 2, "Hidden": 1},
            )
            carrier.properties.update(assignee=actor, story=event)
            carrier.relate("RecordOf", event)
            carrier.relate("Owner", actor)
            memory_carriers.append(carrier)
            event.relate("Witness", actor)

        for left_role, right_role, left_kind, right_kind, strength in spec["bonds"]:
            left, right = characters[left_role], characters[right_role]
            left.relate(left_kind, right, strength, source=event)
            right.relate(right_kind, left, strength, source=event)
            left.relate("PastBond", right, strength, source=event)
            right.relate("PastBond", left, strength, source=event)

    # 2. 构建反应炉破裂事故（终局事故）
    accident = Entity(
        "源质反应炉泄露与临终契约",
        history_archive,
        {"History": 1, "Truth": 1, "SealedMemory": 1, "FinalHistory": 1},
        properties={
            "scene": scenes["源质反应炉"],
            "summary": "校准误差导致反应炉破裂，四位导师以自身建立禁闭契约。",
        },
    )
    accident.relate("OccurredAt", scenes["源质反应炉"])
    history.append(accident)

    # 将十六位学员按 4 人一组分配事故记忆见证
    groups = tuple(
        tuple(characters.values())[i : i + 4]
        for i in range(0, 16, 4)
    )
    for group, (site, name, claim) in zip(groups, ACCIDENT_MEMORY_SPECS):
        assignee = group[-1]
        memory = Knowledge(
            f"关于事故的共同记忆：{name}",
            accident,
            claim,
            subject=accident,
            source=assignee,
            tags={"Memory": 1, "Subjective": 1, "Sealed": 1},
        )
        for actor in group:
            memory.relate("Witness", actor)
            accident.relate("Witness", actor)
        carrier = InformationCarrier(
            name,
            "Record",
            scenes[site],
            memory,
            creator=assignee,
            tags={"MemoryCarrier": 1, "Vestige": 3, "Hidden": 1},
        )
        carrier.properties.update(assignee=assignee, story=accident)
        carrier.relate("RecordOf", accident)
        carrier.relate("Owner", assignee)
        memory_carriers.append(carrier)

    return history, memory_carriers


def build_initial_rules(world, scenes, souls, demon, characters, env):
    """构建恶魔公布的三条全校公开规则及学员初始信念态度。"""
    rules = (
        (
            "恶魔不得直接伤害学员",
            Rule(
                E("true"),
                tuple(
                    Directive(Mode.FORBID, F(op, demon, actor))
                    for actor in characters.values()
                    for op in ("kill", "possess", "control")
                ),
                "广播第一条",
            ),
        ),
        (
            "封印必须由四院共同验证",
            Rule(
                E("true"),
                (Directive(Mode.PREFER, F("contribute_rite", "actor", "rite")),),
                "广播第二条",
            ),
        ),
        (
            "规则交易必须留下书面见证",
            Rule(
                E("true"),
                (Directive(Mode.PREFER, F("establish_rule", "actor", "scope")),),
                "广播第三条",
            ),
        ),
    )
    public_rules = []
    for title, rule in rules:
        carrier = InformationCarrier(
            title,
            "Broadcast",
            scenes["中央厅"],
            tags={"Guarantee": 2, "Law": 2},
        )
        publication = RulePublication(
            rule, demon, carrier=carrier, scope=world, version=1
        )
        publication.name = title
        public_rules.append(publication)
        for role, soul in souls.items():
            soul.receive(publication, confidence=2)
            soul.understand(rule)
            # 部分叛逆/敏锐角色初始拒绝此规则
            (soul.reject if role in {"神偷", "职业棋手", "化学家"} else soul.accept)(rule)

    return public_rules


def build_kotodama_and_recipes(world, rule_archive, team_inventory):
    """构建言灵系统所需的工具道具、Recipe 配方与最终封印仪式配方。"""
    tuning_fork = Entity(
        "黄铜共鸣音叉",
        team_inventory,
        {"Tool": 1, "Instrument": 1, "Mind": 2, "Bond": 2, "Preservation": 1},
    )
    threshold_token = Entity(
        "誓约庭重型钥匙",
        team_inventory,
        {"Tool": 1, "Authority": 2, "Name": 2, "Law": 2, "Threshold": 2},
    )
    rule_ink = Entity(
        "言灵导师的红墨水",
        team_inventory,
        {"Tool": 1, "Authority": 3, "Name": 2, "Law": 3},
        properties={"uses": 4},
    )

    # 言灵配方1：镇心三拍
    calm_word = Recipe(
        "镇心三拍",
        slots={"focus": {"tags": {"Instrument": 1}, "held": True}},
        thresholds={"Mind": 3, "Bond": 2, "Preservation": 1},
        requirements=(
            lambda scope: (
                scope["target"].tag("Panic") >= 2
                and scope["actor"].parent is scope["target"].parent
            ),
        ),
        effects=(F("change", "target", "Panic", -2),),
        parent=rule_archive,
        tags={"Incantation": 1, "Kotodama": 1},
    )

    # 言灵配方2：门槛正名
    open_word = Recipe(
        "门槛正名",
        slots={"token": {"tags": {"Authority": 1, "Threshold": 1}, "held": True}},
        thresholds={"Name": 3, "Law": 2, "Threshold": 2},
        requirements=(
            lambda scope: (
                scope["target"].has("Locked")
                and scope["target"] in scope["actor"].parent.parent.links.get(scope["actor"].parent, {})
            ),
        ),
        effects=(
            F("change", "target", "Locked", -1),
            F("assert", E("unlocked", "target")),
        ),
        parent=rule_archive,
        tags={"Incantation": 1, "Kotodama": 1},
    )

    # 言灵配方3：证词驳伪正言
    refute_word = Recipe(
        "证词驳伪正言",
        slots={"evidence": {"tags": {"Analysis": 1}}},
        thresholds={"Name": 3, "Law": 3, "Mind": 3},
        requirements=(lambda scope: scope["target"].tag("FalseRule") >= 1,),
        effects=(F("change", "target", "FalseRule", -1),),
        parent=rule_archive,
        tags={"Incantation": 1, "Kotodama": 1, "Refutation": 1},
    )

    final_recipe = Recipe(
        "四院真名再封印",
        parent=rule_archive,
        tags={"Rite": 1, "Covenant": 1, "Seal": 1},
    )

    return (
        tuning_fork,
        threshold_token,
        rule_ink,
        calm_word,
        open_word,
        refute_word,
        final_recipe,
    )


def build_objectives_and_board(story_board, history, scenes, characters, demon):
    """构建四院封印目标 Objective 及其对应的封印部件实体。"""
    objectives, seal_components = [], []

    for spec, (component_name, component_tags) in zip(OBJECTIVE_SPECS, COMPONENT_SPECS):
        name, story_index, source_name, anchor_name, leader_role, helper_role, operation = spec
        story = history[story_index]
        leader, helper = characters[leader_role], characters[helper_role]
        source, anchor = scenes[source_name], scenes[anchor_name]

        component = Entity(component_name, anchor, component_tags)
        if component.has("TrueName"):
            component.relate("TrueNameOf", demon, 2)

        objective = Entity(
            name,
            story_board,
            {"Objective": 1, "Active": 1},
            properties={
                "stage": 0,
                "story": story,
                "source": source,
                "anchor": anchor,
                "leader": leader,
                "helper": helper,
                "operation": operation,
                "component": component,
            },
        )
        objective.relate("EvidenceFrom", story)
        objective.relate("LedBy", leader)
        objective.relate("NeedsHelpFrom", helper)
        component.relate("RequiredBy", objective)

        for role in STORY_ROLES[story_index]:
            actor = characters[role]
            actor.relate("CommittedTo", objective, source=objective)
            objective.relate("Member", actor)
            actor.souls[0].learn(
                objective,
                (("search_at", story.get("scene")),),
                source=story_board,
                stage="understood",
            )

        objectives.append(objective)
        seal_components.append(component)

    accident = next(event for event in history if event.has("FinalHistory"))
    accident_inquiry = Entity(
        "反应炉事故因果综合",
        story_board,
        {"Inquiry": 1, "Active": 1},
        properties={
            "story": accident,
            "leader": characters["职业棋手"],
            "source": scenes["中央厅"],
            "stage": 0,
        },
    )
    accident_inquiry.relate("LedBy", characters["职业棋手"])
    characters["职业棋手"].relate("CommittedTo", accident_inquiry, source=accident_inquiry)

    return objectives, seal_components, accident_inquiry
