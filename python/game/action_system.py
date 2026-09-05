"""学园动作与意图执行系统 (Action System)。

工作代码：负责封装玩家及 NPC 提交的 Intent 意图、前置校验、
前后状态快照追踪、移动寻路、规则设立、言灵施行以及最终封印仪式。
"""

from __future__ import annotations

from ..core.engine import (
    Directive,
    E,
    Entity,
    F,
    InformationCarrier,
    Intent,
    Mode,
    Passage,
    Rule,
    RulePatch,
    RulePublication,
    close_intent,
    consume_intent,
    drop_intent,
    examine_intent,
    hold_intent,
    lock_intent,
    open_intent,
    read_intent,
    rest_intent,
    retrieve_intent,
    store_intent,
    take_intent,
    transfer_intent,
    unhold_intent,
    unlock_intent,
    use_intent,
)
from .decision_system import (
    carrier_done,
    objective_for_leader,
    pending_carrier,
    profession_intent,
    refuted_false_rules,
    synthesis_for,
)
from .social_system import active_social_impacts


def snapshot(game):
    """提取当前全局与人物状态快照，用于意图结算前后的因果变化对比。"""
    data = {
        "危机.压力": game.crisis.tag("Pressure"),
        "规则.补丁数": len(game.rule_patches),
        "规则.提案数": len(game.rule_proposals),
        "规则.反伪阻止数": game.patch_blocks,
        "认知.分析数": len(game.analyses),
        "认知.综合数": sum(
            bool(synthesis_for(inquiry))
            for inquiry in (*game.objectives, game.accident_inquiry)
            if inquiry is not None
        ),
        "认知.伪规则发布数": len(game.false_publications),
        "认知.伪规则驳回数": len(refuted_false_rules(game)),
        "协调.成立": bool(game.coordination),
        "仪式.贡献者": len(game.active_final_rite.related("Contributor"))
        if game.active_final_rite
        else 0,
        "仪式.存在": bool(game.active_final_rite),
        "终局.胜利": game.won,
        "终局.结束": game.ended,
    }
    for role, actor in game.characters.items():
        data[f"人物.{role}.位置"] = actor.parent.name if actor.parent else None
        data[f"人物.{role}.恐慌"] = actor.tag("Panic")
        data[f"人物.{role}.稳定"] = actor.souls[0].tag("Stability")
        data[f"人物.{role}.洞见"] = actor.souls[0].tag("Insight")
        data[f"人物.{role}.已接受伪规则"] = actor.souls[0].tag("FalseRule")
        data[f"人物.{role}.有效目标情绪"] = len(active_social_impacts(game, actor.souls[0]))
    for objective in game.objectives:
        data[f"目标.{objective.name}.阶段"] = objective.get("stage", 0)
    for carrier in game.memory_carriers:
        data[f"记忆.{carrier.name}.隐藏"] = carrier.has("Hidden")
        data[f"记忆.{carrier.name}.恢复数"] = len(carrier.related("RecoveredBy"))
        data[f"记忆.{carrier.name}.回应数"] = len(carrier.related("RespondedBy"))
    for request in game.requests:
        data[f"请求.{request.name}.活跃"] = request.has("Active")
        data[f"请求.{request.name}.接受"] = len(request.related("AcceptedBy"))
        data[f"请求.{request.name}.解决"] = request.tag("Resolved")
    for item in game.seal_components:
        data[f"部件.{item.name}.位置"] = item.parent.name if item.parent else None
    for scene in game.scenes.values():
        if scene.tag("Hazard"):
            data[f"场景.{scene.name}.危险"] = scene.tag("Hazard")
    return data


def make_intent(
    game,
    actor,
    operation,
    target,
    cost,
    resolve,
    label,
    reason="",
    validate=lambda: True,
    response_mode=None,
):
    """包装核心引擎的 Intent 对象，注入状态快照捕获与执行前后因果追踪。"""
    holder = {}

    def wrapped():
        before = snapshot(game)
        origin = actor.parent
        outcome = resolve()
        after = snapshot(game)
        changes = [
            f"{key}: {before.get(key)} → {value}"
            for key, value in after.items()
            if before.get(key) != value
        ]
        intent_obj = holder["intent"]
        note = game._decision_notes.pop(id(intent_obj), {})
        game._pending_event_meta[actor] = {
            "origin": origin,
            "destination": actor.parent,
            "reason": note.get("reason", reason or "玩家明确提交"),
            "score": note.get("score"),
            "changes": tuple(changes),
            "contract_checked": actor is game.demon,
        }
        return outcome

    intent = Intent(
        actor,
        actor.souls[0],
        F(operation, actor, target),
        cost,
        wrapped,
        label,
        validate,
        response_mode,
    )
    holder["intent"] = intent
    return intent


def context_for(game, intent):
    """获取意图关联的场景/世界上下文作用域 (Domain)。"""
    domains = [game.world]
    if intent.actor.parent:
        domains.append(intent.actor.parent)
    for value in intent.effect.args:
        if isinstance(value, Entity):
            domains.append(value)
            if value.parent and value.parent.has("Scene"):
                domains.append(value.parent)
    return {"source": intent.actor, "domains": tuple(dict.fromkeys(domains))}


def edge(game, start, destination):
    """查询连接两个场景的 Passage 通道对象。"""
    link = game.world.links.get(start, {}).get(destination)
    return link if isinstance(link, Passage) else None


def set_location(game, actor, destination, initial=False):
    """变更 actor 的物理位置，并同步事实与灵魂认知。"""
    start = actor.parent
    if not initial and start:
        game.env.retract(E("located_at", actor, start))
        actor.souls[0].unobserve(E("located_at", actor, start))
        actor.reparent(destination)
    game.env.assert_fact(E("located_at", actor, destination))
    actor.souls[0].observe(E("located_at", actor, destination))


def move_intent(game, actor, destination):
    """构造一段合法移动的 Intent。"""
    start = actor.parent
    passage = edge(game, start, destination) if destination else None
    if passage is None or not passage.allows(start, actor, game.env):
        return None

    def valid():
        return (
            actor.parent is start
            and passage.other(start) is destination
            and passage.allows(start, actor, game.env)
        )

    def resolve():
        set_location(game, actor, destination)
        if destination.tag("Hazard") > actor.tag("Occult"):
            actor.change("Panic", max(1, destination.tag("Hazard") - actor.tag("Occult")))
        game.log.append(f"{actor.name}经《{passage.name}》从{start.name}抵达{destination.name}。")
        return passage

    return make_intent(
        game,
        actor,
        "move",
        destination,
        max(10, passage.cost_from(start) * 20),
        resolve,
        f"逐段移动至{destination.name}",
        "沿当前目标的最短 Passage 迈出一段",
        valid,
    )


def investigate_intent(game, actor, carrier):
    """构造勘查隐藏记忆材料的 Intent。"""
    def resolve():
        carrier.tags.pop("Hidden", None)
        carrier.relate("DiscoveredBy", actor)
        game.log.append(f"{actor.name}依照分配的搜索范围发现《{carrier.name}》。")
        return carrier

    return make_intent(
        game,
        actor,
        "investigate",
        carrier,
        30,
        resolve,
        f"勘查：{carrier.name}",
        "只搜索已知地点，不读取隐藏内容",
    )


def recover_now(game, actor, carrier):
    """执行记忆载体读取结算。"""
    if carrier.has("Hidden"):
        return None
    claims = carrier.read(actor.souls[0])
    carrier.relate("RecoveredBy", actor)
    if actor.tag("Panic"):
        actor.change("Panic", -1)
    game.log.append(f"{actor.name}从《{carrier.name}》恢复了一段独立主观记忆。")
    return claims


def move(game, actor, destination):
    """玩家/统一接口：执行移动。"""
    intent = move_intent(game, actor, destination)
    return bool(intent and game.submit(intent))


def recover_memory(game, actor, carrier):
    """玩家/统一接口：恢复记忆。"""
    if (
        carrier.parent is not actor.parent
        or not carrier.has("MemoryCarrier")
        or carrier.has("Hidden")
        or carrier_done(actor, carrier)
    ):
        game.log.append("隐藏、异地或已经恢复的记忆载体不能直接读取。")
        return False
    return game.submit(
        make_intent(
            game,
            actor,
            "recover_memory",
            carrier,
            45,
            lambda: recover_now(game, actor, carrier),
            f"恢复记忆：{carrier.name}",
            "玩家读取已揭示载体",
        )
    )


def profession_action(game, actor):
    """玩家/统一接口：执行当前场景的有限职业行动。"""
    carrier = pending_carrier(game, actor)
    if carrier and carrier.parent is actor.parent and carrier.has("Hidden"):
        return game.submit(investigate_intent(game, actor, carrier))
    intent = profession_intent(game, actor)
    if intent is None:
        game.log.append("当前没有与该职业匹配的有限任务，行动不会生成。")
        return False
    return game.submit(intent)


def collect(game, actor, item):
    """玩家/统一接口：收集封印部件。"""
    objective = next((obj for obj in game.objectives if obj.get("component") is item), None)
    if (
        not objective
        or objective.get("stage") != 4
        or objective.get("leader") is not actor
        or item.parent is not actor.parent
    ):
        return False
    return game.submit(game.collect_intent(actor, item, objective))


def invoke_word(game, actor, recipe, target):
    """玩家/统一接口：施行言灵配方。"""
    inputs = (
        {"focus": game.tuning_fork}
        if recipe is game.calm_word
        else {"token": game.threshold_token}
    )

    def resolve():
        result = game.words.invoke(
            recipe,
            actor.souls[0],
            inputs,
            target=target,
            context={
                "actor": actor,
                "holder": game.team_inventory,
                "world": game.world,
            },
        )
        game.log.append(
            f"{actor.name}施行言灵“{recipe.name}”。"
            if result
            else f"言灵“{recipe.name}”未成立：{game.words.last_failure}。"
        )
        return result

    return game.submit(
        make_intent(
            game,
            actor,
            "invoke_word",
            target,
            65,
            resolve,
            f"言灵：{recipe.name}",
            "言灵配方与现实状态共同复核",
        )
    )


def establish_rule(game, actor, witness, operation, subject, target, scope):
    """玩家/统一接口：在契约审议室设立客观规则 (RulePatch)。"""
    if (
        actor.parent is not game.scenes["契约审议室"]
        or witness.parent is not actor.parent
        or witness is actor
        or game.rule_ink.get("uses", 0) <= 0
    ):
        game.log.append("规则设立需要在契约审议室由另一名在场者见证，并消耗红墨水。")
        return False

    rule = Rule(
        E("true"),
        (Directive(Mode.FORBID, F(operation, subject, target)),),
        f"{actor.name}的规则设立",
    )

    def valid():
        return (
            actor.parent is game.scenes["契约审议室"]
            and witness.parent is actor.parent
            and game.rule_ink.get("uses", 0) > 0
        )

    def resolve():
        game.rule_ink.set("uses", game.rule_ink.get("uses") - 1)
        carrier = InformationCarrier(
            f"设立规则：禁止{subject.name}{operation}{getattr(target, 'name', target)}",
            "Manuscript",
            game.rule_archive,
            tags={"Law": 2, "Guarantee": 2},
        )
        publication = RulePublication(
            rule, actor, carrier=carrier, scope=scope, version=game.env.version
        )
        publication.relate("Witness", witness)
        patch = RulePatch(
            publication.name,
            rule,
            game.rule_archive,
            publication,
            scope,
            priority=actor.souls[0].tag("Law"),
        )
        patch.relate("Witness", witness)
        game.rule_patches.append(patch)
        game.runtime.patches.append(patch)
        for participant, confidence in ((actor, 3), (witness, 2)):
            participant.souls[0].receive(publication, confidence)
            participant.souls[0].understand(rule)
            participant.souls[0].accept(rule)
        game.coordination = Entity(
            "四院协同行动令",
            game.story_board,
            {"Coordination": 1, "Active": 1},
            properties={"issuer": actor, "witness": witness},
        )
        game.coordination.relate("IssuedBy", actor)
        game.coordination.relate("Witness", witness)
        game.log.append(f"{actor.name}与{witness.name}建立客观规则，并据此形成四院协同行动令。")
        return patch

    return game.submit(
        make_intent(
            game,
            actor,
            "establish_rule",
            scope,
            20,
            resolve,
            "设立局部规则",
            "见证者、红墨水与精确作用域齐备",
            valid,
        )
    )


def final_ready(game):
    """检查是否达成开启最终真名再封印仪式的全部前置条件。"""
    recovered = sum(bool(carrier.related("RecoveredBy")) for carrier in game.memory_carriers)
    return bool(
        game.coordination
        and recovered >= 15
        and all(synthesis_for(obj) for obj in game.objectives)
        and synthesis_for(game.accident_inquiry)
        and all(obj.get("stage", 0) >= 5 and obj.tag("Verified") for obj in game.objectives)
        and all(item.parent is game.team_inventory for item in game.seal_components)
    )


def begin_rite_intent(game, actor):
    """构造开启最终仪式的 Intent。"""
    def resolve():
        rite = Entity(
            "进行中的四院真名再封印",
            game.rule_archive,
            {"Rite": 1, "Active": 1, "Seal": 1},
            properties={
                "recipe": game.final_recipe,
                "target": game.demon,
                "phase": 0,
            },
        )
        rite.relate("Speaker", actor)
        for objective in game.objectives:
            rite.relate("Requires", objective)
        game.words.rites.append(rite)
        game.active_final_rite = rite
        game.log.append(f"{actor.name}在四院验证与历史证词齐备后建立最终 Rite。")
        return rite

    return make_intent(
        game,
        actor,
        "begin_rite",
        game.demon,
        50,
        resolve,
        "建立四院真名再封印",
        "四院验证、十五份载体、四件部件与协同行动令均成立",
        lambda: (
            final_ready(game)
            and actor.parent is game.scenes["核心隔离舱"]
            and game.active_final_rite is None
        ),
    )


def begin_final_seal(game, actor):
    """玩家/统一接口：开启最终真名再封印仪式。"""
    if not final_ready(game) or actor.parent is not game.scenes["核心隔离舱"]:
        game.log.append("最终封印要求四院验证、至少十五份载体证词、四件部件与协同行动令。")
        return False
    return game.submit(begin_rite_intent(game, actor))


def contribute_rite_intent(game, actor):
    """构造为最终仪式贡献四院验证步骤的 Intent。"""
    rite = game.active_final_rite
    objective = objective_for_leader(game, actor)

    def resolve():
        actor.souls[0].change("Stability", -1)
        rite.relate("Contributor", actor, source=objective)
        objective.relate("ContributedBy", actor, source=rite)
        rite.set("phase", len(rite.related("Contributor")))
        game.env.assert_fact(E("seal_step", objective, actor))
        game.log.append(f"{actor.name}以{objective.name}的验证结果承担仪式第{rite.get('phase')}步。")
        if len({facet.value for facet in rite.related("Contributor")}) >= 4:
            rite.tags.pop("Active", None)
            rite.change("Completed", 1)
            covenant = Entity(
                "重立的真名封印契约",
                game.rule_archive,
                {"Covenant": 1, "Active": 1},
                properties={"active": True},
            )
            covenant.attach(
                "HardConstraint",
                Rule(
                    E("sealed", game.demon),
                    (Directive(Mode.FORBID, F("return", game.demon, game.world)),),
                ),
            )
            covenant.relate("CreatedBy", rite)
            game.words.contracts.append(covenant)
            game.env.assert_fact(E("sealed", game.demon))
            game.demon.reparent(None)
        return objective

    return make_intent(
        game,
        actor,
        "contribute_rite",
        rite,
        65,
        resolve,
        f"承担仪式步骤：{objective.name}",
        "每一院只能由其实际验证者贡献一次",
        lambda: bool(
            rite
            and objective
            and actor.parent is game.scenes["核心隔离舱"]
            and not any(facet.value is actor for facet in rite.related("Contributor"))
        ),
    )


def advance_final_seal(game, actor):
    """玩家/统一接口：推进最终真名再封印步骤。"""
    if not game.active_final_rite or game.active_final_rite.has("Completed"):
        return False
    intent = contribute_rite_intent(game, actor) if objective_for_leader(game, actor) else None
    return bool(intent and game.submit(intent))


def take_item(game, actor, item):
    """玩家/统一接口：拾取场景地面物品。"""
    intent = take_intent(actor, item)
    return game.submit(make_intent(
        game, actor, "take", item, intent.motion_cost, intent.resolve, intent.label, "拾取场景物品", intent.validate
    ))


def drop_item(game, actor, item):
    """玩家/统一接口：丢下随身物品至当前场景。"""
    intent = drop_intent(actor, item)
    return game.submit(make_intent(
        game, actor, "drop", item, intent.motion_cost, intent.resolve, intent.label, "放下随身物品", intent.validate
    ))


def store_item(game, actor, item, container):
    """玩家/统一接口：将物品放入容器。"""
    intent = store_intent(actor, item, container)
    return game.submit(make_intent(
        game, actor, "store", item, intent.motion_cost, intent.resolve, intent.label, "收纳物品至容器", intent.validate
    ))


def retrieve_item(game, actor, item, container):
    """玩家/统一接口：从容器取出物品。"""
    intent = retrieve_intent(actor, item, container)
    return game.submit(make_intent(
        game, actor, "retrieve", item, intent.motion_cost, intent.resolve, intent.label, "从容器取出物品", intent.validate
    ))


def open_target(game, actor, target):
    """玩家/统一接口：打开门或容器。"""
    intent = open_intent(actor, target)
    return game.submit(make_intent(
        game, actor, "open", target, intent.motion_cost, intent.resolve, intent.label, "开启门或容器", intent.validate
    ))


def close_target(game, actor, target):
    """玩家/统一接口：关闭门或容器。"""
    intent = close_intent(actor, target)
    return game.submit(make_intent(
        game, actor, "close", target, intent.motion_cost, intent.resolve, intent.label, "关闭门或容器", intent.validate
    ))


def unlock_target(game, actor, target, key):
    """玩家/统一接口：解锁门或容器。"""
    intent = unlock_intent(actor, target, key)
    return game.submit(make_intent(
        game, actor, "unlock", target, intent.motion_cost, intent.resolve, intent.label, "解锁门或容器", intent.validate
    ))


def lock_target(game, actor, target, key):
    """玩家/统一接口：锁闭门或容器。"""
    intent = lock_intent(actor, target, key)
    return game.submit(make_intent(
        game, actor, "lock", target, intent.motion_cost, intent.resolve, intent.label, "锁闭门或容器", intent.validate
    ))


def hold_item(game, actor, item):
    """玩家/统一接口：持握随身物品。"""
    intent = hold_intent(actor, item)
    return game.submit(make_intent(
        game, actor, "hold", item, intent.motion_cost, intent.resolve, intent.label, "持握装备物品", intent.validate
    ))


def unhold_item(game, actor, item):
    """玩家/统一接口：收起手持物品。"""
    intent = unhold_intent(actor, item)
    return game.submit(make_intent(
        game, actor, "unhold", item, intent.motion_cost, intent.resolve, intent.label, "收起手持物品", intent.validate
    ))


def examine_target(game, actor, target):
    """玩家/统一接口：检视场景物品或设施。"""
    intent = examine_intent(actor, target)
    return game.submit(make_intent(
        game, actor, "examine", target, intent.motion_cost, intent.resolve, intent.label, "检视物品或环境", intent.validate
    ))


def read_target(game, actor, target):
    """玩家/统一接口：阅读书籍或文档。"""
    intent = read_intent(actor, target)
    return game.submit(make_intent(
        game, actor, "read", target, intent.motion_cost, intent.resolve, intent.label, "阅读书籍或公文", intent.validate
    ))


def use_tool(game, actor, tool, target=None):
    """玩家/统一接口：使用工具或设施。"""
    intent = use_intent(actor, tool, target)
    return game.submit(make_intent(
        game, actor, "use", tool, intent.motion_cost, intent.resolve, intent.label, "使用工具或设备", intent.validate
    ))


def consume_item(game, actor, consumable):
    """玩家/统一接口：食用、饮用或消耗药剂。"""
    intent = consume_intent(actor, consumable)
    return game.submit(make_intent(
        game, actor, "consume", consumable, intent.motion_cost, intent.resolve, intent.label, "消耗生活物品", intent.validate
    ))


def rest_at(game, actor, resting_place):
    """玩家/统一接口：在床铺或长椅休憩。"""
    intent = rest_intent(actor, resting_place)
    return game.submit(make_intent(
        game, actor, "rest", resting_place, intent.motion_cost, intent.resolve, intent.label, "在场景中休憩", intent.validate
    ))


def transfer_item(game, giver, receiver, item):
    """玩家/统一接口：同场景角色之间交接物品。"""
    intent = transfer_intent(giver, receiver, item)
    return game.submit(make_intent(
        game, giver, "transfer", item, intent.motion_cost, intent.resolve, intent.label, "交接随身物品", intent.validate
    ))
