"""Audit chapter-one prose and an independent abstract design model.

This does not run Core, Godot, a fluid simulation, sleep physiology, or a playtest.
The model checks specified phase/ownership constraints, not historical uniqueness.
Run: python tools/check_chapter_one_design.py
"""
from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from hashlib import sha256
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
DOC = ROOT / "docs/design-standard.md"
OUT = ROOT / "artifacts/first-awakening/chapter-one-reconstruction-audit.json"


class Actor(str, Enum):
    DETECTIVE = "侦探"
    ENGINEER = "工程师"
    CHEF = "主厨"
    MUSICIAN = "音乐家"
    DEMON = "恶魔"


class Site(str, Enum):
    STUDY = "研习台"
    PREP = "备餐间"
    HALL = "门厅"
    DORM = "宿舍走廊"


@dataclass
class Replay:
    positions: dict = field(default_factory=lambda: {
        Actor.DETECTIVE: Site.HALL, Actor.ENGINEER: Site.STUDY,
        Actor.CHEF: Site.PREP, Actor.MUSICIAN: Site.HALL,
        Actor.DEMON: Site.STUDY,
    })
    knowledge: dict = field(default_factory=lambda: {a: set() for a in Actor})
    busy: set = field(default_factory=set)
    tool_holder: Actor | None = None
    token_holder: Actor | str = "任务位"
    door_braced: bool = False
    door_closed: bool = True
    gray: bool = False
    wedge_soot: bool = False
    wedge_removed: bool = False
    filter_clamped: bool = False
    flowing: bool = False
    keeper: Actor | None = None
    received_gray: bool = False
    upstream_capture: bool = False
    clear: bool = False
    complete: bool = False
    tool_returned: bool = False
    token_returned: bool = False
    receipt: bool = False
    white: bool = False
    drawer_open: bool = False
    stop_moved: bool = False
    bed_forms: bool = False
    traces: set = field(default_factory=set)
    errors: list = field(default_factory=list)
    snapshots: list = field(default_factory=list)

    def fail(self, message):
        self.errors.append(message)
        return False

    def at(self, actor, site):
        return self.positions[actor] == site

    def apply(self, op, actor=None, argument=None):
        ok = True
        if op == "go":
            if actor in self.busy:
                ok = self.fail("持柄父动作未完成；同一身体不能去另一岗位")
            else:
                self.positions[actor] = argument
        elif op == "observe":
            # The caller supplies a validated channel, rather than global knowledge.
            if argument == "door_quiet" and not self.at(actor, Site.HALL):
                ok = self.fail("此位置没有门侧观察渠道")
            elif argument == "pipe_sound" and not self.at(actor, Site.STUDY):
                ok = self.fail("此位置没有台侧压力管观察渠道")
            else:
                self.knowledge[actor].add(argument)
        elif op == "brace":
            if not self.at(actor, Site.HALL) or actor in self.busy:
                ok = self.fail("撑楔需要门侧空闲身体")
            else:
                self.door_braced, self.door_closed = True, False
        elif op == "gray":
            self.gray = True
            if self.door_braced and not self.door_closed:
                self.wedge_soot = True
                self.traces.add("门底遮蔽")
        elif op == "withdraw":
            if not self.at(actor, Site.HALL) or not self.door_braced:
                ok = self.fail("撤楔需在门侧且有支撑物")
            else:
                self.door_braced, self.door_closed, self.wedge_removed = False, True, True
        elif op == "wrap":
            if not self.wedge_removed:
                ok = self.fail("仍在支门的楔不能包裹")
            elif self.wedge_soot:
                self.traces.add("纸内转印")
        elif op == "take":
            if not self.at(actor, Site.STUDY) or self.tool_holder is not None or self.tool_returned:
                ok = self.fail("唯一扳柄不在可取的任务位")
            else:
                self.tool_holder, self.token_holder = actor, actor
        elif op == "clamp":
            if not self.at(actor, Site.PREP) or actor in self.busy:
                ok = self.fail("夹滤需盆侧空闲身体")
            else:
                self.filter_clamped = True
                self.knowledge[Actor.ENGINEER].add("clamp_confirmed")
        elif op == "hold":
            if not self.at(actor, Site.STUDY) or self.tool_holder != actor:
                ok = self.fail("分流需台侧与本人持有扳柄")
            elif "exception_received" not in self.knowledge[actor]:
                ok = self.fail("角色尚未取得并接受持续排灰特例")
            elif not self.filter_clamped or "clamp_confirmed" not in self.knowledge[actor]:
                ok = self.fail("尚未夹滤或主体未获夹滤确认")
            elif actor in self.busy:
                ok = self.fail("该身体已有未完成动作")
            else:
                self.busy.add(actor)
                self.keeper, self.flowing = actor, True
        elif op == "flush":
            if not self.flowing or not self.filter_clamped:
                ok = self.fail("无持续分流与夹滤，不能生成承灰层")
            else:
                self.received_gray = True
                self.upstream_capture = True
                self.traces.add("夹滤承灰")
                self.traces.update({"上游当班捕集灰", "盆内灰"})
        elif op == "clear":
            if not self.received_gray or not self.flowing:
                ok = self.fail("未有有效分流，不能抵达转清阶段")
            else:
                self.clear = True
        elif op == "confirm":
            if not self.at(actor, Site.PREP) or not self.clear:
                ok = self.fail("未从盆侧观察转清，不能给完成确认")
            elif self.keeper is None:
                ok = self.fail("没有接收确认的持柄者")
            else:
                self.knowledge[self.keeper].add("clear_confirmed")
                self.traces.add("转清确认副联")
        elif op == "finish":
            if self.keeper != actor or "clear_confirmed" not in self.knowledge[actor]:
                ok = self.fail("父动作尚未获完成确认")
            else:
                self.flowing, self.complete = False, True
                self.busy.remove(actor)
                self.keeper = None
        elif op == "cancel":
            if self.keeper != actor:
                ok = self.fail("此人没有该持续任务")
            else:
                # Explicit cleanup closes flow, keeps old traces, does not imply success.
                self.flowing = False
                self.busy.remove(actor)
                self.keeper = None
        elif op == "bolt":
            if actor in self.busy or not self.at(actor, Site.HALL) or not self.door_closed:
                ok = self.fail("插销需门侧空闲身体与闭合门扇")
            else:
                self.traces.add("内销")
        elif op == "wipe":
            if not self.at(actor, Site.HALL) or actor in self.busy:
                ok = self.fail("擦地需门侧空闲身体")
            else:
                if self.stop_moved and self.gray:
                    self.traces.add("后发擦带")
                self.traces.add("后擦纤维" if self.received_gray else "仅擦地来源")
        elif op == "move_stop":
            if not self.at(actor, Site.HALL) or actor in self.busy:
                ok = self.fail("移座需门侧空闲身体")
            else:
                self.stop_moved = True
        elif op == "wash":
            if not self.at(actor, Site.PREP):
                ok = self.fail("冲洗需盆侧")
            else:
                self.traces.add("洗布")
                if "后擦纤维" in self.traces or "仅擦地来源" in self.traces:
                    self.traces.add("盆内灰")
        elif op == "write_mistaken_record":
            # A paper statement is evidence with a source, not a physical sensor.
            self.traces.add("转清确认副联")
        elif op == "return_tool":
            if actor in self.busy or not self.at(actor, Site.STUDY) or self.tool_holder != actor:
                ok = self.fail("不能同时持续持柄并归还同一工具")
            else:
                self.tool_returned, self.tool_holder = True, None
                self.white = self.token_returned
        elif op == "return_token":
            if actor in self.busy or not self.at(actor, Site.STUDY) or self.token_holder != actor:
                ok = self.fail("红筹不在该空闲操作者手中")
            else:
                # Stamp follows passage beyond irreversible barrier.
                self.token_holder, self.token_returned, self.receipt = "匣内", True, True
                self.traces.add("红筹接收印")
                self.white = self.tool_returned
        elif op == "bed_forms":
            if not self.white:
                ok = self.fail("清点签条引用的本班白片尚未产生")
            else:
                self.bed_forms = True
                self.traces.add("清点签条")
        elif op == "open_drawer":
            self.drawer_open = True
            self.traces.add("前封条开启")
            self.traces.add("窄手印")
        elif op == "extract":
            if self.token_holder != "匣内" or not self.drawer_open:
                ok = self.fail("红筹仍在匣内，未提供后取路径")
            else:
                self.token_holder = actor
        elif op == "place_chair":
            if self.token_holder != actor:
                ok = self.fail("此人没有可放置的红筹")
            else:
                self.token_holder = "值守椅"
                self.traces.add("椅上红筹")
        else:
            raise ValueError(op)
        self.snapshots.append({"action": op, "actor": actor, "ok": ok,
                               "flowing": self.flowing, "busy": sorted(self.busy),
                               "tool_holder": self.tool_holder,
                               "token_holder": self.token_holder,
                               "receipt": self.receipt, "white": self.white,
                               "complete": self.complete, "traces": sorted(self.traces)})
        return ok


def run(steps):
    result = Replay()
    for step in steps:
        result.apply(*step)
    return result


def count_script(document):
    block = re.search(r"<!-- PLAYER_SCRIPT_BEGIN -->(.*?)<!-- PLAYER_SCRIPT_END -->", document, re.S)
    if not block:
        return {"present": False, "complete": False, "reason": "全文尚未合并"}
    scenes = re.findall(r"<!-- SCENE (\w+) route=(main|alt) -->(.*?)<!-- SCENE_END -->", block[1], re.S)
    counts, seen = {"main": 0, "alt": 0}, set()
    scene_report = []
    mandatory_min = mandatory_max = 0
    for scene_id, route, body in scenes:
        if scene_id in seen:
            raise AssertionError(f"重复场景编号：{scene_id}")
        seen.add(scene_id)
        visible = []
        fixed = 0
        branches = []
        branch = None
        side = 0
        for line in body.splitlines():
            indented = line.startswith(("\t", " "))
            line = line.strip()
            if not line or line.startswith("<!--"):
                continue
            if line.startswith("if "):
                if branch is not None:
                    branches.append(branch)
                branch = [0, 0]
                side = 0
                continue
            if line == "else":
                if branch is None:
                    raise AssertionError(f"{scene_id}存在无对应if的else")
                side = 1
                continue
            if "：" not in line:
                raise AssertionError(f"{scene_id}包含未分类作者文字：{line[:60]}")
            words = line.split("：", 1)[1]
            visible.append(words)
            length = sum(ch.isalnum() for ch in words)
            if branch is not None and indented:
                branch[side] += length
            else:
                if branch is not None:
                    branches.append(branch)
                    branch = None
                fixed += length
        if branch is not None:
            branches.append(branch)
        effective = sum(ch.isalnum() for ch in "".join(visible))
        counts[route] += effective
        shortest = fixed + sum(min(pair) for pair in branches)
        longest = fixed + sum(max(pair) for pair in branches)
        if route == "main":
            mandatory_min += shortest
            mandatory_max += longest
        scene_report.append({"scene": scene_id, "route": route, "effective_characters": effective,
                             "visible_path_characters": [shortest, longest]})
    total = sum(counts.values())
    return {"present": True, "complete": 9000 <= total <= 12000,
            "counting": "Unicode汉字/字母/数字，排除角色前缀、标点、空白、作者标记及条件；全稿包括互斥分支，必经路径报告条件分支字数范围，非净游玩测量",
            "total": total, "mandatory": mandatory_min, "mandatory_max": mandatory_max, "optional": counts["alt"],
            "reading_minutes_total_300_to_250": [round(total/300, 2), round(total/250, 2)],
            "reading_minutes_mandatory_300_to_250": [round(mandatory_min/300, 2), round(mandatory_max/250, 2)],
            "scenes": scene_report}


def audit_model():
    checks = []
    def check(name, condition):
        checks.append({"check": name, "passed": bool(condition)})
        if not condition:
            raise AssertionError(name)
    base = [("brace", Actor.MUSICIAN), ("gray",), ("withdraw", Actor.DETECTIVE),
            ("wrap", Actor.DETECTIVE)]
    task = [("observe", Actor.ENGINEER, "exception_received"), ("take", Actor.ENGINEER),
            ("clamp", Actor.CHEF), ("hold", Actor.ENGINEER), ("flush",), ("clear",),
            ("confirm", Actor.CHEF), ("finish", Actor.ENGINEER),
            ("return_tool", Actor.ENGINEER), ("return_token", Actor.ENGINEER), ("bed_forms",)]
    cleanup = [("go", Actor.CHEF, Site.HALL), ("move_stop", Actor.CHEF),
               ("wipe", Actor.CHEF), ("go", Actor.CHEF, Site.PREP), ("wash", Actor.CHEF)]
    later = [("open_drawer", Actor.DEMON), ("extract", Actor.DEMON), ("place_chair", Actor.DEMON)]
    good = run(base+task+cleanup+later)
    expected = {"门底遮蔽", "纸内转印", "夹滤承灰", "上游当班捕集灰", "转清确认副联", "后发擦带", "后擦纤维",
                "洗布", "红筹接收印", "清点签条", "前封条开启", "窄手印", "椅上红筹"}
    check("正确路径可执行并覆盖规定材料", not good.errors and expected <= good.traces)
    check("后取不倒退接收事实", good.receipt and good.white and good.token_holder == "值守椅")
    check("结束时无占用或流路泄漏", not good.busy and not good.flowing)
    early = run(base+task[:6])
    check("转清而未确认仍保持占用", bool(early.busy) and early.flowing and not early.complete)
    check("同一身体不能离位插销", not early.apply("go", Actor.ENGINEER, Site.HALL))
    check("同一工具不能保持并归还", not early.apply("return_tool", Actor.ENGINEER))
    check("他人可并行做门侧动作", early.apply("bolt", Actor.DETECTIVE))
    check("安全取消显式关闭并解除占用", early.apply("cancel", Actor.ENGINEER) and not early.busy and not early.flowing)
    check("安全取消保留已有灰层却不标任务成功", "夹滤承灰" in early.traces and not early.complete)
    wrong_one = run(base+[("observe", Actor.ENGINEER, "exception_received"), ("take", Actor.ENGINEER),
                         ("clamp", Actor.CHEF), ("hold", Actor.ENGINEER), ("cancel", Actor.ENGINEER),
                         ("flush",), ("go", Actor.ENGINEER, Site.HALL), ("bolt", Actor.ENGINEER)]
                    +cleanup+[("bed_forms",)])
    check("早停未归解释可有门锁洗布但缺承灰和接收", "内销" in wrong_one.traces and "洗布" in wrong_one.traces
          and "夹滤承灰" not in wrong_one.traces and not wrong_one.receipt and not wrong_one.white)
    wrong_two = run(base+[("observe", Actor.CHEF, "exception_received"), ("take", Actor.ENGINEER),
                         ("clamp", Actor.CHEF), ("hold", Actor.CHEF), ("flush",)])
    check("盆侧不能远程使用台侧他人持有工具", bool(wrong_two.errors) and not wrong_two.received_gray)
    washing_h2 = run(base+cleanup+[("take", Actor.ENGINEER), ("write_mistaken_record", Actor.CHEF),
                                  ("return_tool", Actor.ENGINEER), ("return_token", Actor.ENGINEER),
                                  ("bed_forms",)]+later)
    check("完整H2可解释盆灰湿布记录归还及后移", not washing_h2.errors
          and {"盆内灰", "洗布", "转清确认副联", "红筹接收印", "椅上红筹"} <= washing_h2.traces)
    check("完整H2不能逆流生成上游当班捕集灰", not washing_h2.upstream_capture
          and "上游当班捕集灰" not in washing_h2.traces)
    check("误写完成不改变客观转清或任务成功", not washing_h2.clear and not washing_h2.complete)
    unreturned_h1 = run(base+task[:8]+cleanup+[("place_chair", Actor.ENGINEER), ("bed_forms",)])
    check("完整H1可保留真实排灰洗布与椅上筹", unreturned_h1.upstream_capture
          and unreturned_h1.complete and {"洗布", "椅上红筹"} <= unreturned_h1.traces)
    check("完整H1从未归还与当班印白片冲突", not unreturned_h1.receipt and not unreturned_h1.white)
    no_note = run(base+[("take", Actor.ENGINEER), ("clamp", Actor.CHEF), ("hold", Actor.ENGINEER)])
    check("未获特例不能依作者全知启动该策略", bool(no_note.errors) and not no_note.busy)
    unclamped = run(base+[("observe", Actor.ENGINEER, "exception_received"), ("take", Actor.ENGINEER), ("hold", Actor.ENGINEER)])
    check("先夹滤确认再开流", bool(unclamped.errors) and not unclamped.flowing)
    one_slot = run(base+task[:9]+[("bed_forms",)])
    check("只归扳柄不释放白片", one_slot.tool_returned and not one_slot.white)
    stale = run(base+task+[("extract", Actor.DEMON), ("place_chair", Actor.DEMON)])
    check("无开匣路径不能把已收红筹放回椅", bool(stale.errors) and stale.token_holder == "匣内")
    quiet = Replay()
    quiet.apply("observe", Actor.MUSICIAN, "door_quiet")
    quiet.apply("observe", Actor.ENGINEER, "pipe_sound")
    check("不同渠道报告可相容", "door_quiet" in quiet.knowledge[Actor.MUSICIAN]
          and "pipe_sound" in quiet.knowledge[Actor.ENGINEER])
    check("一个人的感知不自动进入他人世界", "pipe_sound" not in quiet.knowledge[Actor.MUSICIAN])
    check("渠道限制未取得信息", not quiet.apply("observe", Actor.CHEF, "door_quiet"))
    variants = []
    for bolter in (Actor.DETECTIVE, Actor.MUSICIAN):
        for clean_before_return in (False, True):
            prefix = base+[("bolt", bolter)]
            steps = prefix+task[:8]+(cleanup if clean_before_return else [])+task[8:]
            if not clean_before_return:
                steps += cleanup
            candidate = run(steps+later)
            variants.append({"bolter": bolter, "cleanup_before_return": clean_before_return,
                             "compatible": not candidate.errors and expected <= candidate.traces})
    check("至少四个允许的等价局部顺序，未伪称全序唯一", all(v["compatible"] for v in variants))
    canceled = run(base+task[:5]+[("cancel", Actor.ENGINEER), ("return_tool", Actor.ENGINEER), ("return_token", Actor.ENGINEER)])
    check("反例：交回工具也可能未完成排灰", canceled.white and not canceled.complete)
    return {"checks": checks, "compatible_variants": variants,
            "correct_path": {"traces": sorted(good.traces), "errors": good.errors},
            "complete_competitor_h2": {"name": "洗布水冒充排灰完成", "traces": sorted(washing_h2.traces),
                                       "errors": washing_h2.errors, "missing_observed_trace": "上游当班捕集灰"},
            "complete_competitor_h1": {"name": "排灰后从未归还", "traces": sorted(unreturned_h1.traces),
                                       "errors": unreturned_h1.errors, "missing_observed_traces": ["红筹接收印", "清点签条"]},
            "partial_wrong_paths": [{"name": "瞬时开流后早停未归", "traces": sorted(wrong_one.traces), "errors": wrong_one.errors},
                                    {"name": "盆侧远程持柄", "traces": sorted(wrong_two.traces), "errors": wrong_two.errors}],
            "limitations": ["这是规则设计的独立抽象校验，并非现有Core或Godot运行",
                            "错误路径只校验局部可执行性；完整竞争解释与证据真实性另经人工审查",
                            "不证明流体/材质残留、历史真实性、睡眠机制或净游玩时长",
                            "新增条件挂起与取消清理尚未在Core实现"]}


def main():
    raw = DOC.read_bytes()
    document = raw.decode("utf-8-sig")
    result = {"artifact": "chapter-one-reconstruction-design-audit", "source": str(DOC),
              "source_sha256": sha256(raw).hexdigest(), "model": audit_model(),
              "script": count_script(document), "runtime_verified": False,
              "human_playtime_verified": False}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    print(json.dumps({"output": str(OUT), "model_checks": len(result["model"]["checks"]),
                      "script": {k: v for k, v in result["script"].items() if k != "scenes"},
                      "runtime_verified": False}, ensure_ascii=False))


if __name__ == "__main__":
    main()
