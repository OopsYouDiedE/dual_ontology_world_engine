"""《异常感知学园》第一章：禁闭后的二十天。

初期篇只处理生存秩序、群像认识、威信形成、共同立规、月相研究与首个威胁。
旧版的四锚封印冲刺不会进入这段时间。人物、知识、规则、言灵和事件继续使用
核心 Entity / Knowledge / Intent / RulePatch / Recipe / Rite 表达。
"""

from __future__ import annotations

from core.python.engine import (
    ActionRuntime,
    Directive,
    E,
    Entity,
    F,
    InformationCarrier,
    Intent,
    Knowledge,
    Mode,
    Recipe,
    Rule,
    RulePatch,
    RulePublication,
    Timeline,
)

from .academy_game import AcademyGame


DAY_SPECS = (
    (1, "醒来的人与广播里的恶魔", "朔后第2日·娥眉月", "life_support", ("临床医生", "机械工程师", "主厨"), ("侦探", "律师", "谁有资格发号施令")),
    (2, "十六只杯子与一座水塔", "朔后第3日·娥眉月", "water", ("博物学家", "机械工程师", "建筑师"), ("主厨", "化学家", "第一份饮水配给")),
    (3, "早餐桌上的多数决", "朔后第4日·娥眉月", "food", ("主厨", "职业棋手", "博物学家"), ("神偷", "主厨", "被顺走又归还的砂糖罐")),
    (4, "没有证物，不准把人送上绞架", "朔后第5日·娥眉月", "records", ("律师", "侦探", "画家"), ("诗人", "律师", "规矩会不会杀死直觉")),
    (5, "图书馆没有答案，只有目录", "朔后第6日·上弦前", "archive", ("考古学家", "画家", "神偷"), ("考古学家", "神偷", "谁动过封闭书架")),
    (6, "停在错误时刻的黄铜月钟", "朔后第7日·上弦", "lunar_clock", ("机械工程师", "建筑师", "音乐家"), ("建筑师", "机械工程师", "快修还是重新测绘")),
    (7, "第一次实验失败得很诚实", "朔后第8日·上弦", "resonance", ("音乐家", "诗人", "舞蹈艺术家"), ("音乐家", "诗人", "失败该算谁的")),
    (8, "允许玩乐也是一条生存规则", "朔后第9日·盈凸月", "morale", ("舞蹈艺术家", "神射手", "职业棋手"), ("画家", "临床医生", "给活人画像还是给遗物编号")),
    (9, "恶魔给出一个不能省略的否定词", "朔后第10日·盈凸月", "lexicon", ("律师", "考古学家", "诗人"), ("律师", "恶魔", "帮助是否等于取得信任")),
    (10, "低功率回声试验", "朔后第11日·盈凸月", "resonance", ("音乐家", "化学家", "机械工程师"), ("化学家", "音乐家", "看不见的结果算不算结果")),
    (11, "值日表第一次不是废纸", "朔后第12日·盈凸月", "duty_roster", ("职业棋手", "主厨", "律师"), ("职业棋手", "主厨", "天才是否可以免洗盘子")),
    (12, "没有案件的自由活动日", "朔后第13日·盈凸月", "morale", ("诗人", "神偷", "画家"), ("神射手", "舞蹈艺术家", "训练和游戏的界线")),
    (13, "三本历书写着三个满月时刻", "朔后第14日·望前", "moon_study", ("职业棋手", "考古学家", "建筑师"), ("侦探", "职业棋手", "误差是事故还是人为")),
    (14, "水压表在无人触碰时归零", "朔后第15日·望", "water", ("机械工程师", "侦探", "化学家"), ("神射手", "恶魔", "最方便的嫌疑人")),
    (15, "满月把旧传声管变成了喉咙", "朔后第16日·望", "life_support", ("音乐家", "机械工程师", "临床医生"), ("诗人", "音乐家", "听见同伴求救却找不到人")),
    (16, "三种假说，谁也不许抢先结案", "朔后第17日·亏凸月", "inquiry", ("侦探", "化学家", "职业棋手"), ("博物学家", "化学家", "承认错误和替人顶罪")),
    (17, "第一次公开质询", "朔后第18日·亏凸月", "inquiry", ("律师", "侦探", "神射手"), ("神偷", "律师", "规则能否保护被怀疑者")),
    (18, "回声标定", "朔后第19日·亏凸月", "resonance", ("音乐家", "诗人", "律师"), ("音乐家", "恶魔", "借用敌人的发音")),
    (19, "把正确猜测写成可执行规则", "朔后第20日·亏凸月", "charter", ("律师", "职业棋手", "机械工程师"), ("侦探", "律师", "临时规则何时失效")),
    (20, "第一场共同防卫", "朔后第21日·亏凸月", "containment", ("机械工程师", "音乐家", "神射手"), ("侦探", "恶魔", "盟友可以只持续到下一次日出")),
)


PROJECT_SPECS = {
    "life_support": ("维生总检", "公共诊疗所", ("临床医生", "机械工程师", "建筑师"), 10),
    "water": ("上水与过滤", "上水库与水务站", ("机械工程师", "博物学家", "化学家"), 10),
    "food": ("配给与温室", "后勤院", ("主厨", "博物学家", "职业棋手"), 8),
    "records": ("公开证据台账", "中央厅", ("律师", "侦探", "画家"), 7),
    "archive": ("图书目录复原", "教学图书馆", ("考古学家", "画家", "神偷"), 9),
    "lunar_clock": ("黄铜月钟修复", "星盘讲堂", ("机械工程师", "建筑师", "音乐家"), 10),
    "resonance": ("回声标定实验", "共鸣厅", ("音乐家", "诗人", "舞蹈艺术家"), 12),
    "morale": ("共同生活与自由活动", "公共娱乐室", ("诗人", "神偷", "舞蹈艺术家", "画家"), 8),
    "lexicon": ("恶魔语义词典", "契约审议室", ("律师", "考古学家", "诗人"), 8),
    "duty_roster": ("轮值与资源调度", "中央厅", ("职业棋手", "主厨", "律师"), 8),
    "moon_study": ("月相与事故时刻校勘", "星盘讲堂", ("职业棋手", "考古学家", "建筑师"), 9),
    "inquiry": ("传声管异常调查", "中央厅", ("侦探", "律师", "化学家", "职业棋手"), 12),
    "charter": ("月相作业规程", "契约审议室", ("律师", "职业棋手", "机械工程师"), 8),
    "containment": ("传声管回声隔离", "动力总站", ("机械工程师", "音乐家", "神射手"), 10),
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


class AcademyPrologue:
    """二十天初期篇运行时；一天由数个有后果的群像决定组成。"""

    campaign_days = (100, 180)

    def __init__(self):
        base = AcademyGame(autonomous=False)
        self.base = base
        self.world, self.env, self.rules, self.words = base.world, base.env, base.rules, base.words
        self.characters, self.souls, self.scenes = base.characters, base.souls, base.scenes
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
            self.timeline, self.env, self.rules, self.words,
            event_parent=self.world, witnesses=self._witnesses,
        )
        self.day = 0
        self.day_summaries = []
        self.log = []
        self.rule_patches = []
        self.analyses = []
        self.word_attempts = []
        self.arc_complete = False
        self.resources = Entity(
            "维生资源账", self.world,
            {"FoodDays": 38, "WaterDays": 32, "PowerDays": 26, "MedicalReserve": 12},
            properties={"population": 16},
        )
        self.gray_tide = Entity(
            "灰潮远期侵蚀", self.world, {"Threat": 1, "Pressure": 0, "Dormant": 1},
            properties={"limit": 24, "earliest_terminal_day": 100},
        )
        self.threat = Entity(
            "月相传声回声", self.world,
            {"Threat": 1, "Dormant": 1, "Veil": 2},
            properties={"first_manifestation": 15, "source": "旧传声管与月钟的相位反馈"},
        )
        self.projects = {}
        for key, (name, scene_name, skills, need) in PROJECT_SPECS.items():
            self.projects[key] = Entity(
                name, self.world, {"Project": 1},
                properties={"scene": self.scenes[scene_name], "skills": skills, "need": need, "key": key},
            )
        self.sources = self._build_sources()
        self.hypotheses = self._build_hypotheses()
        self.provisional_alliance = None
        self.evidence_rule = None
        self.lunar_rule = None
        self.echo_word = self._build_echo_word()

    def _build_sources(self):
        sources = {
            "ledger": InformationCarrier("二十日维生账", "Ledger", self.resources, tags={"Law": 1, "Preservation": 2}),
            "almanac": InformationCarrier("三版本月相历书", "Manuscript", self.scenes["教学图书馆"], tags={"Threshold": 2, "Vestige": 1}),
            "gauge": InformationCarrier("归零前后的水压纸带", "Chart", self.scenes["上水库与水务站"], tags={"Mind": 2, "Form": 1}),
            "phonograph": InformationCarrier("传声管反向录音蜡筒", "Cylinder", self.scenes["声纹与记录室"], tags={"Mind": 2, "Name": 1}),
            "demon_clause": InformationCarrier("恶魔提供的否定词铜版", "Manuscript", self.scenes["契约审议室"], tags={"Name": 2, "Law": 2}),
            "resonator": Entity("校准音叉与黄铜共鸣架", self.scenes["共鸣厅"], {"Instrument": 1, "Mind": 2, "Form": 2}),
            "clock": Entity("停在错误时刻的黄铜月钟", self.scenes["星盘讲堂"], {"Machine": 1, "Damaged": 2, "Threshold": 1}),
        }
        return sources

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
                lambda _bindings: bool(self.sources["demon_clause"].related("ProvidedBy", self.demon)),
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
            data[f"关系.{role}.信任"] = sum(facet.strength for facet in actor.related("Trusts"))
            data[f"关系.{role}.不信任"] = sum(facet.strength for facet in actor.related("Distrusts"))
        return data

    @staticmethod
    def _diff(before, after):
        return tuple(
            f"{key}: {before.get(key)} → {value}"
            for key, value in after.items() if before.get(key) != value
        )

    def _witnesses(self, _event, context):
        participants = context.get("participants", ())
        return tuple(
            actor.souls[0] for actor in participants
            if isinstance(actor, Entity) and actor.souls
        )

    def _intent(self, actor, operation, target, label, resolve, reason, cost=1):
        return Intent(actor, actor.souls[0], F(operation, actor, target), cost, resolve, label), reason

    def _choose(self, options, scene, participants=()):
        """比较不同人物的候选 Intent，保存未选方案、规则淘汰与状态差分。"""
        assessed, blocked = [], []
        domains = (self.world, scene)
        for intent, base, reason in options:
            allowed, decision = self.runtime.review(intent, {"domains": domains})
            record = {
                "actor": intent.actor.name, "operation": intent.effect.op,
                "label": intent.label, "score": decision.score + base,
                "reason": reason, "rule": decision.reason,
            }
            if allowed:
                assessed.append((decision.score + base, intent, reason, record))
            else:
                blocked.append(record)
        if not assessed:
            raise RuntimeError(f"第{self.day}日没有可执行候选：{scene.name}")
        assessed.sort(key=lambda item: item[0], reverse=True)
        score, intent, reason, _ = assessed[0]
        everyone = tuple(dict.fromkeys((intent.actor, *participants)))
        for actor in everyone:
            if actor is not self.demon:
                actor.reparent(scene)
        before = self._snapshot()
        self.timeline.now = self.day * 100 + len([event for event in self.runtime.events if event.get("day") == self.day]) * 10
        if not self.runtime.resolve(intent, {"domains": domains, "participants": everyone}):
            raise RuntimeError(f"第{self.day}日行动结算失败：{intent.label}")
        after = self._snapshot()
        event = self.runtime.events[-1]
        event.set("day", self.day)
        event.set("moon", DAY_SPECS[self.day - 1][2])
        event.set("scene", scene)
        event.set("decision_score", score)
        event.set("decision_reason", reason)
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
                    f"{actor.name}对{project.name}的工作判断", actor.souls[0],
                    ("working_conclusion", project, method), subject=project, source=project,
                    tags={"Analysis": 1, "Mind": 1},
                )
                self.analyses.append(knowledge)
                self.log.append(f"第{self.day}日：{actor.name}{method}，{project.name}获得可复核进展。")
                return project

            intent, reason = self._intent(
                actor, operation, project,
                f"{actor.name}推进《{project.name}》",
                resolve,
                f"职业方法={method}；技能匹配={role in project.get('skills')}；既有威信={actor.tag('Authority')}",
            )
            options.append((intent, 40 + skill + authority, reason))
        return self._choose(options, scene, tuple(self.characters[role] for role in roles))

    def _social(self, actor_role, target_role, topic):
        actor, target = self.characters[actor_role], self.demon if target_role == "恶魔" else self.characters[target_role]
        scene = self.scenes["公共娱乐室"] if target is not self.demon else self.scenes["中央厅"]
        tension = any(word in topic for word in ("资格", "责任", "可疑", "嫌疑", "错误", "失效"))
        vulnerable = any(word in topic for word in ("恐惧", "求救", "信任", "顶罪", "画像"))
        options = []

        def confide():
            self._trust(actor, target, 2, topic)
            if target is not self.demon:
                self._trust(target, actor, 1, topic)
            actor.change("Candor", 1)
            self.log.append(f"第{self.day}日：{actor.name}没有套话，向{target.name}说出自己对“{topic}”真正害怕的部分。")
            return target

        def challenge():
            actor.change("Insight", 1)
            self._trust(actor, target, -1, topic)
            actor.relate("Questioned", target, source=topic)
            self.log.append(f"第{self.day}日：{actor.name}当面质疑{target.name}关于“{topic}”的说法，关系变紧但留下可检查的问题。")
            return target

        def collaborate():
            self._trust(actor, target, 1, topic)
            if target is not self.demon:
                self._trust(target, actor, 1, topic)
            actor.change("Authority", 1)
            self.log.append(f"第{self.day}日：{actor.name}与{target.name}把“{topic}”变成一次共同任务，避免空谈立场。")
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
                actor, operation, target, f"{actor.name}与{target.name}谈：{topic}",
                resolver, f"{phrase}；当前信任={self.trust(actor, target)}",
            )
            options.append((intent, biases[operation] + self.trust(actor, target), reason))
        return self._choose(options, scene, (actor, target))

    def _support(self, actor, source, hypothesis_key, amount=1, contradiction=False):
        hypothesis = self.hypotheses[hypothesis_key]
        knowledge = Knowledge(
            f"{actor.name}分析《{source.name}》", actor.souls[0],
            ("contradicts" if contradiction else "supports", hypothesis, source),
            subject=hypothesis, source=source, tags={"Analysis": 1, "Mind": 2},
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
            alliance = Entity(
                "共同滞留临时协定", self.world, {"Alliance": 1, "Provisional": 1, "Active": 1},
                properties={"until": "任一方取得不依赖对方的生存与广播方案"},
            )
            alliance.relate("Party", self.demon)
            for student in self.characters.values():
                alliance.relate("Party", student)
            alliance.attach("Obligation", F("maintain_life_support", self.demon, self.resources))
            alliance.attach("Obligation", F("maintain_broadcast_clock", self.player, self.world))
            self.provisional_alliance = alliance
            self.demon.change("Reliability", 1)
            self.log.append("第1日：恶魔公开共同死锁——学员死光会让真实胜利路径消失，广播停摆也会令它永远困在核心。")
            return alliance

        intent, reason = self._intent(
            actor, "offer_provisional_alliance", self.resources, "恶魔公开共同死锁与临时协作条件",
            resolve, "契约要求保留真实胜利路径；双方在维生与广播上互相依赖",
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

        def command():
            lawyer.change("Authority", 1)
            return Entity("律师单方面发布的禁言命令", scene, {"RejectedProposal": 1})

        options = []
        for operation, resolver, score, reason in (
            ("establish_rule", establish, 82, "侦探见证、文本公开且所有人保留质疑权"),
            ("issue_order", command, 48, "立即压住争吵，但没有授权来源和失效条件"),
        ):
            intent, why = self._intent(lawyer, operation, self.world, "建立证据指控规则" if operation == "establish_rule" else "单方面禁止公开怀疑", resolver, reason)
            options.append((intent, score, why))
        self._choose(options, scene, tuple(self.characters.values()))

    def _special_7(self):
        musician = self.characters["音乐家"]
        scene = self.scenes["共鸣厅"]

        def attempt():
            result = self.words.invoke(
                self.echo_word, musician.souls[0], {"resonator": self.sources["resonator"]},
                target=self.threat, context={"actor": musician, "world": self.world},
            )
            failure = self.words.last_failure
            record = Entity("第一次回声标定试验记录", scene, {"Experiment": 1, "Failed": int(result is None)}, properties={"failure": failure})
            self.word_attempts.append(record)
            self.projects["resonance"].change("Evidence", 2)
            musician.change("Authority", 1)
            self.log.append(f"第7日：言灵没有发动。音乐家坚持把“{failure}”逐项写进记录，没有用气氛掩盖失败。")
            return record

        intent, reason = self._intent(musician, "test_word", self.threat, "第一次言灵实验：失败记录", attempt, "先允许失败暴露缺失槽位，避免把偶然现象误认成成功")
        self._choose(((intent, 90, reason),), scene, (musician, self.characters["诗人"], self.characters["舞蹈艺术家"]))

    def _special_9(self):
        scene = self.scenes["契约审议室"]

        def translate():
            self.sources["demon_clause"].reparent(scene)
            self.sources["demon_clause"].relate("ProvidedBy", self.demon)
            self.projects["lexicon"].change("Progress", 3)
            self.demon.change("Reliability", 2)
            self._support(self.characters["律师"], self.sources["demon_clause"], "demon", 1, contradiction=True)
            self.log.append("第9日：恶魔补出导师笔记里被刮掉的否定词，并允许律师把它写成可追责的公开译本。")
            return self.sources["demon_clause"]

        intent, reason = self._intent(self.demon, "translate_binding_term", self.sources["demon_clause"], "恶魔公开关键否定词", translate, "广播语义若继续损坏，恶魔也将失去唯一合法发言媒介")
        self._choose(((intent, 95, reason),), scene, (self.characters["律师"], self.characters["考古学家"], self.characters["诗人"]))

    def _special_13(self):
        chess = self.characters["职业棋手"]
        scene = self.scenes["星盘讲堂"]

        def compare():
            knowledge = self._support(chess, self.sources["almanac"], "lunar", 3)
            self.hypotheses["machine"].change("Contradiction", 1)
            chess.change("Authority", 2)
            self.log.append("第13日：棋手把三本历书当成三盘互相矛盾的棋谱，算出仪表漂移会在满月重复，而非随机磨损。")
            return knowledge

        intent, reason = self._intent(chess, "compare_almanacs", self.hypotheses["lunar"], "比较三本历书的误差模式", compare, "三份错误若以固定周期偏移，误差本身就是证据")
        self._choose(((intent, 92, reason),), scene, (chess, self.characters["考古学家"], self.characters["建筑师"]))

    def _special_14(self):
        detective = self.player
        scene = self.scenes["上水库与水务站"]

        def inspect():
            self.threat.tags.pop("Dormant", None)
            self.threat.change("Trace", 1)
            a = self._support(detective, self.sources["gauge"], "machine", 2)
            self._support(detective, self.sources["gauge"], "demon", 1, contradiction=True)
            self.log.append("第14日：水压归零时封条完整，恶魔广播也未经过水务线；侦探保留机械故障假说，同时削弱恶魔破坏说。")
            return a

        intent, reason = self._intent(detective, "inspect_anomaly", self.sources["gauge"], "封存无人触碰的水压纸带", inspect, "先记录封条、时刻与管线状态，不因最方便的嫌疑人跳过现场")
        self._choose(((intent, 94, reason),), scene, (detective, self.characters["机械工程师"], self.characters["化学家"]))

    def _special_15(self):
        scene = self.scenes["生活院"]

        def manifest():
            self.threat.change("Manifested", 1)
            self.gray_tide.change("Pressure", 1)
            self.resources.change("WaterDays", -2)
            self.sources["phonograph"].reparent(scene)
            self._support(self.characters["音乐家"], self.sources["phonograph"], "lunar", 2)
            self.log.append("第15日：满月时，旧传声管依次模仿四名学员求救，并用他们的声音命令水阀关闭；没有人受伤，饮水储备损失两日。")
            return self.threat

        intent, reason = self._intent(self.demon, "announce_external_anomaly", self.threat, "恶魔中断广播并标出伪造声源", manifest, "伪声正在占用它赖以履约的广播层；揭露威胁符合双方利益")
        self._choose(((intent, 100, reason),), scene, tuple(self.characters.values()))

    def _special_16(self):
        scene = self.scenes["中央厅"]
        options = []
        evidence = (
            ("侦探", "lunar", self.sources["phonograph"], 78, False, "反向录音与满月时刻一致"),
            ("化学家", "machine", self.sources["gauge"], 65, False, "压力变化仍可能来自阀门故障"),
            ("职业棋手", "demon", self.sources["demon_clause"], 74, True, "恶魔若主动切断广播会同时损害自身合法行动"),
        )
        for role, hypothesis, source, score, contradiction, reason in evidence:
            actor = self.characters[role]

            def resolve(actor=actor, hypothesis=hypothesis, source=source, contradiction=contradiction):
                result = self._support(actor, source, hypothesis, 2, contradiction)
                actor.change("Authority", 1)
                return result

            intent, why = self._intent(actor, "analyze_evidence", self.hypotheses[hypothesis], f"{actor.name}提交可反驳的异常解释", resolve, reason)
            options.append((intent, score + actor.tag("Authority"), why))
        self._choose(options, scene, tuple(self.characters[role] for role, *_ in evidence))

    def _special_17(self):
        scene = self.scenes["契约审议室"]
        shooter, detective, lawyer = self.characters["神射手"], self.player, self.characters["律师"]

        def accuse():
            self._trust(shooter, self.demon, -2, self.threat)
            return self.demon

        def present():
            result = self._support(detective, self.sources["phonograph"], "lunar", 2)
            detective.change("Authority", 2)
            self.log.append("第17日：侦探没有替恶魔作人格担保，只证明现有材料更支持月相反馈；神射手的拘束主张因没有载体被共同规则挡下。")
            return result

        def defend():
            self.hypotheses["demon"].change("Contradiction", 1)
            lawyer.change("Authority", 1)
            return self.hypotheses["demon"]

        options = []
        for actor, operation, target, resolver, score, reason, label in (
            (shooter, "accuse_without_evidence", self.demon, accuse, 96, "恐惧给出高冲动效用，但违反第4日共同规则", "要求立刻拘束恶魔"),
            (detective, "present_evidence", self.hypotheses["lunar"], present, 84, "蜡筒、历书和水压纸带可以被其他人复核", "提交月相反馈证据链"),
            (lawyer, "argue_exculpation", self.hypotheses["demon"], defend, 76, "恶魔并非无罪，只是当前破坏假说与其利益冲突", "限定恶魔嫌疑的适用范围"),
        ):
            intent, why = self._intent(actor, operation, target, label, resolver, reason)
            options.append((intent, score, why))
        self._choose(options, scene, tuple(self.characters.values()))

    def _special_18(self):
        musician = self.characters["音乐家"]
        scene = self.scenes["共鸣厅"]
        self.env.assert_fact(E("lunar_window", self.threat))

        def invoke():
            rite = self.words.invoke(
                self.echo_word, musician.souls[0],
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
            self.log.append("第18日：音乐家施行真实言灵“回声标定”。诗人守拍，律师核词，恶魔提供否定发音；伪声显出来自月钟而非核心。")
            return rite

        intent, reason = self._intent(musician, "invoke_word", self.threat, "言灵：回声标定", invoke, "失败记录补齐槽位；亏凸月仍满足观测窗；四方投入可追溯")
        self._choose(((intent, 110, reason),), scene, (musician, self.characters["诗人"], self.characters["律师"], self.demon))

    def _special_19(self):
        lawyer, witness = self.characters["律师"], self.characters["职业棋手"]
        scene = self.scenes["契约审议室"]

        def establish():
            clock = self.sources["clock"]
            rule = Rule(
                E("true"),
                tuple(Directive(Mode.FORBID, F("operate_unlogged", actor, clock)) for actor in self.characters.values()),
                "望日前后三日的共鸣设备必须留档并由第二人复核",
            )
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

        intent, reason = self._intent(lawyer, "establish_rule", self.sources["clock"], "设立临时月相作业规程", establish, "证据链、作用对象、月相窗口、见证者和失效复审日均明确")
        self._choose(((intent, 105, reason),), scene, tuple(self.characters.values()))

    def _special_20(self):
        scene = self.scenes["动力总站"]
        engineer, musician, lawyer = self.characters["机械工程师"], self.characters["音乐家"], self.characters["律师"]

        def demon_hold():
            self.demon.change("Reliability", 2)
            self.threat.change("Suppressed", 1)
            self.log.append("第20日：恶魔主动让出全部广播频段，只保留契约规定的维生脉冲，使回声无法继续借它的声音扩散。")
            return self.threat

        intent, reason = self._intent(self.demon, "yield_broadcast_channel", self.threat, "恶魔暂时让出广播频段", demon_hold, "若传声回声夺取广播，恶魔与学员都会失去维生调度")
        self._choose(((intent, 100, reason),), scene, (self.demon, engineer, musician, lawyer))

        def contain():
            if not (
                self.threat.has("Exposed") and self.lunar_rule and self.provisional_alliance
                and self.projects["containment"].tag("Progress") >= 2
            ):
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

        intent, reason = self._intent(engineer, "joint_containment", self.threat, "完成第一次共同防卫", contain, "四种不同能力形成互锁；任何一方单独行动都只能暂时压制回声")
        self._choose(((intent, 120, reason),), scene, (engineer, musician, lawyer, self.demon))

    def _special(self):
        method = getattr(self, f"_special_{self.day}", None)
        if method:
            method()

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
            ((actor.tag("Authority"), role, actor.name) for role, actor in self.characters.items()),
            reverse=True,
        )[:4]
        self.day_summaries.append({
            "day": self.day, "title": title, "moon": moon,
            "events": sum(event.get("day") == self.day for event in self.runtime.events),
            "resources": {
                "food": self.resources.tag("FoodDays"), "water": self.resources.tag("WaterDays"),
                "power": self.resources.tag("PowerDays"),
            },
            "leaders": tuple((role, name, score) for score, role, name in authorities),
            "gray_pressure": self.gray_tide.tag("Pressure"),
        })

    def run(self, days=20):
        if not 1 <= days <= 20:
            raise ValueError("初期篇只实现第1—20日")
        for day, title, moon, project, roles, social in DAY_SPECS[:days]:
            self.day = day
            self.log.append(f"——第{day}日《{title}》；{moon}——")
            if day == 1:
                self._special()
            self._work(project, roles)
            self._social(*social)
            if day != 1:
                self._special()
            self._end_day(title, moon)
        return self

    def acceptance(self):
        effects = [event.get("effect").op for event in self.runtime.events]
        leaders = sum(actor.tag("Authority") >= 3 for actor in self.characters.values())
        trusted_pairs = sum(bool(actor.related("Trusts")) for actor in self.characters.values())
        return {
            "days": self.day,
            "events": len(self.runtime.events),
            "gray_pressure": self.gray_tide.tag("Pressure"),
            "rules": len(self.rule_patches),
            "analyses": len(self.analyses),
            "leaders": leaders,
            "socialized_characters": trusted_pairs,
            "demon_help": sum(event.get("actor") is self.demon for event in self.runtime.events),
            "word_attempts": len(self.word_attempts),
            "word_success": effects.count("invoke_word"),
            "blocked_accusations": sum(bool(event.get("blocked_candidates")) for event in self.runtime.events),
            "threat_manifested": self.threat.has("Manifested"),
            "threat_contained": self.threat.has("Contained"),
            "arc_complete": self.arc_complete,
        }


__all__ = ["AcademyPrologue", "DAY_SPECS"]
