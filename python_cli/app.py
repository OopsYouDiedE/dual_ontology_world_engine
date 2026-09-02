import argparse

from .academy_game import AcademyGame
from .ui import TerminalUI


class GameCLI:
    def __init__(self, ui=None):
        self.ui = ui or TerminalUI()
        self.game = AcademyGame()

    def render(self):
        game, actor = self.game, self.game.player
        self.ui.clear()
        self.ui.heading("异常感知学园：灰潮禁闭", "场景拓扑 · 群像决策 · 言灵规则 · 记忆封印")
        self.ui.status((
            ("体力", f"{actor.tag('Health')}/6"),
            ("稳定", f"{game.player_soul.tag('Stability')}/8"),
            ("恐慌", str(actor.tag("Panic"))),
            ("行动值", f"{game.timeline.now:g}"),
            ("场景", actor.parent.name),
        ))
        people = [f"{item.name} · {item.get('team_role')}" for item in game.local_people()]
        records = [item.name for item in game.local_records() if not item.has("Hidden")]
        components = [item.name for item in game.local_components()]
        self.ui.list("在场人物", people)
        self.ui.list("已发现的记忆材料", records)
        self.ui.list("可取得的封印部件", components)
        self.ui.log(game.log)

    def run(self):
        try:
            while not self.game.ended:
                self.render()
                action = self.ui.menu("提交意图", (
                    ("move", "移动到相邻场景"),
                    ("memory", "接触材料并恢复记忆"),
                    ("work", "执行侦探职业行动"),
                    ("collect", "取得封印部件"),
                    ("word", "施行言灵"),
                    ("rule", "在契约审议室设立规则"),
                    ("rite", "开始或推进最终封印"),
                    ("wait", "等待其他人物行动"),
                    ("cast", "查看人物与羁绊状态"),
                    ("journal", "查看已恢复的主观记忆"),
                    ("help", "查看运行说明"),
                    ("quit", "退出"),
                ))
                if action == "quit":
                    return 0
                getattr(self, f"_{action}")()
            self.render()
            self.ui.panel("结局" if self.game.won else "失败", self.game.ending)
            return 0 if self.game.won else 2
        except (EOFError, KeyboardInterrupt):
            print("\n已退出。")
            return 130

    def choose(self, title, entries, label=lambda item: item.name):
        entries = tuple(entries)
        if not entries:
            self.ui.error("没有符合条件的对象。")
            return None
        self.ui.list(title, [label(item) for item in entries])
        raw = self.ui.prompt("选择编号（0 取消）› ")
        return entries[int(raw) - 1] if raw.isdigit() and 1 <= int(raw) <= len(entries) else None

    def _move(self):
        game, actor = self.game, self.game.player
        choices = []
        for scene in game.world.links.get(actor.parent, {}):
            if game.world.route(actor.parent, scene, actor, game.env):
                choices.append(scene)
        destination = self.choose(
            "相邻可达场景", choices,
            lambda scene: f"{scene.name} · 穿越代价 {game.world.route(actor.parent, scene, actor, game.env).cost:g}",
        )
        if destination:
            game.move(actor, destination)

    def _memory(self):
        records = [item for item in self.game.local_records() if not item.has("Hidden")]
        if carrier := self.choose("选择记忆材料", records):
            self.game.recover_memory(self.game.player, carrier)

    def _work(self):
        self.game.profession_action(self.game.player)

    def _collect(self):
        if item := self.choose("选择封印部件", self.game.local_components()):
            self.game.collect(self.game.player, item)

    def _word(self):
        game, actor = self.game, self.game.player
        options = []
        if targets := [item for item in game.local_people() if item.tag("Panic") >= 2]:
            options.append((game.calm_word, targets))
        if targets := list(game.locked_neighbors()):
            options.append((game.open_word, targets))
        selected = self.choose("可施行言灵", options, lambda pair: pair[0].name)
        if selected and (target := self.choose("选择言灵目标", selected[1])):
            game.invoke_word(actor, selected[0], target)

    def _rule(self):
        game, actor = self.game, self.game.player
        witnesses = [item for item in game.local_people() if item is not actor]
        if actor.parent is not game.scenes["契约审议室"]:
            self.ui.error("规则设立只能在契约审议室进行。")
            return
        witness = self.choose("选择见证者", witnesses)
        subject = self.choose("选择受规则约束的人物", (*game.characters.values(), game.demon))
        targets = game.reachable_scenes(subject) if subject in game.characters.values() else game.scenes.values()
        target = self.choose("选择禁止其抵达的场景", targets)
        if witness and subject and target:
            game.establish_rule(actor, witness, "move", subject, target, target)

    def _rite(self):
        game = self.game
        if game.active_final_rite and not game.active_final_rite.has("Completed"):
            game.advance_final_seal(game.player)
        else:
            game.begin_final_seal(game.player)

    def _wait(self):
        self.game.wait(80)

    def _cast(self):
        lines = []
        for role, actor in self.game.characters.items():
            bonds = len(actor.related("VisibleBond"))
            known = len(self.game.known_memories(self.game.souls[role]))
            lines.append(f"{actor.name} · {actor.parent.name} · 已显化羁绊 {bonds} · 已理解记忆 {known}")
        self.ui.list("十六名学员", lines)
        self.ui.prompt("按回车继续…")

    def _journal(self):
        entries = []
        for knowledge in self.game.known_memories():
            subject = knowledge.get("subject")
            claims = "；".join(str(item.value) for item in knowledge.entries("Claim"))
            entries.append(f"{subject.name}：{claims}")
        self.ui.list("雾岛律已经恢复的记忆", entries)
        self.ui.prompt("按回车继续…")

    def _help(self):
        self.ui.panel(
            "运行说明",
            "十六名学员以实际 Objective、HelpRequest 与承诺对象驱动 Soul 决策。隐藏材料必须先被勘查，"
            "记忆按载体分别恢复，并触发分享、质询或保护。职业行动只在存在有限任务时生成。"
            "四院各需三份主观证词、远程赴援、职业验证和部件交付。最终 Rite 要求四名不同验证者参与；"
            "恶魔也在行动值时间线上施压。玩家需要及时设立规则和协同行动令。移动每次只穿越一条 Passage。",
        )
        self.ui.prompt("按回车继续…")


def main(argv=None):
    parser = argparse.ArgumentParser(description="《异常感知学园：灰潮禁闭》Python 终端版")
    parser.add_argument("--no-color", action="store_true", help="关闭 ANSI 颜色")
    parser.add_argument("--no-clear", action="store_true", help="不清屏")
    args = parser.parse_args(argv)
    return GameCLI(TerminalUI(not args.no_color, not args.no_clear)).run()
