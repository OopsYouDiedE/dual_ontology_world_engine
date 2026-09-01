from math import inf

from core.python.engine import (
    ActionRuntime,
    Character,
    Directive,
    E,
    Entity,
    F,
    Intent,
    KotodamaSystem,
    Mode,
    Rule,
    Recipe,
    RuleEnvironment,
    RuleSystem,
    RuntimePolicy,
    Soul,
    Timeline,
    WorldSpace,
    aggregate_aspects,
)


ASPECTS = {
    "Form": "形质",
    "Name": "名契",
    "Law": "律令",
    "Mind": "心识",
    "Bond": "缘系",
    "Threshold": "门隙",
    "Preservation": "存续",
    "Death": "终寂",
    "Vestige": "遗痕",
}


class ArchiveNightGame:
    def __init__(self, reaction_handler=None):
        self.reaction_handler = reaction_handler or (lambda _game, _decision: True)
        self.policy = RuntimePolicy(has_geometry=False)
        self.rules = RuleSystem()
        self.reset()

    def reset(self):
        self.world = WorldSpace("抄名室")
        self.inventory = Entity("随身物与记录", self.world)
        self.env = RuleEnvironment()
        self.words = KotodamaSystem(self.env, self.rules)
        self.timeline = Timeline()
        self.log = ["你在闭馆后醒来。入馆证上的姓名正逐笔消失。"]
        self.journal = ["目标：验证伪规则，取得真称和西门封条，再支付身份献礼。"]
        self.trainable_aspects = ("Name", "Law", "Mind", "Threshold", "Preservation")
        self.insight = self.turn = self.anomaly_pulses = self.anchor_uses = 0
        self.main_rule_influences = 0
        self.required_intent_generated = False
        self.crossing_cost_paid = 0.0
        self.busy = self.ended = self.won = False
        self.ending = ""

        self.player_soul = Soul(
            "守名者", self.world, {"Soul": 1, "Stability": 8, "Identity": 6}
        )
        self.borrower_soul = Soul(
            "借名者意识", self.world, {"Soul": 1, "Stability": 8, "Identity": 4}
        )

        self.visitor_rules = self._card(
            "《夜班访客守则》",
            "记录 · 来源不明",
            "文本要求在镜廊询名时如实回答。",
            Record=1,
            Law=1,
            Claim=1,
            Cognitohazard=1,
        )
        self.bell = self._card(
            "铜质呼铃",
            "工具 · 可重复使用",
            "敲击外壳会发出短促回声。",
            Tool=1,
            Law=1,
            Mind=1,
            Catalyst=1,
        )
        self.letter = self._card(
            "母亲的来信",
            "记录 · 身份锚点",
            "家人才知道的乳名维持着关系连续性。",
            Record=1,
            Bond=2,
            Preservation=2,
            Offering=1,
        )
        self.pass_card = self._card(
            "褪色入馆证",
            "工具 · 制度身份",
            "照片仍是你，姓名栏正在褪色。",
            Tool=1,
            Name=2,
            Law=1,
            Authority=1,
        )
        self.chalk = self._card(
            "库房粉笔",
            "工具 · 边界标记",
            "粉末无法附着在伪造入口上。",
            Tool=1,
            Form=1,
            Threshold=1,
        )

        self.document_vault = self._location(
            "文书库", "地点 · 门框不稳定", "保存今晚产生的纸面记录。"
        )
        self._hidden(
            self.document_vault,
            "入库调拨单",
            "记录 · 行为证据",
            "纸张今晚调入，落款却在十年前。",
            Record=1,
            Vestige=2,
            Law=1,
        )
        self.mirror_hall = self._location(
            "镜廊", "地点 · 认知危险", "镜面会复述社会身份并索取姓名。"
        )
        self._hidden(
            self.mirror_hall,
            "镜廊回声记录",
            "记录 · 异常反应",
            "铜铃回声比异常复述早半拍。",
            Record=1,
            Mind=2,
            Vestige=1,
        )
        self.personnel_room = self._location(
            "人事室", "地点 · 制度封锁", "门口告示缺少发行担保。"
        )
        self._hidden(
            self.personnel_room,
            "旧值班名册",
            "记录 · 身份遗痕",
            "无名值班员只留下职位和行为历史。",
            Record=1,
            Name=1,
            Vestige=2,
            Mind=1,
        )
        self.west_gate = self._location(
            "西门", "地点 · 馆舍边界", "它不是出口，而是馆舍边界的封口。"
        )
        self._hidden(
            self.west_gate,
            "西门闭馆封条",
            "工具 · 仪式收束",
            "封条能够固定西门边界。",
            Tool=1,
            Law=3,
            Threshold=2,
            Seal=1,
        )

        for left, right, cost in (
            (self.document_vault, self.mirror_hall, 2),
            (self.document_vault, self.personnel_room, 1),
            (self.mirror_hall, self.personnel_room, 2),
            (self.mirror_hall, self.west_gate, 3),
            (self.personnel_room, self.west_gate, 2),
        ):
            self.world.connect(left, right, cost)

        self.player = Character("夜班访客", self.document_vault, {"Character": 1})
        self.borrower = Character(
            "借名者", self.mirror_hall, {"Character": 1, "Authority": 2}
        )
        self.player.bind(self.player_soul)
        self.borrower.bind(self.borrower_soul)
        for name, value in {
            "visitor": self.player,
            "borrower": self.borrower,
            "mirror": self.mirror_hall,
            "west_gate": self.west_gate,
            "archive": self.world,
        }.items():
            self.env.alias(name, value)
        self.env.assert_fact(E("located_at", self.player, self.document_vault))
        self.player_soul.observe(E("located_at", self.player, self.document_vault))

        self.false_mirror_rule = Rule(
            E("challenged_by", "visitor", "mirror"),
            (Directive(Mode.REQUIRE, F("answer", "visitor", "identity")),),
        )
        self.true_mirror_rule = Rule(
            E("challenged_by", "visitor", "mirror"),
            (
                Directive(Mode.FORBID, F("answer", "visitor", "identity")),
                Directive(Mode.REQUIRE, F("remain_silent", "visitor", "mirror")),
            ),
        )
        self.true_seal_rule = Rule(
            E("located_at", "visitor", "west_gate"),
            (Directive(Mode.REQUIRE, F("seal", "visitor", "west_gate")),),
        )

        persistence = Rule(
            E("true"), (Directive(Mode.PREFER, F("erode", self.player, "stability")),)
        )
        replacement = Rule(
            E("true"),
            (Directive(Mode.REQUIRE, F("replace", self.player, "identity")),),
        )
        self.borrower_soul.consider(persistence, self.borrower, 2)
        self.borrower_soul.consider(replacement, self.borrower, 2)

        activation = Rule(
            E(
                "all",
                E("bound_by", "target", "true_name"),
                E("sealed", "west_gate"),
            ),
            (Directive(Mode.REQUIRE, F("keep_holding", "actor", "identity")),),
        )
        constraint = Rule(
            E("sealed", "west_gate"),
            (Directive(Mode.FORBID, F("leave", "target", "world")),),
        )
        self.sealing_word = Recipe(
            "西门守名契",
            slots={
                "true_name": {
                    "tags": {"TrueName": 1}, "relation": "TrueNameOf",
                    "target": "target", "held": True,
                },
                "seal": {"tags": {"Seal": 1}, "held": True},
                "offering": {"tags": {"Offering": 1}, "held": True},
            },
            thresholds={"Name": 4, "Law": 4, "Threshold": 3, "Preservation": 2},
            requirements=(lambda scope: scope["actor"].parent is scope["west_gate"],),
            costs=(F("sequence", F("consume", "offering"), F("spend", "speaker", "Identity", 1, 1)),),
            effects=(F("sequence", F("remove", "target"), F("assert", E("keep_holding", "actor", "identity"))),),
            triggers=(activation,),
            constraints=(constraint,),
            tags={"Incantation": 1, "Covenant": 1, "Geas": 1, "Seal": 1},
        )
        self.borrower_soul.install_step(self._borrower_choices, source=self.borrower)
        self.runtime = ActionRuntime(
            self.timeline, self.env, self.rules, self.words, event_parent=self.world
        )
        self.timeline.schedule(75, "异常：借名者", self._anomaly_pulse)

    @property
    def stability(self):
        return self.player_soul.tag("Stability")

    @stability.setter
    def stability(self, value):
        self.player_soul.change("Stability", value - self.stability)

    @property
    def identity(self):
        return self.player_soul.tag("Identity")

    @identity.setter
    def identity(self, value):
        self.player_soul.change("Identity", value - self.identity)

    @property
    def exposure(self):
        return self.player_soul.tag("Exposure")

    @exposure.setter
    def exposure(self, value):
        self.player_soul.change("Exposure", value - self.exposure)

    @property
    def inventory_cards(self):
        return sorted(
            (item for item in self.inventory.children if item.has("Card")),
            key=lambda item: self.inventory.children.get(item, 0),
        )

    @property
    def locations(self):
        return sorted(
            (
                item
                for item in self.world.children
                if item.has("Card") and item.has("Location")
            ),
            key=lambda item: self.world.children.get(item, 0),
        )

    def card(self, name):
        return next(
            card
            for card in [*self.inventory_cards, *self.locations]
            if card.name == name
        )

    def _card(self, name, subtitle, description, **tags):
        return Entity(
            name,
            self.inventory,
            {"Card": 1, **tags},
            properties={"subtitle": subtitle, "description": description, "studied": False},
        )

    def _location(self, name, subtitle, description):
        return Entity(
            name,
            self.world,
            {"Card": 1, "Scene": 1, "Location": 1},
            properties={"subtitle": subtitle, "description": description, "studied": False},
        )

    def _hidden(self, parent, name, subtitle, description, **tags):
        return Entity(
            name,
            parent,
            {"Card": 1, **tags},
            properties={"subtitle": subtitle, "description": description, "studied": False},
        )

    def total(self, cards, aspect):
        return aggregate_aspects((*cards, self.player_soul)).get(aspect, 0)

    @property
    def disciplines(self):
        return {name: self.player_soul.tag(name) for name in self.trainable_aspects}

    def _report_review(self, context, allowed, decision):
        if not allowed:
            self.log.append(f"{context}意图被客观条件或言灵约束阻止。")
            return
        if decision.basis:
            self.main_rule_influences += context == "主回合"
            self.log.append(
                f"{context}规则评价：{decision.reason}，决策值 {decision.score}。"
            )

    def submit(self, label, effect, motion_cost, resolution):
        if self.ended or self.busy:
            return False
        intent = Intent(
            self.player, self.player_soul, effect, motion_cost, resolution, label
        )
        self.busy = True

        def complete(succeeded):
            if succeeded and not self.ended:
                self.turn += 1
                self._check_end()
            self.busy = False

        accepted = self.runtime.submit(
            intent,
            context={"phase": "main"},
            on_review=lambda allowed, decision: self._report_review(
                "主回合", allowed, decision
            ),
            on_complete=complete,
        )
        if not accepted:
            self.busy = False
        return accepted

    def study(self, card):
        if (
            card.parent is not self.inventory
            or not (card.has("Record") or card.has("Knowledge"))
            or card.get("studied", False)
        ):
            return self._invalid("只能研读库存中尚未研读的记录或知识。")

        def resolve():
            card.set("studied", True)
            self.insight += 1
            self.log.append(f"研读《{card.name}》：洞见 +1。")
            match card.name:
                case "《夜班访客守则》":
                    self.stability -= 1
                    self.exposure += 1
                    belief = self.player_soul.consider(self.false_mirror_rule, card)
                    self.log.append(
                        f"来源无担保，伪规则状态：{belief.get('stance').name}。稳定 -1。"
                    )
                case "伪造规则的批注":
                    self.player_soul.reject(self.false_mirror_rule)
                    self.player_soul.consider(self.true_mirror_rule, card, 2)
                    self.player_soul.consider(self.true_seal_rule, card, 2)
                    self.journal.append(
                        "已求证：普通记录不能跳过 Soul 认知直接强迫行动。"
                    )
                case "旧值班名册":
                    self.journal.append("证据：十年前的无名值班员只留下称号“借名者”。")

        return self.submit("研读", F("study", self.player, card), 100, resolve)

    def explore(self, location, method=None):
        if (
            location not in self.locations
            or method
            and method.parent is not self.inventory
        ):
            return self._invalid("探查需要地点，以及至多一件库存工具或知识。")
        route = self.policy.interaction_route(self.world, self.player, location)
        crossing = self.policy.interaction_cost(self.world, self.player, location)
        if route is None or crossing == inf:
            return self._invalid(f"无法从当前场景到达{location.name}。")
        cards = [location, *([method] if method else [])]

        def move():
            origin = self.player.parent
            if origin is not location:
                previous = E("located_at", self.player, origin)
                self.env.retract(previous)
                self.player_soul.unobserve(previous)
                self.player.reparent(location)
                current = E("located_at", self.player, location)
                self.env.assert_fact(current)
                self.player_soul.observe(current)
                self.crossing_cost_paid += crossing
                path = " → ".join(scene.name for scene in route.nodes)
                self.log.append(f"跨场景：{path}，穿越代价 {crossing:g}。")

        def success(message, reaction=False):
            def resolve():
                move()
                self.exposure += 2 if reaction else 1
                if reaction:
                    self._mirror_reaction(location)
                else:
                    self._reveal(location)
                    self.log.append(message)

            return resolve

        if location.has("Recovered"):

            def revisit():
                move()
                self.stability = min(8, self.stability + 1)
                self.log.append(f"再次探查{location.name}：没有生成新物品，稳定 +1。")

            resolution = revisit
        else:
            match location.name:
                case "文书库" if (
                    self.total(cards, "Form") + self.total(cards, "Threshold") >= 2
                ):
                    resolution = success("辨出伪造门框，取得入库调拨单。")
                case "镜廊" if (
                    method
                    and method.has("Catalyst")
                    and self.total(cards, "Law") + self.total(cards, "Mind") >= 2
                ):
                    resolution = success("镜廊开始询名。", reaction=True)
                case "人事室" if (
                    method
                    and method.get("studied", False)
                    and method.has("Truth")
                    and self.total(cards, "Law") + self.total(cards, "Mind") >= 4
                ):
                    resolution = success("已求证知识使伪造告示失效，取得旧值班名册。")
                case "西门" if (
                    method
                    and method.has("Authority")
                    and self.total(cards, "Name") >= 3
                ):
                    resolution = success("授权与名契使西门承认操作权限，取得闭馆封条。")
                case _:

                    def failure():
                        move()
                        self.stability -= 1
                        self.exposure += 1
                        self.log.append(f"探查{location.name}失败：稳定 -1，暴露 +1。")

                    resolution = failure
        return self.submit(
            "探查", F("explore", self.player, location), 50 + crossing * 15, resolution
        )

    def verify(self, first, second):
        pair = {first.name, second.name}
        if first is second or any(
            card.parent is not self.inventory or not card.get("studied", False)
            for card in (first, second)
        ):
            return self._invalid("校验需要两张不同的已研读库存记录。")

        def resolve():
            self.insight += 1
            if pair == {"《夜班访客守则》", "入库调拨单"} and not self._has(
                "伪造规则的批注"
            ):
                self._card(
                    "伪造规则的批注",
                    "知识 · 已求证",
                    "守则没有发行担保。",
                    Knowledge=1,
                    Law=2,
                    Mind=2,
                    Truth=1,
                    Guarantee=2,
                )
                self.exposure += 1
                self.log.append("校验形成客观结论：取得伪造规则的批注。")
            elif pair == {"镜廊回声记录", "旧值班名册"} and not self._has("借名者真称"):
                true_name = self._card(
                    "借名者真称",
                    "知识 · 高精度寻址",
                    "行为、职位和关系共同固定目标。",
                    Knowledge=1,
                    Correspondence=1,
                    Naming=3,
                    Name=3,
                    Mind=2,
                    TrueName=1,
                )
                true_name.relate("TrueNameOf", self.borrower, 1, source=first)
                self.exposure += 2
                self.log.append("证据闭合为高精度目标：取得借名者真称。")
            else:
                self.stability = min(8, self.stability + 1)
                self.log.append("没有形成新结论；洞见 +1，稳定 +1。")

        return self.submit("校验", F("verify", self.player, first, second), 90, resolve)

    def anchor(self, card):
        if (
            card.parent is not self.inventory
            or card.tag("Bond")
            + card.tag("Preservation")
            + self.disciplines["Preservation"]
            < 2
        ):
            return self._invalid("锚定需要缘系与存续合计 2。")

        def resolve():
            self.anchor_uses += 1
            self.stability = min(
                8, self.stability + (3 if self.anchor_uses == 1 else 1)
            )
            self.identity = min(6, self.identity + int(self.anchor_uses == 1))
            self.exposure = max(0, self.exposure - 1)
            self.log.append(f"借《{card.name}》重连自我与关系。")

        return self.submit("锚定", F("anchor", self.player, card), 60, resolve)

    def train(self, aspect):
        if self.insight <= 0 or aspect not in self.trainable_aspects or self.ended:
            return False
        self.insight -= 1
        self.player_soul.change(aspect, 1)
        self.log.append(f"洞见投入{ASPECTS[aspect]}。")
        return True

    def rite(self, true_name, seal, offering):
        def resolve():
            contract = self.words.invoke(
                self.sealing_word,
                self.player_soul,
                {"true_name": true_name, "seal": seal, "offering": offering},
                target=self.borrower,
                context={
                    "actor": self.player,
                    "holder": self.inventory,
                    "west_gate": self.west_gate,
                    "world": self.world,
                },
            )
            if not contract:
                self.log.append(f"言灵拒绝成立：{self.words.last_failure}。")
                return
            self.env.assert_fact(E("bound_by", self.borrower, true_name))
            self.env.assert_fact(E("sealed", self.west_gate))
            self.env.assert_fact(E("keep_holding", self.player, "identity"))
            if contract in self.words.trigger():
                self._end(True, "封印成立：借名者实体被移出馆舍，身份献礼已支付。")
            else:
                self.log.append("契约已经建立，触发事实尚未闭合。")

        return self.submit("封印", F("seal", self.player, self.west_gate), 140, resolve)

    def _mirror_reaction(self, location):
        challenge = E("challenged_by", self.player, location)
        self.env.assert_fact(challenge)
        self.player_soul.observe(challenge)
        record = next(card for card in location.children if card.has("Card"))

        def reaction(resisted):
            effect = (
                F("remain_silent", self.player, location)
                if resisted else F("answer", self.player, "identity")
            )

            def resolve():
                self.env.assert_fact(E(effect.op, *effect.args))
                self.env.retract(challenge)
                self.player_soul.unobserve(challenge)
                if resisted:
                    self.log.append("你敲击铜铃并保持沉默。")
                else:
                    self.identity -= 2
                    self.exposure += 2
                    record.change("Tainted", 1)
                    self.log.append("你交出了制度姓名：身份 -2。")
                self._reveal(location)

            return Intent(
                self.player, self.player_soul, effect, 0, resolve,
                "保持沉默" if resisted else "回答姓名",
            )

        answer, silence = reaction(False), reaction(True)
        decisions = self.player_soul.assess(
            (answer, silence), self.rules, self.env,
            lambda action: 5 if action.effect.op == "remain_silent" else 0,
        )
        decision = decisions[0]
        self.required_intent_generated = decision.intent.op == "remain_silent"
        resisted = self.reaction_handler(self, decision)
        chosen = silence if resisted else answer
        allowed, review = self.runtime.review(chosen, {"phase": "reaction"})
        self._report_review("反应子回合", allowed, review)
        if allowed:
            self.runtime.resolve(chosen, {"phase": "reaction"})

    def _reveal(self, location):
        if content := next(
            (card for card in location.children if card.has("Card")), None
        ):
            content.reparent(self.inventory)
            location.change("Recovered", 1)

    def _borrower_choices(self, soul, actor, env, context):
        choices = []
        if self.anomaly_pulses % 2 == 0 and self.exposure >= 4:
            def erode():
                self.stability -= 1
                self.log.append("借名者侵蚀解释结构：稳定 -1。")

            choices.append(Intent(
                actor, soul, F("erode", self.player, "stability"), 75,
                erode, "侵蚀解释结构",
            ))
        if env.evaluate(E("ready_to_replace", self.borrower)):
            def replace_identity():
                self.identity -= 1
                self.log.append("借名者覆盖身份记录：身份 -1。")

            choices.append(Intent(
                actor, soul, F("replace", self.player, "identity"), 75,
                replace_identity, "覆盖身份记录",
            ))
        return tuple(choices)

    def _anomaly_pulse(self):
        if self.ended:
            return None
        self.anomaly_pulses += 1
        exposed, ready = (
            E("exposed", self.player, "high"),
            E("ready_to_replace", self.borrower),
        )
        (self.env.assert_fact if self.exposure >= 4 else self.env.retract)(exposed)
        replace_now = (
            self.anomaly_pulses % 3 == 0
            or self.exposure >= 10
            and self.anomaly_pulses % 4 == 0
        )
        (self.env.assert_fact if replace_now else self.env.retract)(ready)
        action = self.borrower_soul.step(
            self.borrower, self.rules, self.env, {"phase": "anomaly"},
            allows=lambda intent: self.words.allows(intent),
        )
        if action:
            allowed, decision = self.runtime.review(action, {"phase": "anomaly"})
            self._report_review("异常主回合", allowed, decision)
            if allowed:
                self.runtime.resolve(action, {"phase": "anomaly"})
        self._check_end()
        return None if self.ended else 75

    def _has(self, name):
        return any(card.name == name for card in self.inventory_cards)

    def _invalid(self, message):
        self.log.append(message)
        return False

    def _check_end(self):
        match self.identity, self.stability, self.turn:
            case identity, _, _ if identity <= 0:
                self._end(False, "身份被替代。")
            case _, stability, _ if stability <= 0:
                self._end(False, "认知结构崩解。")
            case _, _, turn if turn >= 15:
                self._end(False, "闭馆广播结束。")

    def _end(self, won, message):
        self.won, self.ended, self.busy, self.ending = won, True, False, message
        self.log.append(message)

    def smoke(self):
        take = self.card
        actions = (
            lambda: self.study(take("《夜班访客守则》")),
            lambda: self.explore(take("文书库"), take("库房粉笔")),
            lambda: self.study(take("入库调拨单")),
            lambda: self.verify(take("《夜班访客守则》"), take("入库调拨单")),
            lambda: all(self.train(aspect) for aspect in ("Name", "Law", "Threshold")),
            lambda: self.study(take("伪造规则的批注")),
            lambda: self.explore(take("镜廊"), take("铜质呼铃")),
            lambda: self.study(take("镜廊回声记录")),
            lambda: self.explore(take("人事室"), take("伪造规则的批注")),
            lambda: self.study(take("旧值班名册")),
            lambda: self.verify(take("镜廊回声记录"), take("旧值班名册")),
            lambda: self.explore(take("西门"), take("褪色入馆证")),
            lambda: self.rite(
                take("借名者真称"), take("西门闭馆封条"), take("母亲的来信")
            ),
        )
        if not all(action() for action in actions):
            raise RuntimeError("PYTHON_SMOKE_FAIL: intended action rejected")
        constraint_blocks = not self.words.allows(F("leave", self.borrower, self.world))
        accepted = sum(
            belief.has("Accepted")
            for belief in self.player_soul.beliefs
        )
        checks = (
            self.won,
            self.player.parent is self.west_gate,
            self.borrower.parent is None,
            len(self.player_soul.beliefs) == 3 and accepted == 2,
            self.main_rule_influences == 1,
            self.required_intent_generated,
            self.crossing_cost_paid > 0,
            len(self.words.contracts) == 1 and self.words.contracts[0].get("fired"),
            constraint_blocks and self.words.blocked_intents > 0,
        )
        if not all(checks):
            raise RuntimeError(f"PYTHON_SMOKE_FAIL: checks={checks}")
        fresh = ArchiveNightGame(self.reaction_handler)
        if not (
            fresh.turn == 0
            and len(fresh.inventory_cards) == 5
            and len(fresh.locations) == 4
        ):
            raise RuntimeError("PYTHON_SMOKE_FAIL: restart")
        return (
            f"PYTHON_SMOKE_PASS turn={self.turn} stability={self.stability} identity={self.identity} "
            f"exposure={self.exposure} crossing={self.crossing_cost_paid:g} core=PASS restart=PASS"
        )
