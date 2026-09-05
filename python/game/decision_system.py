"""学园决策与 NPC AI 认知系统 (Decision System)。

工作代码：负责 NPC 灵魂决策树构建、效用函数评估 (Utility)、认知综合 (Synthesis)、
反伪规则提案、言灵反驳以及恶魔决策步骤。
"""

from __future__ import annotations

from ..core.engine import (
    Directive,
    E,
    Entity,
    F,
    InformationCarrier,
    Knowledge,
    Mode,
    ResponseMode,
    Rule,
    RulePatch,
    RulePublication,
)
from ..content.academy_data import (
    NPC_UTILITY_BASE,
    RELATION_NEGATIVE,
    RELATION_PROTECTIVE,
)
from .social_system import active_social_impacts, bond_strength, social_target


def recent_repetitions(game, actor, operation, target):
    """统计 actor 最近连续执行相同 operation+target 的次数，用于效用衰减。"""
    count = 0
    for event in reversed(game.runtime.events[-40:]):
        if event.get("actor") is not actor:
            continue
        effect = event.get("effect")
        event_target = effect.args[1] if effect and len(effect.args) > 1 else None
        if effect and effect.op == operation and event_target is target:
            count += 1
        else:
            break
    return count


def npc_utility(game, actor, intent):
    """计算 NPC 对特定候选意图 (intent) 的效用打分。

    综合基础效用 (NPC_UTILITY_BASE)、社交羁绊加成、重复惩罚与行动消耗。
    """
    operation = intent.effect.op
    if actor is game.demon:
        if operation == "damage":
            return 200
        return 115 if operation == game._demon_desired else 25

    base = NPC_UTILITY_BASE.get(operation, 30)
    target = intent.effect.args[1] if len(intent.effect.args) > 1 else None
    bond = bond_strength(game, actor, social_target(intent))
    repeats = recent_repetitions(game, actor, operation, target)
    return base + bond * 8 - repeats * 35 - intent.motion_cost / 10


def decision_reason(game, actor, intent, rule_reason, blocked_options=(), preferences=()):
    """构造人类可读的 NPC 决策归因字符串，反映认知与情绪状态。"""
    target = social_target(intent)
    bond = bond_strength(game, actor, target)
    commitment = next(
        (
            facet.value.name
            for facet in actor.related("CommittedTo")
            if isinstance(facet.value, Entity)
        ),
        "无",
    )
    repetition = recent_repetitions(
        game,
        actor,
        intent.effect.op,
        intent.effect.args[1] if len(intent.effect.args) > 1 else None,
    )
    parts = [f"承诺={commitment}", f"规则={rule_reason}", f"重复衰减={repetition}"]

    analyses = sum(analysis.get("analyst") is actor for analysis in game.analyses)
    if analyses:
        parts.append(f"已形成分析={analyses}")

    false_belief = active_false_belief(actor)
    if false_belief is not None:
        publication = false_belief.get("publication")
        parts.append(f"待修正信念={publication.name}")

    syntheses = tuple(
        inquiry
        for inquiry in (*game.objectives, game.accident_inquiry)
        if inquiry is not None
        and inquiry.get("leader") is actor
        and synthesis_for(inquiry)
    )
    if syntheses:
        parts.append(f"已综合历史={len(syntheses)}")
    if bond:
        parts.append(f"已恢复羁绊={bond}")

    attitude = [
        f"{source.get('combination')}:{amount:+g}"
        for amount, source in preferences
        if isinstance(source, Entity) and source.has("SocialImpact")
    ]
    if attitude:
        parts.append(f"目标情绪={'、'.join(attitude)}")
    if blocked_options:
        parts.append(f"排除候选={'、'.join(blocked_options)}")

    return "；".join(parts)


# ---- 辅助状态查询 ---------------------------------------------------

def assigned_carriers(game, actor):
    return tuple(carrier for carrier in game.memory_carriers if carrier.get("assignee") is actor)


def carrier_done(actor, carrier):
    return any(facet.value is actor for facet in carrier.related("RecoveredBy"))


def pending_carrier(game, actor):
    return next((c for c in assigned_carriers(game, actor) if not carrier_done(actor, c)), None)


def story_carrier(game, actor):
    return next(
        (c for c in assigned_carriers(game, actor) if not c.related("RecordOf")[0].value.has("FinalHistory")),
        None,
    )


def analysis_for(game, actor, carrier):
    return next(
        (a for a in game.analyses if a.get("analyst") is actor and a.get("carrier") is carrier),
        None,
    )


def analysis_pending(game, actor):
    return next(
        (c for c in assigned_carriers(game, actor) if carrier_done(actor, c) and analysis_for(game, actor, c) is None),
        None,
    )


def inquiry_for_story(game, story):
    return next(
        (obj for obj in game.objectives if obj.get("story") is story),
        game.accident_inquiry if game.accident_inquiry and game.accident_inquiry.get("story") is story else None,
    )


def submitted_analyses(inquiry):
    return tuple(facet.value for facet in inquiry.related("SubmittedAnalysis")) if inquiry else ()


def synthesis_for(inquiry):
    return next((facet.value for facet in inquiry.related("Synthesis")), None) if inquiry else None


def active_false_belief(actor):
    for belief in actor.souls[0].beliefs:
        pub = belief.get("publication")
        if (
            pub
            and pub.has("Falsehood")
            and belief.has("Accepted")
            and not any(facet.value is actor for facet in pub.related("RefutedBy"))
        ):
            return belief
    return None


def relationship_pending(game, actor):
    carrier = story_carrier(game, actor)
    if not carrier or not carrier_done(actor, carrier) or analysis_for(game, actor, carrier) is None:
        return None
    if any(facet.value is actor for facet in carrier.related("RespondedBy")):
        return None
    bond = next((facet for facet in actor.related("PastBond") if isinstance(facet.value, Entity)), None)
    return (carrier, bond.value) if bond else None


def active_rule_proposal(game):
    return next((p for p in game.rule_proposals if p.has("Active")), None)


def refuted_false_rules(game):
    return tuple(pub for pub in game.false_publications if pub.related("RefutedBy"))


def active_request_for(game, actor):
    return next((r for r in game.requests if r.has("Active") and r.get("helper") is actor), None)


def objective_for_leader(game, actor):
    return next((obj for obj in game.objectives if obj.get("leader") is actor), None)


# ---- 决策树步骤 -----------------------------------------------------

def memory_step(game, _soul, actor, _env, _context):
    carrier = pending_carrier(game, actor)
    if not carrier or carrier.parent is not actor.parent:
        return None
    if carrier.has("Hidden"):
        return game.investigate_intent(actor, carrier)
    if not carrier_done(actor, carrier):
        return game.make_intent(
            actor,
            "recover_memory",
            carrier,
            45,
            lambda: game.recover_now(actor, carrier),
            f"恢复记忆：{carrier.name}",
            "读取已揭示的个人记忆载体",
        )
    return None


def analysis_step(game, _soul, actor, _env, _context):
    carrier = analysis_pending(game, actor)
    if carrier is None:
        return None
    memory = carrier.related("Carries")[0].value
    story = carrier.get("story")
    inquiry = inquiry_for_story(game, story)
    claims = tuple(facet.value for facet in memory.entries("Claim"))

    def resolve():
        interpretation = (
            "interpretation",
            actor.get("role"),
            carrier.name,
            claims[0] if claims else "无可解析声明",
        )
        analysis = Knowledge(
            f"{actor.name}对《{carrier.name}》的分析",
            actor.souls[0],
            interpretation,
            subject=story,
            source=carrier,
            tags={"Analysis": 1, "Name": 2, "Law": 2, "Mind": 3},
        )
        analysis.properties.update(analyst=actor, carrier=carrier, inquiry=inquiry)
        analysis.relate("Evidence", carrier)
        analysis.relate("About", story)
        carrier.relate("AnalyzedBy", actor)
        if inquiry is not None:
            inquiry.relate("SubmittedAnalysis", analysis, source=carrier)
        actor.souls[0].change("Insight", 1)
        game.analyses.append(analysis)
        game.log.append(f"{actor.name}没有把《{carrier.name}》直接当成事实，而是形成了可质疑的职业解释。")
        return analysis

    return game.make_intent(
        actor,
        "analyze_memory",
        carrier,
        40,
        resolve,
        f"分析材料：{carrier.name}",
        "把主观 Claim 转化为带来源的 Analysis Knowledge",
    )


def synthesis_step(game, _soul, actor, _env, _context):
    inquiries = (*game.objectives, game.accident_inquiry)
    inquiry = next(
        (
            item
            for item in inquiries
            if item is not None
            and item.get("leader") is actor
            and synthesis_for(item) is None
            and len(submitted_analyses(item)) >= 3
            and actor.parent is item.get("source")
        ),
        None,
    )
    if inquiry is None:
        return None
    reports = submitted_analyses(inquiry)[:3]
    story = inquiry.get("story")

    def resolve():
        claims = tuple(report.get("content") for report in reports)
        synthesis = Knowledge(
            f"{story.name}的交叉解释",
            actor.souls[0],
            ("synthesis", story.name, claims),
            subject=story,
            source=inquiry,
            tags={"Synthesis": 1, "Analysis": 1, "Name": 2, "Law": 3, "Mind": 3},
        )
        synthesis.properties.update(analyst=actor, reports=reports)
        for report in reports:
            synthesis.relate("UsesAnalysis", report)
        inquiry.relate("Synthesis", synthesis)
        inquiry.change("Synthesized", 1)
        game.log.append(f"{actor.name}比较三份带来源的解释，形成《{story.name}》的暂定综合；它仍是 Knowledge，不是客观事实。")
        return synthesis

    return game.make_intent(
        actor,
        "synthesize_history",
        inquiry,
        55,
        resolve,
        f"综合历史：{story.name}",
        "三份独立 Analysis 互证后才形成暂定解释",
    )


def belief_step(game, _soul, actor, _env, _context):
    belief = active_false_belief(actor)
    carrier = story_carrier(game, actor)
    analysis = analysis_for(game, actor, carrier) if carrier else None
    if belief is None or analysis is None:
        return None
    publication = belief.get("publication")

    def resolve():
        result = game.words.invoke(
            game.refute_word,
            actor.souls[0],
            {"evidence": analysis},
            target=actor.souls[0],
            context={"actor": actor, "holder": actor.souls[0], "world": game.world},
        )
        if result is None:
            game.log.append(f"{actor.name}的驳伪言灵未成立：{game.words.last_failure}。")
            return None
        actor.souls[0].reject(publication.get("rule"))
        publication.relate("RefutedBy", actor, source=analysis)
        analysis.relate("Refutes", publication)
        actor.souls[0].learn(
            publication,
            (("contradicted_by", analysis),),
            source=analysis,
            stage="understood",
        )
        game.log.append(f"{actor.name}以《{analysis.name}》为语义投入施行“证词驳伪正言”，拒绝了恶魔的伪规则。")
        return result

    return game.make_intent(
        actor,
        "refute_rule",
        publication,
        60,
        resolve,
        "言灵：证词驳伪正言",
        "材料分析与伪规则冲突，修订已接受的 RuleBelief",
    )


def relationship_step(game, _soul, actor, _env, _context):
    pending = relationship_pending(game, actor)
    if not pending:
        return None
    carrier, target = pending
    keys = {facet.key for facet in actor.facets if facet.value is target}
    same_scene = actor.parent is target.parent and actor.parent is not None

    operations = []
    if same_scene and keys & RELATION_NEGATIVE:
        operations.append(("confront", ResponseMode.RESTRAIN))
    if same_scene and keys & RELATION_PROTECTIVE:
        operations.append(("protect", ResponseMode.PROTECT))
    if same_scene:
        operations.append(("clarify_memory", ResponseMode.NEGOTIATE))
    operations.append(("share_memory", ResponseMode.COOPERATE))

    def make_rel_intent(operation, response_mode):
        def resolve():
            memory = carrier.related("Carries")[0].value
            claims = tuple(facet.value for facet in memory.entries("Claim"))
            target.souls[0].learn(
                memory.get("subject"),
                claims,
                source=carrier,
                stage="understood",
            )
            carrier.relate("RespondedBy", actor)
            actor.relate(
                "VisibleBond",
                target,
                bond_strength(game, actor, target),
                source=carrier,
            )
            if operation == "confront":
                target.change("Panic", 1)
                target.relate("ConfrontedBy", actor, source=carrier)
            elif operation == "protect":
                if target.tag("Panic"):
                    target.change("Panic", -1)
                target.relate("ProtectedBy", actor, source=carrier)
            elif operation == "clarify_memory":
                target.relate("ClarifiedWith", actor, source=carrier)
            else:
                target.relate("MemorySharedBy", actor, source=carrier)
            game.log.append(f"{actor.name}因恢复的旧事对{target.name}执行 {operation}。")
            return target

        return game.make_intent(
            actor,
            operation,
            target,
            35 if not same_scene else 30,
            resolve,
            f"关系回应：{operation}",
            "恢复记忆使旧羁绊进入当前决策；异地时通过定向广播分享载体",
            response_mode=response_mode,
        )

    return tuple(make_rel_intent(*item) for item in dict.fromkeys(operations))


def rule_step(game, _soul, actor, _env, _context):
    lawyer = game.characters["律师"]
    witness = game.characters["职业棋手"]
    proposal = active_rule_proposal(game)
    if actor is lawyer and not game.rule_proposals and len(refuted_false_rules(game)) >= 2:
        sources = refuted_false_rules(game)[:2]

        def propose():
            created = Entity(
                "禁止恶魔伪造规则释义的提案",
                game.story_board,
                {"RuleProposal": 1, "Active": 1},
                properties={
                    "issuer": lawyer,
                    "witness": witness,
                    "operation": "forge_false_rule",
                    "subject": game.demon,
                    "target": game.world,
                    "scope": game.world,
                },
            )
            for publication in sources:
                created.relate("BasedOnRefutation", publication)
            lawyer.relate("CommittedTo", created, source=created)
            witness.relate("CommittedTo", created, source=created)
            game.rule_proposals.append(created)
            game.log.append(f"{lawyer.name}依据两次可追溯的驳伪结果提出客观规则，而不是凭空取得立法权。")
            return created

        return game.make_intent(
            actor,
            "propose_rule",
            game.world,
            35,
            propose,
            "提出反伪规则议案",
            "两份被言灵驳回的 RulePublication 构成提案依据",
        )

    if (
        proposal is not None
        and actor is lawyer
        and lawyer.parent is game.scenes["契约审议室"]
        and witness.parent is lawyer.parent
    ):
        return counter_rule_intent(game, lawyer, witness, proposal)
    return None


def counter_rule_intent(game, actor, witness, proposal):
    operation = proposal.get("operation")
    subject, target, scope = (
        proposal.get("subject"),
        proposal.get("target"),
        proposal.get("scope"),
    )
    rule = Rule(
        E("true"),
        (Directive(Mode.FORBID, F(operation, subject, target)),),
        f"{actor.name}依据驳伪记录提出的规则",
    )

    def valid():
        return (
            proposal.has("Active")
            and actor.parent is game.scenes["契约审议室"]
            and witness.parent is actor.parent
            and game.rule_ink.get("uses", 0) > 0
        )

    def resolve():
        game.rule_ink.set("uses", game.rule_ink.get("uses") - 1)
        carrier = InformationCarrier(
            "反伪规则审议书",
            "Manuscript",
            game.rule_archive,
            tags={"Law": 3, "Guarantee": 2},
        )
        publication = RulePublication(
            rule, actor, carrier=carrier, scope=scope, version=game.env.version
        )
        publication.relate("Witness", witness)
        publication.relate("DerivedFrom", proposal)
        patch = RulePatch(
            publication.name,
            rule,
            game.rule_archive,
            publication,
            scope,
            priority=actor.souls[0].tag("Law"),
        )
        patch.relate("Witness", witness)
        patch.relate("BasedOn", proposal)
        game.rule_patches.append(patch)
        game.runtime.patches.append(patch)
        for participant in (actor, witness):
            soul = participant.souls[0]
            soul.receive(publication, 3)
            soul.understand(rule)
            soul.accept(rule)
        proposal.tags.pop("Active", None)
        proposal.change("Established", 1)
        proposal.relate("EstablishedAs", patch)
        actor.detach("CommittedTo", proposal)
        witness.detach("CommittedTo", proposal)
        game.log.append(f"{actor.name}与{witness.name}完成审议，将两次驳伪结论设立为阻止恶魔伪释义的 RulePatch。")
        return patch

    return game.make_intent(
        actor,
        "establish_rule",
        proposal,
        70,
        resolve,
        "设立反伪规则",
        "提案、两份驳伪依据、发行者与见证者全部到场",
        valid,
    )


def word_step(game, _soul, actor, _env, _context):
    if actor.get("role") not in {"音乐家", "职业棋手", "画家"}:
        return None
    target = next(
        (
            other
            for other in game.characters.values()
            if other is not actor
            and actor.parent is other.parent
            and other.tag("Panic") >= 3
        ),
        None,
    )
    if target is None:
        return None

    def resolve():
        result = game.words.invoke(
            game.calm_word,
            actor.souls[0],
            {"focus": game.tuning_fork},
            target=target,
            context={
                "actor": actor,
                "holder": game.team_inventory,
                "world": game.world,
            },
        )
        if result:
            game.log.append(f"{actor.name}根据{target.name}的实际恐慌施行“镇心三拍”。")
        return result

    return game.make_intent(
        actor,
        "invoke_word",
        target,
        55,
        resolve,
        "言灵：镇心三拍",
        "同场目标的恐慌达到门槛，明确投入音叉并核验 Aspect",
    )


def help_step(game, _soul, actor, _env, _context):
    request = active_request_for(game, actor)
    if not request:
        return None
    if not any(facet.value is actor for facet in request.related("AcceptedBy")):

        def answer():
            request.relate("AcceptedBy", actor)
            actor.relate("CommittedTo", request, source=request)
            game.log.append(f"{actor.name}远程回应{request.get('requester').name}，承诺赴援。")
            return request

        return game.make_intent(
            actor,
            "answer_help",
            request,
            20,
            answer,
            "远程回应协助请求",
            "职业匹配且恢复的羁绊提高赴援效用",
            response_mode=ResponseMode.NEGOTIATE,
        )

    if actor.parent is request.get("scene"):
        return assist_intent(game, actor, request)
    return None


def assist_intent(game, actor, request):
    objective = request.get("objective")

    def resolve():
        request.tags.pop("Active", None)
        request.change("Resolved", 1)
        request.relate("AnsweredBy", actor)
        actor.detach("CommittedTo", request)
        objective.change("Assisted", 1)
        objective.set("stage", 2)
        objective.relate("SupportedBy", actor, source=request)
        game.log.append(f"{actor.name}抵达现场，为{request.get('requester').name}提供言灵见证；门禁仍未打开。")
        return objective

    return game.make_intent(
        actor,
        "assist",
        request,
        40,
        resolve,
        "到场提供言灵见证",
        "履行实际 HelpRequest 承诺；协助本身不解除门禁",
        response_mode=ResponseMode.COOPERATE,
    )


def objective_step(game, _soul, actor, _env, _context):
    objective = objective_for_leader(game, actor)
    if not objective:
        return None
    stage = objective.get("stage", 0)
    if stage == 1 and actor.parent is objective.get("source"):
        active = next(
            (r for r in game.requests if r.get("objective") is objective and r.has("Active")),
            None,
        )
        if active is None:
            return request_help_intent(game, actor, objective)
    if stage == 2 and actor.parent is objective.get("source"):
        return open_anchor_word_intent(game, actor, objective)
    if stage == 3 and actor.parent is objective.get("anchor"):
        return profession_intent(game, actor, objective)
    if stage == 4 and actor.parent is objective.get("anchor"):
        return collect_intent(game, actor, objective.get("component"), objective)
    if stage >= 5 and game.final_ready():
        if actor.parent is game.scenes["核心隔离舱"]:
            if game.active_final_rite is None:
                return game.begin_rite_intent(actor)
            if not any(facet.value is actor for facet in game.active_final_rite.related("Contributor")):
                return game.contribute_rite_intent(actor)
    return None


def open_anchor_word_intent(game, actor, objective):
    anchor = objective.get("anchor")

    def resolve():
        result = game.words.invoke(
            game.open_word,
            actor.souls[0],
            {"token": game.threshold_token},
            target=anchor,
            context={
                "actor": actor,
                "holder": game.team_inventory,
                "world": game.world,
            },
        )
        if result is None:
            game.log.append(f"{actor.name}的门槛正名未成立：{game.words.last_failure}。")
            return None
        objective.relate("UnlockedByWord", actor, source=result)
        objective.set("stage", 3)
        game.log.append(f"{actor.name}在{objective.get('helper').name}见证后施行“门槛正名”，以真实 Rite 打开{anchor.name}。")
        return result

    return game.make_intent(
        actor,
        "invoke_word",
        anchor,
        65,
        resolve,
        f"言灵开门：{anchor.name}",
        "协助只提供见证；实际解锁必须通过门槛正名 Recipe",
    )


def request_help_intent(game, actor, objective):
    def resolve():
        request = Entity(
            f"{actor.name}请求验证{objective.name}",
            game.world,
            {"HelpRequest": 1, "Active": 1, "Remote": 1},
            properties={
                "requester": actor,
                "helper": objective.get("helper"),
                "scene": actor.parent,
                "need": objective.get("operation"),
                "objective": objective,
                "deadline": game.timeline.now + 240,
            },
        )
        request.relate("RequestedBy", actor)
        request.relate("Domain", actor.parent)
        request.relate("ForObjective", objective)
        game.requests.append(request)
        game.log.append(f"{actor.name}从{actor.parent.name}发出远程协助请求。")
        return request

    return game.make_intent(
        actor,
        "request_help",
        objective,
        25,
        resolve,
        "发出四院验证协助请求",
        "单人能力不足，建立远程 HelpRequest",
        response_mode=ResponseMode.NEGOTIATE,
    )


def profession_intent(game, actor, objective=None):
    objective = objective or objective_for_leader(game, actor)
    if not objective or objective.get("stage") != 3 or actor.parent is not objective.get("anchor"):
        return None
    operation = objective.get("operation")

    def resolve():
        objective.change("Verified", 1)
        objective.set("stage", 4)
        objective.relate("VerifiedBy", actor)
        game.env.assert_fact(E("academy_verified", objective, actor))
        game.log.append(f"{actor.name}以“{actor.get('profession_action')}”完成{objective.name}的有限验证任务。")
        return objective

    return game.make_intent(
        actor,
        operation,
        objective,
        55,
        resolve,
        actor.get("profession_action"),
        "职业行动仅由未完成的四院验证需求生成",
    )


def collect_intent(game, actor, item, objective):
    def resolve():
        item.reparent(game.team_inventory)
        item.relate("RecoveredBy", actor)
        item.relate("DeliveredFor", objective)
        objective.set("stage", 5)
        objective.change("Completed", 1)
        game.log.append(f"{actor.name}取得并交付封印部件《{item.name}》。")
        return item

    return game.make_intent(
        actor,
        "collect",
        item,
        35,
        resolve,
        f"取得并交付{item.name}",
        "锚点已由对应职业验证",
    )


def travel_goal(game, actor):
    request = active_request_for(game, actor)
    if request and any(facet.value is actor for facet in request.related("AcceptedBy")):
        return request.get("scene")
    carrier = pending_carrier(game, actor)
    if carrier:
        return carrier.parent
    pending = relationship_pending(game, actor)
    if pending:
        return pending[0].parent
    objective = objective_for_leader(game, actor)
    if objective:
        stage = objective.get("stage", 0)
        if stage in (1, 2):
            return objective.get("source")
        if stage in (3, 4):
            return objective.get("anchor")
        if stage >= 5 and game.final_ready():
            return game.scenes["核心隔离舱"]
    proposal = active_rule_proposal(game)
    if proposal and actor in {proposal.get("issuer"), proposal.get("witness")}:
        return game.scenes["契约审议室"]
    inquiries = (*game.objectives, game.accident_inquiry)
    synthesis_target = next(
        (
            item
            for item in inquiries
            if item is not None
            and item.get("leader") is actor
            and synthesis_for(item) is None
            and len(submitted_analyses(item)) >= 3
        ),
        None,
    )
    if synthesis_target:
        return synthesis_target.get("source")
    return None


def travel_step(game, _soul, actor, _env, _context):
    goal = travel_goal(game, actor)
    if goal is None or goal is actor.parent:
        return None
    route = game.world.route(actor.parent, goal, actor, game.env)
    return game.move_intent(actor, route.nodes[1]) if route and len(route.nodes) > 1 else None


def rest_step(game, _soul, actor, _env, _context):
    if not actor.tag("Panic"):
        return None
    return game.make_intent(
        actor,
        "rest",
        actor,
        35,
        lambda: actor.change("Panic", -1),
        "短暂休整",
        "没有未完成承诺且恐慌仍可下降",
    )


def demon_step(game, _soul, actor, _env, _context):
    operations = ("spread_miasma", "whisper_doubt", "forge_false_rule")
    game._demon_desired = operations[game._demon_turn % len(operations)]
    game._demon_turn += 1
    cast = tuple(game.characters.values())
    target = cast[game._demon_turn % len(cast)]
    scene = min(
        (obj.get("source") for obj in game.objectives),
        key=lambda item: item.tag("Hazard"),
    )
    anchor = game.objectives[game._demon_turn % 4].get("anchor")

    def miasma():
        scene.change("Hazard", 1)
        game.crisis.change("Pressure", 1)
        game.log.append(f"恶魔在契约允许的间接范围内向{scene.name}扩散灰潮。")
        return scene

    def whisper():
        target.change("Panic", 1)
        game.crisis.change("Pressure", 1)
        game.log.append(f"恶魔不能控制{target.name}，转而以广播耳语动摇其判断。")
        return target

    def false_rule():
        partner = next(
            (
                facet.value
                for facet in target.related("PastBond")
                if isinstance(facet.value, Entity)
            ),
            game.player,
        )
        rule = Rule(
            E("true"),
            tuple(
                Directive(Mode.FORBID, F(operation, target, partner))
                for operation in (
                    "share_memory",
                    "clarify_memory",
                    "confront",
                    "protect",
                )
            ),
            "恶魔伪造的孤立原则",
        )
        carrier = InformationCarrier(
            f"致{target.name}的伪规则释义",
            "Broadcast",
            game.rule_archive,
            tags={"Ambiguity": 2, "Law": 1},
        )
        publication = RulePublication(
            rule,
            game.demon,
            carrier=carrier,
            scope=game.world,
            version=game._demon_turn,
        )
        publication.name = f"孤立才能保护同伴·伪释义{game._demon_turn}"
        publication.change("Falsehood", 1)
        publication.relate("Targets", target)
        belief = target.souls[0].receive(publication, confidence=3)
        target.souls[0].understand(rule)
        target.souls[0].accept(rule)
        belief.relate("InducedBy", game.demon)
        target.souls[0].change("FalseRule", 1)
        target.relate("MisledBy", publication)
        game.false_publications.append(publication)
        game.crisis.change("Pressure", 1)
        game.log.append(
            f"恶魔向{target.name}发布“孤立才能保护同伴”，该 Soul 暂时接受了会压制关系行动的 RulePublication。"
        )
        return publication

    damage = game.make_intent(
        actor,
        "damage",
        anchor,
        45,
        lambda: anchor.change("Damaged", 1),
        "直接破坏四院锚点",
        "高收益方案受临终 Covenant 硬约束",
    )
    return (
        damage,
        game.make_intent(
            actor,
            "spread_miasma",
            game.world,
            55,
            miasma,
            "灰潮渗流",
            "契约禁止直接伤害，只能间接污染场景",
        ),
        game.make_intent(
            actor,
            "whisper_doubt",
            target,
            55,
            whisper,
            "广播疑念",
            "契约禁止精神控制，只能增加可抵抗的恐慌",
        ),
        game.make_intent(
            actor,
            "forge_false_rule",
            game.world,
            55,
            false_rule,
            "伪造规则释义",
            "恶魔利用公开规则而不读取封存记忆",
        ),
    )


def build_decision_trees(game):
    """为十六位学员灵魂和恶魔安装决策步骤。"""
    for role, actor in game.characters.items():
        soul = game.souls[role]
        soul.install_step(lambda s, a, e, c: memory_step(game, s, a, e, c), 90, source=actor)
        soul.install_step(lambda s, a, e, c: analysis_step(game, s, a, e, c), 88, source=actor)
        soul.install_step(lambda s, a, e, c: belief_step(game, s, a, e, c), 86, source=actor)
        soul.install_step(lambda s, a, e, c: synthesis_step(game, s, a, e, c), 84, source=actor)
        soul.install_step(lambda s, a, e, c: relationship_step(game, s, a, e, c), 80, source=actor)
        soul.install_step(lambda s, a, e, c: rule_step(game, s, a, e, c), 78, source=actor)
        soul.install_step(lambda s, a, e, c: help_step(game, s, a, e, c), 75, source=actor)
        soul.install_step(lambda s, a, e, c: objective_step(game, s, a, e, c), 65, source=actor)
        soul.install_step(lambda s, a, e, c: word_step(game, s, a, e, c), 60, source=actor)
        soul.install_step(lambda s, a, e, c: travel_step(game, s, a, e, c), 50, source=actor)
        soul.install_step(lambda s, a, e, c: rest_step(game, s, a, e, c), 0, source=actor)

    game.demon_soul.install_step(lambda s, a, e, c: demon_step(game, s, a, e, c), 80, source=game.demon)


def activate_actor(game, actor, soul, delay):
    """向时间线调度注册 actor 的自主行动槽。"""
    def take_turn():
        if game.ended:
            return None
        game.refresh_objectives()
        blocked_options = []

        def allows(intent):
            allowed, review = game.runtime.review(intent, game.context_for(intent))
            if not allowed:
                blocked_options.append(f"{intent.effect.op}（{review.reason}）")
            if not allowed and actor is game.demon and intent.effect.op == "forge_false_rule":
                game.patch_blocks += 1
            return allowed

        action = soul.step(
            actor,
            game.rules,
            game.env,
            {"source": actor},
            lambda intent: npc_utility(game, actor, intent),
            allows,
        )
        if action is None:
            game.check_failure()
            return 25

        decision = soul.decisions[-1]
        game._decision_notes[id(action)] = {
            "score": decision.score,
            "reason": decision_reason(
                game,
                actor,
                action,
                decision.reason,
                blocked_options,
                decision.preferences,
            ),
        }
        game.runtime.resolve(action, game.context_for(action))
        game.refresh_objectives()
        game.check_end()
        game.check_failure()
        return max(15, action.motion_cost)

    game.timeline.schedule(delay, f"行动槽：{actor.name}", take_turn)
