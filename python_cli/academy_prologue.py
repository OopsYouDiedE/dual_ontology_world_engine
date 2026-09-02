"""《异常感知学园》第一章：禁闭后的二十天。

初期篇只处理生存秩序、群像认识、威信形成、共同立规、月相研究与首个威胁。
旧版的四锚封印冲刺不会进入这段时间。人物、知识、规则、言灵和事件继续使用
核心 Entity / Knowledge / Intent / RulePatch / Recipe / Rite 表达。
"""

from __future__ import annotations

from core.python.engine import (
    ActionRuntime,
    Character,
    Directive,
    E,
    Entity,
    F,
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

from .academy_game import AcademyGame

DAY_SPECS = (
    (
        1,
        "醒来的人与广播里的恶魔",
        "朔后第2日·娥眉月",
        "life_support",
        ("临床医生", "机械工程师", "主厨"),
        ("侦探", "律师", "谁有资格发号施令"),
    ),
    (
        2,
        "十六只杯子与一座水塔",
        "朔后第3日·娥眉月",
        "water",
        ("博物学家", "机械工程师", "建筑师"),
        ("主厨", "化学家", "第一份饮水配给"),
    ),
    (
        3,
        "早餐桌上的多数决",
        "朔后第4日·娥眉月",
        "food",
        ("主厨", "职业棋手", "博物学家"),
        ("神偷", "主厨", "被顺走又归还的砂糖罐"),
    ),
    (
        4,
        "没有证物，不准把人送上绞架",
        "朔后第5日·娥眉月",
        "records",
        ("律师", "侦探", "画家"),
        ("诗人", "律师", "规矩会不会杀死直觉"),
    ),
    (
        5,
        "图书馆没有答案，只有目录",
        "朔后第6日·上弦前",
        "archive",
        ("考古学家", "画家", "神偷"),
        ("考古学家", "神偷", "谁动过封闭书架"),
    ),
    (
        6,
        "停在错误时刻的黄铜月钟",
        "朔后第7日·上弦",
        "lunar_clock",
        ("机械工程师", "建筑师", "音乐家"),
        ("建筑师", "机械工程师", "快修还是重新测绘"),
    ),
    (
        7,
        "第一次实验失败得很诚实",
        "朔后第8日·上弦",
        "resonance",
        ("音乐家", "诗人", "舞蹈艺术家"),
        ("音乐家", "诗人", "失败该算谁的"),
    ),
    (
        8,
        "允许玩乐也是一条生存规则",
        "朔后第9日·盈凸月",
        "morale",
        ("舞蹈艺术家", "神射手", "职业棋手"),
        ("画家", "临床医生", "给活人画像还是给遗物编号"),
    ),
    (
        9,
        "恶魔给出一个不能省略的否定词",
        "朔后第10日·盈凸月",
        "lexicon",
        ("律师", "考古学家", "诗人"),
        ("律师", "恶魔", "帮助是否等于取得信任"),
    ),
    (
        10,
        "低功率回声试验",
        "朔后第11日·盈凸月",
        "resonance",
        ("音乐家", "化学家", "机械工程师"),
        ("化学家", "音乐家", "看不见的结果算不算结果"),
    ),
    (
        11,
        "值日表第一次不是废纸",
        "朔后第12日·盈凸月",
        "duty_roster",
        ("职业棋手", "主厨", "律师"),
        ("职业棋手", "主厨", "天才是否可以免洗盘子"),
    ),
    (
        12,
        "没有案件的自由活动日",
        "朔后第13日·盈凸月",
        "morale",
        ("诗人", "神偷", "画家"),
        ("神射手", "舞蹈艺术家", "训练和游戏的界线"),
    ),
    (
        13,
        "三本历书写着三个满月时刻",
        "朔后第14日·望前",
        "moon_study",
        ("职业棋手", "考古学家", "建筑师"),
        ("侦探", "职业棋手", "误差是事故还是人为"),
    ),
    (
        14,
        "水压表在无人触碰时归零",
        "朔后第15日·望",
        "water",
        ("机械工程师", "侦探", "化学家"),
        ("神射手", "恶魔", "最方便的嫌疑人"),
    ),
    (
        15,
        "满月把旧传声管变成了喉咙",
        "朔后第16日·望",
        "life_support",
        ("音乐家", "机械工程师", "临床医生"),
        ("诗人", "音乐家", "听见同伴求救却找不到人"),
    ),
    (
        16,
        "三种假说，谁也不许抢先结案",
        "朔后第17日·亏凸月",
        "inquiry",
        ("侦探", "化学家", "职业棋手"),
        ("博物学家", "化学家", "承认错误和替人顶罪"),
    ),
    (
        17,
        "第一次公开质询",
        "朔后第18日·亏凸月",
        "inquiry",
        ("律师", "侦探", "神射手"),
        ("神偷", "律师", "规则能否保护被怀疑者"),
    ),
    (
        18,
        "回声标定",
        "朔后第19日·亏凸月",
        "resonance",
        ("音乐家", "诗人", "律师"),
        ("音乐家", "恶魔", "借用敌人的发音"),
    ),
    (
        19,
        "把正确猜测写成可执行规则",
        "朔后第20日·亏凸月",
        "charter",
        ("律师", "职业棋手", "机械工程师"),
        ("侦探", "律师", "临时规则何时失效"),
    ),
    (
        20,
        "第一场共同防卫",
        "朔后第21日·亏凸月",
        "containment",
        ("机械工程师", "音乐家", "神射手"),
        ("侦探", "恶魔", "盟友可以只持续到下一次日出"),
    ),
)


PROJECT_SPECS = {
    "life_support": (
        "维生总检",
        "公共诊疗所",
        ("临床医生", "机械工程师", "建筑师"),
        10,
    ),
    "water": ("上水与过滤", "上水库与水务站", ("机械工程师", "博物学家", "化学家"), 10),
    "food": ("配给与温室", "后勤院", ("主厨", "博物学家", "职业棋手"), 8),
    "records": ("公开证据台账", "中央厅", ("律师", "侦探", "画家"), 7),
    "archive": ("图书目录复原", "教学图书馆", ("考古学家", "画家", "神偷"), 9),
    "lunar_clock": ("黄铜月钟修复", "星盘讲堂", ("机械工程师", "建筑师", "音乐家"), 10),
    "resonance": ("回声标定实验", "共鸣厅", ("音乐家", "诗人", "舞蹈艺术家"), 12),
    "morale": (
        "共同生活与自由活动",
        "公共娱乐室",
        ("诗人", "神偷", "舞蹈艺术家", "画家"),
        8,
    ),
    "lexicon": ("恶魔语义词典", "契约审议室", ("律师", "考古学家", "诗人"), 8),
    "duty_roster": ("轮值与资源调度", "中央厅", ("职业棋手", "主厨", "律师"), 8),
    "moon_study": (
        "月相与事故时刻校勘",
        "星盘讲堂",
        ("职业棋手", "考古学家", "建筑师"),
        9,
    ),
    "inquiry": ("传声管异常调查", "中央厅", ("侦探", "律师", "化学家", "职业棋手"), 12),
    "charter": ("月相作业规程", "契约审议室", ("律师", "职业棋手", "机械工程师"), 8),
    "containment": (
        "传声管回声隔离",
        "动力总站",
        ("机械工程师", "音乐家", "神射手"),
        10,
    ),
}


APPROACH = {
    "侦探": ("inspect", "先封存读数，再追问矛盾"),
    "诗人": ("reframe", "把恐慌说成可以共同讨论的东西"),
    "音乐家": ("measure", "用节拍和音叉留下可重复的读数"),
    "博物学家": ("sample", "比较水、苔藓和活体反应"),
    "机械工程师": ("repair", "拆开设备并保留旧零件作为证物"),
    "主厨": ("ration", "用实际库存反驳空泛争论"),
    "考古学家": ("collate", "校勘版本差异而不挑最顺眼的一本"),
    "神偷": ("probe", "绕到规矩背后确认它保护了什么"),
    "律师": ("audit", "把主张改写成能够被反驳的句子"),
    "职业棋手": ("simulate", "先推演失去一个节点后的连锁后果"),
    "化学家": ("test", "设计会让错误假说当场失败的对照组"),
    "神射手": ("guard", "把危险方向和撤退路线一起标出"),
    "舞蹈艺术家": ("rehearse", "用身体走完协作顺序并找出拥堵"),
    "画家": ("record", "把肉眼差异固定成可供复核的图像"),
    "建筑师": ("survey", "先确认负载、方位和管线关系"),
    "临床医生": ("triage", "先区分不适、恐慌与真实暴露"),
}

LEISURE = {
    "侦探": "整理怀表与随手记录",
    "诗人": "在草场试写没有用途的新诗",
    "音乐家": "独自调音并听走廊回声",
    "博物学家": "照料采集箱里的普通植物",
    "机械工程师": "给修好的设备起绰号",
    "主厨": "试做不列入配给账的小点心",
    "考古学家": "翻看与事故无关的旧校刊",
    "神偷": "练习纸牌与不伤人的小把戏",
    "律师": "整理私人文具与当天措辞",
    "职业棋手": "摆残局或直接补觉",
    "化学家": "清洗玻璃器皿并记录无害反应",
    "神射手": "保养器械并做短距离训练",
    "舞蹈艺术家": "在空地练习基础步",
    "画家": "画没有调查价值的日常速写",
    "建筑师": "摆正公共区域的家具与标牌",
    "临床医生": "喝凉掉的茶并短暂打盹",
}

REPAIR_SCENES = (
    "生活院",
    "公共浴室与温泉池",
    "公共娱乐室",
    "后勤院",
    "公共诊疗所",
    "上水库与水务站",
    "动力总站",
    "教学图书馆",
    "中央厅",
    "训练院",
    "美术工房与冬园",
    "共鸣厅",
)


class AcademyPrologue:
    """二十天初期篇运行时；一天由数个有后果的群像决定组成。"""

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
        self.projects = {}
        for key, (name, scene_name, skills, need) in PROJECT_SPECS.items():
            self.projects[key] = Entity(
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
        """事故后的生活区和公共设施；修复是前二十日的日常压力。"""
        workplaces = tuple(actor.get("home") for actor in self.characters.values())
        damaged = tuple(
            dict.fromkeys(
                (
                    *(self.scenes[name] for name in REPAIR_SCENES),
                    *workplaces,
                )
            )
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
        """保存恶魔首日选择公开的三组真话与后续独立校验来源。"""
        self.demon.tags.update(
            {
                "HumanGuise": 1,
                "ManifestAnywhere": 1,
                "Suggestion": 1,
                "BroadcastInterference": 1,
                "CostlyTextEdit": 1,
            }
        )
        self.demon.properties.update(
            usual_presentation="无害、礼貌且主动提供有限帮助的人形",
            speech_constraint="所有说出口的命题必须为真；允许沉默与省略",
            text_edit_limit="每次只能修改少量文字，并支付契约代价",
        )
        closed = E("school_closed", self.world)
        survival = E("maintains_basic_survival", self.demon, self.resources)
        truthful = E("must_speak_truth", self.demon)
        escape = E("can_help_escape_island", self.demon, self.world)
        for fact in (closed, survival, truthful, escape):
            self.env.assert_fact(fact)
        self.demon_opening_statements = (
            InformationCarrier(
                "广播真话一：封闭事实与最低生存契约",
                "Broadcast",
                self.scenes["中央厅"],
                creator=self.demon,
                claims=(closed, survival),
                tags={"Truth": 1, "Guarantee": 1},
            ),
            InformationCarrier(
                "广播真话二：恶魔发言不得为假",
                "Broadcast",
                self.scenes["中央厅"],
                creator=self.demon,
                claims=(truthful,),
                tags={"Truth": 1, "Guarantee": 1},
            ),
            InformationCarrier(
                "广播真话三：恶魔能够协助逃岛",
                "Broadcast",
                self.scenes["中央厅"],
                creator=self.demon,
                claims=(escape,),
                tags={"Truth": 1, "Guarantee": 1},
            ),
        )
        self.occult_primer = InformationCarrier(
            "《神秘学导论·恶魔契约章》",
            "Manuscript",
            self.scenes["教学图书馆"],
            creator=self.base.mentors[0],
            claims=(survival, truthful, escape),
            tags={"Hidden": 1, "Law": 2, "Mind": 2, "IndependentSource": 1},
        )
        self.pre_school_demon_lore = Entity(
            "入学前流传的恶魔常识",
            self.world,
            {"BackgroundKnowledge": 1},
        )
        truth_rule = Rule(
            E("true"),
            (Directive(Mode.FORBID, F("speak_false", self.demon, self.world)),),
            "恶魔发言不得为假",
        )
        self.base.demon_covenant.attach("HardConstraint", truth_rule)

    def _build_lethal_mechanism(self):
        scene = self.scenes["废物处理站"]
        self.lethal_mechanism = Entity(
            "废物处理站旧式配重焚化闸",
            scene,
            {"Machine": 1, "HiddenDanger": 1, "LethalMechanism": 1, "Damage": 4},
            properties={
                "connected_to_exit_service_tunnel": True,
                "restores_waste_and_water_pressure": True,
                "safety_interlock_known": False,
                "disabled": False,
            },
        )
        # 前二十日只开放一次机会；放在第19日，使普通路线即使已确认真话规则，
        # 仍来不及掌握这台隐藏机关的安全联锁。
        self.demon_attack_window = (19, 19)
        self.pair_maintenance_rule = None

    def _give_knowledge(self, soul, statement, stage="received", confidence=1):
        claims = tuple(facet.value for facet in statement.entries("Claim"))
        return soul.learn(
            self.demon,
            claims,
            source=statement,
            confidence=confidence,
            stage=stage,
            name=f"{soul.name}接收的{statement.name}",
        )

    def _build_sources(self):
        sources = {
            "ledger": InformationCarrier(
                "二十日维生账",
                "Ledger",
                self.resources,
                tags={"Law": 1, "Preservation": 2},
            ),
            "almanac": InformationCarrier(
                "三版本月相历书",
                "Manuscript",
                self.scenes["教学图书馆"],
                tags={"Threshold": 2, "Vestige": 1},
            ),
            "gauge": InformationCarrier(
                "归零前后的水压纸带",
                "Chart",
                self.scenes["上水库与水务站"],
                tags={"Mind": 2, "Form": 1},
            ),
            "phonograph": InformationCarrier(
                "传声管反向录音蜡筒",
                "Cylinder",
                self.scenes["声纹与记录室"],
                tags={"Mind": 2, "Name": 1},
            ),
            "demon_clause": InformationCarrier(
                "恶魔提供的否定词铜版",
                "Manuscript",
                self.scenes["契约审议室"],
                tags={"Name": 2, "Law": 2},
            ),
            "resonator": Entity(
                "校准音叉与黄铜共鸣架",
                self.scenes["共鸣厅"],
                {"Instrument": 1, "Mind": 2, "Form": 2},
            ),
            "clock": Entity(
                "停在错误时刻的黄铜月钟",
                self.scenes["星盘讲堂"],
                {"Machine": 1, "Damaged": 2, "Threshold": 1},
            ),
        }
        return sources

    def _build_hypotheses(self):
        return {
            "demon": Entity(
                "假说A：恶魔主动破坏维生系统",
                self.world,
                {"Hypothesis": 1},
                properties={"key": "demon"},
            ),
            "machine": Entity(
                "假说B：水务机械自然故障",
                self.world,
                {"Hypothesis": 1},
                properties={"key": "machine"},
            ),
            "lunar": Entity(
                "假说C：月相令旧传声回路发生反馈",
                self.world,
                {"Hypothesis": 1},
                properties={"key": "lunar"},
            ),
        }

    def _build_echo_word(self):
        return Recipe(
            "回声标定",
            slots={
                "resonator": {"tags": {"Instrument": 1}},
                "almanac": {"tags": {"Threshold": 2}},
                "negative": {"tags": {"Name": 2, "Law": 2}},
            },
            thresholds={"Mind": 3, "Name": 3, "Law": 2, "Threshold": 2},
            requirements=(
                lambda _bindings: self.env.evaluate(E("lunar_window", self.threat)),
                lambda _bindings: self.projects["lunar_clock"].tag("Progress") >= 2,
                lambda _bindings: self.projects["resonance"].tag("Progress") >= 4,
                lambda _bindings: self.projects["lexicon"].tag("Progress") >= 3,
                lambda _bindings: self.hypotheses["lunar"].tag("Support") >= 7,
                lambda _bindings: bool(
                    self.sources["demon_clause"].related("ProvidedBy", self.demon)
                ),
            ),
            effects=(F("change", self.threat, "Veil", -2),),
            parent=self.scenes["共鸣厅"],
            tags={"Incantation": 1, "Kotodama": 1, "ResearchWord": 1},
        )

    def trust(self, actor, target):
        return sum(facet.strength for facet in actor.related("Trusts", target))

    def _trust(self, actor, target, amount, source=None):
        if amount > 0:
            actor.relate("Trusts", target, amount, source=source)
        elif amount < 0:
            actor.relate("Distrusts", target, -amount, source=source)

    def _snapshot(self):
        data = {
            "日": self.day,
            "资源.食物日": self.resources.tag("FoodDays"),
            "资源.饮水日": self.resources.tag("WaterDays"),
            "资源.动力日": self.resources.tag("PowerDays"),
            "灰潮.压力": self.gray_tide.tag("Pressure"),
            "首个威胁.遮蔽": self.threat.tag("Veil"),
            "首个威胁.显现": self.threat.tag("Manifested"),
            "首个威胁.隔离": self.threat.tag("Contained"),
            "恶魔.可靠度": self.demon.tag("Reliability"),
            "规则.数量": len(self.rule_patches),
            "知识.分析数": len(self.analyses),
        }
        for key, project in self.projects.items():
            data[f"项目.{key}.进展"] = project.tag("Progress")
        for key, hypothesis in self.hypotheses.items():
            data[f"假说.{key}.支持"] = hypothesis.tag("Support")
            data[f"假说.{key}.反证"] = hypothesis.tag("Contradiction")
        for role, actor in self.characters.items():
            data[f"威信.{role}"] = actor.tag("Authority")
            data[f"关系.{role}.信任"] = sum(
                facet.strength for facet in actor.related("Trusts")
            )
            data[f"关系.{role}.不信任"] = sum(
                facet.strength for facet in actor.related("Distrusts")
            )
        return data

    @staticmethod
    def _diff(before, after):
        return tuple(
            f"{key}: {before.get(key)} → {value}"
            for key, value in after.items()
            if before.get(key) != value
        )

    def _witnesses(self, _event, context):
        participants = context.get("participants", ())
        return tuple(
            actor.souls[0]
            for actor in participants
            if isinstance(actor, Entity) and actor.souls
        )

    def _intent(
        self,
        actor,
        operation,
        target,
        label,
        resolve,
        reason,
        cost=20,
        response_mode=None,
    ):
        return Intent(
            actor,
            actor.souls[0],
            F(operation, actor, target),
            cost,
            resolve,
            label,
            response_mode=response_mode,
        ), reason

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
                    (
                        E("must_speak_truth", self.demon),
                        E("can_omit_truth", self.demon),
                        E("can_adjust_emotion", self.demon),
                        E("can_manifest_anywhere", self.demon, self.world),
                    ),
                    source=self.pre_school_demon_lore,
                    confidence=3,
                    stage="accepted",
                    name=f"{student.name}入学前掌握的恶魔基本定律",
                )
                self._add_social_impact(
                    student,
                    self.demon,
                    self.pre_school_demon_lore,
                    {
                        "bond": 0,
                        "malicious_intent": True,
                        "threat": 2,
                        "norm_breach": True,
                    },
                )
            elif level == 2:
                student.souls[0].learn(
                    self.demon,
                    (E("has_dangerous_reputation", self.demon),),
                    source=self.pre_school_demon_lore,
                    confidence=1,
                    stage="accepted",
                    name=f"{student.name}听说过的恶魔坏名声",
                )

    def _observe_social_outcome(self, event, context):
        effect, actor = event.get("effect"), event.get("actor")
        if effect is None or not isinstance(actor, Character):
            return
        operation = effect.op
        created = []
        if operation == "publish_true_offer":
            for receiver in self.characters.values():
                created.append(
                    self._add_social_impact(
                        receiver,
                        actor,
                        event,
                        {
                            "bond": self.trust(receiver, actor),
                            "credible_but_unverified_offer": True,
                        },
                    )
                )
        elif operation in set(item[0] for item in APPROACH.values()):
            for receiver in context.get("participants", ()):
                if receiver is not actor and isinstance(receiver, Character):
                    created.append(
                        self._add_social_impact(
                            receiver,
                            actor,
                            event,
                            {
                                "bond": self.trust(receiver, actor),
                                "support_received": True,
                            },
                        )
                    )
        elif operation == "confide":
            target = effect.args[1]
            created.extend(
                (
                    self._add_social_impact(
                        actor,
                        target,
                        event,
                        {
                            "bond": self.trust(actor, target),
                            "vulnerability_accepted": True,
                        },
                    ),
                    self._add_social_impact(
                        target,
                        actor,
                        event,
                        {"bond": self.trust(target, actor), "support_received": True},
                    ),
                )
            )
        elif operation == "challenge":
            receiver = effect.args[1]
            bond = self.trust(receiver, actor)
            created.append(
                self._add_social_impact(
                    receiver,
                    actor,
                    event,
                    {
                        "bond": bond,
                        "truth_conflict": bond >= 1,
                        "norm_breach": bond < 1,
                    },
                )
            )
        elif operation == "collaborate":
            target = effect.args[1]
            created.extend(
                (
                    self._add_social_impact(
                        actor,
                        target,
                        event,
                        {
                            "bond": self.trust(actor, target),
                            "shared_objective_advanced": True,
                        },
                    ),
                    self._add_social_impact(
                        target,
                        actor,
                        event,
                        {
                            "bond": self.trust(target, actor),
                            "shared_objective_advanced": True,
                        },
                    ),
                )
            )
        created = tuple(item for item in created if item is not None)
        if created:
            event.set("social_impacts", created)

    def _choose(self, options, scene, participants=()):
        """比较不同人物的候选 Intent，保存未选方案、规则淘汰与状态差分。"""
        assessed, blocked = [], []
        domains = (self.world, scene)
        for intent, base, reason in options:
            allowed, _ = self.runtime.review(intent, {"domains": domains})
            decision = intent.mind.assess(
                (intent,),
                self.rules,
                self.env,
                lambda _intent, base=base: base,
                context={"domains": domains, "source": intent.actor},
            )[0]
            social = tuple(
                (amount, source.get("combination"))
                for amount, source in decision.preferences
                if isinstance(source, Entity) and source.has("SocialImpact")
            )
            record = {
                "actor": intent.actor.name,
                "operation": intent.effect.op,
                "label": intent.label,
                "score": decision.score,
                "reason": reason,
                "rule": decision.reason,
                "social_pressure": social,
            }
            if allowed:
                assessed.append((decision.score, intent, reason, record))
            else:
                blocked.append(record)
        if not assessed:
            raise RuntimeError(f"第{self.day}日没有可执行候选：{scene.name}")
        assessed.sort(key=lambda item: item[0], reverse=True)
        score, intent, reason, selected = assessed[0]
        everyone = tuple(dict.fromkeys((intent.actor, *participants)))
        for actor in everyone:
            if actor is not self.demon:
                actor.reparent(scene)
        before = self._snapshot()
        day_event_count = len(
            [event for event in self.runtime.events if event.get("day") == self.day]
        )
        self.timeline.now = (self.day - 1) * 200 + min(day_event_count, 5) * 20
        if not self.runtime.resolve(
            intent, {"domains": domains, "participants": everyone}
        ):
            raise RuntimeError(f"第{self.day}日行动结算失败：{intent.label}")
        after = self._snapshot()
        event = self.runtime.events[-1]
        event.set("day", self.day)
        event.set("moon", DAY_SPECS[self.day - 1][2])
        event.set("scene", scene)
        event.set("decision_score", score)
        social_reason = "、".join(
            f"{combination}:{amount:+g}"
            for amount, combination in selected["social_pressure"]
        )
        event.set(
            "decision_reason",
            reason + (f"；目标情绪={social_reason}" if social_reason else ""),
        )
        event.set("candidates", tuple(item[3] for item in assessed))
        event.set("blocked_candidates", tuple(blocked))
        event.set("state_changes", self._diff(before, after))
        return event

    def _work(self, project_key, roles):
        project = self.projects[project_key]
        scene = project.get("scene")
        options = []
        for role in roles:
            actor = self.characters[role]
            operation, method = APPROACH[role]
            skill = 18 if role in project.get("skills") else 5
            authority = actor.tag("Authority") * 2

            def resolve(actor=actor, role=role, method=method):
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
                self.log.append(
                    f"第{self.day}日：{actor.name}{method}，{project.name}获得可复核进展。"
                )
                return project

            intent, reason = self._intent(
                actor,
                operation,
                project,
                f"{actor.name}推进《{project.name}》",
                resolve,
                f"职业方法={method}；技能匹配={role in project.get('skills')}；既有威信={actor.tag('Authority')}",
            )
            options.append((intent, 40 + skill + authority, reason))
        return self._choose(
            options, scene, tuple(self.characters[role] for role in roles)
        )

    def _social(self, actor_role, target_role, topic):
        actor, target = (
            self.characters[actor_role],
            self.demon if target_role == "恶魔" else self.characters[target_role],
        )
        scene = (
            self.scenes["公共娱乐室"]
            if target is not self.demon
            else self.scenes["中央厅"]
        )
        tension = any(
            word in topic for word in ("资格", "责任", "可疑", "嫌疑", "错误", "失效")
        )
        vulnerable = any(
            word in topic for word in ("恐惧", "求救", "信任", "顶罪", "画像")
        )
        options = []

        def confide():
            self._trust(actor, target, 2, topic)
            if target is not self.demon:
                self._trust(target, actor, 1, topic)
            actor.change("Candor", 1)
            self.log.append(
                f"第{self.day}日：{actor.name}没有套话，向{target.name}说出自己对“{topic}”真正害怕的部分。"
            )
            return target

        def challenge():
            actor.change("Insight", 1)
            self._trust(actor, target, -1, topic)
            actor.relate("Questioned", target, source=topic)
            self.log.append(
                f"第{self.day}日：{actor.name}当面质疑{target.name}关于“{topic}”的说法，关系变紧但留下可检查的问题。"
            )
            return target

        def collaborate():
            self._trust(actor, target, 1, topic)
            if target is not self.demon:
                self._trust(target, actor, 1, topic)
            actor.change("Authority", 1)
            self.log.append(
                f"第{self.day}日：{actor.name}与{target.name}把“{topic}”变成一次共同任务，避免空谈立场。"
            )
            return target

        biases = {
            "confide": 64 if vulnerable else 45,
            "challenge": 66 if tension else 42,
            "collaborate": 62 if not tension else 50,
        }
        for operation, resolver, phrase in (
            ("confide", confide, "公开自身风险换取对方回应"),
            ("challenge", challenge, "承受关系损耗以取得可证伪问题"),
            ("collaborate", collaborate, "用共同劳动代替口头站队"),
        ):
            intent, reason = self._intent(
                actor,
                operation,
                target,
                f"{actor.name}与{target.name}谈：{topic}",
                resolver,
                f"{phrase}；当前信任={self.trust(actor, target)}",
                response_mode={
                    "confide": ResponseMode.NEGOTIATE,
                    "challenge": ResponseMode.RESTRAIN,
                    "collaborate": ResponseMode.COOPERATE,
                }[operation],
            )
            options.append(
                (intent, biases[operation] + self.trust(actor, target), reason)
            )
        return self._choose(options, scene, (actor, target))

    def _support(self, actor, source, hypothesis_key, amount=1, contradiction=False):
        hypothesis = self.hypotheses[hypothesis_key]
        knowledge = Knowledge(
            f"{actor.name}分析《{source.name}》",
            actor.souls[0],
            ("contradicts" if contradiction else "supports", hypothesis, source),
            subject=hypothesis,
            source=source,
            tags={"Analysis": 1, "Mind": 2},
        )
        relation = "Contradicts" if contradiction else "Supports"
        knowledge.relate(relation, hypothesis)
        hypothesis.change("Contradiction" if contradiction else "Support", amount)
        hypothesis.relate("Evidence", source, amount, source=knowledge)
        actor.souls[0].relate("Considers", hypothesis, amount, source=knowledge)
        self.analyses.append(knowledge)
        return knowledge

    def _special_1(self):
        scene = self.scenes["中央厅"]
        actor = self.demon

        def resolve():
            offer = Entity(
                "恶魔的首日潜在盟友提议",
                self.world,
                {"Proposal": 1, "Provisional": 1, "Active": 1},
                properties={
                    "strategy": "只公开有利于建立盟友位置的真话；省略封闭原因和自身目标",
                    "accepted": False,
                },
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

        intent, reason = self._intent(
            actor,
            "publish_true_offer",
            self.resources,
            "恶魔以三组真话提出潜在合作",
            resolve,
            "三组命题都为真，且选择性公开能把恶魔放到危险但有用的潜在盟友位置",
            response_mode=ResponseMode.NEGOTIATE,
        )
        self._choose(((intent, 100, reason),), scene, tuple(self.characters.values()))

    def _special_4(self):
        lawyer, witness = self.characters["律师"], self.characters["侦探"]
        scene = self.scenes["契约审议室"]

        def establish():
            directives = tuple(
                Directive(Mode.FORBID, F("accuse_without_evidence", actor, self.demon))
                for actor in self.characters.values()
            )
            rule = Rule(E("true"), directives, "公开指控必须附带可检查材料")
            carrier = InformationCarrier(
                "第一版共同调查守则",
                "Manuscript",
                scene,
                tags={"Law": 2, "Guarantee": 2},
            )
            publication = RulePublication(
                rule, lawyer, carrier=carrier, scope=self.world, version=1
            )
            publication.relate("Witness", witness)
            patch = RulePatch(
                publication.name, rule, scene, publication, self.world, priority=3
            )
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
            self.log.append(
                "第4日：全员接受第一条共同规则：怀疑可以公开，定罪式指控必须交出材料。"
            )
            return patch

        def command():
            lawyer.change("Authority", 1)
            return Entity("律师单方面发布的禁言命令", scene, {"RejectedProposal": 1})

        options = []
        for operation, resolver, score, reason in (
            ("establish_rule", establish, 82, "侦探见证、文本公开且所有人保留质疑权"),
            ("issue_order", command, 48, "立即压住争吵，但没有授权来源和失效条件"),
        ):
            intent, why = self._intent(
                lawyer,
                operation,
                self.world,
                "建立证据指控规则"
                if operation == "establish_rule"
                else "单方面禁止公开怀疑",
                resolver,
                reason,
            )
            options.append((intent, score, why))
        self._choose(options, scene, tuple(self.characters.values()))

    def _special_5(self):
        archaeologist = self.characters["考古学家"]
        scene = self.scenes["教学图书馆"]

        def verify():
            self.occult_primer.tags.pop("Hidden", None)
            claims = tuple(facet.value for facet in self.occult_primer.entries("Claim"))
            for student in self.characters.values():
                student.souls[0].learn(
                    self.demon,
                    claims,
                    source=self.occult_primer,
                    confidence=3,
                    stage="accepted",
                    name=f"{student.name}由《神秘学导论》确认的恶魔契约知识",
                )
            for statement in self.demon_opening_statements:
                statement.relate("VerifiedBy", self.occult_primer)
            archaeologist.change("Authority", 1)
            self.log.append(
                "第5日：《神秘学导论》独立确认首日三组广播全部为真；"
                "众人同时确认真话可以被恶意裁剪，验证没有自动转化为信任。"
            )
            return self.occult_primer

        intent, reason = self._intent(
            archaeologist,
            "verify_demon_statements",
            self.occult_primer,
            "用《神秘学导论》校验恶魔首日广播",
            verify,
            "教材来源独立于恶魔，并分别支持最低生存契约、真话限制与逃岛能力",
            response_mode=ResponseMode.VERIFY,
        )
        self._choose(((intent, 96, reason),), scene, tuple(self.characters.values()))

    def _special_7(self):
        musician = self.characters["音乐家"]
        scene = self.scenes["共鸣厅"]

        def attempt():
            result = self.words.invoke(
                self.echo_word,
                musician.souls[0],
                {"resonator": self.sources["resonator"]},
                target=self.threat,
                context={"actor": musician, "world": self.world},
            )
            failure = self.words.last_failure
            record = Entity(
                "第一次回声标定试验记录",
                scene,
                {"Experiment": 1, "Failed": int(result is None)},
                properties={"failure": failure},
            )
            self.word_attempts.append(record)
            self.projects["resonance"].change("Evidence", 2)
            musician.change("Authority", 1)
            self.log.append(
                f"第7日：言灵没有发动。音乐家坚持把“{failure}”逐项写进记录，没有用气氛掩盖失败。"
            )
            return record

        intent, reason = self._intent(
            musician,
            "test_word",
            self.threat,
            "第一次言灵实验：失败记录",
            attempt,
            "先允许失败暴露缺失槽位，避免把偶然现象误认成成功",
        )
        self._choose(
            ((intent, 90, reason),),
            scene,
            (musician, self.characters["诗人"], self.characters["舞蹈艺术家"]),
        )

    def _special_9(self):
        scene = self.scenes["契约审议室"]

        def translate():
            self.sources["demon_clause"].reparent(scene)
            self.sources["demon_clause"].relate("ProvidedBy", self.demon)
            self.projects["lexicon"].change("Progress", 3)
            self.demon.change("Reliability", 2)
            self._support(
                self.characters["律师"],
                self.sources["demon_clause"],
                "demon",
                1,
                contradiction=True,
            )
            self.log.append(
                "第9日：恶魔补出导师笔记里被刮掉的否定词，并允许律师把它写成可追责的公开译本。"
            )
            return self.sources["demon_clause"]

        intent, reason = self._intent(
            self.demon,
            "translate_binding_term",
            self.sources["demon_clause"],
            "恶魔公开关键否定词",
            translate,
            "广播语义若继续损坏，恶魔也将失去唯一合法发言媒介",
        )
        self._choose(
            ((intent, 95, reason),),
            scene,
            (
                self.characters["律师"],
                self.characters["考古学家"],
                self.characters["诗人"],
            ),
        )

    def _special_13(self):
        chess = self.characters["职业棋手"]
        scene = self.scenes["星盘讲堂"]

        def compare():
            knowledge = self._support(chess, self.sources["almanac"], "lunar", 3)
            self.hypotheses["machine"].change("Contradiction", 1)
            chess.change("Authority", 2)
            self.log.append(
                "第13日：棋手把三本历书当成三盘互相矛盾的棋谱，算出仪表漂移会在满月重复，而非随机磨损。"
            )
            return knowledge

        intent, reason = self._intent(
            chess,
            "compare_almanacs",
            self.hypotheses["lunar"],
            "比较三本历书的误差模式",
            compare,
            "三份错误若以固定周期偏移，误差本身就是证据",
        )
        self._choose(
            ((intent, 92, reason),),
            scene,
            (chess, self.characters["考古学家"], self.characters["建筑师"]),
        )

    def _special_14(self):
        detective = self.player
        scene = self.scenes["上水库与水务站"]

        def inspect():
            self.threat.tags.pop("Dormant", None)
            self.threat.change("Trace", 1)
            a = self._support(detective, self.sources["gauge"], "machine", 2)
            self._support(
                detective, self.sources["gauge"], "demon", 1, contradiction=True
            )
            self.log.append(
                "第14日：水压归零时封条完整，恶魔广播也未经过水务线；侦探保留机械故障假说，同时削弱恶魔破坏说。"
            )
            return a

        intent, reason = self._intent(
            detective,
            "inspect_anomaly",
            self.sources["gauge"],
            "封存无人触碰的水压纸带",
            inspect,
            "先记录封条、时刻与管线状态，不因最方便的嫌疑人跳过现场",
        )
        self._choose(
            ((intent, 94, reason),),
            scene,
            (detective, self.characters["机械工程师"], self.characters["化学家"]),
        )

    def _special_15(self):
        scene = self.scenes["生活院"]

        def manifest():
            self.threat.change("Manifested", 1)
            self.gray_tide.change("Pressure", 1)
            self.resources.change("WaterDays", -2)
            self.sources["phonograph"].reparent(scene)
            self._support(
                self.characters["音乐家"], self.sources["phonograph"], "lunar", 2
            )
            self.log.append(
                "第15日：满月时，旧传声管依次模仿四名学员求救，并用他们的声音命令水阀关闭；没有人受伤，饮水储备损失两日。"
            )
            return self.threat

        intent, reason = self._intent(
            self.demon,
            "announce_external_anomaly",
            self.threat,
            "恶魔中断广播并标出伪造声源",
            manifest,
            "伪声正在占用它赖以履约的广播层；揭露威胁符合双方利益",
        )
        self._choose(((intent, 100, reason),), scene, tuple(self.characters.values()))

    def _special_16(self):
        scene = self.scenes["中央厅"]
        options = []
        evidence = (
            (
                "侦探",
                "lunar",
                self.sources["phonograph"],
                78,
                False,
                "反向录音与满月时刻一致",
            ),
            (
                "化学家",
                "machine",
                self.sources["gauge"],
                65,
                False,
                "压力变化仍可能来自阀门故障",
            ),
            (
                "职业棋手",
                "demon",
                self.sources["demon_clause"],
                74,
                True,
                "恶魔若主动切断广播会同时损害自身合法行动",
            ),
        )
        for role, hypothesis, source, score, contradiction, reason in evidence:
            actor = self.characters[role]

            def resolve(
                actor=actor,
                hypothesis=hypothesis,
                source=source,
                contradiction=contradiction,
            ):
                result = self._support(actor, source, hypothesis, 2, contradiction)
                actor.change("Authority", 1)
                return result

            intent, why = self._intent(
                actor,
                "analyze_evidence",
                self.hypotheses[hypothesis],
                f"{actor.name}提交可反驳的异常解释",
                resolve,
                reason,
            )
            options.append((intent, score + actor.tag("Authority"), why))
        self._choose(
            options, scene, tuple(self.characters[role] for role, *_ in evidence)
        )

    def _special_17(self):
        scene = self.scenes["契约审议室"]
        shooter, detective, lawyer = (
            self.characters["神射手"],
            self.player,
            self.characters["律师"],
        )

        def accuse():
            self._trust(shooter, self.demon, -2, self.threat)
            return self.demon

        def present():
            result = self._support(detective, self.sources["phonograph"], "lunar", 2)
            detective.change("Authority", 2)
            self.log.append(
                "第17日：侦探没有替恶魔作人格担保，只证明现有材料更支持月相反馈；神射手的拘束主张因没有载体被共同规则挡下。"
            )
            return result

        def defend():
            self.hypotheses["demon"].change("Contradiction", 1)
            lawyer.change("Authority", 1)
            return self.hypotheses["demon"]

        options = []
        for actor, operation, target, resolver, score, reason, label in (
            (
                shooter,
                "accuse_without_evidence",
                self.demon,
                accuse,
                96,
                "恐惧给出高冲动效用，但违反第4日共同规则",
                "要求立刻拘束恶魔",
            ),
            (
                detective,
                "present_evidence",
                self.hypotheses["lunar"],
                present,
                84,
                "蜡筒、历书和水压纸带可以被其他人复核",
                "提交月相反馈证据链",
            ),
            (
                lawyer,
                "argue_exculpation",
                self.hypotheses["demon"],
                defend,
                76,
                "恶魔并非无罪，只是当前破坏假说与其利益冲突",
                "限定恶魔嫌疑的适用范围",
            ),
        ):
            intent, why = self._intent(
                actor, operation, target, label, resolver, reason
            )
            options.append((intent, score, why))
        self._choose(options, scene, tuple(self.characters.values()))

    def _special_18(self):
        musician = self.characters["音乐家"]
        scene = self.scenes["共鸣厅"]
        self.env.assert_fact(E("lunar_window", self.threat))

        def invoke():
            rite = self.words.invoke(
                self.echo_word,
                musician.souls[0],
                {
                    "resonator": self.sources["resonator"],
                    "almanac": self.sources["almanac"],
                    "negative": self.sources["demon_clause"],
                },
                target=self.threat,
                context={"actor": musician, "holder": scene, "world": self.world},
            )
            if rite is None:
                raise RuntimeError(f"回声标定未成立：{self.words.last_failure}")
            self.threat.change("Exposed", 1)
            self.hypotheses["lunar"].change("Support", 3)
            musician.change("Authority", 2)
            rite.relate("Contributor", self.characters["诗人"])
            rite.relate("Contributor", self.characters["律师"])
            rite.relate("Contributor", self.demon)
            self.log.append(
                "第18日：音乐家施行真实言灵“回声标定”。诗人守拍，律师核词，恶魔提供否定发音；伪声显出来自月钟而非核心。"
            )
            return rite

        intent, reason = self._intent(
            musician,
            "invoke_word",
            self.threat,
            "言灵：回声标定",
            invoke,
            "失败记录补齐槽位；亏凸月仍满足观测窗；四方投入可追溯",
        )
        self._choose(
            ((intent, 110, reason),),
            scene,
            (musician, self.characters["诗人"], self.characters["律师"], self.demon),
        )

    def _special_19(self):
        lawyer, witness = self.characters["律师"], self.characters["职业棋手"]
        scene = self.scenes["契约审议室"]

        def establish():
            clock = self.sources["clock"]
            rule = Rule(
                E("true"),
                tuple(
                    Directive(Mode.FORBID, F("operate_unlogged", actor, clock))
                    for actor in self.characters.values()
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
                rule, lawyer, carrier=carrier, scope=self.world, version=2
            )
            publication.relate("Witness", witness)
            patch = RulePatch(
                publication.name, rule, scene, publication, self.world, priority=4
            )
            patch.relate("BasedOn", self.sources["almanac"])
            patch.relate("BasedOn", self.sources["phonograph"])
            patch.relate("Witness", witness)
            self.rule_patches.append(patch)
            self.runtime.patches.append(patch)
            self.lunar_rule = patch
            lawyer.change("Authority", 2)
            witness.change("Authority", 1)
            self.log.append(
                "第19日：临时月相规程成立。它只管望日前后三日、只管共鸣设备，并写明第30日必须复审。"
            )
            return patch

        intent, reason = self._intent(
            lawyer,
            "establish_rule",
            self.sources["clock"],
            "设立临时月相作业规程",
            establish,
            "证据链、作用对象、月相窗口、见证者和失效复审日均明确",
        )
        self._choose(((intent, 105, reason),), scene, tuple(self.characters.values()))

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

    def _discover_death(self):
        victim = self._fatal_victim()
        if victim is None or self.death_discovery is not None:
            return self.death_discovery
        scene = self.lethal_mechanism.parent
        discoverer = self._alive_role("主厨", "诗人", "舞蹈艺术家")
        doctor = self._alive_role("临床医生", "博物学家")
        investigator = self._alive_role("侦探", "律师")
        engineer = self._alive_role("机械工程师", "建筑师")

        def discover():
            evidence = Entity(
                f"{victim.name}死亡现场的第一轮记录",
                scene,
                {"Evidence": 1, "DeathEvidence": 1, "Public": 1},
                properties={
                    "victim": victim,
                    "mechanism": self.lethal_mechanism,
                    "confirmed": ("死亡", "机关已经触发", "死者曾独自进入"),
                    "not_confirmed": (
                        "恶魔是否诱导",
                        "是否存在第三方",
                        "死者当时的完整动机",
                    ),
                },
            )
            evidence.relate("BodyOf", victim)
            evidence.relate("FoundAt", scene)
            evidence.relate("Records", self.lethal_mechanism)
            self.death_evidence.append(evidence)
            for role, student in self.characters.items():
                if student.has("Dead"):
                    continue
                fear = (
                    2 if student.tag("Occult") >= 3 or role in {"神射手", "诗人"} else 1
                )
                student.change("Panic", fear)
                Influence(
                    f"{student.name}面对首名死者的恐慌",
                    "Fear",
                    fear,
                    student.souls[0],
                    target=victim,
                    source=evidence,
                    mode="death_shock",
                    persistence=2,
                    started_at=self.timeline.now,
                    expires_at=self.timeline.now + 400,
                )
                student.souls[0].learn(
                    victim,
                    (
                        E("dead", victim),
                        E("found_at", victim, scene),
                        E("lethal_mechanism_triggered", self.lethal_mechanism, victim),
                    ),
                    source=evidence,
                    confidence=3,
                    stage="accepted",
                    name=f"{student.name}得知的死亡事实",
                )
                if student.tag("Occult") >= 3:
                    student.souls[0].learn(
                        self.demon,
                        (E("could_have_induced", self.demon, victim),),
                        source=evidence,
                        confidence=2,
                        stage="understood",
                        name=f"{student.name}对恶魔间接诱导的高警戒判断",
                    )
                    self._trust(student, self.demon, -2, evidence)
                elif student.tag("Occult") == 2 or role in {"神射手", "诗人"}:
                    student.souls[0].learn(
                        self.demon,
                        (E("possibly_involved", self.demon, victim),),
                        source=evidence,
                        confidence=1,
                        stage="observed",
                        name=f"{student.name}对恶魔介入可能性的怀疑",
                    )
                    self._trust(student, self.demon, -1, evidence)
            self.log.append(
                f"第20日清晨：{discoverer.name}发现{victim.name}没有返回，"
                "搜索者在废物处理站确认死亡。全员只知道机关致死和独处事实，尚不知道诱导过程。"
            )
            return evidence

        intent, reason = self._intent(
            discoverer,
            "discover_death",
            victim,
            f"发现{victim.name}失踪并确认死亡现场",
            discover,
            "死者没有参加早餐、寝室睡眠记录中断，搜索最终抵达仍在运行的机关区域",
            response_mode=ResponseMode.VERIFY,
        )
        event = self._choose(
            ((intent, 120, reason),),
            scene,
            (discoverer, doctor, investigator, engineer),
        )
        self.death_discovery = event
        evidence = event.get("outcome")
        for student in self.characters.values():
            if student.has("Dead") or student.tag("Occult") < 3:
                continue
            self._add_social_impact(
                student,
                self.demon,
                event,
                {"bond": 0, "malicious_intent": True, "threat": 2, "norm_breach": True},
            )
        return event

    def _death_response(self):
        victim = self._fatal_victim()
        scene = self.lethal_mechanism.parent
        doctor = self._alive_role("临床医生", "博物学家")
        investigator = self._alive_role("侦探", "律师")
        engineer = self._alive_role("机械工程师", "建筑师")

        def examine():
            evidence = Entity(
                f"{victim.name}的初步尸检记录",
                scene,
                {"Evidence": 1, "MedicalFinding": 1},
                properties={
                    "finding": "致命伤与配重机关一致；没有直接外力控制身体的可见痕迹"
                },
            )
            evidence.relate("Subject", victim)
            self.death_evidence.append(evidence)
            doctor.change("Authority", 1)
            return evidence

        def timeline():
            evidence = Entity(
                f"{victim.name}最后行动时间线",
                scene,
                {"Evidence": 1, "TimelineEvidence": 1},
                properties={
                    "finding": "死者临时偏离原定休闲安排，独自前往此前没有登记的机关区域",
                    "gap": "没有人目击其改变决定的原因",
                },
            )
            evidence.relate("Subject", victim)
            self.death_evidence.append(evidence)
            investigator.change("Authority", 1)
            return evidence

        def secure():
            self.lethal_mechanism.set("disabled", True)
            self.lethal_mechanism.change("Secured", 1)
            engineer.change("Authority", 1)
            return self.lethal_mechanism

        for actor, operation, label, resolve, reason, mode in (
            (
                doctor,
                "examine_death",
                f"检查{victim.name}的死因",
                examine,
                "区分机关伤、直接袭击和异常影响能够留下的可见痕迹",
                ResponseMode.VERIFY,
            ),
            (
                investigator,
                "reconstruct_death_timeline",
                f"重建{victim.name}最后六个行动段",
                timeline,
                "比较寝室、活动账、工具领取和机关触发时刻，不把空白自动解释成恶魔",
                ResponseMode.VERIFY,
            ),
            (
                engineer,
                "secure_lethal_mechanism",
                "断能并封锁旧式配重焚化闸",
                secure,
                "现场仍有二次伤亡风险，调查不能以继续暴露所有人为代价",
                ResponseMode.PROTECT,
            ),
        ):
            intent, why = self._intent(
                actor,
                operation,
                victim,
                label,
                resolve,
                reason,
                response_mode=mode,
            )
            self._choose(((intent, 100, why),), scene, (actor,))

    def _death_conflict(self):
        victim = self._fatal_victim()
        scene = self.scenes["中央厅"]
        accuser = max(
            (
                actor
                for actor in self.characters.values()
                if not actor.has("Dead") and actor.tag("Occult") >= 3
            ),
            key=lambda actor: actor.tag("Panic"),
        )
        detective = self._alive_role("侦探", "律师")

        def suspect():
            hypothesis = Knowledge(
                f"{accuser.name}提出的恶魔间接诱导假说",
                accuser.souls[0],
                E("possibly_induced", self.demon, victim),
                subject=self.demon,
                source=self.death_discovery,
                tags={"Hypothesis": 1, "Unconfirmed": 1},
            )
            accuser.relate("Suspects", self.demon, 2, source=hypothesis)
            self.death_hypotheses["demon_inducement"] = hypothesis
            self.log.append(
                f"第20日：{accuser.name}依据入学前的恶魔知识主张调查间接诱导；"
                "这是一项有能力依据、但没有本案直接证据的假说。"
            )
            return hypothesis

        intent, reason = self._intent(
            accuser,
            "raise_demon_suspicion",
            self.demon,
            f"{accuser.name}公开提出恶魔间接诱导假说",
            suspect,
            "神秘学三级先验确认恶魔具备选择性真话和情绪调整能力；现场仍缺少直接目击",
            response_mode=ResponseMode.VERIFY,
        )
        self._choose(((intent, 96, reason),), scene, tuple(self.characters.values()))

        def contest():
            detective.relate(
                "QuestionsAttributionOf", accuser, source=self.death_discovery
            )
            self._trust(accuser, detective, -1, self.death_discovery)
            self.log.append(
                f"第20日：{detective.name}承认恶魔具备能力，但拒绝把能力直接等同于本案实施。"
                f"{accuser.name}因此认为侦探正在替恶魔拖延。"
            )
            return self.death_hypotheses["demon_inducement"]

        intent, reason = self._intent(
            detective,
            "challenge",
            accuser,
            f"{detective.name}要求把恶魔能力与本案证据分开",
            contest,
            "保护可验证调查程序，但会被高恐慌的怀疑者理解成替恶魔辩护",
            response_mode=ResponseMode.RESTRAIN,
        )
        event = self._choose(((intent, 90, reason),), scene, (detective, accuser))
        self._add_social_impact(
            accuser,
            detective,
            event,
            {"bond": 0, "norm_breach": True},
        )

    def _special_20_after_death(self):
        victim = self._fatal_victim()
        scene = self.scenes["契约审议室"]
        lawyer = self._alive_role("律师", "侦探")
        witness = self._alive_role("职业棋手", "建筑师", "神射手")

        def establish_safety():
            directives = tuple(
                Directive(Mode.FORBID, F("operate_high_risk_alone", actor, self.world))
                for actor in self.characters.values()
                if not actor.has("Dead")
            ) + tuple(
                Directive(Mode.FORBID, F("private_suggestion", self.demon, actor))
                for actor in self.characters.values()
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
                rule, lawyer, carrier=carrier, scope=self.world, version=3
            )
            publication.relate("Witness", witness)
            patch = RulePatch(
                publication.name, rule, scene, publication, self.world, priority=5
            )
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

        intent, reason = self._intent(
            lawyer,
            "establish_rule",
            self.world,
            "建立死亡调查期间的结伴与公开谈话规则",
            establish_safety,
            "规则只约束可观察的独处和高危操作，不把尚未证明的诱导写成既成事实",
            response_mode=ResponseMode.PROTECT,
        )
        self._choose(((intent, 110, reason),), scene, tuple(self.characters.values()))

        def testify():
            claims = (
                E("spoke_privately_with", self.demon, victim),
                E("did_not_directly_control", self.demon, victim),
                E("did_not_directly_kill", self.demon, victim),
            )
            for claim in claims:
                self.env.assert_fact(claim)
            carrier = InformationCarrier(
                "恶魔关于死者的选择性证言",
                "Testimony",
                scene,
                creator=self.demon,
                claims=claims,
                tags={"Truth": 1, "Incomplete": 1, "Law": 1},
            )
            carrier.relate("RefusedToExplain", self.demon)
            for student in self.characters.values():
                if student.has("Dead"):
                    continue
                student.souls[0].learn(
                    self.demon,
                    claims,
                    source=carrier,
                    confidence=3,
                    stage="accepted",
                    name=f"{student.name}接收的恶魔死亡证言",
                )
                self._trust(student, self.demon, -1, carrier)
            self.log.append(
                "第20日：恶魔承认曾与死者独处，并如实否认直接控制和直接杀人；"
                "它拒绝说明是否调整过情绪。真话增加了事实，也扩大了敌意。"
            )
            return carrier

        intent, reason = self._intent(
            self.demon,
            "provide_partial_true_testimony",
            victim,
            "恶魔提供真实但不完整的死亡证言",
            testify,
            "真话契约迫使已说出的三项命题为真；沉默保留其对情绪调整的隐瞒",
            response_mode=ResponseMode.CONCEAL,
        )
        event = self._choose(
            ((intent, 100, reason),), scene, tuple(self.characters.values())
        )
        for student in self.characters.values():
            if student.has("Dead"):
                continue
            self._add_social_impact(
                student,
                self.demon,
                event,
                {"bond": 0, "malicious_intent": True, "threat": 2, "norm_breach": True},
            )
        self.projects["containment"].change("PausedByDeath", 1)
        self.threat.change("Uncontained", 1)
        self.arc_complete = False

    def _special_20(self):
        if self.death_discovery is not None:
            return self._special_20_after_death()
        scene = self.scenes["动力总站"]
        engineer, musician, lawyer = (
            self.characters["机械工程师"],
            self.characters["音乐家"],
            self.characters["律师"],
        )

        def demon_hold():
            self.demon.change("Reliability", 2)
            self.threat.change("Suppressed", 1)
            self.log.append(
                "第20日：恶魔主动让出全部广播频段，只保留契约规定的维生脉冲，使回声无法继续借它的声音扩散。"
            )
            return self.threat

        intent, reason = self._intent(
            self.demon,
            "yield_broadcast_channel",
            self.threat,
            "恶魔暂时让出广播频段",
            demon_hold,
            "若传声回声夺取广播，恶魔与学员都会失去维生调度",
        )
        self._choose(
            ((intent, 100, reason),), scene, (self.demon, engineer, musician, lawyer)
        )

        def contain():
            if not (
                self.threat.has("Exposed")
                and self.lunar_rule
                and self.provisional_alliance
                and self.projects["containment"].tag("Progress") >= 2
            ):
                raise RuntimeError("共同隔离缺少言灵揭示、月相规则或临时协定")
            rite = Entity(
                "第一次共同防卫：传声回声隔离", scene, {"Rite": 1, "Completed": 1}
            )
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
            self.log.append(
                "第20日：工程师切断旧管，音乐家持续标定，律师守住临时规则，恶魔维持广播脉冲。威胁被隔离，没有人把它误写成最终胜利。"
            )
            return rite

        intent, reason = self._intent(
            engineer,
            "joint_containment",
            self.threat,
            "完成第一次共同防卫",
            contain,
            "四种不同能力形成互锁；任何一方单独行动都只能暂时压制回声",
        )
        self._choose(
            ((intent, 120, reason),), scene, (engineer, musician, lawyer, self.demon)
        )

    def _special(self):
        method = getattr(self, f"_special_{self.day}", None)
        if method:
            method()

    def _record_daily_action(
        self,
        actor,
        slot,
        operation,
        label,
        scene,
        target=None,
        event=None,
    ):
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
        return max(self.damaged_scenes, key=lambda scene: scene.tag("Damage"))

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
        """为每名存活学员记录六个白昼时段和四个连续睡眠时段。"""
        project = self.projects[project_key]
        day_events = tuple(
            event for event in self.runtime.events if event.get("day") == self.day
        )
        participation = {
            actor: [] for actor in self.characters.values() if not actor.has("Dead")
        }
        for event in day_events:
            actor, effect = event.get("actor"), event.get("effect")
            if actor in participation:
                participation[actor].append(
                    (event, effect.op, event.name, event.get("scene"))
                )
            if effect and effect.op in {"confide", "challenge", "collaborate"}:
                target = effect.args[1]
                if target in participation:
                    participation[target].append(
                        (
                            event,
                            "participate_in_conversation",
                            f"回应{actor.name}关于当天问题的谈话",
                            event.get("scene"),
                        )
                    )

        for role, actor in self.characters.items():
            if actor.has("Dead"):
                continue
            main = participation[actor]
            repair_scene = self._repair_target_for(actor, project)
            primary_scene = main[0][3] if main else project.get("scene")
            self._record_daily_action(
                actor,
                0,
                "morning_life",
                "检查睡处、洗漱并处理第一餐"
                if self.day == 1
                else "起床、洗漱并处理早餐",
                actor.get("home"),
                actor.get("quarters"),
            )
            self._record_daily_action(
                actor,
                1,
                "move_or_prepare",
                "前往当天活动区域并准备工具",
                primary_scene,
                primary_scene,
            )
            for slot in (2, 3):
                if main:
                    event, operation, label, scene = main.pop(0)
                    self._record_daily_action(
                        actor,
                        slot,
                        operation,
                        label,
                        scene,
                        event.get("outcome"),
                        event,
                    )
                elif slot == 2:
                    self._support_project_activity(actor, project)
                    self._record_daily_action(
                        actor,
                        slot,
                        "support_project",
                        f"为《{project.name}》搬运、清点或完成辅助工作",
                        project.get("scene"),
                        project,
                    )
                else:
                    self._repair_scene(repair_scene)
                    repair_location = (
                        repair_scene
                        if repair_scene.has("Scene")
                        else repair_scene.parent
                    )
                    self._record_daily_action(
                        actor,
                        slot,
                        "repair_damage",
                        f"清障、封漏并修复{repair_scene.name}的生活功能",
                        repair_location,
                        repair_scene,
                    )
            if main:
                event, operation, label, scene = main.pop(0)
                self._record_daily_action(
                    actor,
                    4,
                    operation,
                    label,
                    scene,
                    event.get("outcome"),
                    event,
                )
            else:
                self._record_daily_action(
                    actor,
                    4,
                    "communal_life",
                    "取餐、清洁公共用品并交换当天见闻",
                    self.scenes["后勤院"],
                    self.resources,
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
                    actor,
                    5,
                    "cope_with_death",
                    coping,
                    self.scenes["公共娱乐室"],
                    self.death_discovery,
                )
            else:
                self._record_daily_action(
                    actor,
                    5,
                    "leisure",
                    LEISURE[role],
                    actor.get("workplace"),
                    actor,
                )
            for slot in range(6, 10):
                if (
                    self.death_discovery is not None
                    and self.day == 20
                    and slot == 6
                    and actor.tag("Panic") >= 3
                ):
                    actor.change("Fatigue", 1)
                    self._record_daily_action(
                        actor,
                        slot,
                        "insomnia",
                        "因恐慌无法入睡，留在寝室等待下一次点名",
                        actor.get("home"),
                        actor.get("quarters"),
                    )
                    continue
                self._record_daily_action(
                    actor,
                    slot,
                    "sleep",
                    "依夜禁在住处连续睡眠",
                    actor.get("home"),
                    actor.get("quarters"),
                )
            social_today = any(
                event.get("effect")
                and event.get("effect").op in {"confide", "challenge", "collaborate"}
                and (
                    event.get("actor") is actor or event.get("effect").args[1] is actor
                )
                for event in day_events
            )
            actor.set(
                "solitary_days",
                0 if social_today else actor.get("solitary_days", 0) + 1,
            )

        expected = sum(not actor.has("Dead") for actor in self.characters.values()) * 10
        actual = sum(action.get("day") == self.day for action in self.daily_actions)
        if actual != expected:
            raise RuntimeError(f"第{self.day}日行动账不完整：{actual}/{expected}")

    def _private_demon_attack(self):
        """在真实条件满足时结算二十日内唯一一次不可见诱导。"""
        if self.demon_fatal_actions or not (
            self.demon_attack_window[0] <= self.day <= self.demon_attack_window[1]
        ):
            return None
        if self.lethal_mechanism.get("disabled"):
            return None
        next_spec = DAY_SPECS[self.day] if self.day < len(DAY_SPECS) else None
        publicly_expected_roles = set(next_spec[4]) if next_spec else set()
        if next_spec:
            publicly_expected_roles.update(
                role for role in next_spec[5][:2] if role != "恶魔"
            )
        candidates = tuple(
            actor
            for actor in self.characters.values()
            if not actor.has("Dead")
            and actor.tag("Occult") < 3
            and actor.get("solitary_days", 0) >= 5
            and actor.get("role") not in publicly_expected_roles
        )
        if not candidates:
            return None

        def exposure(actor):
            duty = (
                2
                if actor.get("role") in {"机械工程师", "建筑师", "主厨", "临床医生"}
                else 0
            )
            ties = sum(facet.strength for facet in actor.related("Trusts"))
            return (
                actor.get("solitary_days", 0) * 2
                + actor.tag("Panic")
                + (3 - actor.tag("Occult")) * 2
                + duty
                - ties
            )

        target = max(candidates, key=exposure)
        scene = self.lethal_mechanism.parent

        def suggest():
            claims = (
                E("connected_to_exit_service_tunnel", self.lethal_mechanism),
                E("restores_waste_and_water_pressure", self.lethal_mechanism),
                E("delay_can_harm_occupied_areas", self.lethal_mechanism),
            )
            for claim in claims:
                self.env.assert_fact(claim)
            knowledge = target.souls[0].learn(
                self.lethal_mechanism,
                claims,
                source=self.demon,
                confidence=3,
                stage="accepted",
                name=f"{target.name}从恶魔处得到的三段局部真相",
            )
            Influence(
                f"{target.name}被压低的求助冲动",
                "Serenity",
                3,
                target.souls[0],
                target=target,
                source=self.demon,
                mode="hidden_suggestion",
                persistence=1,
                started_at=self.timeline.now,
                expires_at=self.timeline.now + 20,
            )
            Influence(
                f"{target.name}被放大的立即处理冲动",
                "Hope",
                3,
                target.souls[0],
                target=self.lethal_mechanism,
                source=self.demon,
                mode="hidden_suggestion",
                persistence=1,
                started_at=self.timeline.now,
                expires_at=self.timeline.now + 20,
            )
            self.log.append(
                f"第{self.day}日：恶魔在无人旁观时向{target.name}提供三段真实但不完整的信息，"
                "并压低其求助冲动；它没有声称机关安全。"
            )
            return knowledge

        suggestion, reason = self._intent(
            self.demon,
            "private_suggestion",
            target,
            f"恶魔私下向{target.name}选择性灌输真相",
            suggest,
            "目标连续独处、缺少充分恶魔知识；三项陈述都为真，但省略损坏的安全联锁",
            response_mode=ResponseMode.CONCEAL,
        )
        suggestion_event = self._choose(((suggestion, 100, reason),), scene, (target,))

        rescuable = (
            self.pair_maintenance_rule is not None
            and bool(self.lethal_mechanism.related("InspectedBy", self.player))
            and not self.player.has("Dead")
        )

        def trigger():
            attack = Entity(
                f"第{self.day}日恶魔唯一诱导行动",
                self.world,
                {"DemonFatalAction": 1, "Hidden": 1},
                properties={
                    "selective_truth": True,
                    "emotion_adjustment": True,
                    "direct_control": False,
                    "direct_kill": False,
                    "rescue_window_open": rescuable,
                },
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
                self.log.append(
                    f"第{self.day}日：结伴维修规则和侦探的提前检查共同破坏诱导条件；"
                    f"{target.name}重伤但被救下。"
                )
            else:
                target.change("Health", -target.tag("Health"))
                target.change("Dead", 1)
                attack.change("Fatal", 1)
                self.log.append(
                    f"第{self.day}日：{target.name}独自触发损坏的配重焚化闸。"
                    "现场只呈现为修复事故，旁人没有看见恶魔的暗示过程。"
                )
            return attack

        fatal_intent, fatal_reason = self._intent(
            target,
            "trigger_lethal_mechanism",
            self.lethal_mechanism,
            f"{target.name}独自复位旧式配重焚化闸",
            trigger,
            "主体相信不立即处理会伤及他人，却不知道安全联锁已经损坏",
            response_mode=ResponseMode.PROTECT,
        )
        fatal_event = self._choose(
            ((fatal_intent, 100, fatal_reason),), scene, (target,)
        )
        activity = next(
            action
            for action in self.daily_actions
            if action.get("day") == self.day
            and action.get("slot") == 5
            and action.related("Actor", target)
        )
        activity.set("operation", "follow_private_clue")
        activity.set("label", f"独自前往{scene.name}处理恶魔指出的真实风险")
        activity.relate("RealizedBy", suggestion_event)
        activity.relate("RealizedBy", fatal_event)
        if target.has("Dead"):
            for sleep in self.daily_actions:
                if (
                    sleep.get("day") == self.day
                    and sleep.get("slot", 0) >= 6
                    and sleep.related("Actor", target)
                ):
                    sleep.change("Cancelled", 1)
                    sleep.set("cost", 0)
                    sleep.set("operation", "death_prevents_sleep")
        return fatal_event

    def _end_day(self, title, moon):
        # 日消耗远小于旧版灰潮施压；工作项目把储备上限缓慢推回去。
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
            (
                (actor.tag("Authority"), role, actor.name)
                for role, actor in self.characters.items()
            ),
            reverse=True,
        )[:4]
        actions = tuple(
            action for action in self.daily_actions if action.get("day") == self.day
        )
        self.day_summaries.append(
            {
                "day": self.day,
                "title": title,
                "moon": moon,
                "events": sum(
                    event.get("day") == self.day for event in self.runtime.events
                ),
                "resources": {
                    "food": self.resources.tag("FoodDays"),
                    "water": self.resources.tag("WaterDays"),
                    "power": self.resources.tag("PowerDays"),
                },
                "leaders": tuple(
                    (role, name, score) for score, role, name in authorities
                ),
                "gray_pressure": self.gray_tide.tag("Pressure"),
                "daily_actions": len(actions),
                "sleep_slots": sum(
                    action.get("operation") == "sleep" for action in actions
                ),
                "repair_slots": sum(
                    action.get("operation") == "repair_damage" for action in actions
                ),
                "leisure_slots": sum(
                    action.get("operation") == "leisure" for action in actions
                ),
                "living_students": sum(
                    not actor.has("Dead") for actor in self.characters.values()
                ),
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
        effects = [event.get("effect").op for event in self.runtime.events]
        leaders = sum(actor.tag("Authority") >= 3 for actor in self.characters.values())
        trusted_pairs = sum(
            bool(actor.related("Trusts")) for actor in self.characters.values()
        )
        completed_actions = tuple(
            action for action in self.daily_actions if not action.has("Cancelled")
        )
        return {
            "days": self.day,
            "events": len(self.runtime.events),
            "gray_pressure": self.gray_tide.tag("Pressure"),
            "rules": len(self.rule_patches),
            "analyses": len(self.analyses),
            "leaders": leaders,
            "socialized_characters": trusted_pairs,
            "demon_help": sum(
                event.get("effect").op
                in {
                    "publish_true_offer",
                    "translate_binding_term",
                    "announce_external_anomaly",
                    "yield_broadcast_channel",
                }
                for event in self.runtime.events
                if event.get("effect")
            ),
            "word_attempts": len(self.word_attempts),
            "word_success": effects.count("invoke_word"),
            "blocked_accusations": sum(
                bool(event.get("blocked_candidates")) for event in self.runtime.events
            ),
            "threat_manifested": self.threat.has("Manifested"),
            "threat_contained": self.threat.has("Contained"),
            "arc_complete": self.arc_complete,
            "daily_action_records": len(self.daily_actions),
            "completed_daily_actions": len(completed_actions),
            "sleep_slots": sum(
                action.get("operation") == "sleep" for action in self.daily_actions
            ),
            "repair_slots": sum(
                action.get("operation") == "repair_damage"
                for action in self.daily_actions
            ),
            "leisure_slots": sum(
                action.get("operation") == "leisure" for action in self.daily_actions
            ),
            "demon_fatal_actions": len(self.demon_fatal_actions),
            "student_deaths": sum(
                actor.has("Dead") for actor in self.characters.values()
            ),
            "death_crisis_stabilized": bool(
                self.death_discovery
                and len(self.death_evidence) >= 3
                and self.pair_maintenance_rule
                and self.lethal_mechanism.get("disabled")
            ),
        }


__all__ = ["AcademyPrologue", "DAY_SPECS"]
