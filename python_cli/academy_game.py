"""《异常感知学园》群像叙事运行时。

本模块只实现游戏内容层。目标、承诺、请求、记忆、贡献与压力均使用核心现有
Entity、Facet、Knowledge、Intent、Event、Rule、Rite、Covenant 和对象引用表达。
"""

from __future__ import annotations

from core.python.engine import (
    ActionRuntime,
    Character,
    Directive,
    E,
    Entity,
    F,
    InformationCarrier,
    Intent,
    Knowledge,
    KotodamaSystem,
    Mode,
    Passage,
    Recipe,
    ResponseMode,
    Rule,
    RuleEnvironment,
    RulePatch,
    RulePublication,
    RuleSystem,
    SocialImpact,
    Soul,
    Timeline,
    WorldSpace,
    select_social_influence,
    social_attitude_pressure,
)

from .academy_content import CHARACTER_SPECS, CONNECTIONS, SCENE_GROUPS, STORY_SPECS

OBJECTIVE_SPECS = (
    ("文契院真名锚", 0, "文献工房", "名录库", "考古学家", "律师", "decipher"),
    ("星仪院共鸣锚", 1, "星盘讲堂", "阵法基座室", "音乐家", "诗人", "listen"),
    ("铁工院联锁锚", 3, "动力总站", "机械联锁室", "机械工程师", "建筑师", "repair"),
    ("白塔安全载体锚", 2, "手术与解剖室", "学园长隔离室", "临床医生", "主厨", "treat"),
)

RELATION_NEGATIVE = {"Distrust", "Resents", "OwesTruthTo"}
RELATION_PROTECTIVE = {
    "Protects",
    "Shelters",
    "KeepsAlive",
    "Guards",
    "SacrificedFor",
    "ReliesOn",
}
STORY_ROLES = tuple(tuple(spec["memories"]) for spec in STORY_SPECS)


class AcademyGame:
    """由实际对象承诺网络驱动的嵌套行动值回合制游戏。"""

    def __init__(self, autonomous=True, social_emotions=True):
        self.autonomous = autonomous
        self.social_emotions = social_emotions
        self.reset()

    def reset(self):
        self.world = WorldSpace("灰潮岛异常感知学园")
        self.env, self.rules, self.timeline = (
            RuleEnvironment(),
            RuleSystem(),
            Timeline(),
        )
        self.words = KotodamaSystem(self.env, self.rules)
        self.log = ["记忆封存后的第一声钟响。恶魔通过中央厅广播宣布禁闭游戏开始。"]
        self.ended = self.won = False
        self.ending = ""
        self.round = 0
        self.history_archive = Entity("被封存的学园历史", self.world, {"Archive": 1})
        self.rule_archive = Entity("规则与契约", self.world, {"Archive": 1})
        self.team_inventory = Entity(
            "共同调查箱", self.world, {"Container": 1, "Shared": 1}
        )
        self.story_board = Entity("四院共同作战板", self.world, {"ObjectiveBoard": 1})
        self.crisis = Entity(
            "灰潮侵蚀时钟",
            self.world,
            {"Crisis": 1, "Active": 1, "Pressure": 0},
            properties={"limit": 30},
        )
        self.scenes, self.passages = {}, []
        self.characters, self.souls = {}, {}
        self.history, self.memory_carriers = [], []
        self.seal_components, self.objectives = [], []
        self.requests, self.rule_patches = [], []
        self.social_impacts = []
        self.analyses, self.false_publications, self.rule_proposals = [], [], []
        self.patch_blocks = 0
        self.accident_inquiry = None
        self.coordination = None
        self.active_final_rite = None
        self._pending_event_meta = {}
        self._decision_notes = {}
        self._demon_turn = 0
        self._demon_desired = ""
        self._build_map()
        self._build_cast()
        self._build_faculty_and_demon()
        self._build_histories()
        self._build_rules()
        self._build_kotodama()
        self._build_objectives()
        self._build_decision_trees()
        self.runtime = ActionRuntime(
            self.timeline,
            self.env,
            self.rules,
            self.words,
            event_parent=self.world,
            witnesses=self._witnesses,
            listeners=(self._observe_social_outcome,),
        )
        self.runtime.patches.extend(self.rule_patches)
        if self.autonomous:
            for index, (role, actor) in enumerate(self.characters.items()):
                if actor is not self.player:
                    self._activate(actor, self.souls[role], 70 + index)
            self._activate(self.demon, self.demon_soul, 30)
        return self

    # ---- 世界与内容对象 -------------------------------------------------

    def _build_map(self):
        restricted = {
            "名录库",
            "阵法基座室",
            "机械联锁室",
            "学园长隔离室",
            "源质反应炉",
            "核心隔离舱",
        }
        hazardous = {
            "灵体观察室",
            "污染标本库",
            "勤务环廊",
            "废物处理站",
            "四锚封印廊",
            "源质反应炉",
            "核心隔离舱",
        }
        for layer, names in SCENE_GROUPS.items():
            for name in names:
                tags = {"Scene": 1, "Discovered": 1}
                if name in restricted:
                    tags["Locked"] = 1
                if name in hazardous:
                    tags["Hazard"] = 4 if name in {"源质反应炉", "核心隔离舱"} else 2
                self.scenes[name] = Entity(
                    name, self.world, tags, properties={"layer": layer}
                )
        for index, raw in enumerate(CONNECTIONS, 1):
            left, right, cost, *gate_name = raw
            gate = self.scenes[gate_name[0]] if gate_name else None

            def condition(_from, _actor, env, gate=gate):
                return (
                    gate is None
                    or not gate.has("Locked")
                    or bool(env and env.evaluate(E("unlocked", gate)))
                )

            passage = Passage(
                f"通道{index}：{left}—{right}",
                self.scenes[left],
                self.scenes[right],
                cost,
                tags={"Restricted": 1} if gate else None,
                condition=condition,
            )
            self.world.add_passage(passage)
            self.passages.append(passage)

    def _build_cast(self):
        aspect_profiles = {
            "侦探": {"Mind": 3, "Law": 1},
            "诗人": {"Name": 2, "Desire": 2},
            "音乐家": {"Mind": 3, "Preservation": 2},
            "博物学家": {"Preservation": 3, "Form": 1},
            "机械工程师": {"Form": 3, "Preservation": 1},
            "主厨": {"Preservation": 3, "Bond": 2},
            "考古学家": {"Vestige": 3, "Name": 1},
            "神偷": {"Threshold": 3, "Mind": 1},
            "律师": {"Law": 3, "Name": 2},
            "职业棋手": {"Mind": 4},
            "化学家": {"Transmutation": 3, "Form": 1},
            "神射手": {"Strife": 3, "Threshold": 1},
            "舞蹈艺术家": {"Form": 2, "Bond": 2},
            "画家": {"Mind": 2, "Vestige": 2},
            "建筑师": {"Form": 2, "Threshold": 2},
            "临床医生": {"Preservation": 3, "Death": 1},
        }
        for role, name, occult, home, action, operation, team_role in CHARACTER_SPECS:
            actor = Character(
                f"{name}·{role}",
                self.scenes[home],
                {"Student": 1, "Health": 6, "Panic": 1, "Occult": occult},
            )
            actor.properties.update(
                role=role,
                personal_name=name,
                home=self.scenes[home],
                profession_action=action,
                operation=operation,
                team_role=team_role,
            )
            soul = Soul(
                f"{name}的心识",
                self.world,
                {
                    "Stability": 8,
                    "Identity": 6,
                    "Law": 1,
                    "Name": 1,
                    **aspect_profiles[role],
                },
            )
            actor.bind(soul)
            self.characters[role], self.souls[role] = actor, soul
            self.env.alias(role, actor)
            self.env.alias(name, actor)
            self._set_location(actor, actor.parent, initial=True)
        self.player = self.characters["侦探"]
        self.player_soul = self.souls["侦探"]

    def _build_faculty_and_demon(self):
        mentor_specs = (
            ("鸦羽静文", "言灵与文书学导师", "契约担保者"),
            ("星见慈", "阵法与仪式学导师", "能量封印核心"),
            ("铁堂轰", "动力工程与自动化导师", "物理阻隔控制者"),
            ("御门玄理", "异象与医学导师／学园长", "事故第一目击者"),
        )
        self.mentors = [
            Entity(
                f"{name}·{discipline}",
                self.history_archive,
                {"Mentor": 1, "Vestige": 3, "Authority": 3},
                properties={
                    "discipline": discipline,
                    "duty": duty,
                    "status": "牺牲或成为封印载体",
                },
            )
            for name, discipline, duty in mentor_specs
        ]
        self.demon = Character(
            "校内代管者",
            self.scenes["核心隔离舱"],
            {"Demon": 1, "Authority": 5, "Name": 4, "Law": 4, "Desire": 4},
        )
        self.demon.designate("恶魔", 1)
        self.demon_soul = Soul(
            "代管者意志", self.world, {"Stability": 12, "Mind": 4, "Law": 4}
        )
        self.demon.bind(self.demon_soul)
        self.env.alias("恶魔", self.demon)
        self.env.assert_fact(E("confined", self.demon, self.world))
        anchors = tuple(
            self.scenes[name]
            for name in ("名录库", "阵法基座室", "机械联锁室", "学园长隔离室")
        )
        covenant = Entity(
            "禁闭游戏契约",
            self.rule_archive,
            {"Covenant": 1, "Active": 1, "DyingWord": 1},
            properties={
                "active": True,
                "parties": (self.demon, *self.mentors),
                "fired": True,
            },
        )
        forbidden = [
            F("leave", self.demon, self.world),
            F("break_seal", self.demon, self.world),
        ]
        forbidden += [
            F(op, self.demon, actor)
            for actor in self.characters.values()
            for op in ("kill", "possess", "control")
        ]
        forbidden += [F("damage", self.demon, anchor) for anchor in anchors]
        for effect in forbidden:
            covenant.attach(
                "HardConstraint",
                Rule(E("true"), (Directive(Mode.FORBID, effect),), "三导师临终契约"),
            )
        for mentor in self.mentors[:3]:
            covenant.relate("GuaranteedBy", mentor)
        self.words.contracts.append(covenant)
        self.demon_covenant = covenant

    def _build_histories(self):
        for story_index, spec in enumerate(STORY_SPECS):
            event = Entity(
                spec["title"],
                self.history_archive,
                {"History": 1, "Truth": 1, "SealedMemory": 1},
                properties={
                    "scene": self.scenes[spec["scene"]],
                    "summary": spec["summary"],
                },
            )
            event.relate("OccurredAt", self.scenes[spec["scene"]])
            self.history.append(event)
            carrier_scene = (
                self.scenes["动力总站"]
                if story_index == 3
                else self.scenes[spec["scene"]]
            )
            for role, (material_name, medium, claim) in spec["memories"].items():
                actor = self.characters[role]
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
                self.memory_carriers.append(carrier)
                event.relate("Witness", actor)
            for left_role, right_role, left_kind, right_kind, strength in spec["bonds"]:
                left, right = self.characters[left_role], self.characters[right_role]
                left.relate(left_kind, right, strength, source=event)
                right.relate(right_kind, left, strength, source=event)
                left.relate("PastBond", right, strength, source=event)
                right.relate("PastBond", left, strength, source=event)

        accident = Entity(
            "源质反应炉泄露与临终契约",
            self.history_archive,
            {"History": 1, "Truth": 1, "SealedMemory": 1, "FinalHistory": 1},
            properties={
                "scene": self.scenes["源质反应炉"],
                "summary": "校准误差导致反应炉破裂，四位导师以自身建立禁闭契约。",
            },
        )
        accident.relate("OccurredAt", self.scenes["源质反应炉"])
        self.history.append(accident)
        groups = tuple(
            tuple(self.characters.values())[index : index + 4]
            for index in range(0, 16, 4)
        )
        sites = ("中央厅", "星盘讲堂", "动力总站", "手术与解剖室")
        names = ("停摆的总课表", "星盘夹层合影", "焦黑联锁手记", "沾血解剖日志")
        claims = (
            "广播先报出错误校准时刻，随后才响起反应炉破裂声。",
            "阵法导师以石化肉身承受主阵，要求学员不要移动她。",
            "工程导师把生物指纹熔入联锁，切断恶魔的机械权限。",
            "学园长知道校准存在误差，仍批准了源质提取实验。",
        )
        for group, site, name, claim in zip(groups, sites, names, claims):
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
                self.scenes[site],
                memory,
                creator=assignee,
                tags={"MemoryCarrier": 1, "Vestige": 3, "Hidden": 1},
            )
            carrier.properties.update(assignee=assignee, story=accident)
            carrier.relate("RecordOf", accident)
            carrier.relate("Owner", assignee)
            self.memory_carriers.append(carrier)

    def _build_rules(self):
        rules = (
            (
                "恶魔不得直接伤害学员",
                Rule(
                    E("true"),
                    tuple(
                        Directive(Mode.FORBID, F(op, self.demon, actor))
                        for actor in self.characters.values()
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
        self.public_rules = []
        for title, rule in rules:
            carrier = InformationCarrier(
                title,
                "Broadcast",
                self.scenes["中央厅"],
                tags={"Guarantee": 2, "Law": 2},
            )
            publication = RulePublication(
                rule, self.demon, carrier=carrier, scope=self.world, version=1
            )
            publication.name = title
            self.public_rules.append(publication)
            for role, soul in self.souls.items():
                soul.receive(publication, confidence=2)
                soul.understand(rule)
                (
                    soul.reject
                    if role in {"神偷", "职业棋手", "化学家"}
                    else soul.accept
                )(rule)

    def _build_kotodama(self):
        self.tuning_fork = Entity(
            "黄铜共鸣音叉",
            self.team_inventory,
            {"Tool": 1, "Instrument": 1, "Mind": 2, "Bond": 2, "Preservation": 1},
        )
        self.threshold_token = Entity(
            "誓约庭重型钥匙",
            self.team_inventory,
            {"Tool": 1, "Authority": 2, "Name": 2, "Law": 2, "Threshold": 2},
        )
        self.rule_ink = Entity(
            "言灵导师的红墨水",
            self.team_inventory,
            {"Tool": 1, "Authority": 3, "Name": 2, "Law": 3},
            properties={"uses": 4},
        )
        self.calm_word = Recipe(
            "镇心三拍",
            slots={"focus": {"tags": {"Instrument": 1}, "held": True}},
            thresholds={"Mind": 3, "Bond": 2, "Preservation": 1},
            requirements=(
                lambda scope: (
                    scope["target"].tag("Panic") >= 2
                    and self._same_scene(scope["actor"], scope["target"])
                ),
            ),
            effects=(F("change", "target", "Panic", -2),),
            parent=self.rule_archive,
            tags={"Incantation": 1, "Kotodama": 1},
        )
        self.open_word = Recipe(
            "门槛正名",
            slots={"token": {"tags": {"Authority": 1, "Threshold": 1}, "held": True}},
            thresholds={"Name": 3, "Law": 2, "Threshold": 2},
            requirements=(
                lambda scope: (
                    scope["target"].has("Locked")
                    and self._adjacent(scope["actor"].parent, scope["target"])
                ),
            ),
            effects=(
                F("change", "target", "Locked", -1),
                F("assert", E("unlocked", "target")),
            ),
            parent=self.rule_archive,
            tags={"Incantation": 1, "Kotodama": 1},
        )
        self.refute_word = Recipe(
            "证词驳伪正言",
            slots={"evidence": {"tags": {"Analysis": 1}}},
            thresholds={"Name": 3, "Law": 3, "Mind": 3},
            requirements=(lambda scope: scope["target"].tag("FalseRule") >= 1,),
            effects=(F("change", "target", "FalseRule", -1),),
            parent=self.rule_archive,
            tags={"Incantation": 1, "Kotodama": 1, "Refutation": 1},
        )
        self.word_recipes = (self.calm_word, self.open_word, self.refute_word)
        self.final_recipe = Recipe(
            "四院真名再封印",
            parent=self.rule_archive,
            tags={"Rite": 1, "Covenant": 1, "Seal": 1},
        )

    def _build_objectives(self):
        component_specs = (
            (
                "逆写第七码真名结构",
                {"SealComponent": 1, "TrueName": 1, "Name": 5, "Law": 2, "Vestige": 2},
            ),
            (
                "四院闭合封印阵图",
                {
                    "SealComponent": 1,
                    "SealPattern": 1,
                    "Form": 2,
                    "Law": 2,
                    "Threshold": 3,
                },
            ),
            (
                "导师指纹机械联锁",
                {
                    "SealComponent": 1,
                    "IsolationMechanism": 1,
                    "Form": 4,
                    "Preservation": 2,
                },
            ),
            (
                "铅玻璃源质安全载体",
                {
                    "SealComponent": 1,
                    "SafeVessel": 1,
                    "Preservation": 4,
                    "Threshold": 2,
                },
            ),
        )
        # OBJECTIVE_SPECS 的次序就是终局四步次序；故事索引保持对象引用。
        for spec, (component_name, component_tags) in zip(
            OBJECTIVE_SPECS, component_specs
        ):
            (
                name,
                story_index,
                source_name,
                anchor_name,
                leader_role,
                helper_role,
                operation,
            ) = spec
            story = self.history[story_index]
            leader, helper = self.characters[leader_role], self.characters[helper_role]
            source, anchor = self.scenes[source_name], self.scenes[anchor_name]
            component = Entity(component_name, anchor, component_tags)
            if component.has("TrueName"):
                component.relate("TrueNameOf", self.demon, 2)
            objective = Entity(
                name,
                self.story_board,
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
                actor = self.characters[role]
                actor.relate("CommittedTo", objective, source=objective)
                objective.relate("Member", actor)
                actor.souls[0].learn(
                    objective,
                    (("search_at", story.get("scene")),),
                    source=self.story_board,
                    stage="understood",
                )
            self.objectives.append(objective)
            self.seal_components.append(component)
        accident = next(event for event in self.history if event.has("FinalHistory"))
        self.accident_inquiry = Entity(
            "反应炉事故因果综合",
            self.story_board,
            {"Inquiry": 1, "Active": 1},
            properties={
                "story": accident,
                "leader": self.characters["职业棋手"],
                "source": self.scenes["中央厅"],
                "stage": 0,
            },
        )
        self.accident_inquiry.relate("LedBy", self.characters["职业棋手"])
        self.characters["职业棋手"].relate(
            "CommittedTo", self.accident_inquiry, source=self.accident_inquiry
        )

    # ---- 决策树与效用 ---------------------------------------------------

    def _build_decision_trees(self):
        for role, actor in self.characters.items():
            soul = self.souls[role]
            soul.install_step(self._memory_step, 90, source=actor)
            soul.install_step(self._analysis_step, 88, source=actor)
            soul.install_step(self._belief_step, 86, source=actor)
            soul.install_step(self._synthesis_step, 84, source=actor)
            soul.install_step(self._relationship_step, 80, source=actor)
            soul.install_step(self._rule_step, 78, source=actor)
            soul.install_step(self._help_step, 75, source=actor)
            soul.install_step(self._objective_step, 65, source=actor)
            soul.install_step(self._word_step, 60, source=actor)
            soul.install_step(self._travel_step, 50, source=actor)
            soul.install_step(self._rest_step, 0, source=actor)
        self.demon_soul.install_step(self._demon_step, 80, source=self.demon)

    def _activate(self, actor, soul, delay):
        def take_turn():
            if self.ended:
                return None
            self._refresh_objectives()
            blocked_options = []

            def allows(intent):
                allowed, review = self.runtime.review(intent, self._context_for(intent))
                if not allowed:
                    blocked_options.append(f"{intent.effect.op}（{review.reason}）")
                if (
                    not allowed
                    and actor is self.demon
                    and intent.effect.op == "forge_false_rule"
                ):
                    self.patch_blocks += 1
                return allowed

            action = soul.step(
                actor,
                self.rules,
                self.env,
                {"source": actor},
                lambda intent: self._npc_utility(actor, intent),
                allows,
            )
            if action is None:
                self._check_failure()
                return 25
            decision = soul.decisions[-1]
            self._decision_notes[id(action)] = {
                "score": decision.score,
                "reason": self._decision_reason(
                    actor,
                    action,
                    decision.reason,
                    blocked_options,
                    decision.preferences,
                ),
            }
            self.runtime.resolve(action, self._context_for(action))
            self._refresh_objectives()
            self._check_end()
            self._check_failure()
            return max(15, action.motion_cost)

        self.timeline.schedule(delay, f"行动槽：{actor.name}", take_turn)

    def _npc_utility(self, actor, intent):
        operation = intent.effect.op
        if actor is self.demon:
            if operation == "damage":
                return 200
            return 115 if operation == self._demon_desired else 25
        base = {
            "recover_memory": 120,
            "investigate": 112,
            "analyze_memory": 126,
            "refute_rule": 132,
            "synthesize_history": 128,
            "share_memory": 100,
            "clarify_memory": 100,
            "confront": 100,
            "protect": 100,
            "propose_rule": 126,
            "establish_rule": 134,
            "invoke_word": 122,
            "answer_help": 118,
            "assist": 125,
            "request_help": 110,
            "decipher": 115,
            "listen": 115,
            "repair": 115,
            "treat": 115,
            "collect": 108,
            "begin_rite": 130,
            "contribute_rite": 135,
            "move": 88,
            "rest": 8,
        }.get(operation, 30)
        target = intent.effect.args[1] if len(intent.effect.args) > 1 else None
        bond = self._bond_strength(actor, self._social_target(intent))
        repeats = self._recent_repetitions(actor, operation, target)
        return base + bond * 8 - repeats * 35 - intent.motion_cost / 10

    def _decision_reason(
        self,
        actor,
        intent,
        rule_reason,
        blocked_options=(),
        preferences=(),
    ):
        target = self._social_target(intent)
        bond = self._bond_strength(actor, target)
        commitment = next(
            (
                facet.value.name
                for facet in actor.related("CommittedTo")
                if isinstance(facet.value, Entity)
            ),
            "无",
        )
        repetition = self._recent_repetitions(
            actor,
            intent.effect.op,
            intent.effect.args[1] if len(intent.effect.args) > 1 else None,
        )
        parts = [f"承诺={commitment}", f"规则={rule_reason}", f"重复衰减={repetition}"]
        analyses = sum(analysis.get("analyst") is actor for analysis in self.analyses)
        if analyses:
            parts.append(f"已形成分析={analyses}")
        false_belief = self._active_false_belief(actor)
        if false_belief is not None:
            publication = false_belief.get("publication")
            parts.append(f"待修正信念={publication.name}")
        syntheses = tuple(
            inquiry
            for inquiry in (*self.objectives, self.accident_inquiry)
            if inquiry is not None
            and inquiry.get("leader") is actor
            and self._synthesis_for(inquiry)
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

    def _recent_repetitions(self, actor, operation, target):
        count = 0
        for event in reversed(self.runtime.events[-40:]):
            if event.get("actor") is not actor:
                continue
            effect = event.get("effect")
            event_target = effect.args[1] if effect and len(effect.args) > 1 else None
            if effect and effect.op == operation and event_target is target:
                count += 1
            else:
                break
        return count

    # ---- 候选行动 -------------------------------------------------------

    def _assigned_carriers(self, actor):
        return tuple(
            carrier
            for carrier in self.memory_carriers
            if carrier.get("assignee") is actor
        )

    def _carrier_done(self, actor, carrier):
        return any(facet.value is actor for facet in carrier.related("RecoveredBy"))

    def _pending_carrier(self, actor):
        return next(
            (
                carrier
                for carrier in self._assigned_carriers(actor)
                if not self._carrier_done(actor, carrier)
            ),
            None,
        )

    def _story_carrier(self, actor):
        return next(
            (
                carrier
                for carrier in self._assigned_carriers(actor)
                if not carrier.related("RecordOf")[0].value.has("FinalHistory")
            ),
            None,
        )

    def _memory_step(self, _soul, actor, _env, _context):
        carrier = self._pending_carrier(actor)
        if not carrier or carrier.parent is not actor.parent:
            return None
        if carrier.has("Hidden"):
            return self._investigate_intent(actor, carrier)
        if not self._carrier_done(actor, carrier):
            return self._intent(
                actor,
                "recover_memory",
                carrier,
                45,
                lambda: self._recover_now(actor, carrier),
                f"恢复记忆：{carrier.name}",
                "读取已揭示的个人记忆载体",
            )
        return None

    def _investigate_intent(self, actor, carrier):
        def resolve():
            carrier.tags.pop("Hidden", None)
            carrier.relate("DiscoveredBy", actor)
            self.log.append(f"{actor.name}依照分配的搜索范围发现《{carrier.name}》。")
            return carrier

        return self._intent(
            actor,
            "investigate",
            carrier,
            30,
            resolve,
            f"勘查：{carrier.name}",
            "只搜索已知地点，不读取隐藏内容",
        )

    def _analysis_for(self, actor, carrier):
        return next(
            (
                analysis
                for analysis in self.analyses
                if analysis.get("analyst") is actor
                and analysis.get("carrier") is carrier
            ),
            None,
        )

    def _analysis_pending(self, actor):
        return next(
            (
                carrier
                for carrier in self._assigned_carriers(actor)
                if self._carrier_done(actor, carrier)
                and self._analysis_for(actor, carrier) is None
            ),
            None,
        )

    def _inquiry_for_story(self, story):
        return next(
            (
                objective
                for objective in self.objectives
                if objective.get("story") is story
            ),
            self.accident_inquiry
            if self.accident_inquiry and self.accident_inquiry.get("story") is story
            else None,
        )

    def _analysis_step(self, _soul, actor, _env, _context):
        carrier = self._analysis_pending(actor)
        if carrier is None:
            return None
        memory = carrier.related("Carries")[0].value
        story = carrier.get("story")
        inquiry = self._inquiry_for_story(story)
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
            self.analyses.append(analysis)
            self.log.append(
                f"{actor.name}没有把《{carrier.name}》直接当成事实，而是形成了可质疑的职业解释。"
            )
            return analysis

        return self._intent(
            actor,
            "analyze_memory",
            carrier,
            40,
            resolve,
            f"分析材料：{carrier.name}",
            "把主观 Claim 转化为带来源的 Analysis Knowledge",
        )

    def _submitted_analyses(self, inquiry):
        return (
            tuple(facet.value for facet in inquiry.related("SubmittedAnalysis"))
            if inquiry
            else ()
        )

    def _synthesis_for(self, inquiry):
        return (
            next((facet.value for facet in inquiry.related("Synthesis")), None)
            if inquiry
            else None
        )

    def _synthesis_step(self, _soul, actor, _env, _context):
        inquiries = (*self.objectives, self.accident_inquiry)
        inquiry = next(
            (
                item
                for item in inquiries
                if item is not None
                and item.get("leader") is actor
                and self._synthesis_for(item) is None
                and len(self._submitted_analyses(item)) >= 3
                and actor.parent is item.get("source")
            ),
            None,
        )
        if inquiry is None:
            return None
        reports = self._submitted_analyses(inquiry)[:3]
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
            self.log.append(
                f"{actor.name}比较三份带来源的解释，形成《{story.name}》的暂定综合；它仍是 Knowledge，不是客观事实。"
            )
            return synthesis

        return self._intent(
            actor,
            "synthesize_history",
            inquiry,
            55,
            resolve,
            f"综合历史：{story.name}",
            "三份独立 Analysis 互证后才形成暂定解释",
        )

    def _active_false_belief(self, actor):
        for belief in actor.souls[0].beliefs:
            publication = belief.get("publication")
            if (
                publication
                and publication.has("Falsehood")
                and belief.has("Accepted")
                and not any(
                    facet.value is actor for facet in publication.related("RefutedBy")
                )
            ):
                return belief
        return None

    def _belief_step(self, _soul, actor, _env, _context):
        belief = self._active_false_belief(actor)
        carrier = self._story_carrier(actor)
        analysis = self._analysis_for(actor, carrier) if carrier else None
        if belief is None or analysis is None:
            return None
        publication = belief.get("publication")

        def resolve():
            result = self.words.invoke(
                self.refute_word,
                actor.souls[0],
                {"evidence": analysis},
                target=actor.souls[0],
                context={"actor": actor, "holder": actor.souls[0], "world": self.world},
            )
            if result is None:
                self.log.append(
                    f"{actor.name}的驳伪言灵未成立：{self.words.last_failure}。"
                )
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
            self.log.append(
                f"{actor.name}以《{analysis.name}》为语义投入施行“证词驳伪正言”，拒绝了恶魔的伪规则。"
            )
            return result

        return self._intent(
            actor,
            "refute_rule",
            publication,
            60,
            resolve,
            "言灵：证词驳伪正言",
            "材料分析与伪规则冲突，修订已接受的 RuleBelief",
        )

    def _relationship_pending(self, actor):
        carrier = self._story_carrier(actor)
        if (
            not carrier
            or not self._carrier_done(actor, carrier)
            or self._analysis_for(actor, carrier) is None
        ):
            return None
        if any(facet.value is actor for facet in carrier.related("RespondedBy")):
            return None
        bond = next(
            (
                facet
                for facet in actor.related("PastBond")
                if isinstance(facet.value, Entity)
            ),
            None,
        )
        return (carrier, bond.value) if bond else None

    def _relationship_step(self, _soul, actor, _env, _context):
        pending = self._relationship_pending(actor)
        if not pending:
            return None
        carrier, target = pending
        keys = {facet.key for facet in actor.facets if facet.value is target}
        same_scene = self._same_scene(actor, target)
        # 关系节点提供少量真实可行的候选；人格、情绪、规则和任务效用只排序，
        # 不再由关系类别预先指定唯一动作。
        operations = []
        if same_scene and keys & RELATION_NEGATIVE:
            operations.append(("confront", ResponseMode.RESTRAIN))
        if same_scene and keys & RELATION_PROTECTIVE:
            operations.append(("protect", ResponseMode.PROTECT))
        if same_scene:
            operations.append(("clarify_memory", ResponseMode.NEGOTIATE))
        operations.append(("share_memory", ResponseMode.COOPERATE))

        def make_intent(operation, response_mode):
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
                    self._bond_strength(actor, target),
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
                self.log.append(
                    f"{actor.name}因恢复的旧事对{target.name}执行 {operation}。"
                )
                return target

            return self._intent(
                actor,
                operation,
                target,
                35 if not same_scene else 30,
                resolve,
                f"关系回应：{operation}",
                "恢复记忆使旧羁绊进入当前决策；异地时通过定向广播分享载体",
                response_mode=response_mode,
            )

        return tuple(make_intent(*item) for item in dict.fromkeys(operations))

    def _active_rule_proposal(self):
        return next(
            (proposal for proposal in self.rule_proposals if proposal.has("Active")),
            None,
        )

    def _refuted_false_rules(self):
        return tuple(
            publication
            for publication in self.false_publications
            if publication.related("RefutedBy")
        )

    def _rule_step(self, _soul, actor, _env, _context):
        lawyer = self.characters["律师"]
        witness = self.characters["职业棋手"]
        proposal = self._active_rule_proposal()
        if (
            actor is lawyer
            and not self.rule_proposals
            and len(self._refuted_false_rules()) >= 2
        ):
            sources = self._refuted_false_rules()[:2]

            def propose():
                created = Entity(
                    "禁止恶魔伪造规则释义的提案",
                    self.story_board,
                    {"RuleProposal": 1, "Active": 1},
                    properties={
                        "issuer": lawyer,
                        "witness": witness,
                        "operation": "forge_false_rule",
                        "subject": self.demon,
                        "target": self.world,
                        "scope": self.world,
                    },
                )
                for publication in sources:
                    created.relate("BasedOnRefutation", publication)
                lawyer.relate("CommittedTo", created, source=created)
                witness.relate("CommittedTo", created, source=created)
                self.rule_proposals.append(created)
                self.log.append(
                    f"{lawyer.name}依据两次可追溯的驳伪结果提出客观规则，而不是凭空取得立法权。"
                )
                return created

            return self._intent(
                actor,
                "propose_rule",
                self.world,
                35,
                propose,
                "提出反伪规则议案",
                "两份被言灵驳回的 RulePublication 构成提案依据",
            )
        if (
            proposal is not None
            and actor is lawyer
            and lawyer.parent is self.scenes["契约审议室"]
            and witness.parent is lawyer.parent
        ):
            return self._counter_rule_intent(lawyer, witness, proposal)
        return None

    def _counter_rule_intent(self, actor, witness, proposal):
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
                and actor.parent is self.scenes["契约审议室"]
                and witness.parent is actor.parent
                and self.rule_ink.get("uses", 0) > 0
            )

        def resolve():
            self.rule_ink.set("uses", self.rule_ink.get("uses") - 1)
            carrier = InformationCarrier(
                "反伪规则审议书",
                "Manuscript",
                self.rule_archive,
                tags={"Law": 3, "Guarantee": 2},
            )
            publication = RulePublication(
                rule, actor, carrier=carrier, scope=scope, version=self.env.version
            )
            publication.relate("Witness", witness)
            publication.relate("DerivedFrom", proposal)
            patch = RulePatch(
                publication.name,
                rule,
                self.rule_archive,
                publication,
                scope,
                priority=actor.souls[0].tag("Law"),
            )
            patch.relate("Witness", witness)
            patch.relate("BasedOn", proposal)
            self.rule_patches.append(patch)
            self.runtime.patches.append(patch)
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
            self.log.append(
                f"{actor.name}与{witness.name}完成审议，将两次驳伪结论设立为阻止恶魔伪释义的 RulePatch。"
            )
            return patch

        return self._intent(
            actor,
            "establish_rule",
            proposal,
            70,
            resolve,
            "设立反伪规则",
            "提案、两份驳伪依据、发行者与见证者全部到场",
            valid,
        )

    def _word_step(self, _soul, actor, _env, _context):
        if actor.get("role") not in {"音乐家", "职业棋手", "画家"}:
            return None
        target = next(
            (
                other
                for other in self.characters.values()
                if other is not actor
                and self._same_scene(actor, other)
                and other.tag("Panic") >= 3
            ),
            None,
        )
        if target is None:
            return None

        def resolve():
            result = self.words.invoke(
                self.calm_word,
                actor.souls[0],
                {"focus": self.tuning_fork},
                target=target,
                context={
                    "actor": actor,
                    "holder": self.team_inventory,
                    "world": self.world,
                },
            )
            if result:
                self.log.append(
                    f"{actor.name}根据{target.name}的实际恐慌施行“镇心三拍”。"
                )
            return result

        return self._intent(
            actor,
            "invoke_word",
            target,
            55,
            resolve,
            "言灵：镇心三拍",
            "同场目标的恐慌达到门槛，明确投入音叉并核验 Aspect",
        )

    def _active_request_for(self, actor):
        return next(
            (
                request
                for request in self.requests
                if request.has("Active") and request.get("helper") is actor
            ),
            None,
        )

    def _help_step(self, _soul, actor, _env, _context):
        request = self._active_request_for(actor)
        if not request:
            return None
        if not any(facet.value is actor for facet in request.related("AcceptedBy")):

            def answer():
                request.relate("AcceptedBy", actor)
                actor.relate("CommittedTo", request, source=request)
                self.log.append(
                    f"{actor.name}远程回应{request.get('requester').name}，承诺赴援。"
                )
                return request

            return self._intent(
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
            return self._assist_intent(actor, request)
        return None

    def _assist_intent(self, actor, request):
        objective = request.get("objective")

        def resolve():
            request.tags.pop("Active", None)
            request.change("Resolved", 1)
            request.relate("AnsweredBy", actor)
            actor.detach("CommittedTo", request)
            objective.change("Assisted", 1)
            objective.set("stage", 2)
            objective.relate("SupportedBy", actor, source=request)
            self.log.append(
                f"{actor.name}抵达现场，为{request.get('requester').name}提供言灵见证；门禁仍未打开。"
            )
            return objective

        return self._intent(
            actor,
            "assist",
            request,
            40,
            resolve,
            "到场提供言灵见证",
            "履行实际 HelpRequest 承诺；协助本身不解除门禁",
            response_mode=ResponseMode.COOPERATE,
        )

    def _objective_for_leader(self, actor):
        return next(
            (
                objective
                for objective in self.objectives
                if objective.get("leader") is actor
            ),
            None,
        )

    def _objective_step(self, _soul, actor, _env, _context):
        objective = self._objective_for_leader(actor)
        if not objective:
            return None
        stage = objective.get("stage", 0)
        if stage == 1 and actor.parent is objective.get("source"):
            active = next(
                (
                    request
                    for request in self.requests
                    if request.get("objective") is objective and request.has("Active")
                ),
                None,
            )
            if active is None:
                return self._request_help_intent(actor, objective)
        if stage == 2 and actor.parent is objective.get("source"):
            return self._open_anchor_word_intent(actor, objective)
        if stage == 3 and actor.parent is objective.get("anchor"):
            return self._profession_intent(actor, objective)
        if stage == 4 and actor.parent is objective.get("anchor"):
            return self._collect_intent(actor, objective.get("component"), objective)
        if stage >= 5 and self._final_ready():
            if actor.parent is self.scenes["核心隔离舱"]:
                if self.active_final_rite is None:
                    return self._begin_rite_intent(actor)
                if not any(
                    facet.value is actor
                    for facet in self.active_final_rite.related("Contributor")
                ):
                    return self._contribute_rite_intent(actor)
        return None

    def _open_anchor_word_intent(self, actor, objective):
        anchor = objective.get("anchor")

        def resolve():
            result = self.words.invoke(
                self.open_word,
                actor.souls[0],
                {"token": self.threshold_token},
                target=anchor,
                context={
                    "actor": actor,
                    "holder": self.team_inventory,
                    "world": self.world,
                },
            )
            if result is None:
                self.log.append(
                    f"{actor.name}的门槛正名未成立：{self.words.last_failure}。"
                )
                return None
            objective.relate("UnlockedByWord", actor, source=result)
            objective.set("stage", 3)
            self.log.append(
                f"{actor.name}在{objective.get('helper').name}见证后施行“门槛正名”，以真实 Rite 打开{anchor.name}。"
            )
            return result

        return self._intent(
            actor,
            "invoke_word",
            anchor,
            65,
            resolve,
            f"言灵开门：{anchor.name}",
            "协助只提供见证；实际解锁必须通过门槛正名 Recipe",
        )

    def _request_help_intent(self, actor, objective):
        def resolve():
            request = Entity(
                f"{actor.name}请求验证{objective.name}",
                self.world,
                {"HelpRequest": 1, "Active": 1, "Remote": 1},
                properties={
                    "requester": actor,
                    "helper": objective.get("helper"),
                    "scene": actor.parent,
                    "need": objective.get("operation"),
                    "objective": objective,
                    "deadline": self.timeline.now + 240,
                },
            )
            request.relate("RequestedBy", actor)
            request.relate("Domain", actor.parent)
            request.relate("ForObjective", objective)
            self.requests.append(request)
            self.log.append(f"{actor.name}从{actor.parent.name}发出远程协助请求。")
            return request

        return self._intent(
            actor,
            "request_help",
            objective,
            25,
            resolve,
            "发出四院验证协助请求",
            "单人能力不足，建立远程 HelpRequest",
            response_mode=ResponseMode.NEGOTIATE,
        )

    def _profession_intent(self, actor, objective=None):
        objective = objective or self._objective_for_leader(actor)
        if (
            not objective
            or objective.get("stage") != 3
            or actor.parent is not objective.get("anchor")
        ):
            return None
        operation = objective.get("operation")

        def resolve():
            objective.change("Verified", 1)
            objective.set("stage", 4)
            objective.relate("VerifiedBy", actor)
            self.env.assert_fact(E("academy_verified", objective, actor))
            self.log.append(
                f"{actor.name}以“{actor.get('profession_action')}”完成{objective.name}的有限验证任务。"
            )
            return objective

        return self._intent(
            actor,
            operation,
            objective,
            55,
            resolve,
            actor.get("profession_action"),
            "职业行动仅由未完成的四院验证需求生成",
        )

    def _collect_intent(self, actor, item, objective):
        def resolve():
            item.reparent(self.team_inventory)
            item.relate("RecoveredBy", actor)
            item.relate("DeliveredFor", objective)
            objective.set("stage", 5)
            objective.change("Completed", 1)
            self.log.append(f"{actor.name}取得并交付封印部件《{item.name}》。")
            return item

        return self._intent(
            actor,
            "collect",
            item,
            35,
            resolve,
            f"取得并交付{item.name}",
            "锚点已由对应职业验证",
        )

    def _travel_step(self, _soul, actor, _env, _context):
        goal = self._travel_goal(actor)
        if goal is None or goal is actor.parent:
            return None
        route = self.world.route(actor.parent, goal, actor, self.env)
        return (
            self._move_intent(actor, route.nodes[1])
            if route and len(route.nodes) > 1
            else None
        )

    def _travel_goal(self, actor):
        request = self._active_request_for(actor)
        if request and any(
            facet.value is actor for facet in request.related("AcceptedBy")
        ):
            return request.get("scene")
        carrier = self._pending_carrier(actor)
        if carrier:
            return carrier.parent
        pending = self._relationship_pending(actor)
        if pending:
            # 不追逐正在移动的关系对象；回到共同历史现场等待，避免全知追踪。
            return pending[0].parent
        objective = self._objective_for_leader(actor)
        if objective:
            stage = objective.get("stage", 0)
            if stage in (1, 2):
                return objective.get("source")
            if stage in (3, 4):
                return objective.get("anchor")
            if stage >= 5 and self._final_ready():
                return self.scenes["核心隔离舱"]
        proposal = self._active_rule_proposal()
        if proposal and actor in {proposal.get("issuer"), proposal.get("witness")}:
            return self.scenes["契约审议室"]
        inquiries = (*self.objectives, self.accident_inquiry)
        synthesis_target = next(
            (
                inquiry
                for inquiry in inquiries
                if inquiry is not None
                and inquiry.get("leader") is actor
                and self._synthesis_for(inquiry) is None
                and len(self._submitted_analyses(inquiry)) >= 3
            ),
            None,
        )
        if synthesis_target:
            return synthesis_target.get("source")
        return None

    def _rest_step(self, _soul, actor, _env, _context):
        if not actor.tag("Panic"):
            return None
        return self._intent(
            actor,
            "rest",
            actor,
            35,
            lambda: actor.change("Panic", -1),
            "短暂休整",
            "没有未完成承诺且恐慌仍可下降",
        )

    def _demon_step(self, _soul, actor, _env, _context):
        operations = ("spread_miasma", "whisper_doubt", "forge_false_rule")
        self._demon_desired = operations[self._demon_turn % len(operations)]
        self._demon_turn += 1
        cast = tuple(self.characters.values())
        target = cast[self._demon_turn % len(cast)]
        scene = min(
            (objective.get("source") for objective in self.objectives),
            key=lambda item: item.tag("Hazard"),
        )
        anchor = self.objectives[self._demon_turn % 4].get("anchor")

        def miasma():
            scene.change("Hazard", 1)
            self.crisis.change("Pressure", 1)
            self.log.append(f"恶魔在契约允许的间接范围内向{scene.name}扩散灰潮。")
            return scene

        def whisper():
            target.change("Panic", 1)
            self.crisis.change("Pressure", 1)
            self.log.append(f"恶魔不能控制{target.name}，转而以广播耳语动摇其判断。")
            return target

        def false_rule():
            partner = next(
                (
                    facet.value
                    for facet in target.related("PastBond")
                    if isinstance(facet.value, Entity)
                ),
                self.player,
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
                self.rule_archive,
                tags={"Ambiguity": 2, "Law": 1},
            )
            publication = RulePublication(
                rule,
                self.demon,
                carrier=carrier,
                scope=self.world,
                version=self._demon_turn,
            )
            publication.name = f"孤立才能保护同伴·伪释义{self._demon_turn}"
            publication.change("Falsehood", 1)
            publication.relate("Targets", target)
            belief = target.souls[0].receive(publication, confidence=3)
            target.souls[0].understand(rule)
            target.souls[0].accept(rule)
            belief.relate("InducedBy", self.demon)
            target.souls[0].change("FalseRule", 1)
            target.relate("MisledBy", publication)
            self.false_publications.append(publication)
            self.crisis.change("Pressure", 1)
            self.log.append(
                f"恶魔向{target.name}发布“孤立才能保护同伴”，该 Soul 暂时接受了会压制关系行动的 RulePublication。"
            )
            return publication

        damage = self._intent(
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
            self._intent(
                actor,
                "spread_miasma",
                self.world,
                55,
                miasma,
                "灰潮渗流",
                "契约禁止直接伤害，只能间接污染场景",
            ),
            self._intent(
                actor,
                "whisper_doubt",
                target,
                55,
                whisper,
                "广播疑念",
                "契约禁止精神控制，只能增加可抵抗的恐慌",
            ),
            self._intent(
                actor,
                "forge_false_rule",
                self.world,
                55,
                false_rule,
                "伪造规则释义",
                "恶魔利用公开规则而不读取封存记忆",
            ),
        )

    # ---- 行动结算与轨迹元数据 -----------------------------------------

    def _snapshot(self):
        data = {
            "危机.压力": self.crisis.tag("Pressure"),
            "规则.补丁数": len(self.rule_patches),
            "规则.提案数": len(self.rule_proposals),
            "规则.反伪阻止数": self.patch_blocks,
            "认知.分析数": len(self.analyses),
            "认知.综合数": sum(
                bool(self._synthesis_for(inquiry))
                for inquiry in (*self.objectives, self.accident_inquiry)
                if inquiry is not None
            ),
            "认知.伪规则发布数": len(self.false_publications),
            "认知.伪规则驳回数": len(self._refuted_false_rules()),
            "协调.成立": bool(self.coordination),
            "仪式.贡献者": len(self.active_final_rite.related("Contributor"))
            if self.active_final_rite
            else 0,
            "仪式.存在": bool(self.active_final_rite),
            "终局.胜利": self.won,
            "终局.结束": self.ended,
        }
        for role, actor in self.characters.items():
            data[f"人物.{role}.位置"] = actor.parent.name if actor.parent else None
            data[f"人物.{role}.恐慌"] = actor.tag("Panic")
            data[f"人物.{role}.稳定"] = actor.souls[0].tag("Stability")
            data[f"人物.{role}.洞见"] = actor.souls[0].tag("Insight")
            data[f"人物.{role}.已接受伪规则"] = actor.souls[0].tag("FalseRule")
            data[f"人物.{role}.有效目标情绪"] = len(
                self._active_social_impacts(actor.souls[0])
            )
        for objective in self.objectives:
            data[f"目标.{objective.name}.阶段"] = objective.get("stage", 0)
        for carrier in self.memory_carriers:
            data[f"记忆.{carrier.name}.隐藏"] = carrier.has("Hidden")
            data[f"记忆.{carrier.name}.恢复数"] = len(carrier.related("RecoveredBy"))
            data[f"记忆.{carrier.name}.回应数"] = len(carrier.related("RespondedBy"))
        for request in self.requests:
            data[f"请求.{request.name}.活跃"] = request.has("Active")
            data[f"请求.{request.name}.接受"] = len(request.related("AcceptedBy"))
            data[f"请求.{request.name}.解决"] = request.tag("Resolved")
        for item in self.seal_components:
            data[f"部件.{item.name}.位置"] = item.parent.name if item.parent else None
        for scene in self.scenes.values():
            if scene.tag("Hazard"):
                data[f"场景.{scene.name}.危险"] = scene.tag("Hazard")
        return data

    def _intent(
        self,
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
        holder = {}

        def wrapped():
            before = self._snapshot()
            origin = actor.parent
            outcome = resolve()
            after = self._snapshot()
            changes = [
                f"{key}: {before.get(key)} → {value}"
                for key, value in after.items()
                if before.get(key) != value
            ]
            intent = holder["intent"]
            note = self._decision_notes.pop(id(intent), {})
            self._pending_event_meta[actor] = {
                "origin": origin,
                "destination": actor.parent,
                "reason": note.get("reason", reason or "玩家明确提交"),
                "score": note.get("score"),
                "changes": tuple(changes),
                "contract_checked": actor is self.demon,
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

    def _context_for(self, intent):
        domains = [self.world]
        if intent.actor.parent:
            domains.append(intent.actor.parent)
        for value in intent.effect.args:
            if isinstance(value, Entity):
                domains.append(value)
                if value.parent and value.parent.has("Scene"):
                    domains.append(value.parent)
        return {"source": intent.actor, "domains": tuple(dict.fromkeys(domains))}

    def _witnesses(self, event, _context):
        actor = event.get("actor")
        meta = self._pending_event_meta.pop(actor, {})
        origin, destination = meta.get("origin"), meta.get("destination")
        if isinstance(origin, Entity):
            event.relate("OccurredAt", origin)
            event.set("origin", origin)
        if isinstance(destination, Entity):
            event.set("destination", destination)
            if destination is not origin:
                event.relate("ArrivedAt", destination)
        event.set("decision_reason", meta.get("reason", "未记录"))
        event.set("decision_score", meta.get("score"))
        event.set("state_changes", meta.get("changes", ()))
        event.set("contract_checked", meta.get("contract_checked", False))
        scenes = {
            item
            for item in (origin, destination)
            if isinstance(item, Entity) and item.has("Scene")
        }
        return tuple(
            soul
            for role, soul in self.souls.items()
            if self.characters[role].parent in scenes
        )

    @staticmethod
    def _impact_target(impact):
        relation = impact.related("Target")
        return relation[0].value if relation else None

    def _active_social_impacts(self, soul, target=None):
        return tuple(
            impact
            for impact in self.social_impacts
            if impact.parent is soul
            and (target is None or self._impact_target(impact) is target)
            and self.timeline.now < impact.get("expires_at", self.timeline.now)
        )

    def _add_social_impact(self, receiver, target, event, appraisal):
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
            existing.relate("ReinforcedBy", event)
            event.relate("CreatedImpact", existing)
            return existing

        impact = SocialImpact(
            f"{receiver.name}对{target.name}：{combination}",
            soul,
            target,
            event,
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
        event.relate("CreatedImpact", impact)
        return impact

    def _observe_social_outcome(self, event, _context):
        """把真实结算结果评价为目标情绪；不读取剧情节点或预期动作。"""
        if not self.social_emotions:
            return
        effect, actor = event.get("effect"), event.get("actor")
        if effect is None or not isinstance(actor, Character):
            return
        operation = effect.op
        receiver = target = None
        appraisal = None
        if operation in {"share_memory", "clarify_memory", "protect"}:
            receiver, target = effect.args[1], actor
            appraisal = {
                "bond": self._bond_strength(receiver, actor),
                "support_received": True,
            }
            if operation == "clarify_memory":
                appraisal["vulnerability_accepted"] = True
        elif operation == "confront":
            receiver, target = effect.args[1], actor
            bond = self._bond_strength(receiver, actor)
            appraisal = {
                "bond": bond,
                "truth_conflict": bond >= 1,
                "norm_breach": bond < 1,
            }
        elif operation in {"answer_help", "assist"}:
            request = effect.args[1]
            receiver, target = request.get("requester"), actor
            appraisal = {
                "bond": self._bond_strength(receiver, actor),
                "support_received": True,
                "shared_objective_advanced": operation == "assist",
            }
        elif operation == "whisper_doubt":
            receiver, target = effect.args[1], actor
            appraisal = {
                "bond": 0,
                "malicious_intent": True,
                "threat": 2,
                "norm_breach": True,
            }
        elif operation == "forge_false_rule":
            publication = event.get("outcome")
            targets = (
                publication.related("Targets")
                if isinstance(publication, Entity)
                else ()
            )
            receiver = targets[0].value if targets else None
            target = actor
            appraisal = {
                "bond": 0,
                "malicious_intent": True,
                "threat": 2,
                "norm_breach": True,
            }
        if appraisal is None:
            return
        impact = self._add_social_impact(receiver, target, event, appraisal)
        if impact is not None:
            event.set("social_impact", impact.get("combination"))
            event.set("social_receiver", receiver)
            event.set("social_target", target)
            self.log.append(
                f"{event.name}使{receiver.name}对{target.name}产生"
                f"{impact.get('combination')}，它只形成临时态度偏置。"
            )

    def _submit(self, intent, context=None):
        context = context or self._context_for(intent)

        def reviewed(allowed, decision):
            self._decision_notes[id(intent)] = {
                "score": decision.score,
                "reason": f"玩家提交；规则={decision.reason}",
            }
            self.log.append(
                f"{intent.label}被客观规则或契约阻止。"
                if not allowed
                else f"决策评估：{decision.reason}。"
            )

        result = self.runtime.submit(intent, context=context, on_review=reviewed)
        self._refresh_objectives()
        self._check_end()
        self._check_failure()
        return result

    def _edge(self, start, destination):
        edge = self.world.links.get(start, {}).get(destination)
        return edge if isinstance(edge, Passage) else None

    def _move_intent(self, actor, destination):
        start = actor.parent
        passage = self._edge(start, destination) if destination else None
        if passage is None or not passage.allows(start, actor, self.env):
            return None

        def valid():
            return (
                actor.parent is start
                and passage.other(start) is destination
                and passage.allows(start, actor, self.env)
            )

        def resolve():
            self._set_location(actor, destination)
            if destination.tag("Hazard") > actor.tag("Occult"):
                actor.change(
                    "Panic", max(1, destination.tag("Hazard") - actor.tag("Occult"))
                )
            self.log.append(
                f"{actor.name}经《{passage.name}》从{start.name}抵达{destination.name}。"
            )
            return passage

        return self._intent(
            actor,
            "move",
            destination,
            max(10, passage.cost_from(start) * 20),
            resolve,
            f"逐段移动至{destination.name}",
            "沿当前目标的最短 Passage 迈出一段",
            valid,
        )

    def _set_location(self, actor, destination, initial=False):
        start = actor.parent
        if not initial and start:
            self.env.retract(E("located_at", actor, start))
            actor.souls[0].unobserve(E("located_at", actor, start))
            actor.reparent(destination)
        self.env.assert_fact(E("located_at", actor, destination))
        actor.souls[0].observe(E("located_at", actor, destination))

    # ---- 玩家可用动作 ---------------------------------------------------

    def move(self, actor, destination):
        intent = self._move_intent(actor, destination)
        return bool(intent and self._submit(intent))

    def _recover_now(self, actor, carrier):
        if carrier.has("Hidden"):
            return None
        claims = carrier.read(actor.souls[0])
        carrier.relate("RecoveredBy", actor)
        if actor.tag("Panic"):
            actor.change("Panic", -1)
        self.log.append(f"{actor.name}从《{carrier.name}》恢复了一段独立主观记忆。")
        return claims

    def recover_memory(self, actor, carrier):
        if (
            carrier.parent is not actor.parent
            or not carrier.has("MemoryCarrier")
            or carrier.has("Hidden")
            or self._carrier_done(actor, carrier)
        ):
            self.log.append("隐藏、异地或已经恢复的记忆载体不能直接读取。")
            return False
        return self._submit(
            self._intent(
                actor,
                "recover_memory",
                carrier,
                45,
                lambda: self._recover_now(actor, carrier),
                f"恢复记忆：{carrier.name}",
                "玩家读取已揭示载体",
            )
        )

    def profession_action(self, actor):
        carrier = self._pending_carrier(actor)
        if carrier and carrier.parent is actor.parent and carrier.has("Hidden"):
            return self._submit(self._investigate_intent(actor, carrier))
        intent = self._profession_intent(actor)
        if intent is None:
            self.log.append("当前没有与该职业匹配的有限任务，行动不会生成。")
            return False
        return self._submit(intent)

    def collect(self, actor, item):
        objective = next(
            (
                objective
                for objective in self.objectives
                if objective.get("component") is item
            ),
            None,
        )
        if (
            not objective
            or objective.get("stage") != 4
            or objective.get("leader") is not actor
            or item.parent is not actor.parent
        ):
            return False
        return self._submit(self._collect_intent(actor, item, objective))

    def invoke_word(self, actor, recipe, target):
        inputs = (
            {"focus": self.tuning_fork}
            if recipe is self.calm_word
            else {"token": self.threshold_token}
        )

        def resolve():
            result = self.words.invoke(
                recipe,
                actor.souls[0],
                inputs,
                target=target,
                context={
                    "actor": actor,
                    "holder": self.team_inventory,
                    "world": self.world,
                },
            )
            self.log.append(
                f"{actor.name}施行言灵“{recipe.name}”。"
                if result
                else f"言灵“{recipe.name}”未成立：{self.words.last_failure}。"
            )
            return result

        return self._submit(
            self._intent(
                actor,
                "invoke_word",
                target,
                65,
                resolve,
                f"言灵：{recipe.name}",
                "言灵配方与现实状态共同复核",
            )
        )

    def establish_rule(self, actor, witness, operation, subject, target, scope):
        if (
            actor.parent is not self.scenes["契约审议室"]
            or witness.parent is not actor.parent
            or witness is actor
            or self.rule_ink.get("uses", 0) <= 0
        ):
            self.log.append(
                "规则设立需要在契约审议室由另一名在场者见证，并消耗红墨水。"
            )
            return False
        rule = Rule(
            E("true"),
            (Directive(Mode.FORBID, F(operation, subject, target)),),
            f"{actor.name}的规则设立",
        )

        def valid():
            return (
                actor.parent is self.scenes["契约审议室"]
                and witness.parent is actor.parent
                and self.rule_ink.get("uses", 0) > 0
            )

        def resolve():
            self.rule_ink.set("uses", self.rule_ink.get("uses") - 1)
            carrier = InformationCarrier(
                f"设立规则：禁止{subject.name}{operation}{getattr(target, 'name', target)}",
                "Manuscript",
                self.rule_archive,
                tags={"Law": 2, "Guarantee": 2},
            )
            publication = RulePublication(
                rule, actor, carrier=carrier, scope=scope, version=self.env.version
            )
            publication.relate("Witness", witness)
            patch = RulePatch(
                publication.name,
                rule,
                self.rule_archive,
                publication,
                scope,
                priority=actor.souls[0].tag("Law"),
            )
            patch.relate("Witness", witness)
            self.rule_patches.append(patch)
            self.runtime.patches.append(patch)
            for participant, confidence in ((actor, 3), (witness, 2)):
                participant.souls[0].receive(publication, confidence)
                participant.souls[0].understand(rule)
                participant.souls[0].accept(rule)
            self.coordination = Entity(
                "四院协同行动令",
                self.story_board,
                {"Coordination": 1, "Active": 1},
                properties={"issuer": actor, "witness": witness},
            )
            self.coordination.relate("IssuedBy", actor)
            self.coordination.relate("Witness", witness)
            self.log.append(
                f"{actor.name}与{witness.name}建立客观规则，并据此形成四院协同行动令。"
            )
            return patch

        return self._submit(
            self._intent(
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

    def _final_ready(self):
        recovered = sum(
            bool(carrier.related("RecoveredBy")) for carrier in self.memory_carriers
        )
        return bool(
            self.coordination
            and recovered >= 15
            and all(self._synthesis_for(objective) for objective in self.objectives)
            and self._synthesis_for(self.accident_inquiry)
            and all(
                objective.get("stage", 0) >= 5 and objective.tag("Verified")
                for objective in self.objectives
            )
            and all(item.parent is self.team_inventory for item in self.seal_components)
        )

    def _begin_rite_intent(self, actor):
        def resolve():
            rite = Entity(
                "进行中的四院真名再封印",
                self.rule_archive,
                {"Rite": 1, "Active": 1, "Seal": 1},
                properties={
                    "recipe": self.final_recipe,
                    "target": self.demon,
                    "phase": 0,
                },
            )
            rite.relate("Speaker", actor)
            for objective in self.objectives:
                rite.relate("Requires", objective)
            self.words.rites.append(rite)
            self.active_final_rite = rite
            self.log.append(f"{actor.name}在四院验证与历史证词齐备后建立最终 Rite。")
            return rite

        return self._intent(
            actor,
            "begin_rite",
            self.demon,
            50,
            resolve,
            "建立四院真名再封印",
            "四院验证、十五份载体、四件部件与协同行动令均成立",
            lambda: (
                self._final_ready()
                and actor.parent is self.scenes["核心隔离舱"]
                and self.active_final_rite is None
            ),
        )

    def begin_final_seal(self, actor):
        if not self._final_ready() or actor.parent is not self.scenes["核心隔离舱"]:
            self.log.append(
                "最终封印要求四院验证、至少十五份载体证词、四件部件与协同行动令。"
            )
            return False
        return self._submit(self._begin_rite_intent(actor))

    def _contribute_rite_intent(self, actor):
        rite = self.active_final_rite
        objective = self._objective_for_leader(actor)

        def resolve():
            actor.souls[0].change("Stability", -1)
            rite.relate("Contributor", actor, source=objective)
            objective.relate("ContributedBy", actor, source=rite)
            rite.set("phase", len(rite.related("Contributor")))
            self.env.assert_fact(E("seal_step", objective, actor))
            self.log.append(
                f"{actor.name}以{objective.name}的验证结果承担仪式第{rite.get('phase')}步。"
            )
            if len({facet.value for facet in rite.related("Contributor")}) >= 4:
                rite.tags.pop("Active", None)
                rite.change("Completed", 1)
                covenant = Entity(
                    "重立的真名封印契约",
                    self.rule_archive,
                    {"Covenant": 1, "Active": 1},
                    properties={"active": True},
                )
                covenant.attach(
                    "HardConstraint",
                    Rule(
                        E("sealed", self.demon),
                        (Directive(Mode.FORBID, F("return", self.demon, self.world)),),
                    ),
                )
                covenant.relate("CreatedBy", rite)
                self.words.contracts.append(covenant)
                self.env.assert_fact(E("sealed", self.demon))
                self.demon.reparent(None)
            return objective

        return self._intent(
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
                and actor.parent is self.scenes["核心隔离舱"]
                and not any(
                    facet.value is actor for facet in rite.related("Contributor")
                )
            ),
        )

    def advance_final_seal(self, actor):
        if not self.active_final_rite or self.active_final_rite.has("Completed"):
            return False
        intent = (
            self._contribute_rite_intent(actor)
            if self._objective_for_leader(actor)
            else None
        )
        return bool(intent and self._submit(intent))

    # ---- 状态派生、胜负与查询 -----------------------------------------

    def _refresh_objectives(self):
        for objective in self.objectives:
            if objective.get("stage", 0) != 0:
                continue
            synthesis = self._synthesis_for(objective)
            if synthesis is not None:
                for report in synthesis.related("UsesAnalysis"):
                    objective.relate("VerifiedAnalysis", report.value, source=synthesis)
                objective.set("stage", 1)
                objective.change("EvidenceReady", 1)
                self.log.append(
                    f"{objective.name}已由三份独立 Analysis 形成暂定 Synthesis，进入协作验证阶段。"
                )
        if all(objective.get("stage", 0) >= 5 for objective in self.objectives):
            for name in ("源质反应炉", "核心隔离舱"):
                scene = self.scenes[name]
                if scene.has("Locked"):
                    scene.tags.pop("Locked", None)
                    self.env.assert_fact(E("unlocked", scene))

    def _recovered_claims(self, story):
        claims = []
        for carrier in self.memory_carriers:
            if carrier.get("story") is not story or not carrier.related("RecoveredBy"):
                continue
            for carried in carrier.related("Carries"):
                memory = carried.value
                claims.extend(
                    (carrier, facet.value) for facet in memory.entries("Claim")
                )
        return tuple(claims)

    def _check_end(self):
        if self.demon.parent is None and self.env.evaluate(E("sealed", self.demon)):
            self.won = self.ended = True
            self.crisis.tags.pop("Active", None)
            contributors = (
                len(self.active_final_rite.related("Contributor"))
                if self.active_final_rite
                else 0
            )
            self.ending = f"四院防卫重新闭合。{contributors}名不同验证者共同承担仪式，学员保留已恢复的相互矛盾的记忆。"

    def _check_failure(self):
        if self.ended:
            return
        incapacitated = sum(
            actor.tag("Panic") >= 9 or actor.tag("Health") <= 0
            for actor in self.characters.values()
        )
        if (
            self.crisis.tag("Pressure") >= self.crisis.get("limit")
            or incapacitated >= 4
        ):
            self.ended = True
            self.won = False
            self.crisis.tags.pop("Active", None)
            cause = (
                "灰潮侵蚀时钟越过临界值"
                if self.crisis.tag("Pressure") >= self.crisis.get("limit")
                else "四名学员失去行动能力"
            )
            self.ending = f"封印失败：{cause}。恶魔没有违反临终契约，却利用迟疑耗尽了学园的协作窗口。"

    def wait(self, amount=60):
        target = self.timeline.now + amount
        progressed = False
        while (
            self.timeline.entries
            and self.timeline.entries[0][0] <= target
            and not self.ended
        ):
            self.timeline.next()
            progressed = True
        self.round += 1
        return progressed or not self.ended

    def known_memories(self, soul=None):
        soul = soul or self.player_soul
        return tuple(
            knowledge
            for knowledge in soul.children
            if knowledge.has("Knowledge") and knowledge.get("subject") in self.history
        )

    def local_people(self, actor=None):
        actor = actor or self.player
        return tuple(
            item for item in self.characters.values() if item.parent is actor.parent
        )

    def local_records(self, actor=None):
        actor = actor or self.player
        return tuple(
            item
            for item in actor.parent.children
            if isinstance(item, Entity) and item.has("MemoryCarrier")
        )

    def local_components(self, actor=None):
        actor = actor or self.player
        return tuple(
            item
            for item in actor.parent.children
            if isinstance(item, Entity) and item.has("SealComponent")
        )

    def reachable_scenes(self, actor=None):
        actor = actor or self.player
        return tuple(
            scene
            for scene in self.scenes.values()
            if scene is not actor.parent
            and self.world.route(actor.parent, scene, actor, self.env)
        )

    def locked_neighbors(self, actor=None):
        actor = actor or self.player
        return tuple(
            scene
            for scene in self.world.links.get(actor.parent, {})
            if scene.has("Locked")
        )

    def _same_scene(self, left, right):
        return (
            left.parent is right.parent
            and left.parent is not None
            and left.parent.has("Scene")
        )

    def _adjacent(self, left, right):
        return right in self.world.links.get(left, {})

    def _bond_strength(self, actor, target):
        if not isinstance(target, Entity):
            return 0
        carrier = self._story_carrier(actor)
        if not carrier or not self._carrier_done(actor, carrier):
            return 0
        return max(
            (
                facet.strength
                for facet in actor.related("PastBond")
                if facet.value is target
            ),
            default=0,
        )

    @staticmethod
    def _social_target(intent):
        if intent.effect.op in {
            "share_memory",
            "clarify_memory",
            "confront",
            "protect",
        }:
            return intent.effect.args[1]
        if intent.effect.op in {"answer_help", "assist"}:
            request = intent.effect.args[1]
            return request.get("requester") if isinstance(request, Entity) else None
        return None


__all__ = ["AcademyGame", "OBJECTIVE_SPECS"]

# 保证先导入本模块或先导入 academy 都得到同一个正式公开类。
import sys as _sys

if _academy_module := _sys.modules.get(f"{__package__}.academy"):
    _academy_module.AcademyGame = AcademyGame
