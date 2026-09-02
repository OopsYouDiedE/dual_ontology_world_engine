"""《异常感知学园》第一章（禁闭后二十天）运行时 (AcademyPrologue Runtime)。

工作代码：基于核心 IncidentDispatcher 与条件驱动事件模版，
实现由在场人员、灵魂偏好与情境条件自动触发并进行加权选择的群像叙事流程。
"""

from __future__ import annotations

from core.python.engine import (
    ActionRuntime,
    Directive,
    E,
    Entity,
    F,
    IncidentDispatcher,
    IncidentOption,
    IncidentTemplate,
    Influence,
    InformationCarrier,
    Intent,
    Knowledge,
    Mode,
    Recipe,
    ResponseMode,
    Rule,
    RulePatch,
    RulePublication,
    SocialImpact,
    Timeline,
    select_social_influence,
    social_attitude_pressure,
)
from ..content.prologue_data import (
    APPROACH,
    DAY_SPECS,
    LEISURE,
    PROJECT_SPECS,
    REPAIR_SCENES,
)
from ..game.academy_game import AcademyGame


class AcademyPrologue:
    """二十天初期篇运行时；由通用情境事件调度器驱动。"""

    campaign_days = (100, 180)

    def __init__(self):
        base = AcademyGame(autonomous=False)
        self.base = base
        self.world, self.env, self.rules, self.words = (
            base.world,
            base.env,
            base.rules,
            base.words,
        )
        self.characters, self.souls, self.scenes = (
            base.characters,
            base.souls,
            base.scenes,
        )
        self.player, self.demon = base.player, base.demon

        # 清除默认步骤与承诺
        for soul in (*self.souls.values(), base.demon_soul):
            soul.detach("Step")
        for actor in self.characters.values():
            actor.detach("CommittedTo")
            actor.tags.pop("Authority", None)
        self.demon.tags.pop("Authority", None)
        self.demon.change("Reliability", 1)

        self.timeline = Timeline()
        self.runtime = ActionRuntime(
            self.timeline,
            self.env,
            self.rules,
            self.words,
            event_parent=self.world,
            witnesses=self._witnesses,
            listeners=(self._observe_social_outcome,),
        )

        # 实例化通用情境事件调度器
        self.dispatcher = IncidentDispatcher(self.runtime, self.rules, self.env)

        self.social_impacts = []
        self.daily_actions = []
        self.demon_fatal_actions = []
        self.death_discovery = None
        self.death_evidence = []
        self.death_hypotheses = {}
        self.day = 0
        self.day_summaries = []
        self.log = []
        self.rule_patches = []
        self.analyses = []
        self.word_attempts = []
        self.arc_complete = False

        self.resources = Entity(
            "维生资源账",
            self.world,
            {"FoodDays": 38, "WaterDays": 32, "PowerDays": 26, "MedicalReserve": 12},
            properties={"population": 16},
        )
        self.gray_tide = Entity(
            "灰潮远期侵蚀",
            self.world,
            {"Threat": 1, "Pressure": 0, "Dormant": 1},
            properties={"limit": 24, "earliest_terminal_day": 100},
        )
        self.threat = Entity(
            "月相传声回声",
            self.world,
            {"Threat": 1, "Dormant": 1, "Veil": 2},
            properties={
                "first_manifestation": 15,
                "source": "旧传声管与月钟的相位反馈",
            },
        )
        self.projects = {
            key: Entity(
                name,
                self.world,
                {"Project": 1},
                properties={
                    "scene": self.scenes[scene_name],
                    "skills": skills,
                    "need": need,
                    "key": key,
                },
            )
            for key, (name, scene_name, skills, need) in PROJECT_SPECS.items()
        }

        self._build_damaged_campus()
        self._build_demon_truths()
        self._build_lethal_mechanism()
        self.sources = self._build_sources()
        self.hypotheses = self._build_hypotheses()
        self.provisional_alliance = None
        self.evidence_rule = None
        self.lunar_rule = None
        self.echo_word = self._build_echo_word()
        self._seed_initial_demon_attitude()

    def _build_damaged_campus(self):
        workplaces = tuple(actor.get("home") for actor in self.characters.values())
        damaged = tuple(
            dict.fromkeys((*(self.scenes[name] for name in REPAIR_SCENES), *workplaces))
        )
        self.damaged_scenes = damaged
        for scene in self.damaged_scenes:
            scene.tags["Damage"] = max(scene.tag("Damage"), 3)
            scene.tags["Safety"] = max(scene.tag("Safety"), 1)
            scene.set("repair_effort", 0)
        living = self.scenes["生活院"]
        self.quarters = {}
        for role, actor in self.characters.items():
            quarters = Entity(
                f"{actor.get('personal_name')}的受损寝室",
                living,
                {"Quarters": 1, "SleepingArea": 1, "Damage": 2},
                properties={"occupant": actor},
            )
            actor.set("workplace", actor.get("home"))
            actor.set("home", living)
            actor.set("quarters", quarters)
            actor.reparent(living)
            self.quarters[role] = quarters

    def _build_demon_truths(self):
        self.demon.tags.update(
            {"HumanGuise": 1, "ManifestAnywhere": 1, "Suggestion": 1, "BroadcastInterference": 1, "CostlyTextEdit": 1}
        )
        self.demon.properties.update(
            usual_presentation="无害、礼貌且主动提供有限帮助的人形",
            speech_constraint="所有说出口的命题必须为真；允许沉默与省略",
            text_edit_limit="每次只能修改少量文字，并支付契约代价",
        )
        closed, survival, truthful, escape = (
            E("school_closed", self.world),
            E("maintains_basic_survival", self.demon, self.resources),
            E("must_speak_truth", self.demon),
            E("can_help_escape_island", self.demon, self.world),
        )
        for fact in (closed, survival, truthful, escape):
            self.env.assert_fact(fact)
        self.demon_opening_statements = (
            InformationCarrier("广播真话一：封闭事实与最低生存契约", "Broadcast", self.scenes["中央厅"], creator=self.demon, claims=(closed, survival), tags={"Truth": 1, "Guarantee": 1}),
            InformationCarrier("广播真话二：恶魔发言不得为假", "Broadcast", self.scenes["中央厅"], creator=self.demon, claims=(truthful,), tags={"Truth": 1, "Guarantee": 1}),
            InformationCarrier("广播真话三：恶魔能够协助逃岛", "Broadcast", self.scenes["中央厅"], creator=self.demon, claims=(escape,), tags={"Truth": 1, "Guarantee": 1}),
        )
        self.occult_primer = InformationCarrier(
            "《神秘学导论·恶魔契约章》", "Manuscript", self.scenes["教学图书馆"],
            creator=self.base.mentors[0], claims=(survival, truthful, escape),
            tags={"Hidden": 1, "Law": 2, "Mind": 2, "IndependentSource": 1},
        )
        self.pre_school_demon_lore = Entity("入学前流传的恶魔常识", self.world, {"BackgroundKnowledge": 1})
        truth_rule = Rule(E("true"), (Directive(Mode.FORBID, F("speak_false", self.demon, self.world)),), "恶魔发言不得为假")
        self.base.demon_covenant.attach("HardConstraint", truth_rule)

    def _build_lethal_mechanism(self):
        scene = self.scenes["废物处理站"]
        self.lethal_mechanism = Entity(
            "废物处理站旧式配重焚化闸", scene, {"Machine": 1, "HiddenDanger": 1, "LethalMechanism": 1, "Damage": 4},
            properties={"connected_to_exit_service_tunnel": True, "restores_waste_and_water_pressure": True, "safety_interlock_known": False, "disabled": False},
        )
        self.demon_attack_window = (19, 19)
        self.pair_maintenance_rule = None

    def _give_knowledge(self, soul, statement, stage="received", confidence=1):
        claims = tuple(facet.value for facet in statement.entries("Claim"))
        return soul.learn(
            self.demon, claims, source=statement, confidence=confidence, stage=stage,
            name=f"{soul.name}接收的{statement.name}",
        )

    def _build_sources(self):
        return {
            "ledger": InformationCarrier("二十日维生账", "Ledger", self.resources, tags={"Law": 1, "Preservation": 2}),
            "almanac": InformationCarrier("三版本月相历书", "Manuscript", self.scenes["教学图书馆"], tags={"Threshold": 2, "Vestige": 1}),
            "gauge": InformationCarrier("归零前后的水压纸带", "Chart", self.scenes["上水库与水务站"], tags={"Mind": 2, "Form": 1}),
            "phonograph": InformationCarrier("传声管反向录音蜡筒", "Cylinder", self.scenes["声纹与记录室"], tags={"Mind": 2, "Name": 1}),
            "demon_clause": InformationCarrier("恶魔提供的否定词铜版", "Manuscript", self.scenes["契约审议室"], tags={"Name": 2, "Law": 2}),
            "resonator": Entity("校准音叉与黄铜共鸣架", self.scenes["共鸣厅"], {"Instrument": 1, "Mind": 2, "Form": 2}),
            "clock": Entity("停在错误时刻的黄铜月钟", self.scenes["星盘讲堂"], {"Machine": 1, "Damaged": 2, "Threshold": 1}),
        }

    def _build_hypotheses(self):
        return {
            "demon": Entity("假说A：恶魔主动破坏维生系统", self.world, {"Hypothesis": 1}, properties={"key": "demon"}),
            "machine": Entity("假说B：水务机械自然故障", self.world, {"Hypothesis": 1}, properties={"key": "machine"}),
            "lunar": Entity("假说C：月相令旧传声回路发生反馈", self.world, {"Hypothesis": 1}, properties={"key": "lunar"}),
        }

    def _build_echo_word(self):
        return Recipe(
            "回声标定",
            slots={
                "resonator": {"tags": {"Instrument": 1}},
                "almanac": {"tags": {"Threshold": 1}},
                "negative": {"tags": {"Name": 1, "Law": 1}},
            },
            thresholds={"Mind": 4, "Name": 3, "Law": 3, "Threshold": 2},
            requirements=(lambda scope: bool(self.env.evaluate(E("lunar_window", scope["target"]))),),
            effects=(F("change", "target", "Exposed", 1), F("assert", E("threat_identified", "target"))),
            parent=self.base.rule_archive,
            tags={"Incantation": 1, "Kotodama": 1, "Rite": 1},
        )

    def trust(self, actor, target):
        relation = actor.related("Trusts")
        return next((facet.strength for facet in relation if facet.value is target), 0)

    def _trust(self, actor, target, amount, source=None):
        current = self.trust(actor, target)
        updated = max(-3, min(3, current + amount))
        actor.detach("Trusts", target)
        if updated != 0:
            actor.relate("Trusts", target, updated, source=source)
        return updated

    def _snapshot(self):
        data = {
            "资源.食物": self.resources.tag("FoodDays"),
            "资源.饮水": self.resources.tag("WaterDays"),
            "资源.电力": self.resources.tag("PowerDays"),
            "资源.医资": self.resources.tag("MedicalReserve"),
            "灰潮.压力": self.gray_tide.tag("Pressure"),
            "威胁.显化": self.threat.tag("Manifested"),
            "威胁.暴露": self.threat.tag("Exposed"),
            "威胁.隔离": self.threat.tag("Contained"),
            "恶魔.可靠度": self.demon.tag("Reliability"),
            "恶魔.警惕度": self.demon.tag("Vigilance"),
            "恶魔.敌意度": self.demon.tag("Hostility"),
            "规则.补丁数": len(self.rule_patches),
            "认知.分析数": len(self.analyses),
            "假说.恶魔支持": self.hypotheses["demon"].tag("Support"),
            "假说.机械支持": self.hypotheses["machine"].tag("Support"),
            "假说.月相支持": self.hypotheses["lunar"].tag("Support"),
            "假说.恶魔矛盾": self.hypotheses["demon"].tag("Contradiction"),
            "假说.机械矛盾": self.hypotheses["machine"].tag("Contradiction"),
        }
        for key, project in self.projects.items():
            data[f"项目.{key}.进度"] = project.tag("Progress")
            data[f"项目.{key}.劳动"] = project.tag("Labor")
        for role, actor in self.characters.items():
            data[f"人物.{role}.位置"] = actor.parent.name if actor.parent else None
            data[f"人物.{role}.恐慌"] = actor.tag("Panic")
            data[f"人物.{role}.威信"] = actor.tag("Authority")
            data[f"人物.{role}.疲劳"] = actor.tag("Fatigue")
            data[f"人物.{role}.健康"] = actor.tag("Health")
            data[f"人物.{role}.对恶魔信任"] = self.trust(actor, self.demon)
        return data

    @staticmethod
    def _diff(before, after):
        return tuple(
            f"{key}: {before.get(key)} → {value}"
            for key, value in after.items()
            if before.get(key) != value
        )

    def _witnesses(self, _event, context):
        domains = context.get("domains", ())
        scenes = {d for d in domains if isinstance(d, Entity) and d.has("Scene")}
        if not scenes:
            return tuple(self.souls.values())
        return tuple(
            self.souls[role]
            for role, actor in self.characters.items()
            if actor.parent in scenes
        )

    def _intent(self, actor, operation, target, label, resolve, reason="", validate=lambda: True, response_mode=None):
        return (
            Intent(
                actor,
                actor.souls[0],
                F(operation, actor, target),
                20,
                resolve,
                label,
                validate,
                response_mode,
            ),
            reason,
        )

    @staticmethod
    def _impact_target(impact):
        relation = impact.related("Target")
        return relation[0].value if relation else None

    @staticmethod
    def _social_target(intent):
        return intent.effect.args[1] if len(intent.effect.args) > 1 else None

    def _active_social_impacts(self, soul, target=None):
        return tuple(
            impact
            for impact in self.social_impacts
            if impact.parent is soul
            and (target is None or self._impact_target(impact) is target)
            and self.timeline.now < impact.get("expires_at", self.timeline.now)
        )

    def _add_social_impact(self, receiver, target, source, appraisal):
        if not isinstance(receiver, Character) or not isinstance(target, Character):
            return None
        combination = select_social_influence(appraisal)
        if combination == "NO_STRONG_CHANGE":
            return None
        soul = receiver.souls[0]
        existing = next(
            (
                impact
                for impact in self._active_social_impacts(soul, target)
                if impact.get("combination") == combination
            ),
            None,
        )
        if existing is not None:
            existing.set(
                "expires_at",
                max(
                    existing.get("expires_at", self.timeline.now),
                    self.timeline.now + existing.get("duration", 200),
                ),
            )
            existing.relate("ReinforcedBy", source)
            source.relate("CreatedImpact", existing)
            return existing

        impact = SocialImpact(
            f"{receiver.name}对{target.name}：{combination}",
            soul,
            target,
            source,
            combination,
            self.timeline.now,
        )

        def attitude(_soul, intent, _env, _context, impact=impact, target=target):
            if self.timeline.now >= impact.get("expires_at", self.timeline.now):
                return 0
            if self._social_target(intent) is not target:
                return 0
            return social_attitude_pressure(
                impact.get("combination"),
                intent.response_mode,
            )

        soul.install_preference(attitude, source=impact)
        self.social_impacts.append(impact)
        source.relate("CreatedImpact", impact)
        return impact

    def _seed_initial_demon_attitude(self):
        for student in self.characters.values():
            level = student.tag("Occult")
            if level >= 3:
                student.souls[0].learn(
                    self.demon,
                    (E("must_speak_truth", self.demon), E("costly_text_edit", self.demon), E("indirect_suggestion_danger", self.demon)),
                    source=self.pre_school_demon_lore, confidence=3, stage="accepted",
                    name=f"{student.name}掌握的恶魔契约知识",
                )
            elif level == 2:
                student.souls[0].learn(
                    self.demon,
                    (E("must_speak_truth", self.demon), E("costly_text_edit", self.demon)),
                    source=self.pre_school_demon_lore, confidence=2, stage="understood",
                    name=f"{student.name}了解的恶魔常识",
                )
            else:
                student.souls[0].learn(
                    self.demon, (E("must_speak_truth", self.demon),),
                    source=self.pre_school_demon_lore, confidence=1, stage="received",
                    name=f"{student.name}听说的恶魔传闻",
                )

    def _observe_social_outcome(self, event, _context):
        effect, actor = event.get("effect"), event.get("actor")
        if effect is None or not isinstance(actor, Character):
            return
        op = effect.op
        if op in {"confide", "challenge", "collaborate"}:
            receiver, target = effect.args[1], actor
            if not isinstance(receiver, Character):
                return
            appraisal = {
                "bond": self.trust(receiver, actor),
                "support_received": op != "challenge",
                "norm_breach": op == "challenge",
            }
            if op == "confide":
                appraisal["vulnerability_accepted"] = True
            impact = self._add_social_impact(receiver, target, event, appraisal)
            if impact is not None:
                event.set("social_impact", impact.get("combination"))
                event.set("social_receiver", receiver)
                event.set("social_target", target)
                self.log.append(
                    f"{event.name}使{receiver.name}对{target.name}产生"
                    f"{impact.get('combination')}，它只形成临时态度偏置。"
                )

    def _choose_incident(self, candidates, scene, participants=(), weighted=False):
        """调用通用 IncidentDispatcher 驱动情境决选、状态差分与因果事件记录。"""
        # 将无 opt 包装的元组自动转为标准四元组
        norm_candidates = [
            (c[0], c[1], c[2], c[3] if len(c) > 3 else IncidentOption(c[0].effect.op, c[0].label, c[0].resolve))
            for c in candidates
        ]

        intent, score, reason, opt, blocked = self.dispatcher.assess_and_choose(
            norm_candidates, scene, self.world, weighted=weighted
        )

        everyone = tuple(dict.fromkeys((intent.actor, *participants)))
        for actor in everyone:
            if actor is not self.demon:
                actor.reparent(scene)

        before = self._snapshot()
        day_event_count = len([e for e in self.runtime.events if e.get("day") == self.day])
        self.timeline.now = (self.day - 1) * 200 + min(day_event_count, 5) * 20
        if not self.runtime.resolve(intent, {"domains": (self.world, scene), "participants": everyone}):
            raise RuntimeError(f"第{self.day}日行动结算失败：{intent.label}")
        after = self._snapshot()

        event = self.runtime.events[-1]
        event.set("day", self.day)
        event.set("moon", DAY_SPECS[self.day - 1][2])
        event.set("scene", scene)
        event.set("decision_score", score)
        event.set("decision_reason", reason)
        event.set("candidates", tuple(c[3] for c in norm_candidates))
        event.set("blocked_candidates", tuple(blocked))
        event.set("state_changes", self._diff(before, after))
        return event

    def _work(self, project_key, roles):
        """由情境模板驱动的日常项目推进。"""
        project = self.projects[project_key]
        scene = project.get("scene")

        # 声明通用的工作推进 IncidentTemplate
        template = IncidentTemplate(
            name=f"推进《{project.name}》",
            slots={
                "actor": lambda a, s: a.has("Student"),
                "project": lambda p, s: p is project,
            },
            options=[
                IncidentOption(
                    operation=APPROACH[r][0],
                    label=f"{{actor.name}}推进《{project.name}》",
                    resolve=self._make_work_resolver(project, r, APPROACH[r][1]),
                    base_score=lambda s, r=r: 40 + (18 if r in project.get("skills") else 5) + s["actor"].tag("Authority") * 2,
                    reason=f"职业方法={APPROACH[r][1]}；技能匹配={r in project.get('skills')}",
                    response_mode=ResponseMode.COOPERATE,
                )
                for r in roles
            ],
        )

        candidates = []
        for r in roles:
            actor = self.characters[r]
            scope = {"actor": actor, "project": project}
            for opt in template.options:
                if opt.operation == APPROACH[r][0]:
                    intents = self.dispatcher.build_intents(
                        IncidentTemplate(name=template.name, slots={}, options=[opt]),
                        scope,
                    )
                    candidates.extend(intents)

        return self._choose_incident(candidates, scene, tuple(self.characters[r] for r in roles))

    def _make_work_resolver(self, project, role, method):
        def resolve(scope):
            actor = scope["actor"]
            gain = 2 if role in project.get("skills") else 1
            project.change("Progress", gain)
            project.relate("Contributor", actor, gain)
            actor.change("Authority", 1)
            knowledge = Knowledge(
                f"{actor.name}对{project.name}的工作判断",
                actor.souls[0],
                ("working_conclusion", project, method),
                subject=project,
                source=project,
                tags={"Analysis": 1, "Mind": 1},
            )
            self.analyses.append(knowledge)
            self.log.append(f"第{self.day}日：{actor.name}{method}，{project.name}获得可复核进展。")
            return project
        return resolve

    def _social(self, actor_role, target_role, topic):
        """由情境模板驱动的人际交流（倾诉、协作、质询）。"""
        actor = self.characters[actor_role]
        target = self.demon if target_role == "恶魔" else self.characters[target_role]
        scene = self.scenes["公共娱乐室"] if target is not self.demon else self.scenes["中央厅"]

        tension = any(word in topic for word in ("资格", "责任", "可疑", "嫌疑", "错误", "失效"))
        vulnerable = any(word in topic for word in ("恐惧", "求救", "信任", "顶罪", "画像"))

        if target is self.demon:
            opt = IncidentOption(
                operation="consult_demon_records",
                label=f"{actor.name}就“{topic}”调阅恶魔文书",
                resolve=lambda s: self.demon,
                base_score=88,
                reason="恶魔真话受契约约束，但省略与诱导仍需由规则审查",
                response_mode=ResponseMode.NEGOTIATE,
            )
            tmpl = IncidentTemplate("调阅恶魔文书", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": actor, "target": target})
            self._choose_incident(intents, scene, (actor,))
            return

        def confide_res(scope):
            self._trust(actor, target, 2, topic)
            self._trust(target, actor, 1, topic)
            actor.change("Candor", 1)
            self.log.append(f"第{self.day}日：{actor.name}没有套话，向{target.name}说出自己对“{topic}”真正害怕的部分。")
            return target

        def challenge_res(scope):
            actor.change("Insight", 1)
            self._trust(actor, target, -1, topic)
            actor.relate("Questioned", target, source=topic)
            self.log.append(f"第{self.day}日：{actor.name}当面质疑{target.name}关于“{topic}”的说法，关系变紧但留下可检查的问题。")
            return target

        def collaborate_res(scope):
            self._trust(actor, target, 1, topic)
            self._trust(target, actor, 1, topic)
            actor.change("Authority", 1)
            self.log.append(f"第{self.day}日：{actor.name}与{target.name}把“{topic}”变成一次共同任务，避免空谈立场。")
            return target

        options = [
            IncidentOption(
                operation="confide",
                label=f"{actor.name}向{target.name}倾诉：{topic}",
                resolve=confide_res,
                base_score=lambda s: (64 if vulnerable else 45) + self.trust(actor, target) * 4,
                reason=f"公开自身风险换取对方回应；当前信任={self.trust(actor, target)}",
                response_mode=ResponseMode.COOPERATE,
            ),
            IncidentOption(
                operation="challenge",
                label=f"{actor.name}就“{topic}”当面质询{target.name}",
                resolve=challenge_res,
                base_score=lambda s: (66 if tension else 42) + self.trust(actor, target) * 4,
                reason=f"承受关系损耗以取得可证伪问题；当前信任={self.trust(actor, target)}",
                response_mode=ResponseMode.RESTRAIN,
            ),
            IncidentOption(
                operation="collaborate",
                label=f"{actor.name}与{target.name}就“{topic}”商定协作",
                resolve=collaborate_res,
                base_score=lambda s: (62 if not tension else 50) + self.trust(actor, target) * 4,
                reason=f"用共同劳动代替口头站队；当前信任={self.trust(actor, target)}",
                response_mode=ResponseMode.COOPERATE,
            ),
        ]

        template = IncidentTemplate("人际沟通", slots={}, options=options)
        intents = self.dispatcher.build_intents(template, {"actor": actor, "target": target})
        self._choose_incident(intents, scene, (actor, target))

    def _support(self, actor, source, hypothesis_key, amount=1, contradiction=False):
        hypothesis = self.hypotheses[hypothesis_key]
        knowledge = Knowledge(
            f"{actor.name}关于{hypothesis.name}的{'反证' if contradiction else '支持'}分析",
            actor.souls[0],
            ("contradicts" if contradiction else "supports", source.name, hypothesis_key),
            subject=hypothesis,
            source=source,
            tags={"Analysis": 1, "Name": 2, "Law": 2},
        )
        hypothesis.change("Contradiction" if contradiction else "Support", amount)
        hypothesis.relate("Evidence", source, amount, source=knowledge)
        actor.souls[0].relate("Considers", hypothesis, amount, source=knowledge)
        self.analyses.append(knowledge)
        return knowledge

    def _special(self):
        """触发特定日的剧情情境事件（由条件驱动）。"""
        day = self.day
        if day == 1:
            scene = self.scenes["中央厅"]
            opt = IncidentOption(
                operation="publish_true_offer",
                label="恶魔以三组真话提出潜在合作",
                resolve=self._make_day1_resolver(),
                base_score=100,
                reason="三组命题都为真，且选择性公开能把恶魔放到危险但有用的潜在盟友位置",
                response_mode=ResponseMode.NEGOTIATE,
            )
            tmpl = IncidentTemplate("恶魔首日真话提议", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": self.demon, "target": self.resources})
            self._choose_incident(intents, scene, tuple(self.characters.values()))

        elif day == 4:
            lawyer, witness = self.characters["律师"], self.characters["侦探"]
            scene = self.scenes["契约审议室"]
            options = [
                IncidentOption("establish_rule", "建立证据指控规则", self._make_day4_rule_resolver(lawyer, witness, scene), base_score=82, reason="侦探见证、文本公开且所有人保留质疑权"),
                IncidentOption("issue_order", "单方面禁止公开怀疑", lambda s: Entity("律师单方面发布的禁言命令", scene, {"RejectedProposal": 1}), base_score=48, reason="立即压住争吵，但没有授权来源和失效条件"),
            ]
            tmpl = IncidentTemplate("调查守则设立", slots={}, options=options)
            intents = self.dispatcher.build_intents(tmpl, {"actor": lawyer, "target": self.world})
            self._choose_incident(intents, scene, tuple(self.characters.values()))

        elif day == 5:
            archaeologist = self.characters["考古学家"]
            scene = self.scenes["教学图书馆"]
            opt = IncidentOption(
                operation="verify_demon_statements",
                label="用《神秘学导论》校验恶魔首日广播",
                resolve=self._make_day5_verify_resolver(archaeologist),
                base_score=96,
                reason="教材来源独立于恶魔，并分别支持最低生存契约、真话限制与逃岛能力",
                response_mode=ResponseMode.VERIFY,
            )
            tmpl = IncidentTemplate("导论文献校验", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": archaeologist, "target": self.occult_primer})
            self._choose_incident(intents, scene, tuple(self.characters.values()))

        elif day == 7:
            musician = self.characters["音乐家"]
            scene = self.scenes["共鸣厅"]
            opt = IncidentOption(
                operation="test_word",
                label="第一次言灵实验：失败记录",
                resolve=self._make_day7_attempt_resolver(musician, scene),
                base_score=90,
                reason="先允许失败暴露缺失槽位，避免把偶然现象误认成成功",
            )
            tmpl = IncidentTemplate("初次回声实验", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": musician, "target": self.threat})
            self._choose_incident(intents, scene, (musician, self.characters["诗人"], self.characters["舞蹈艺术家"]))

        elif day == 9:
            scene = self.scenes["契约审议室"]
            opt = IncidentOption(
                operation="translate_binding_term",
                label="恶魔公开关键否定词",
                resolve=self._make_day9_translate_resolver(scene),
                base_score=95,
                reason="广播语义若继续损坏，恶魔也将失去唯一合法发言媒介",
            )
            tmpl = IncidentTemplate("否定词铜版公开", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": self.demon, "target": self.sources["demon_clause"]})
            self._choose_incident(intents, scene, (self.characters["律师"], self.characters["考古学家"], self.characters["诗人"]))

        elif day == 13:
            chess = self.characters["职业棋手"]
            scene = self.scenes["星盘讲堂"]
            opt = IncidentOption(
                operation="compare_almanacs",
                label="比较三本历书的误差模式",
                resolve=lambda s: (self._support(chess, self.sources["almanac"], "lunar", 3), self.hypotheses["machine"].change("Contradiction", 1), chess.change("Authority", 2), self.log.append("第13日：棋手把三本历书当成三盘互相矛盾的棋谱，算出仪表漂移会在满月重复，而非随机磨损。"))[0],
                base_score=92,
                reason="三份错误若以固定周期偏移，误差本身就是证据",
            )
            tmpl = IncidentTemplate("历书误差比较", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": chess, "target": self.hypotheses["lunar"]})
            self._choose_incident(intents, scene, (chess, self.characters["考古学家"], self.characters["建筑师"]))

        elif day == 14:
            detective = self.player
            scene = self.scenes["上水库与水务站"]
            opt = IncidentOption(
                operation="inspect_anomaly",
                label="封存无人触碰的水压纸带",
                resolve=lambda s: (self.threat.tags.pop("Dormant", None), self.threat.change("Trace", 1), self._support(detective, self.sources["gauge"], "machine", 2), self._support(detective, self.sources["gauge"], "demon", 1, contradiction=True), self.log.append("第14日：水压归零时封条完整，恶魔广播也未经过水务线；侦探保留机械故障假说，同时削弱恶魔破坏说。"))[2],
                base_score=94,
                reason="先记录封条、时刻与管线状态，不因最方便的嫌疑人跳过现场",
            )
            tmpl = IncidentTemplate("水压纸带封存", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": detective, "target": self.sources["gauge"]})
            self._choose_incident(intents, scene, (detective, self.characters["机械工程师"], self.characters["化学家"]))

        elif day == 15:
            scene = self.scenes["生活院"]
            opt = IncidentOption(
                operation="announce_external_anomaly",
                label="恶魔中断广播并标出伪造声源",
                resolve=lambda s: (self.threat.change("Manifested", 1), self.gray_tide.change("Pressure", 1), self.resources.change("WaterDays", -2), self.sources["phonograph"].reparent(scene), self._support(self.characters["音乐家"], self.sources["phonograph"], "lunar", 2), self.log.append("第15日：满月时，旧传声管依次模仿四名学员求救，并用他们的声音命令水阀关闭；没有人受伤，饮水储备损失两日。"), self.threat)[-1],
                base_score=100,
                reason="伪声正在占用它赖以履约的广播层；揭露威胁符合双方利益",
            )
            tmpl = IncidentTemplate("伪声显现", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": self.demon, "target": self.threat})
            self._choose_incident(intents, scene, tuple(self.characters.values()))

        elif day == 16:
            scene = self.scenes["中央厅"]
            evidence = (
                ("侦探", "lunar", self.sources["phonograph"], 78, False, "反向录音与满月时刻一致"),
                ("化学家", "machine", self.sources["gauge"], 65, False, "压力变化仍可能来自阀门故障"),
                ("职业棋手", "demon", self.sources["demon_clause"], 74, True, "恶魔若主动切断广播会同时损害自身合法行动"),
            )
            candidates = []
            for role, hyp, src, score, cont, reason in evidence:
                actor = self.characters[role]
                def make_res(act=actor, h=hyp, s=src, c=cont):
                    def res(scope):
                        r = self._support(act, s, h, 2, c)
                        act.change("Authority", 1)
                        return r
                    return res
                opt = IncidentOption(
                    operation="analyze_evidence",
                    label=f"{actor.name}提交可反驳的异常解释",
                    resolve=make_res(),
                    base_score=score + actor.tag("Authority"),
                    reason=reason,
                )
                tmpl = IncidentTemplate("假说分析", slots={}, options=[opt])
                candidates.extend(self.dispatcher.build_intents(tmpl, {"actor": actor, "target": self.hypotheses[hyp]}))
            self._choose_incident(candidates, scene, tuple(self.characters[r] for r, *_ in evidence))

        elif day == 17:
            scene = self.scenes["契约审议室"]
            shooter, detective, lawyer = self.characters["神射手"], self.player, self.characters["律师"]
            options = [
                IncidentOption("accuse_without_evidence", "要求立刻拘束恶魔", lambda s: (self._trust(shooter, self.demon, -2, self.threat), self.demon)[-1], base_score=96, reason="恐惧给出高冲动效用，但违反第4日共同规则"),
                IncidentOption("present_evidence", "提交月相反馈证据链", lambda s: (self._support(detective, self.sources["phonograph"], "lunar", 2), detective.change("Authority", 2), self.log.append("第17日：侦探没有替恶魔作人格担保，只证明现有材料更支持月相反馈；神射手的拘束主张因没有载体被共同规则挡下。"))[0], base_score=84, reason="蜡筒、历书和水压纸带可以被其他人复核"),
                IncidentOption("argue_exculpation", "限定恶魔嫌疑的适用范围", lambda s: (self.hypotheses["demon"].change("Contradiction", 1), lawyer.change("Authority", 1), self.hypotheses["demon"])[-1], base_score=76, reason="恶魔并非无罪，只是当前破坏假说与其利益冲突"),
            ]
            candidates = [
                *self.dispatcher.build_intents(IncidentTemplate("拘束主张", slots={}, options=[options[0]]), {"actor": shooter, "target": self.demon}),
                *self.dispatcher.build_intents(IncidentTemplate("证据链", slots={}, options=[options[1]]), {"actor": detective, "target": self.hypotheses["lunar"]}),
                *self.dispatcher.build_intents(IncidentTemplate("适用辩护", slots={}, options=[options[2]]), {"actor": lawyer, "target": self.hypotheses["demon"]}),
            ]
            self._choose_incident(candidates, scene, tuple(self.characters.values()))

        elif day == 18:
            musician = self.characters["音乐家"]
            scene = self.scenes["共鸣厅"]
            self.env.assert_fact(E("lunar_window", self.threat))
            opt = IncidentOption(
                operation="invoke_word",
                label="言灵：回声标定",
                resolve=self._make_day18_echo_resolver(musician, scene),
                base_score=110,
                reason="失败记录补齐槽位；亏凸月仍满足观测窗；四方投入可追溯",
            )
            tmpl = IncidentTemplate("回声标定言灵", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": musician, "target": self.threat})
            self._choose_incident(intents, scene, (musician, self.characters["诗人"], self.characters["律师"], self.demon))

        elif day == 19:
            lawyer, witness = self.characters["律师"], self.characters["职业棋手"]
            scene = self.scenes["契约审议室"]
            opt = IncidentOption(
                operation="establish_rule",
                label="设立临时月相作业规程",
                resolve=self._make_day19_charter_resolver(lawyer, witness, scene),
                base_score=105,
                reason="证据链、作用对象、月相窗口、见证者和失效复审日均明确",
            )
            tmpl = IncidentTemplate("月相规程设立", slots={}, options=[opt])
            intents = self.dispatcher.build_intents(tmpl, {"actor": lawyer, "target": self.sources["clock"]})
            self._choose_incident(intents, scene, tuple(self.characters.values()))

        elif day == 20:
            if self.death_discovery is not None:
                self._special_20_death()
            else:
                self._special_20_normal()

    def _make_day1_resolver(self):
        def resolve(scope):
            offer = Entity(
                "恶魔的首日潜在盟友提议", self.world, {"Proposal": 1, "Provisional": 1, "Active": 1},
                properties={"strategy": "只公开有利于建立盟友位置的真话；省略封闭原因和自身目标", "accepted": False},
            )
            offer.relate("OfferedBy", self.demon)
            for student in self.characters.values():
                offer.relate("OfferedTo", student)
                for statement in self.demon_opening_statements:
                    self._give_knowledge(student.souls[0], statement)
            for statement in self.demon_opening_statements:
                offer.relate("BasedOnStatement", statement)
            self.provisional_alliance = offer
            self.log.append(
                "第1日：恶魔只公开三组真话：学园已经封闭且它负有最低生存契约；"
                "恶魔说出口的话不得为假；它能够协助学员逃离荒岛。它没有解释封闭原因。"
            )
            return offer
        return resolve

    def _make_day4_rule_resolver(self, lawyer, witness, scene):
        def resolve(scope):
            directives = tuple(
                Directive(Mode.FORBID, F("accuse_without_evidence", actor, self.demon))
                for actor in self.characters.values()
            )
            rule = Rule(E("true"), directives, "公开指控必须附带可检查材料")
            carrier = InformationCarrier("第一版共同调查守则", "Manuscript", scene, tags={"Law": 2, "Guarantee": 2})
            publication = RulePublication(rule, lawyer, carrier=carrier, scope=self.world, version=1)
            publication.relate("Witness", witness)
            patch = RulePatch(publication.name, rule, scene, publication, self.world, priority=3)
            patch.relate("Witness", witness)
            self.runtime.patches.append(patch)
            self.rule_patches.append(patch)
            for student in self.characters.values():
                soul = student.souls[0]
                soul.receive(publication, 3)
                soul.understand(rule)
                soul.accept(rule)
            lawyer.change("Authority", 2)
            witness.change("Authority", 1)
            self.evidence_rule = patch
            self.log.append("第4日：全员接受第一条共同规则：怀疑可以公开，定罪式指控必须交出材料。")
            return patch
        return resolve

    def _make_day5_verify_resolver(self, archaeologist):
        def resolve(scope):
            self.occult_primer.tags.pop("Hidden", None)
            claims = tuple(facet.value for facet in self.occult_primer.entries("Claim"))
            for student in self.characters.values():
                student.souls[0].learn(
                    self.demon, claims, source=self.occult_primer, confidence=3, stage="accepted",
                    name=f"{student.name}由《神秘学导论》确认的恶魔契约知识",
                )
            for statement in self.demon_opening_statements:
                statement.relate("VerifiedBy", self.occult_primer)
            archaeologist.change("Authority", 1)
            self.log.append("第5日：《神秘学导论》独立确认首日三组广播全部为真；众人同时确认真话可以被恶意裁剪，验证没有自动转化为信任。")
            return self.occult_primer
        return resolve

    def _make_day7_attempt_resolver(self, musician, scene):
        def resolve(scope):
            result = self.words.invoke(
                self.echo_word, musician.souls[0],
                {"resonator": self.sources["resonator"]}, target=self.threat,
                context={"actor": musician, "world": self.world},
            )
            failure = self.words.last_failure
            record = Entity("第一次回声标定试验记录", scene, {"Experiment": 1, "Failed": int(result is None)}, properties={"failure": failure})
            self.word_attempts.append(record)
            self.projects["resonance"].change("Evidence", 2)
            musician.change("Authority", 1)
            self.log.append(f"第7日：言灵没有发动。音乐家坚持把“{failure}”逐项写进记录，没有用气氛掩盖失败。")
            return record
        return resolve

    def _make_day9_translate_resolver(self, scene):
        def resolve(scope):
            self.sources["demon_clause"].reparent(scene)
            self.sources["demon_clause"].relate("ProvidedBy", self.demon)
            self.projects["lexicon"].change("Progress", 3)
            self.demon.change("Reliability", 2)
            self._support(self.characters["律师"], self.sources["demon_clause"], "demon", 1, contradiction=True)
            self.log.append("第9日：恶魔补出导师笔记里被刮掉的否定词，并允许律师把它写成可追责的公开译本。")
            return self.sources["demon_clause"]
        return resolve

    def _make_day18_echo_resolver(self, musician, scene):
        def resolve(scope):
            rite = self.words.invoke(
                self.echo_word, musician.souls[0],
                {"resonator": self.sources["resonator"], "almanac": self.sources["almanac"], "negative": self.sources["demon_clause"]},
                target=self.threat, context={"actor": musician, "holder": scene, "world": self.world},
            )
            if rite is None:
                raise RuntimeError(f"回声标定未成立：{self.words.last_failure}")
            self.threat.change("Exposed", 1)
            self.hypotheses["lunar"].change("Support", 3)
            musician.change("Authority", 2)
            rite.relate("Contributor", self.characters["诗人"])
            rite.relate("Contributor", self.characters["律师"])
            rite.relate("Contributor", self.demon)
            self.log.append("第18日：音乐家施行真实言灵“回声标定”。诗人守拍，律师核词，恶魔提供否定发音；伪声显出来自月钟而非核心。")
            return rite
        return resolve

    def _make_day19_charter_resolver(self, lawyer, witness, scene):
        def resolve(scope):
            clock = self.sources["clock"]
            rule = Rule(E("true"), tuple(Directive(Mode.FORBID, F("operate_unlogged", actor, clock)) for actor in self.characters.values()), "望日前后三日的共鸣设备必须留档并由第二人复核")
            carrier = InformationCarrier("月相作业规程·临时版", "Manuscript", scene, tags={"Law": 3, "Guarantee": 2})
            publication = RulePublication(rule, lawyer, carrier=carrier, scope=self.world, version=2)
            publication.relate("Witness", witness)
            patch = RulePatch(publication.name, rule, scene, publication, self.world, priority=4)
            patch.relate("BasedOn", self.sources["almanac"])
            patch.relate("BasedOn", self.sources["phonograph"])
            patch.relate("Witness", witness)
            self.rule_patches.append(patch)
            self.runtime.patches.append(patch)
            self.lunar_rule = patch
            lawyer.change("Authority", 2)
            witness.change("Authority", 1)
            self.log.append("第19日：临时月相规程成立。它只管望日前后三日、只管共鸣设备，并写明第30日必须复审。")
            return patch
        return resolve

    def _special_20_normal(self):
        scene = self.scenes["动力总站"]
        engineer, musician, lawyer = self.characters["机械工程师"], self.characters["音乐家"], self.characters["律师"]

        opt_demon = IncidentOption(
            operation="yield_broadcast_channel", label="恶魔暂时让出广播频段",
            resolve=lambda s: (self.demon.change("Reliability", 2), self.threat.change("Suppressed", 1), self.log.append("第20日：恶魔主动让出全部广播频段，只保留契约规定的维生脉冲，使回声无法继续借它的声音扩散。"), self.threat)[-1],
            base_score=100, reason="若传声回声夺取广播，恶魔与学员都会失去维生调度",
        )
        intents_demon = self.dispatcher.build_intents(IncidentTemplate("恶魔让频", slots={}, options=[opt_demon]), {"actor": self.demon, "target": self.threat})
        self._choose_incident(intents_demon, scene, (self.demon, engineer, musician, lawyer))

        def contain_res(scope):
            if not (self.threat.has("Exposed") and self.lunar_rule and self.provisional_alliance and self.projects["containment"].tag("Progress") >= 2):
                raise RuntimeError("共同隔离缺少言灵揭示、月相规则或临时协定")
            rite = Entity("第一次共同防卫：传声回声隔离", scene, {"Rite": 1, "Completed": 1})
            for contributor in (engineer, musician, lawyer, self.demon):
                rite.relate("Contributor", contributor)
            rite.relate("Targets", self.threat)
            rite.relate("UsesRule", self.lunar_rule)
            self.threat.change("Contained", 1)
            self.gray_tide.change("Pressure", 1)
            self.resources.change("PowerDays", -1)
            engineer.change("Authority", 2)
            musician.change("Authority", 1)
            self.arc_complete = True
            self.log.append("第20日：工程师切断旧管，音乐家持续标定，律师守住临时规则，恶魔维持广播脉冲。威胁被隔离，没有人把它误写成最终胜利。")
            return rite

        opt_contain = IncidentOption(
            operation="joint_containment", label="完成第一次共同防卫", resolve=contain_res,
            base_score=120, reason="四种不同能力形成互锁；任何一方单独行动都只能暂时压制回声",
        )
        intents_contain = self.dispatcher.build_intents(IncidentTemplate("共同防卫", slots={}, options=[opt_contain]), {"actor": engineer, "target": self.threat})
        self._choose_incident(intents_contain, scene, (engineer, musician, lawyer, self.demon))

    def _special_20_death(self):
        victim = self._fatal_victim()
        scene = self.scenes["契约审议室"]
        lawyer = self._alive_role("律师", "侦探")
        witness = self._alive_role("职业棋手", "建筑师", "神射手")

        def establish_safety(scope):
            directives = tuple(
                Directive(Mode.FORBID, F("operate_high_risk_alone", actor, self.world))
                for actor in self.characters.values() if not actor.has("Dead")
            ) + tuple(
                Directive(Mode.FORBID, F("private_suggestion", self.demon, actor))
                for actor in self.characters.values() if not actor.has("Dead")
            )
            rule = Rule(E("true"), directives, "死亡调查期间禁止独自操作高危机关或与恶魔秘密会谈")
            carrier = InformationCarrier("死亡调查临时安全规则", "Manuscript", scene, tags={"Law": 3, "Guarantee": 2})
            publication = RulePublication(rule, lawyer, carrier=carrier, scope=self.world, version=3)
            publication.relate("Witness", witness)
            patch = RulePatch(publication.name, rule, scene, publication, self.world, priority=5)
            patch.relate("BasedOn", self.death_discovery)
            patch.relate("Witness", witness)
            self.runtime.patches.append(patch)
            self.rule_patches.append(patch)
            self.pair_maintenance_rule = patch
            for student in self.characters.values():
                if student.has("Dead"):
                    continue
                soul = student.souls[0]
                soul.receive(publication, 3)
                soul.understand(rule)
                soul.accept(rule)
            return patch

        opt_rule = IncidentOption(
            operation="establish_rule", label="建立死亡调查期间的结伴与公开谈话规则", resolve=establish_safety,
            base_score=110, reason="规则只约束可观察的独处和高危操作，不把尚未证明的诱导写成既成事实", response_mode=ResponseMode.PROTECT,
        )
        intents_rule = self.dispatcher.build_intents(IncidentTemplate("安全规则", slots={}, options=[opt_rule]), {"actor": lawyer, "target": self.world})
        self._choose_incident(intents_rule, scene, tuple(self.characters.values()))

        def testify(scope):
            claims = (E("spoke_privately_with", self.demon, victim), E("did_not_directly_control", self.demon, victim), E("did_not_directly_kill", self.demon, victim))
            for claim in claims:
                self.env.assert_fact(claim)
            carrier = InformationCarrier("恶魔关于死者的选择性证言", "Testimony", scene, creator=self.demon, claims=claims, tags={"Truth": 1, "Incomplete": 1, "Law": 1})
            carrier.relate("RefusedToExplain", self.demon)
            for student in self.characters.values():
                if student.has("Dead"):
                    continue
                student.souls[0].learn(self.demon, claims, source=carrier, confidence=3, stage="accepted", name=f"{student.name}接收的恶魔死亡证言")
                self._trust(student, self.demon, -1, carrier)
            self.log.append("第20日：恶魔承认曾与死者独处，并如实否认直接控制和直接杀人；它拒绝说明是否调整过情绪。真话增加了事实，也扩大了敌意。")
            return carrier

        opt_testify = IncidentOption(
            operation="provide_partial_true_testimony", label="恶魔提供真实但不完整的死亡证言", resolve=testify,
            base_score=100, reason="真话契约迫使已说出的三项命题为真；沉默保留其对情绪调整的隐瞒", response_mode=ResponseMode.CONCEAL,
        )
        intents_testify = self.dispatcher.build_intents(IncidentTemplate("恶魔死亡证言", slots={}, options=[opt_testify]), {"actor": self.demon, "target": victim})
        event = self._choose_incident(intents_testify, scene, tuple(self.characters.values()))
        for student in self.characters.values():
            if student.has("Dead"):
                continue
            self._add_social_impact(student, self.demon, event, {"bond": 0, "malicious_intent": True, "threat": 2, "norm_breach": True})
        self.projects["containment"].change("PausedByDeath", 1)
        self.threat.change("Uncontained", 1)
        self.arc_complete = False

    def _fatal_victim(self):
        if not self.demon_fatal_actions:
            return None
        targets = self.demon_fatal_actions[-1].related("Target")
        return targets[0].value if targets else None

    def _alive_role(self, *roles):
        return next(
            self.characters[role]
            for role in roles
            if not self.characters[role].has("Dead")
        )

    def _record_daily_action(self, actor, slot, operation, label, scene, target=None, event=None):
        phase = "daylight" if slot < 6 else "night"
        action = Entity(
            f"第{self.day}日·{actor.name}·{slot + 1}·{label}",
            self.world,
            {"DailyAction": 1, phase.title(): 1},
            properties={
                "day": self.day,
                "slot": slot,
                "phase": phase,
                "start_av": (self.day - 1) * 200 + slot * 20,
                "end_av": (self.day - 1) * 200 + (slot + 1) * 20,
                "cost": 20,
                "operation": operation,
                "label": label,
                "scene": scene,
            },
        )
        action.relate("Actor", actor)
        action.relate("OccurredAt", scene)
        if isinstance(target, Entity):
            action.relate("Target", target)
        if isinstance(event, Entity):
            action.relate("RealizedBy", event)
            event.relate("ScheduledAs", action)
        self.daily_actions.append(action)
        return action

    def _repair_target_for(self, actor, project):
        quarters = actor.get("quarters")
        if isinstance(quarters, Entity) and quarters.tag("Damage") > 0:
            return quarters
        home = actor.get("home")
        if isinstance(home, Entity) and home.tag("Damage") > 0:
            return home
        workplace = actor.get("workplace")
        if isinstance(workplace, Entity) and workplace.tag("Damage") > 0:
            return workplace
        project_scene = project.get("scene")
        if project_scene.tag("Damage") > 0:
            return project_scene
        return max(self.damaged_scenes, key=lambda s: s.tag("Damage"))

    @staticmethod
    def _repair_scene(scene):
        effort = scene.get("repair_effort", 0) + 1
        scene.set("repair_effort", effort)
        if effort % 4 == 0 and scene.tag("Damage") > 0:
            scene.change("Damage", -1)
            scene.change("Safety", 1)

    def _support_project_activity(self, actor, project):
        project.change("Labor", 1)
        if project.tag("Labor") % 8 == 0:
            project.change("Progress", 1)
            project.relate("Contributor", actor, source=self.day)

    def _schedule_daily_actions(self, project_key):
        project = self.projects[project_key]
        day_events = tuple(e for e in self.runtime.events if e.get("day") == self.day)
        participation = {actor: [] for actor in self.characters.values() if not actor.has("Dead")}

        for event in day_events:
            actor, effect = event.get("actor"), event.get("effect")
            if actor in participation:
                participation[actor].append((event, effect.op, event.name, event.get("scene")))
            if effect and effect.op in {"confide", "challenge", "collaborate"}:
                target = effect.args[1]
                if target in participation:
                    participation[target].append(
                        (event, "participate_in_conversation", f"回应{actor.name}关于当天问题的谈话", event.get("scene"))
                    )

        for role, actor in self.characters.items():
            if actor.has("Dead"):
                continue
            main = participation[actor]
            repair_scene = self._repair_target_for(actor, project)
            primary_scene = main[0][3] if main else project.get("scene")

            self._record_daily_action(
                actor, 0, "morning_life",
                "检查睡处、洗漱并处理第一餐" if self.day == 1 else "起床、洗漱并处理早餐",
                actor.get("home"), actor.get("quarters"),
            )
            self._record_daily_action(
                actor, 1, "move_or_prepare", "前往当天活动区域并准备工具",
                primary_scene, primary_scene,
            )

            for slot in (2, 3):
                if main:
                    event, operation, label, sc = main.pop(0)
                    self._record_daily_action(actor, slot, operation, label, sc, event.get("outcome"), event)
                elif slot == 2:
                    self._support_project_activity(actor, project)
                    self._record_daily_action(
                        actor, slot, "support_project",
                        f"为《{project.name}》搬运、清点或完成辅助工作",
                        project.get("scene"), project,
                    )
                else:
                    self._repair_scene(repair_scene)
                    loc = repair_scene if repair_scene.has("Scene") else repair_scene.parent
                    self._record_daily_action(
                        actor, slot, "repair_damage",
                        f"清障、封漏并修复{repair_scene.name}的生活功能",
                        loc, repair_scene,
                    )

            if main:
                event, operation, label, sc = main.pop(0)
                self._record_daily_action(actor, 4, operation, label, sc, event.get("outcome"), event)
            else:
                self._record_daily_action(
                    actor, 4, "communal_life", "取餐、清洁公共用品并交换当天见闻",
                    self.scenes["后勤院"], self.resources,
                )

            if self.death_discovery is not None and self.day == 20:
                coping = {
                    "侦探": "反复整理时间线，无法真正休息",
                    "诗人": "把原定娱乐改成简短悼念",
                    "音乐家": "留在有人能看见的地方调音",
                    "博物学家": "整理死者遗留物并拒绝独处",
                    "机械工程师": "检查寝室门锁与紧急照明",
                    "主厨": "强迫仍在发抖的人吃下热食",
                    "考古学家": "整理死者留下的资料",
                    "神偷": "检查出入口后回到公共区域",
                    "律师": "誊写证言并标出尚未证实的部分",
                    "职业棋手": "与同伴复盘可能的嫌疑链",
                    "化学家": "在他人见证下清洗检验器材",
                    "神射手": "与同伴轮换警戒而不再单独巡逻",
                    "舞蹈艺术家": "组织低强度集体活动稳定呼吸",
                    "画家": "在公共区域记录死者最后的物品",
                    "建筑师": "重新标出禁止单独进入的路线",
                    "临床医生": "陪伴恐慌者并登记失眠情况",
                }[role]
                self._record_daily_action(
                    actor, 5, "cope_with_death", coping,
                    self.scenes["公共娱乐室"], self.death_discovery,
                )
            else:
                self._record_daily_action(
                    actor, 5, "leisure", LEISURE[role],
                    actor.get("workplace"), actor,
                )

            for slot in range(6, 10):
                if self.death_discovery is not None and self.day == 20 and slot == 6 and actor.tag("Panic") >= 3:
                    actor.change("Fatigue", 1)
                    self._record_daily_action(
                        actor, slot, "insomnia", "因恐慌无法入睡，留在寝室等待下一次点名",
                        actor.get("home"), actor.get("quarters"),
                    )
                    continue
                self._record_daily_action(
                    actor, slot, "sleep", "依夜禁在住处连续睡眠",
                    actor.get("home"), actor.get("quarters"),
                )

            social_today = any(
                e.get("effect") and e.get("effect").op in {"confide", "challenge", "collaborate"}
                and (e.get("actor") is actor or e.get("effect").args[1] is actor)
                for e in day_events
            )
            actor.set("solitary_days", 0 if social_today else actor.get("solitary_days", 0) + 1)

        expected = sum(not a.has("Dead") for a in self.characters.values()) * 10
        actual = sum(a.get("day") == self.day for a in self.daily_actions)
        if actual != expected:
            raise RuntimeError(f"第{self.day}日行动账不完整：{actual}/{expected}")

    def _private_demon_attack(self):
        if self.demon_fatal_actions or not (self.demon_attack_window[0] <= self.day <= self.demon_attack_window[1]):
            return None
        if self.lethal_mechanism.get("disabled"):
            return None
        next_spec = DAY_SPECS[self.day] if self.day < len(DAY_SPECS) else None
        publicly_expected_roles = set(next_spec[4]) if next_spec else set()
        if next_spec:
            publicly_expected_roles.update(r for r in next_spec[5][:2] if r != "恶魔")
        candidates = tuple(
            a for a in self.characters.values()
            if not a.has("Dead") and a.tag("Occult") < 3 and a.get("solitary_days", 0) >= 5
            and a.get("role") not in publicly_expected_roles
        )
        if not candidates:
            return None

        def exposure(actor):
            duty = 2 if actor.get("role") in {"机械工程师", "建筑师", "主厨", "临床医生"} else 0
            ties = sum(f.strength for f in actor.related("Trusts"))
            return actor.get("solitary_days", 0) * 2 + actor.tag("Panic") + (3 - actor.tag("Occult")) * 2 + duty - ties

        target = max(candidates, key=exposure)
        scene = self.lethal_mechanism.parent

        def suggest(scope):
            claims = (E("connected_to_exit_service_tunnel", self.lethal_mechanism), E("restores_waste_and_water_pressure", self.lethal_mechanism), E("delay_can_harm_occupied_areas", self.lethal_mechanism))
            for claim in claims:
                self.env.assert_fact(claim)
            knowledge = target.souls[0].learn(
                self.lethal_mechanism, claims, source=self.demon, confidence=3, stage="accepted",
                name=f"{target.name}从恶魔处得到的三段局部真相",
            )
            Influence(f"{target.name}被压低的求助冲动", "Serenity", 3, target.souls[0], target=target, source=self.demon, mode="hidden_suggestion", persistence=1, started_at=self.timeline.now, expires_at=self.timeline.now + 20)
            Influence(f"{target.name}被放大的立即处理冲动", "Hope", 3, target.souls[0], target=self.lethal_mechanism, source=self.demon, mode="hidden_suggestion", persistence=1, started_at=self.timeline.now, expires_at=self.timeline.now + 20)
            self.log.append(f"第{self.day}日：恶魔在无人旁观时向{target.name}提供三段真实但不完整的信息，并压低其求助冲动；它没有声称机关安全。")
            return knowledge

        opt_sug = IncidentOption(
            operation="private_suggestion", label=f"恶魔私下向{target.name}选择性灌输真相", resolve=suggest,
            base_score=100, reason="目标连续独处、缺少充分恶魔知识；三项陈述都为真，但省略损坏的安全联锁", response_mode=ResponseMode.CONCEAL,
        )
        suggestion_intents = self.dispatcher.build_intents(IncidentTemplate("恶魔诱导", slots={}, options=[opt_sug]), {"actor": self.demon, "target": target})
        suggestion_event = self._choose_incident(suggestion_intents, scene, (target,))

        rescuable = (
            self.pair_maintenance_rule is not None
            and bool(self.lethal_mechanism.related("InspectedBy", self.player))
            and not self.player.has("Dead")
        )

        def trigger(scope):
            attack = Entity(
                f"第{self.day}日恶魔唯一诱导行动", self.world, {"DemonFatalAction": 1, "Hidden": 1},
                properties={"selective_truth": True, "emotion_adjustment": True, "direct_control": False, "direct_kill": False, "rescue_window_open": rescuable},
            )
            attack.relate("InstigatedBy", self.demon)
            attack.relate("Target", target)
            attack.relate("Uses", self.lethal_mechanism)
            attack.relate("BasedOn", suggestion_event)
            self.demon_fatal_actions.append(attack)
            self.lethal_mechanism.change("Triggered", 1)
            if rescuable:
                target.change("Health", -4)
                target.change("Injured", 1)
                self.lethal_mechanism.set("disabled", True)
                attack.change("Rescued", 1)
                self.log.append(f"第{self.day}日：结伴维修规则和侦探的提前检查共同破坏诱导条件；{target.name}重伤但被救下。")
            else:
                target.change("Health", -target.tag("Health"))
                target.change("Dead", 1)
                attack.change("Fatal", 1)
                self.log.append(f"第{self.day}日：{target.name}独自触发损坏的配重焚化闸。现场只呈现为修复事故，旁人没有看见恶魔的暗示过程。")
            return attack

        opt_trig = IncidentOption(
            operation="trigger_lethal_mechanism", label=f"{target.name}独自复位旧式配重焚化闸", resolve=trigger,
            base_score=100, reason="主体相信不立即处理会伤及他人，却不知道安全联锁已经损坏", response_mode=ResponseMode.PROTECT,
        )
        fatal_intents = self.dispatcher.build_intents(IncidentTemplate("机关触发", slots={}, options=[opt_trig]), {"actor": target, "target": self.lethal_mechanism})
        fatal_event = self._choose_incident(fatal_intents, scene, (target,))

        activity = next(a for a in self.daily_actions if a.get("day") == self.day and a.get("slot") == 5 and a.related("Actor", target))
        activity.set("operation", "follow_private_clue")
        activity.set("label", f"独自前往{scene.name}处理恶魔指出的真实风险")
        activity.relate("RealizedBy", suggestion_event)
        activity.relate("RealizedBy", fatal_event)
        if target.has("Dead"):
            for sleep in self.daily_actions:
                if sleep.get("day") == self.day and sleep.get("slot", 0) >= 6 and sleep.related("Actor", target):
                    sleep.change("Cancelled", 1)
                    sleep.set("cost", 0)
                    sleep.set("operation", "death_prevents_sleep")
        return fatal_event

    def _discover_death(self):
        victim = self._fatal_victim()
        if victim is None or self.death_discovery is not None:
            return self.death_discovery
        scene = self.lethal_mechanism.parent
        discoverer = self._alive_role("主厨", "诗人", "舞蹈艺术家")
        doctor = self._alive_role("临床医生", "博物学家")
        investigator = self._alive_role("侦探", "律师")
        engineer = self._alive_role("机械工程师", "建筑师")

        def discover(scope):
            evidence = Entity(
                f"{victim.name}死亡现场的第一轮记录", scene, {"Evidence": 1, "DeathEvidence": 1, "Public": 1},
                properties={"victim": victim, "mechanism": self.lethal_mechanism, "confirmed": ("死亡", "机关已经触发", "死者曾独自进入"), "not_confirmed": ("恶魔是否诱导", "是否存在第三方", "死者当时的完整动机")},
            )
            evidence.relate("BodyOf", victim)
            evidence.relate("FoundAt", scene)
            evidence.relate("Records", self.lethal_mechanism)
            self.death_evidence.append(evidence)
            for role, student in self.characters.items():
                if student.has("Dead"):
                    continue
                fear = 2 if student.tag("Occult") >= 3 or role in {"神射手", "诗人"} else 1
                student.change("Panic", fear)
                Influence(f"{student.name}面对首名死者的恐慌", "Fear", fear, student.souls[0], target=victim, source=evidence, mode="death_shock", persistence=2, started_at=self.timeline.now, expires_at=self.timeline.now + 400)
                student.souls[0].learn(
                    victim, (E("dead", victim), E("found_at", victim, scene), E("lethal_mechanism_triggered", self.lethal_mechanism, victim)),
                    source=evidence, confidence=3, stage="accepted", name=f"{student.name}得知的死亡事实",
                )
                if student.tag("Occult") >= 3:
                    student.souls[0].learn(self.demon, (E("could_have_induced", self.demon, victim),), source=evidence, confidence=2, stage="understood", name=f"{student.name}对恶魔间接诱导的高警戒判断")
                    self._trust(student, self.demon, -2, evidence)
                elif student.tag("Occult") == 2 or role in {"神射手", "诗人"}:
                    student.souls[0].learn(self.demon, (E("possibly_involved", self.demon, victim),), source=evidence, confidence=1, stage="observed", name=f"{student.name}对恶魔介入可能性的怀疑")
                    self._trust(student, self.demon, -1, evidence)
            self.log.append(f"第20日清晨：{discoverer.name}发现{victim.name}没有返回，搜索者在废物处理站确认死亡。全员只知道机关致死和独处事实，尚不知道诱导过程。")
            return evidence

        opt_disc = IncidentOption(
            operation="discover_death", label=f"发现{victim.name}失踪并确认死亡现场", resolve=discover,
            base_score=120, reason="死者没有参加早餐、寝室睡眠记录中断，搜索最终抵达仍在运行的机关区域", response_mode=ResponseMode.VERIFY,
        )
        intents_disc = self.dispatcher.build_intents(IncidentTemplate("死亡发现", slots={}, options=[opt_disc]), {"actor": discoverer, "target": victim})
        event = self._choose_incident(intents_disc, scene, (discoverer, doctor, investigator, engineer))
        self.death_discovery = event
        for student in self.characters.values():
            if student.has("Dead") or student.tag("Occult") < 3:
                continue
            self._add_social_impact(student, self.demon, event, {"bond": 0, "malicious_intent": True, "threat": 2, "norm_breach": True})
        return event

    def _death_response(self):
        victim = self._fatal_victim()
        scene = self.lethal_mechanism.parent
        doctor = self._alive_role("临床医生", "博物学家")
        investigator = self._alive_role("侦探", "律师")
        engineer = self._alive_role("机械工程师", "建筑师")

        def examine(scope):
            evidence = Entity(f"{victim.name}的初步尸检记录", scene, {"Evidence": 1, "MedicalFinding": 1}, properties={"finding": "致命伤与配重机关一致；没有直接外力控制身体的可见痕迹"})
            evidence.relate("Subject", victim)
            self.death_evidence.append(evidence)
            doctor.change("Authority", 1)
            return evidence

        def timeline(scope):
            evidence = Entity(f"{victim.name}最后行动时间线", scene, {"Evidence": 1, "TimelineEvidence": 1}, properties={"finding": "死者临时偏离原定休闲安排，独自前往此前没有登记的机关区域", "gap": "没有人目击其改变决定的原因"})
            evidence.relate("Subject", victim)
            self.death_evidence.append(evidence)
            investigator.change("Authority", 1)
            return evidence

        def secure(scope):
            self.lethal_mechanism.set("disabled", True)
            self.lethal_mechanism.change("Secured", 1)
            engineer.change("Authority", 1)
            return self.lethal_mechanism

        for actor, op, label, res, reason, mode in (
            (doctor, "examine_death", f"检查{victim.name}的死因", examine, "区分机关伤、直接袭击和异常影响能够留下的可见痕迹", ResponseMode.VERIFY),
            (investigator, "reconstruct_death_timeline", f"重建{victim.name}最后六个行动段", timeline, "比较寝室、活动账、工具领取和机关触发时刻，不把空白自动解释成恶魔", ResponseMode.VERIFY),
            (engineer, "secure_lethal_mechanism", "断能并封锁旧式配重焚化闸", secure, "现场仍有二次伤亡风险，调查不能以继续暴露所有人为代价", ResponseMode.PROTECT),
        ):
            opt = IncidentOption(op, label, res, base_score=100, reason=reason, response_mode=mode)
            intents = self.dispatcher.build_intents(IncidentTemplate("调查", slots={}, options=[opt]), {"actor": actor, "target": victim})
            self._choose_incident(intents, scene, (actor,))

    def _death_conflict(self):
        victim = self._fatal_victim()
        scene = self.scenes["中央厅"]
        accuser = max(
            (a for a in self.characters.values() if not a.has("Dead") and a.tag("Occult") >= 3),
            key=lambda a: a.tag("Panic"),
        )
        detective = self._alive_role("侦探", "律师")

        def suspect(scope):
            hypothesis = Knowledge(
                f"{accuser.name}提出的恶魔间接诱导假说", accuser.souls[0],
                E("possibly_induced", self.demon, victim), subject=self.demon,
                source=self.death_discovery, tags={"Hypothesis": 1, "Unconfirmed": 1},
            )
            accuser.relate("Suspects", self.demon, 2, source=hypothesis)
            self.death_hypotheses["demon_inducement"] = hypothesis
            self.log.append(f"第20日：{accuser.name}依据入学前的恶魔知识主张调查间接诱导；这是一项有能力依据、但没有本案直接证据的假说。")
            return hypothesis

        opt_susp = IncidentOption(
            "raise_demon_suspicion", f"{accuser.name}公开提出恶魔间接诱导假说", suspect,
            base_score=96, reason="神秘学三级先验确认恶魔具备选择性真话和情绪调整能力；现场仍缺少直接目击", response_mode=ResponseMode.VERIFY,
        )
        intents_susp = self.dispatcher.build_intents(IncidentTemplate("怀疑提出", slots={}, options=[opt_susp]), {"actor": accuser, "target": self.demon})
        self._choose_incident(intents_susp, scene, tuple(self.characters.values()))

        def contest(scope):
            detective.relate("QuestionsAttributionOf", accuser, source=self.death_discovery)
            self._trust(accuser, detective, -1, self.death_discovery)
            self.log.append(f"第20日：{detective.name}承认恶魔具备能力，但拒绝把能力直接等同于本案实施。{accuser.name}因此认为侦探正在替恶魔拖延。")
            return self.death_hypotheses["demon_inducement"]

        opt_cont = IncidentOption(
            "challenge", f"{detective.name}要求把恶魔能力与本案证据分开", contest,
            base_score=90, reason="保护可验证调查程序，但会被高恐慌的怀疑者理解成替恶魔辩护", response_mode=ResponseMode.RESTRAIN,
        )
        intents_cont = self.dispatcher.build_intents(IncidentTemplate("辩驳", slots={}, options=[opt_cont]), {"actor": detective, "target": accuser})
        event = self._choose_incident(intents_cont, scene, (detective, accuser))
        self._add_social_impact(accuser, detective, event, {"bond": 0, "norm_breach": True})

    def _end_day(self, title, moon):
        if self.day % 4 == 0:
            self.resources.change("FoodDays", -1)
        if self.day % 5 == 0:
            self.resources.change("WaterDays", -1)
        if self.day % 6 == 0:
            self.resources.change("PowerDays", -1)
        if self.projects["food"].tag("Progress") >= 4 and self.day in (8, 16):
            self.resources.change("FoodDays", 4)
        if self.projects["water"].tag("Progress") >= 4 and self.day in (10, 20):
            self.resources.change("WaterDays", 4)
        if self.projects["lunar_clock"].tag("Progress") >= 4 and self.day in (12, 18):
            self.resources.change("PowerDays", 3)

        authorities = sorted(
            ((a.tag("Authority"), role, a.name) for role, a in self.characters.items()),
            reverse=True,
        )[:4]
        actions = tuple(a for a in self.daily_actions if a.get("day") == self.day)
        self.day_summaries.append(
            {
                "day": self.day,
                "title": title,
                "moon": moon,
                "events": sum(e.get("day") == self.day for e in self.runtime.events),
                "resources": {
                    "food": self.resources.tag("FoodDays"),
                    "water": self.resources.tag("WaterDays"),
                    "power": self.resources.tag("PowerDays"),
                },
                "leaders": tuple((role, name, score) for score, role, name in authorities),
                "gray_pressure": self.gray_tide.tag("Pressure"),
                "daily_actions": len(actions),
                "sleep_slots": sum(a.get("operation") == "sleep" for a in actions),
                "repair_slots": sum(a.get("operation") == "repair_damage" for a in actions),
                "leisure_slots": sum(a.get("operation") == "leisure" for a in actions),
                "living_students": sum(not a.has("Dead") for a in self.characters.values()),
            }
        )

    def run(self, days=20):
        if not 1 <= days <= 20:
            raise ValueError("初期篇只实现第1—20日")
        for day, title, moon, project, roles, social in DAY_SPECS[:days]:
            self.day = day
            if day == 20 and self.demon_fatal_actions:
                title = "第一具尸体与被中止的共同防卫"
            self.log.append(f"——第{day}日《{title}》；{moon}——")
            if day == 20 and self.demon_fatal_actions:
                self._discover_death()
                self._death_response()
                self._death_conflict()
                self._special()
                self._schedule_daily_actions("inquiry")
                self._end_day(title, moon)
                continue
            if day == 1:
                self._special()
            self._work(project, roles)
            self._social(*social)
            if day != 1:
                self._special()
            self._schedule_daily_actions(project)
            self._private_demon_attack()
            self._end_day(title, moon)
        return self

    def acceptance(self):
        effects = [e.get("effect").op for e in self.runtime.events]
        leaders = sum(a.tag("Authority") >= 3 for a in self.characters.values())
        trusted_pairs = sum(bool(a.related("Trusts")) for a in self.characters.values())
        completed_actions = tuple(a for a in self.daily_actions if not a.has("Cancelled"))
        return {
            "days": self.day,
            "events": len(self.runtime.events),
            "gray_pressure": self.gray_tide.tag("Pressure"),
            "rules": len(self.rule_patches),
            "analyses": len(self.analyses),
            "leaders": leaders,
            "socialized_characters": trusted_pairs,
            "demon_help": sum(
                e.get("effect").op in {
                    "publish_true_offer",
                    "translate_binding_term",
                    "announce_external_anomaly",
                    "yield_broadcast_channel",
                }
                for e in self.runtime.events if e.get("effect")
            ),
            "word_attempts": len(self.word_attempts),
            "word_success": effects.count("invoke_word"),
            "blocked_accusations": sum(bool(e.get("blocked_candidates")) for e in self.runtime.events),
            "threat_manifested": self.threat.has("Manifested"),
            "threat_contained": self.threat.has("Contained"),
            "arc_complete": self.arc_complete,
            "daily_action_records": len(self.daily_actions),
            "completed_daily_actions": len(completed_actions),
            "sleep_slots": sum(a.get("operation") == "sleep" for a in self.daily_actions),
            "repair_slots": sum(a.get("operation") == "repair_damage" for a in self.daily_actions),
            "leisure_slots": sum(a.get("operation") == "leisure" for a in self.daily_actions),
            "demon_fatal_actions": len(self.demon_fatal_actions),
            "student_deaths": sum(a.has("Dead") for a in self.characters.values()),
            "death_crisis_stabilized": bool(
                self.death_discovery
                and len(self.death_evidence) >= 3
                and self.pair_maintenance_rule
                and self.lethal_mechanism.get("disabled")
            ),
        }


__all__ = ["AcademyPrologue", "DAY_SPECS"]
