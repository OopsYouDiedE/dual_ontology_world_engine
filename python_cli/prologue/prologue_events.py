"""初期篇（前二十日）每日特殊事件与剧情决策逻辑 (Prologue Events)。

工作代码：负责第1至第20日的特异剧情事件触发、言灵试验、死亡发现与冲突质询。
"""

from __future__ import annotations

from core.python.engine import (
    Directive,
    E,
    Entity,
    F,
    Influence,
    InformationCarrier,
    Knowledge,
    Mode,
    ResponseMode,
    Rule,
    RulePatch,
    RulePublication,
)


def special_1(prologue):
    """第1日：恶魔的首日公开真话提议。"""
    scene = prologue.scenes["中央厅"]
    actor = prologue.demon

    def resolve():
        offer = Entity(
            "恶魔的首日潜在盟友提议",
            prologue.world,
            {"Proposal": 1, "Provisional": 1, "Active": 1},
            properties={
                "strategy": "只公开有利于建立盟友位置的真话；省略封闭原因和自身目标",
                "accepted": False,
            },
        )
        offer.relate("OfferedBy", prologue.demon)
        for student in prologue.characters.values():
            offer.relate("OfferedTo", student)
            for statement in prologue.demon_opening_statements:
                prologue._give_knowledge(student.souls[0], statement)
        for statement in prologue.demon_opening_statements:
            offer.relate("BasedOnStatement", statement)
        prologue.provisional_alliance = offer
        prologue.log.append(
            "第1日：恶魔只公开三组真话：学园已经封闭且它负有最低生存契约；"
            "恶魔说出口的话不得为假；它能够协助学员逃离荒岛。它没有解释封闭原因。"
        )
        return offer

    intent, reason = prologue._intent(
        actor,
        "publish_true_offer",
        prologue.resources,
        "恶魔以三组真话提出潜在合作",
        resolve,
        "三组命题都为真，且选择性公开能把恶魔放到危险但有用的潜在盟友位置",
        response_mode=ResponseMode.NEGOTIATE,
    )
    prologue._choose(((intent, 100, reason),), scene, tuple(prologue.characters.values()))


def special_4(prologue):
    """第4日：律师与侦探建立公开证据调查守则。"""
    lawyer, witness = prologue.characters["律师"], prologue.characters["侦探"]
    scene = prologue.scenes["契约审议室"]

    def establish():
        directives = tuple(
            Directive(Mode.FORBID, F("accuse_without_evidence", actor, prologue.demon))
            for actor in prologue.characters.values()
        )
        rule = Rule(E("true"), directives, "公开指控必须附带可检查材料")
        carrier = InformationCarrier(
            "第一版共同调查守则",
            "Manuscript",
            scene,
            tags={"Law": 2, "Guarantee": 2},
        )
        publication = RulePublication(
            rule, lawyer, carrier=carrier, scope=prologue.world, version=1
        )
        publication.relate("Witness", witness)
        patch = RulePatch(
            publication.name, rule, scene, publication, prologue.world, priority=3
        )
        patch.relate("Witness", witness)
        prologue.runtime.patches.append(patch)
        prologue.rule_patches.append(patch)
        for student in prologue.characters.values():
            soul = student.souls[0]
            soul.receive(publication, 3)
            soul.understand(rule)
            soul.accept(rule)
        lawyer.change("Authority", 2)
        witness.change("Authority", 1)
        prologue.evidence_rule = patch
        prologue.log.append("第4日：全员接受第一条共同规则：怀疑可以公开，定罪式指控必须交出材料。")
        return patch

    def command():
        lawyer.change("Authority", 1)
        return Entity("律师单方面发布的禁言命令", scene, {"RejectedProposal": 1})

    options = []
    for operation, resolver, score, reason in (
        ("establish_rule", establish, 82, "侦探见证、文本公开且所有人保留质疑权"),
        ("issue_order", command, 48, "立即压住争吵，但没有授权来源和失效条件"),
    ):
        intent, why = prologue._intent(
            lawyer,
            operation,
            prologue.world,
            "建立证据指控规则" if operation == "establish_rule" else "单方面禁止公开怀疑",
            resolver,
            reason,
        )
        options.append((intent, score, why))
    prologue._choose(options, scene, tuple(prologue.characters.values()))


def special_5(prologue):
    """第5日：考古学家以教材独立验证恶魔真话。"""
    archaeologist = prologue.characters["考古学家"]
    scene = prologue.scenes["教学图书馆"]

    def verify():
        prologue.occult_primer.tags.pop("Hidden", None)
        claims = tuple(facet.value for facet in prologue.occult_primer.entries("Claim"))
        for student in prologue.characters.values():
            student.souls[0].learn(
                prologue.demon,
                claims,
                source=prologue.occult_primer,
                confidence=3,
                stage="accepted",
                name=f"{student.name}由《神秘学导论》确认的恶魔契约知识",
            )
        for statement in prologue.demon_opening_statements:
            statement.relate("VerifiedBy", prologue.occult_primer)
        archaeologist.change("Authority", 1)
        prologue.log.append(
            "第5日：《神秘学导论》独立确认首日三组广播全部为真；"
            "众人同时确认真话可以被恶意裁剪，验证没有自动转化为信任。"
        )
        return prologue.occult_primer

    intent, reason = prologue._intent(
        archaeologist,
        "verify_demon_statements",
        prologue.occult_primer,
        "用《神秘学导论》校验恶魔首日广播",
        verify,
        "教材来源独立于恶魔，并分别支持最低生存契约、真话限制与逃岛能力",
        response_mode=ResponseMode.VERIFY,
    )
    prologue._choose(((intent, 96, reason),), scene, tuple(prologue.characters.values()))


def special_7(prologue):
    """第7日：音乐家初次回声标定言灵实验及失败记录。"""
    musician = prologue.characters["音乐家"]
    scene = prologue.scenes["共鸣厅"]

    def attempt():
        result = prologue.words.invoke(
            prologue.echo_word,
            musician.souls[0],
            {"resonator": prologue.sources["resonator"]},
            target=prologue.threat,
            context={"actor": musician, "world": prologue.world},
        )
        failure = prologue.words.last_failure
        record = Entity(
            "第一次回声标定试验记录",
            scene,
            {"Experiment": 1, "Failed": int(result is None)},
            properties={"failure": failure},
        )
        prologue.word_attempts.append(record)
        prologue.projects["resonance"].change("Evidence", 2)
        musician.change("Authority", 1)
        prologue.log.append(f"第7日：言灵没有发动。音乐家坚持把“{failure}”逐项写进记录，没有用气氛掩盖失败。")
        return record

    intent, reason = prologue._intent(
        musician,
        "test_word",
        prologue.threat,
        "第一次言灵实验：失败记录",
        attempt,
        "先允许失败暴露缺失槽位，避免把偶然现象误认成成功",
    )
    prologue._choose(
        ((intent, 90, reason),),
        scene,
        (musician, prologue.characters["诗人"], prologue.characters["舞蹈艺术家"]),
    )


def special_9(prologue):
    """第9日：恶魔提供被刮除的否定词铜版。"""
    scene = prologue.scenes["契约审议室"]

    def translate():
        prologue.sources["demon_clause"].reparent(scene)
        prologue.sources["demon_clause"].relate("ProvidedBy", prologue.demon)
        prologue.projects["lexicon"].change("Progress", 3)
        prologue.demon.change("Reliability", 2)
        prologue._support(
            prologue.characters["律师"],
            prologue.sources["demon_clause"],
            "demon",
            1,
            contradiction=True,
        )
        prologue.log.append("第9日：恶魔补出导师笔记里被刮掉的否定词，并允许律师把它写成可追责的公开译本。")
        return prologue.sources["demon_clause"]

    intent, reason = prologue._intent(
        prologue.demon,
        "translate_binding_term",
        prologue.sources["demon_clause"],
        "恶魔公开关键否定词",
        translate,
        "广播语义若继续损坏，恶魔也将失去唯一合法发言媒介",
    )
    prologue._choose(
        ((intent, 95, reason),),
        scene,
        (
            prologue.characters["律师"],
            prologue.characters["考古学家"],
            prologue.characters["诗人"],
        ),
    )


def special_13(prologue):
    """第13日：职业棋手校勘三本历书的误差模式。"""
    chess = prologue.characters["职业棋手"]
    scene = prologue.scenes["星盘讲堂"]

    def compare():
        knowledge = prologue._support(chess, prologue.sources["almanac"], "lunar", 3)
        prologue.hypotheses["machine"].change("Contradiction", 1)
        chess.change("Authority", 2)
        prologue.log.append("第13日：棋手把三本历书当成三盘互相矛盾的棋谱，算出仪表漂移会在满月重复，而非随机磨损。")
        return knowledge

    intent, reason = prologue._intent(
        chess,
        "compare_almanacs",
        prologue.hypotheses["lunar"],
        "比较三本历书的误差模式",
        compare,
        "三份错误若以固定周期偏移，误差本身就是证据",
    )
    prologue._choose(
        ((intent, 92, reason),),
        scene,
        (chess, prologue.characters["考古学家"], prologue.characters["建筑师"]),
    )


def special_14(prologue):
    """第14日：侦探勘验并封存水压归零纸带。"""
    detective = prologue.player
    scene = prologue.scenes["上水库与水务站"]

    def inspect():
        prologue.threat.tags.pop("Dormant", None)
        prologue.threat.change("Trace", 1)
        a = prologue._support(detective, prologue.sources["gauge"], "machine", 2)
        prologue._support(detective, prologue.sources["gauge"], "demon", 1, contradiction=True)
        prologue.log.append("第14日：水压归零时封条完整，恶魔广播也未经过水务线；侦探保留机械故障假说，同时削弱恶魔破坏说。")
        return a

    intent, reason = prologue._intent(
        detective,
        "inspect_anomaly",
        prologue.sources["gauge"],
        "封存无人触碰的水压纸带",
        inspect,
        "先记录封条、时刻与管线状态，不因最方便的嫌疑人跳过现场",
    )
    prologue._choose(
        ((intent, 94, reason),),
        scene,
        (detective, prologue.characters["机械工程师"], prologue.characters["化学家"]),
    )


def special_15(prologue):
    """第15日：满月传声管异常显现与恶魔广播预警。"""
    scene = prologue.scenes["生活院"]

    def manifest():
        prologue.threat.change("Manifested", 1)
        prologue.gray_tide.change("Pressure", 1)
        prologue.resources.change("WaterDays", -2)
        prologue.sources["phonograph"].reparent(scene)
        prologue._support(prologue.characters["音乐家"], prologue.sources["phonograph"], "lunar", 2)
        prologue.log.append(
            "第15日：满月时，旧传声管依次模仿四名学员求救，并用他们的声音命令水阀关闭；没有人受伤，饮水储备损失两日。"
        )
        return prologue.threat

    intent, reason = prologue._intent(
        prologue.demon,
        "announce_external_anomaly",
        prologue.threat,
        "恶魔中断广播并标出伪造声源",
        manifest,
        "伪声正在占用它赖以履约的广播层；揭露威胁符合双方利益",
    )
    prologue._choose(((intent, 100, reason),), scene, tuple(prologue.characters.values()))


def special_16(prologue):
    """第16日：三方假说与证据交叉对比分析。"""
    scene = prologue.scenes["中央厅"]
    options = []
    evidence = (
        ("侦探", "lunar", prologue.sources["phonograph"], 78, False, "反向录音与满月时刻一致"),
        ("化学家", "machine", prologue.sources["gauge"], 65, False, "压力变化仍可能来自阀门故障"),
        ("职业棋手", "demon", prologue.sources["demon_clause"], 74, True, "恶魔若主动切断广播会同时损害自身合法行动"),
    )
    for role, hypothesis, source, score, contradiction, reason in evidence:
        actor = prologue.characters[role]

        def make_resolver(act=actor, hyp=hypothesis, src=source, cont=contradiction):
            def resolve():
                result = prologue._support(act, src, hyp, 2, cont)
                act.change("Authority", 1)
                return result
            return resolve

        intent, why = prologue._intent(
            actor,
            "analyze_evidence",
            prologue.hypotheses[hypothesis],
            f"{actor.name}提交可反驳的异常解释",
            make_resolver(),
            reason,
        )
        options.append((intent, score + actor.tag("Authority"), why))
    prologue._choose(options, scene, tuple(prologue.characters[role] for role, *_ in evidence))


def special_17(prologue):
    """第17日：公开质询与规则保护机制检验。"""
    scene = prologue.scenes["契约审议室"]
    shooter, detective, lawyer = (
        prologue.characters["神射手"],
        prologue.player,
        prologue.characters["律师"],
    )

    def accuse():
        prologue._trust(shooter, prologue.demon, -2, prologue.threat)
        return prologue.demon

    def present():
        result = prologue._support(detective, prologue.sources["phonograph"], "lunar", 2)
        detective.change("Authority", 2)
        prologue.log.append(
            "第17日：侦探没有替恶魔作人格担保，只证明现有材料更支持月相反馈；神射手的拘束主张因没有载体被共同规则挡下。"
        )
        return result

    def defend():
        prologue.hypotheses["demon"].change("Contradiction", 1)
        lawyer.change("Authority", 1)
        return prologue.hypotheses["demon"]

    options = []
    for actor, operation, target, resolver, score, reason, label in (
        (shooter, "accuse_without_evidence", prologue.demon, accuse, 96, "恐惧给出高冲动效用，但违反第4日共同规则", "要求立刻拘束恶魔"),
        (detective, "present_evidence", prologue.hypotheses["lunar"], present, 84, "蜡筒、历书和水压纸带可以被其他人复核", "提交月相反馈证据链"),
        (lawyer, "argue_exculpation", prologue.hypotheses["demon"], defend, 76, "恶魔并非无罪，只是当前破坏假说与其利益冲突", "限定恶魔嫌疑的适用范围"),
    ):
        intent, why = prologue._intent(actor, operation, target, label, resolver, reason)
        options.append((intent, score, why))
    prologue._choose(options, scene, tuple(prologue.characters.values()))


def special_18(prologue):
    """第18日：四方协同施行言灵“回声标定”。"""
    musician = prologue.characters["音乐家"]
    scene = prologue.scenes["共鸣厅"]
    prologue.env.assert_fact(E("lunar_window", prologue.threat))

    def invoke():
        rite = prologue.words.invoke(
            prologue.echo_word,
            musician.souls[0],
            {
                "resonator": prologue.sources["resonator"],
                "almanac": prologue.sources["almanac"],
                "negative": prologue.sources["demon_clause"],
            },
            target=prologue.threat,
            context={"actor": musician, "holder": scene, "world": prologue.world},
        )
        if rite is None:
            raise RuntimeError(f"回声标定未成立：{prologue.words.last_failure}")
        prologue.threat.change("Exposed", 1)
        prologue.hypotheses["lunar"].change("Support", 3)
        musician.change("Authority", 2)
        rite.relate("Contributor", prologue.characters["诗人"])
        rite.relate("Contributor", prologue.characters["律师"])
        rite.relate("Contributor", prologue.demon)
        prologue.log.append("第18日：音乐家施行真实言灵“回声标定”。诗人守拍，律师核词，恶魔提供否定发音；伪声显出来自月钟而非核心。")
        return rite

    intent, reason = prologue._intent(
        musician,
        "invoke_word",
        prologue.threat,
        "言灵：回声标定",
        invoke,
        "失败记录补齐槽位；亏凸月仍满足观测窗；四方投入可追溯",
    )
    prologue._choose(
        ((intent, 110, reason),),
        scene,
        (musician, prologue.characters["诗人"], prologue.characters["律师"], prologue.demon),
    )


def special_19(prologue):
    """第19日：设立临时月相作业规程 RulePatch。"""
    lawyer, witness = prologue.characters["律师"], prologue.characters["职业棋手"]
    scene = prologue.scenes["契约审议室"]

    def establish():
        clock = prologue.sources["clock"]
        rule = Rule(
            E("true"),
            tuple(
                Directive(Mode.FORBID, F("operate_unlogged", actor, clock))
                for actor in prologue.characters.values()
            ),
            "望日前后三日的共鸣设备必须留档并由第二人复核",
        )
        carrier = InformationCarrier(
            "月相作业规程·临时版",
            "Manuscript",
            scene,
            tags={"Law": 3, "Guarantee": 2},
        )
        publication = RulePublication(
            rule, lawyer, carrier=carrier, scope=prologue.world, version=2
        )
        publication.relate("Witness", witness)
        patch = RulePatch(
            publication.name, rule, scene, publication, prologue.world, priority=4
        )
        patch.relate("BasedOn", prologue.sources["almanac"])
        patch.relate("BasedOn", prologue.sources["phonograph"])
        patch.relate("Witness", witness)
        prologue.rule_patches.append(patch)
        prologue.runtime.patches.append(patch)
        prologue.lunar_rule = patch
        lawyer.change("Authority", 2)
        witness.change("Authority", 1)
        prologue.log.append("第19日：临时月相规程成立。它只管望日前后三日、只管共鸣设备，并写明第30日必须复审。")
        return patch

    intent, reason = prologue._intent(
        lawyer,
        "establish_rule",
        prologue.sources["clock"],
        "设立临时月相作业规程",
        establish,
        "证据链、作用对象、月相窗口、见证者和失效复审日均明确",
    )
    prologue._choose(((intent, 105, reason),), scene, tuple(prologue.characters.values()))


def special_20(prologue):
    """第20日正常分支：第一次共同防卫（传声回声隔离）。"""
    if prologue.death_discovery is not None:
        return special_20_after_death(prologue)

    scene = prologue.scenes["动力总站"]
    engineer, musician, lawyer = (
        prologue.characters["机械工程师"],
        prologue.characters["音乐家"],
        prologue.characters["律师"],
    )

    def demon_hold():
        prologue.demon.change("Reliability", 2)
        prologue.threat.change("Suppressed", 1)
        prologue.log.append("第20日：恶魔主动让出全部广播频段，只保留契约规定的维生脉冲，使回声无法继续借它的声音扩散。")
        return prologue.threat

    intent, reason = prologue._intent(
        prologue.demon,
        "yield_broadcast_channel",
        prologue.threat,
        "恶魔暂时让出广播频段",
        demon_hold,
        "若传声回声夺取广播，恶魔与学员都会失去维生调度",
    )
    prologue._choose(((intent, 100, reason),), scene, (prologue.demon, engineer, musician, lawyer))

    def contain():
        if not (
            prologue.threat.has("Exposed")
            and prologue.lunar_rule
            and prologue.provisional_alliance
            and prologue.projects["containment"].tag("Progress") >= 2
        ):
            raise RuntimeError("共同隔离缺少言灵揭示、月相规则或临时协定")
        rite = Entity("第一次共同防卫：传声回声隔离", scene, {"Rite": 1, "Completed": 1})
        for contributor in (engineer, musician, lawyer, prologue.demon):
            rite.relate("Contributor", contributor)
        rite.relate("Targets", prologue.threat)
        rite.relate("UsesRule", prologue.lunar_rule)
        prologue.threat.change("Contained", 1)
        prologue.gray_tide.change("Pressure", 1)
        prologue.resources.change("PowerDays", -1)
        engineer.change("Authority", 2)
        musician.change("Authority", 1)
        prologue.arc_complete = True
        prologue.log.append("第20日：工程师切断旧管，音乐家持续标定，律师守住临时规则，恶魔维持广播脉冲。威胁被隔离，没有人把它误写成最终胜利。")
        return rite

    intent, reason = prologue._intent(
        engineer,
        "joint_containment",
        prologue.threat,
        "完成第一次共同防卫",
        contain,
        "四种不同能力形成互锁；任何一方单独行动都只能暂时压制回声",
    )
    prologue._choose(((intent, 120, reason),), scene, (engineer, musician, lawyer, prologue.demon))


def special_20_after_death(prologue):
    """第20日死亡分支：建立死亡调查临时安全规则及恶魔证言。"""
    victim = prologue._fatal_victim()
    scene = prologue.scenes["契约审议室"]
    lawyer = prologue._alive_role("律师", "侦探")
    witness = prologue._alive_role("职业棋手", "建筑师", "神射手")

    def establish_safety():
        directives = tuple(
            Directive(Mode.FORBID, F("operate_high_risk_alone", actor, prologue.world))
            for actor in prologue.characters.values()
            if not actor.has("Dead")
        ) + tuple(
            Directive(Mode.FORBID, F("private_suggestion", prologue.demon, actor))
            for actor in prologue.characters.values()
            if not actor.has("Dead")
        )
        rule = Rule(
            E("true"),
            directives,
            "死亡调查期间禁止独自操作高危机关或与恶魔秘密会谈",
        )
        carrier = InformationCarrier(
            "死亡调查临时安全规则",
            "Manuscript",
            scene,
            tags={"Law": 3, "Guarantee": 2},
        )
        publication = RulePublication(
            rule, lawyer, carrier=carrier, scope=prologue.world, version=3
        )
        publication.relate("Witness", witness)
        patch = RulePatch(
            publication.name, rule, scene, publication, prologue.world, priority=5
        )
        patch.relate("BasedOn", prologue.death_discovery)
        patch.relate("Witness", witness)
        prologue.runtime.patches.append(patch)
        prologue.rule_patches.append(patch)
        prologue.pair_maintenance_rule = patch
        for student in prologue.characters.values():
            if student.has("Dead"):
                continue
            soul = student.souls[0]
            soul.receive(publication, 3)
            soul.understand(rule)
            soul.accept(rule)
        return patch

    intent, reason = prologue._intent(
        lawyer,
        "establish_rule",
        prologue.world,
        "建立死亡调查期间的结伴与公开谈话规则",
        establish_safety,
        "规则只约束可观察的独处和高危操作，不把尚未证明的诱导写成既成事实",
        response_mode=ResponseMode.PROTECT,
    )
    prologue._choose(((intent, 110, reason),), scene, tuple(prologue.characters.values()))

    def testify():
        claims = (
            E("spoke_privately_with", prologue.demon, victim),
            E("did_not_directly_control", prologue.demon, victim),
            E("did_not_directly_kill", prologue.demon, victim),
        )
        for claim in claims:
            prologue.env.assert_fact(claim)
        carrier = InformationCarrier(
            "恶魔关于死者的选择性证言",
            "Testimony",
            scene,
            creator=prologue.demon,
            claims=claims,
            tags={"Truth": 1, "Incomplete": 1, "Law": 1},
        )
        carrier.relate("RefusedToExplain", prologue.demon)
        for student in prologue.characters.values():
            if student.has("Dead"):
                continue
            student.souls[0].learn(
                prologue.demon,
                claims,
                source=carrier,
                confidence=3,
                stage="accepted",
                name=f"{student.name}接收的恶魔死亡证言",
            )
            prologue._trust(student, prologue.demon, -1, carrier)
        prologue.log.append(
            "第20日：恶魔承认曾与死者独处，并如实否认直接控制和直接杀人；"
            "它拒绝说明是否调整过情绪。真话增加了事实，也扩大了敌意。"
        )
        return carrier

    intent, reason = prologue._intent(
        prologue.demon,
        "provide_partial_true_testimony",
        victim,
        "恶魔提供真实但不完整的死亡证言",
        testify,
        "真话契约迫使已说出的三项命题为真；沉默保留其对情绪调整的隐瞒",
        response_mode=ResponseMode.CONCEAL,
    )
    event = prologue._choose(((intent, 100, reason),), scene, tuple(prologue.characters.values()))
    for student in prologue.characters.values():
        if student.has("Dead"):
            continue
        prologue._add_social_impact(
            student,
            prologue.demon,
            event,
            {"bond": 0, "malicious_intent": True, "threat": 2, "norm_breach": True},
        )
    prologue.projects["containment"].change("PausedByDeath", 1)
    prologue.threat.change("Uncontained", 1)
    prologue.arc_complete = False
