"""《异常感知学园》群像叙事主游戏运行时 (AcademyGame Runtime)。

工作代码：整合世界构造、社交反馈、NPC决策树、行动系统与时间线，
驱动嵌套行动值回合制游戏生命周期。
"""

from __future__ import annotations

from ..core.engine import (
    ActionRuntime,
    E,
    Entity,
    KotodamaSystem,
    RuleEnvironment,
    RuleSystem,
    Timeline,
    WorldSpace,
)
from ..content.academy_data import OBJECTIVE_SPECS
from . import action_system as act
from . import decision_system as dec
from . import social_system as soc
from . import world_builder as builder


class AcademyGame:
    """由实际对象承诺网络驱动的嵌套行动值回合制游戏。"""

    def __init__(self, autonomous=True, social_emotions=True):
        self.autonomous = autonomous
        self.social_emotions = social_emotions
        self.reset()

    def reset(self):
        """重置并初始化完整的学园世界、实体、规则与行动运行时。"""
        # 1. 核心世界与物理时间线容器
        self.world = WorldSpace("灰潮岛异常感知学园")
        self.env = RuleEnvironment()
        self.rules = RuleSystem()
        self.timeline = Timeline()
        self.words = KotodamaSystem(self.env, self.rules)

        # 2. 状态变量与追踪列表
        self.log = ["记忆封存后的第一声钟响。恶魔通过中央厅广播宣布禁闭游戏开始。"]
        self.ended = self.won = False
        self.ending = ""
        self.round = 0
        self.patch_blocks = 0
        self.coordination = None
        self.active_final_rite = None
        self._pending_event_meta = {}
        self._decision_notes = {}
        self._demon_turn = 0
        self._demon_desired = ""

        # 3. 根容器实体
        self.history_archive = Entity("被封存的学园历史", self.world, {"Archive": 1})
        self.rule_archive = Entity("规则与契约", self.world, {"Archive": 1})
        self.team_inventory = Entity("共同调查箱", self.world, {"Container": 1, "Shared": 1})
        self.story_board = Entity("四院共同作战板", self.world, {"ObjectiveBoard": 1})
        self.crisis = Entity(
            "灰潮侵蚀时钟",
            self.world,
            {"Crisis": 1, "Active": 1, "Pressure": 0},
            properties={"limit": 30},
        )

        # 4. 跟踪收集列表
        self.scenes, self.passages = {}, []
        self.characters, self.souls = {}, {}
        self.history, self.memory_carriers = [], []
        self.seal_components, self.objectives = [], []
        self.character_items, self.scene_items = [], []
        self.requests, self.rule_patches = [], []
        self.social_impacts = []
        self.analyses, self.false_publications, self.rule_proposals = [], [], []

        # 5. 调用构建器模块实例化世界对象
        self.scenes, self.passages = builder.build_scenes_and_passages(self.world)
        self.characters, self.souls = builder.build_characters_and_souls(self.world, self.env, self.scenes)
        self.player = self.characters["侦探"]
        self.player_soul = self.souls["侦探"]

        (
            self.mentors,
            self.demon,
            self.demon_soul,
            self.demon_covenant,
        ) = builder.build_faculty_and_demon(
            self.world,
            self.env,
            self.scenes,
            self.history_archive,
            self.rule_archive,
            self.words,
            self.characters,
        )

        self.history, self.memory_carriers = builder.build_histories_and_memories(
            self.history_archive, self.scenes, self.characters
        )
        self.public_rules = builder.build_initial_rules(
            self.world, self.scenes, self.souls, self.demon, self.characters, self.env
        )
        (
            self.tuning_fork,
            self.threshold_token,
            self.rule_ink,
            self.calm_word,
            self.open_word,
            self.refute_word,
            self.final_recipe,
        ) = builder.build_kotodama_and_recipes(self.world, self.rule_archive, self.team_inventory)
        self.word_recipes = (self.calm_word, self.open_word, self.refute_word)

        (
            self.objectives,
            self.seal_components,
            self.accident_inquiry,
        ) = builder.build_objectives_and_board(
            self.story_board, self.history, self.scenes, self.characters, self.demon
        )

        (
            self.character_items,
            self.scene_items,
        ) = builder.build_items_and_furnishings(self.scenes, self.characters)

        # 6. 构建决策树与安装监听器
        dec.build_decision_trees(self)

        self.runtime = ActionRuntime(
            self.timeline,
            self.env,
            self.rules,
            self.words,
            event_parent=self.world,
            witnesses=lambda event, ctx: soc.witnesses_collector(self, event, ctx),
            listeners=(lambda event, ctx: soc.observe_social_outcome(self, event, ctx),),
        )
        self.runtime.patches.extend(self.rule_patches)

        # 7. 激活自主行动 NPC 与恶魔行动槽
        if self.autonomous:
            for index, (role, actor) in enumerate(self.characters.items()):
                if actor is not self.player:
                    dec.activate_actor(self, actor, self.souls[role], 70 + index)
            dec.activate_actor(self, self.demon, self.demon_soul, 30)

        return self

    # ---- 代理系统方法（桥接至各子系统，保持清晰接口） ----------------

    def snapshot(self):
        return act.snapshot(self)

    def make_intent(self, actor, operation, target, cost, resolve, label, reason="", validate=lambda: True, response_mode=None):
        return act.make_intent(self, actor, operation, target, cost, resolve, label, reason, validate, response_mode)

    def context_for(self, intent):
        return act.context_for(self, intent)

    def move_intent(self, actor, destination):
        return act.move_intent(self, actor, destination)

    def investigate_intent(self, actor, carrier):
        return act.investigate_intent(self, actor, carrier)

    def recover_now(self, actor, carrier):
        return act.recover_now(self, actor, carrier)

    def collect_intent(self, actor, item, objective):
        return dec.collect_intent(self, actor, item, objective)

    def begin_rite_intent(self, actor):
        return act.begin_rite_intent(self, actor)

    def contribute_rite_intent(self, actor):
        return act.contribute_rite_intent(self, actor)

    def final_ready(self):
        return act.final_ready(self)

    def story_carrier(self, actor):
        return dec.story_carrier(self, actor)

    def carrier_done(self, actor, carrier):
        return dec.carrier_done(actor, carrier)

    def pending_carrier(self, actor):
        return dec.pending_carrier(self, actor)

    def submit(self, intent, context=None):
        """提交玩家行动意图并触发规则审查。"""
        ctx = context or self.context_for(intent)

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

        result = self.runtime.submit(intent, context=ctx, on_review=reviewed)
        self.refresh_objectives()
        self.check_end()
        self.check_failure()
        return result

    # ---- 玩家动作统一公开接口 -----------------------------------------

    def move(self, actor, destination):
        return act.move(self, actor, destination)

    def recover_memory(self, actor, carrier):
        return act.recover_memory(self, actor, carrier)

    def profession_action(self, actor):
        return act.profession_action(self, actor)

    def collect(self, actor, item):
        return act.collect(self, actor, item)

    def invoke_word(self, actor, recipe, target):
        return act.invoke_word(self, actor, recipe, target)

    def establish_rule(self, actor, witness, operation, subject, target, scope):
        return act.establish_rule(self, actor, witness, operation, subject, target, scope)

    def begin_final_seal(self, actor):
        return act.begin_final_seal(self, actor)

    def advance_final_seal(self, actor):
        return act.advance_final_seal(self, actor)

    # ---- 状态派生、阶段推进与胜负判定 ---------------------------------

    def refresh_objectives(self):
        """刷新四院验证目标阶段；当三份独立 Analysis 齐备后推进至协作验证。"""
        for objective in self.objectives:
            if objective.get("stage", 0) != 0:
                continue
            synthesis = dec.synthesis_for(objective)
            if synthesis is not None:
                for report in synthesis.related("UsesAnalysis"):
                    objective.relate("VerifiedAnalysis", report.value, source=synthesis)
                objective.set("stage", 1)
                objective.change("EvidenceReady", 1)
                self.log.append(
                    f"{objective.name}已由三份独立 Analysis 形成暂定 Synthesis，进入协作验证阶段。"
                )
        if all(obj.get("stage", 0) >= 5 for obj in self.objectives):
            for name in ("源质反应炉", "核心隔离舱"):
                scene = self.scenes[name]
                if scene.has("Locked"):
                    scene.tags.pop("Locked", None)
                    self.env.assert_fact(E("unlocked", scene))

    def check_end(self):
        """检查是否达成真名封印胜利条件。"""
        if self.demon.parent is None and self.env.evaluate(E("sealed", self.demon)):
            self.won = self.ended = True
            self.crisis.tags.pop("Active", None)
            contributors = (
                len(self.active_final_rite.related("Contributor"))
                if self.active_final_rite
                else 0
            )
            self.ending = f"四院防卫重新闭合。{contributors}名不同验证者共同承担仪式，学员保留已恢复的相互矛盾的记忆。"

    def check_failure(self):
        """检查是否因压力爆表或人员丧失行动能力而导致封印失败。"""
        if self.ended:
            return
        incapacitated = sum(
            actor.tag("Panic") >= 9 or actor.tag("Health") <= 0
            for actor in self.characters.values()
        )
        if self.crisis.tag("Pressure") >= self.crisis.get("limit") or incapacitated >= 4:
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
        """推进时间线等待其他人物行动。"""
        target = self.timeline.now + amount
        progressed = False
        while self.timeline.entries and self.timeline.entries[0][0] <= target and not self.ended:
            self.timeline.next()
            progressed = True
        self.round += 1
        return progressed or not self.ended

    # ---- 常用场景与记忆查询辅助函数 -----------------------------------

    def known_memories(self, soul=None):
        soul = soul or self.player_soul
        return tuple(
            k
            for k in soul.children
            if k.has("Knowledge") and k.get("subject") in self.history
        )

    def local_people(self, actor=None):
        actor = actor or self.player
        return tuple(item for item in self.characters.values() if item.parent is actor.parent)

    def local_records(self, actor=None):
        actor = actor or self.player
        return tuple(item for item in actor.parent.children if isinstance(item, Entity) and item.has("MemoryCarrier"))

    def local_components(self, actor=None):
        actor = actor or self.player
        return tuple(item for item in actor.parent.children if isinstance(item, Entity) and item.has("SealComponent"))

    def reachable_scenes(self, actor=None):
        actor = actor or self.player
        return tuple(
            s for s in self.scenes.values()
            if s is not actor.parent and self.world.route(actor.parent, s, actor, self.env)
        )

    def locked_neighbors(self, actor=None):
        actor = actor or self.player
        return tuple(s for s in self.world.links.get(actor.parent, {}) if s.has("Locked"))

    def refuted_false_rules(self):
        return dec.refuted_false_rules(self)


__all__ = ["AcademyGame", "OBJECTIVE_SPECS"]
