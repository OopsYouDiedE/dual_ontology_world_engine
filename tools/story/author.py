"""Author-only checks over the production DV export. This tool never advances a game.

No second DV parser and no LLM service are hidden here: export calls the real CLI,
and prompt prepares a reviewable local handoff for the author's chosen generator.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
FACETS = {"state", "phys", "trace", "lock", "doc", "infer"}


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def project_path(value):
    path = (ROOT / str(value).removeprefix("res://")).resolve()
    if not path.is_relative_to(ROOT):
        raise ValueError("作者工具只访问当前项目内文件")
    return path


def output_path(value):
    path = project_path(value)
    if not path.is_relative_to(ROOT / ".runtime"):
        raise ValueError("派生作者视图和检查回执必须放入 .runtime")
    path.parent.mkdir(parents=True, exist_ok=True)
    return path


def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def field(value):
    """Normalize already-parsed compact field references, not DV source syntax."""
    if isinstance(value, dict):
        return ".".join(str(value.get(k, value.get(k.title(), ""))) for k in ("object", "facet", "field"))
    prefix, sep, name = str(value).rpartition(".")
    if not sep:
        raise ValueError("字段缺少对象: " + str(value))
    obj, dot, facet = prefix.rpartition(".")
    return f"{obj}.{facet}.{name}" if dot and facet in FACETS else f"{prefix}.state.{name}"


def refs(node):
    if isinstance(node, dict):
        if {"object", "field"} <= node.keys():
            yield field(node)
        for item in node.values():
            yield from refs(item)
    elif isinstance(node, list):
        for item in node:
            yield from refs(item)


def compact_refs(node):
    return set(refs(node)) if "op" in node else {field(key) for key in node}


def equals(node):
    if isinstance(node, dict):
        if node.get("op") == "equals" and "ref" in node:
            yield field(node["ref"]), node.get("value")
        for item in node.values():
            yield from equals(item)
    elif isinstance(node, list):
        for item in node:
            yield from equals(item)


def absence_assertions(node):
    if isinstance(node, dict):
        if node.get("op") == "not" and node.get("item", {}).get("op") == "known":
            yield from refs(node["item"])
        for item in node.values():
            yield from absence_assertions(item)
    elif isinstance(node, list):
        for item in node:
            yield from absence_assertions(item)


def objects_fields(objects):
    for obj, item in objects.items():
        for facet, values in item.get("facets", {}).items():
            for name in values:
                yield f"{obj}.{facet}.{name}"


class Inspection:
    def __init__(self, view, contract):
        self.view, self.contract = view, contract
        self.data = view["declaration"]
        self.actors = {a["id"]: a for a in self.data["actors"]}
        self.owner = {a["id"]: actor for actor, definition in self.actors.items() for a in definition["actions"]}
        self.actions = {a["id"]: a for definition in self.actors.values() for a in definition["actions"]}
        self.laws = self.data["physical"]["adoptions"]
        self.proofs = {p["action"]: p for p in view.get("author_proofs", [])}
        self.fields = set(objects_fields(self.data["physical"]["objects"]))
        self.producers = defaultdict(list)
        self.writes = {}
        self.checks = []
        self.warnings = []
        self.index()

    def add_source(self, actor, fact, kind, action):
        self.producers[(actor, field(fact))].append({"kind": kind, "action": action})

    def index(self):
        for actor, definition in self.actors.items():
            for fact in objects_fields(definition["mind"]["objects"]):
                self.add_source(actor, fact, "initial_memory", "")
            for sensor in definition.get("perceptions", []):
                for ref in sensor.get("refs", []):
                    self.add_source(actor, ref, "perception", sensor["id"])
            for name, rule in definition["mind"].get("adoptions", {}).items():
                for effect in rule["law"].get("effects", []):
                    self.fields.add(field(effect["ref"]))
                    self.add_source(actor, effect["ref"], "inference", name)
            for action in definition["actions"]:
                action_id = action["id"]
                effects = self.laws[action["law"]]["law"].get("effects", [])
                self.writes[action_id] = {field(effect["ref"]): effect.get("value") for effect in effects if effect.get("op") == "assign"}
                self.fields.update(self.writes[action_id])
                for ref in action.get("grants", []):
                    self.add_source(actor, ref, "action_feedback", action_id)
                for handoff in action.get("handoffs", []):
                    for ref in handoff.get("refs", []) + handoff.get("result_refs", []):
                        self.add_source(handoff["actor"], ref, "received_report", action_id)
                for fact in action.get("deduction", {}).get("learns", {}):
                    self.fields.add(field(fact))
                    self.add_source(actor, fact, "deduction", action_id)
                if proposal := action.get("suggestion"):
                    self.fields.add(field(proposal["result"]))
                    for observer in (actor, proposal["target"]):
                        self.add_source(observer, proposal["result"], "opinion_feedback", action_id)
                    for fact in proposal["evidence"]:
                        self.add_source(proposal["target"], fact, "proposal_evidence", action_id)
                if magic := action.get("incantation"):
                    for fact in (magic["resource"], magic["backlash"]["field"], magic["rule"]["field"]):
                        self.add_source(actor, fact, "incantation_feedback", action_id)
                    for observer in magic.get("witnesses", []):
                        self.add_source(observer, magic["rule"]["field"], "witnessed_rule", action_id)
                if strategy := action.get("strategy"):
                    self.fields.add(field(strategy["output"]))
                    self.add_source(actor, strategy["output"], "own_strategy_result", action_id)
        for event in self.data.get("world_events", []):
            for fact in event.get("writes", {}):
                self.fields.add(field(fact))
                for observer in event.get("tell", []):
                    self.add_source(observer, fact, "perceived_world_event", event["id"])

    def check(self, id, ok, detail):
        self.checks.append({"id": id, "passed": bool(ok), "detail": detail})

    def action(self, id):
        result = self.actions.get(id)
        self.check("reference/action/" + id, result is not None, id)
        return result or {}

    def conditions(self, action, objective=False):
        node = action.get("authority_precondition", {}) if objective else action.get("precondition", {})
        return set(refs(node))

    def run(self):
        self.check("production_export", self.view.get("schema") == "story-authoring-view-v1" and self.view.get("initialization") == "validated_without_stepping" and self.view.get("gameplay_accepted") is False, "正式解析、展开和初始化；没有推进会话")
        for path, source in self.view.get("source_files", {}).items():
            actual = project_path(source["actual_path"])
            self.check("source/" + path, actual.exists() and sha(actual) == source["sha256"], source)
        for source in self.contract["source_anchors"]:
            self.check("anchor/" + source["path"], project_path(source["path"]).is_file(), source)
        all_refs = set(refs(self.data))
        # An explicit assertion that a fabricated proposition is NOT known may
        # intentionally refer to no declared world fact. Never create that fact.
        absent_assertions = set(absence_assertions(self.data.get("expectations", [])))
        runtime_refs = set(refs({key: value for key, value in self.data.items() if key != "expectations"}))
        optional_absences = absent_assertions - runtime_refs
        all_refs -= optional_absences
        missing = sorted(all_refs - self.fields)
        self.check("declared_references", not missing, {"field_count": len(self.fields), "missing": missing, "explicit_not_known_expectations": sorted(optional_absences)})
        self.check("distinct_action_ids", len(self.actions) == sum(len(a["actions"]) for a in self.actors.values()), len(self.actions))
        self.check_investigations()
        self.check_opinions_and_spell()
        self.check_branches()
        self.check_reports_and_exits()
        self.check_planning()
        self.check_autonomous_observation()
        return {"schema": "story-authoring-check-v1", "content_hash": self.view.get("content_hash"), "source_fingerprint": self.view.get("source_fingerprint"),
                "scope": "production-expanded declaration, provenance possibilities, proof design and explicit content contract",
                "checks": self.checks, "passed": sum(c["passed"] for c in self.checks), "total": len(self.checks), "failed": [c for c in self.checks if not c["passed"]],
                "warnings": self.warnings, "gameplay_accepted": False,
                "literary_quality": {"automatic_verdict": None, "requires_human_review": True, "review_method": "将实际先见、证据重读、人物选择与付出代价逐项对照；第五日不预写必然转折。"},
                "limits": ["存在可获取来源不等于所有时序下必能获取。", "全前提证明检查是假设性作者检查，不向游戏人物授予任何证据。", "导航、独立自主五日与接管趣味性须由正式Godot新局另行验证。"]}

    def check_autonomous_observation(self):
        spec = self.contract["completion"]
        completion = self.data.get("completion", {})
        phase = self.data.get("autonomy_phase", {})
        self.check("completion/observation_window", completion.get("mode") == spec["mode"] and completion.get("end_minutes") == spec["end_minutes"] and self.data.get("run", {}).get("end_minutes") == spec["end_minutes"], completion)
        self.check("completion/no_scripted_facts", not completion.get("facts"), completion.get("facts", []))
        self.check("autonomy/no_task_guidance", phase.get("task_guidance") is False and phase.get("start_minutes") == spec["autonomy_start_minutes"], phase)
        autonomous = self.contract["autonomous_day"]
        day = autonomous["day"]
        actions = {id: action for id, action in self.actions.items() if action.get("campaign_day") == day}
        tasks = [task for actor in self.actors.values() for task in actor.get("planning", {}).get("tasks", []) if any(step.get("action") in actions for step in task.get("steps", []))]
        self.check("autonomy/no_plot_tasks", not tasks, [task["id"] for task in tasks])
        events = [event.get("id") for event in self.data.get("world_events", []) if event.get("id", "").startswith("day" + str(day) + ":")]
        self.check("autonomy/no_scheduled_plot_events", not events, events)
        forbidden = {field(item) for item in autonomous["forbidden_closure_fields"]}
        gates = {id: sorted((self.conditions(action) | self.conditions(action, True)) & forbidden) for id, action in actions.items()}
        self.check("autonomy/no_forced_closure_gates", not any(gates.values()), {id: value for id, value in gates.items() if value})
        physical = self.data["physical"]["objects"]
        resources = {field(item) for item in autonomous["shared_resource_fields"]}
        resource_actors = set()

        def lower_bounds(condition):
            if condition.get("op") == "at_least":
                yield field(condition["ref"]), condition.get("value")
            elif condition.get("op") == "all":
                for item in condition.get("items", []):
                    yield from lower_bounds(item)

        for id, action in actions.items():
            law = self.laws[action["law"]]["law"]
            for effect in law.get("effects", []):
                if effect.get("op") != "increment":
                    continue
                key = field(effect["ref"])
                r = effect["ref"]
                initial = physical.get(r["object"], {}).get("facets", {}).get(r.get("facet", "state"), {}).get(r["field"], {})
                value = initial.get("value")
                self.check("autonomy/numeric_initial/" + id + "/" + key, initial.get("known") is True and isinstance(value, (int, float)) and not isinstance(value, bool), initial)
                if key in resources:
                    resource_actors.add(self.owner[id])
                delta = effect.get("value")
                if not isinstance(delta, (int, float)) or isinstance(delta, bool) or delta >= 0:
                    continue
                authority = dict(lower_bounds(action.get("authority_precondition", {})))
                premise = dict(lower_bounds(law.get("premise", {})))
                guarded = authority.get(key, float("-inf")) >= -delta and premise.get(key, float("-inf")) >= -delta
                self.check("autonomy/physical_stock_guard/" + id + "/" + key, guarded and bool(action.get("resource_reservations")), {"delta": delta, "admission": authority.get(key), "commit": premise.get(key), "reservations": action.get("resource_reservations", [])})
        self.check("autonomy/multiple_resource_actors", len(resource_actors) >= autonomous["minimum_independent_resource_actors"], sorted(resource_actors))
        sensors = [sensor for actor in self.actors.values() for sensor in actor.get("perceptions", []) if sensor.get("repeat_on_change")]
        scoped = all(any(ref.endswith(".state.room") for ref, value in equals(sensor.get("condition", {}))) for sensor in sensors)
        self.check("autonomy/in_place_changed_observations", bool(sensors) and scoped, {"count": len(sensors), "rule": "仅检查声明的在场门槛；实机仍需核对来源与版本。"})
        self.check_resource_locations(actions, autonomous)
        self.check_self_awareness_and_delivery(actions, autonomous)

    def check_self_awareness_and_delivery(self, actions, autonomous):
        for actor, fields in autonomous["self_observable_fields"].items():
            for key in fields:
                wanted = field(key)
                sources = [sensor for sensor in self.actors[actor].get("perceptions", [])
                           if wanted in {field(item) for item in sensor.get("refs", [])}
                           and wanted not in set(refs(sensor.get("condition", {})))
                           and wanted not in set(refs(sensor.get("knowledge_condition", {})))]
                self.check("autonomy/own_state_obtainable/" + wanted, bool(sources), [sensor["id"] for sensor in sources])
        private = {field(key) for key in autonomous["private_self_fields"]}
        for actor, declaration in self.actors.items():
            leaks = [sensor["id"] for sensor in declaration.get("perceptions", [])
                     if any(field(item) in private and item["object"] != actor for item in sensor.get("refs", []))]
            self.check("autonomy/no_other_person_subjective_state/" + actor, not leaks, leaks)

        def bounds(condition):
            if condition.get("op") == "at_least":
                yield field(condition["ref"]), condition.get("value")
            elif condition.get("op") == "all":
                for item in condition.get("items", []):
                    yield from bounds(item)

        def positive_equals(condition):
            if condition.get("op") == "equals":
                yield field(condition["ref"]), condition.get("value")
            elif condition.get("op") == "all":
                for item in condition.get("items", []):
                    yield from positive_equals(item)

        for actor, declaration in self.actors.items():
            own = {id: action for id, action in actions.items() if self.owner[id] == actor}
            if not own:
                continue
            spaces = [room.replace("{home_room}", declaration["home_room"]) for room in autonomous["transport_spaces"]["default"]]
            spaces += autonomous["transport_spaces"]["extra"].get(actor, [])
            for cargo, route in autonomous["carried_inventory_routes"].items():
                key = field(actor + ".phys." + cargo)
                amount, destination = route["amount"], route["delivery"]
                deliveries = []
                for id, action in own.items():
                    effects = self.laws[action["law"]]["law"].get("effects", [])
                    cargo_effects = [effect for effect in effects if field(effect["ref"]) == key and effect.get("op") == "increment"]
                    for effect in cargo_effects:
                        if effect["value"] > 0:
                            gate = dict(positive_equals(action.get("precondition", {})))
                            physical = dict(positive_equals(action.get("authority_precondition", {})))
                            self.check("autonomy/no_duplicate_cargo/" + id + "/" + key, gate.get(key) == 0 and physical.get(key) == 0, {"knowledge_empty": gate.get(key), "physical_empty": physical.get(key)})
                        elif effect["value"] <= -amount:
                            deliveries.append(id)
                self.check("autonomy/cargo_has_delivery/" + key, bool(deliveries), deliveries)
                for room in spaces:
                    if room == destination:
                        continue
                    paths = [id for id, action in own.items()
                             if action.get("kind") == "walk"
                             and (field(actor + ".room"), room) in list(positive_equals(action.get("precondition", {})))
                             and self.writes.get(id, {}).get(field(actor + ".room")) == destination
                             and dict(bounds(action.get("precondition", {}))).get(key, float("-inf")) >= amount]
                    self.check("autonomy/loaded_route/" + key + "/" + room, bool(paths), {"destination": destination, "actions": paths})

    def check_resource_locations(self, actions, autonomous):
        locations = {field(key): set(value) for key, value in autonomous["resource_locations"].items()}
        routes = autonomous["carried_inventory_routes"]

        def fixed_rooms(condition, actor):
            # A negated equality or one branch of an OR does not establish presence.
            if condition.get("op") == "equals" and field(condition.get("ref", "missing.field")) == field(actor + ".room"):
                return {condition.get("value")}
            if condition.get("op") == "all":
                return set().union(*(fixed_rooms(item, actor) for item in condition.get("items", [])))
            return set()

        for actor, declaration in self.actors.items():
            for sensor in declaration.get("perceptions", []):
                if not sensor.get("repeat_on_change"):
                    continue
                observed = {field(item) for item in sensor.get("refs", [])} & locations.keys()
                rooms = fixed_rooms(sensor.get("condition", {}), actor)
                for key in observed:
                    self.check("autonomy/local_resource_perception/" + sensor["id"] + "/" + key, len(rooms) == 1 and rooms <= locations[key], {"rooms": sorted(rooms), "resource_rooms": sorted(locations[key])})
        for id, action in actions.items():
            actor = self.owner[id]
            rooms = fixed_rooms(action.get("precondition", {}), actor)
            law = self.laws[action["law"]]["law"]
            for grant in action.get("grants", []):
                key = field(grant)
                if key in locations:
                    self.check("autonomy/local_resource_feedback/" + id + "/" + key, len(rooms) == 1 and rooms <= locations[key], {"rooms": sorted(rooms), "resource_rooms": sorted(locations[key])})
            for effect in law.get("effects", []):
                key = field(effect["ref"])
                if key in locations:
                    self.check("autonomy/local_resource_mutation/" + id + "/" + key, len(rooms) == 1 and rooms <= locations[key], {"op": effect.get("op"), "rooms": sorted(rooms), "resource_rooms": sorted(locations[key])})
                r = effect["ref"]
                if r.get("facet") != "phys" or r.get("field") not in routes:
                    continue
                route = routes[r["field"]]
                delta = effect.get("value")
                expected = route["pickup"] if isinstance(delta, (int, float)) and delta > 0 else route["delivery"]
                self.check("autonomy/physical_carrier/" + id + "/" + key, r["object"] == actor and effect.get("op") == "increment" and rooms == {expected}, {"owner": actor, "field_owner": r["object"], "delta": delta, "rooms": sorted(rooms), "required_room": expected})

    def check_investigations(self):
        for chain in self.contract["investigations"]:
            id = chain["id"]; action = self.action(chain["deduction"]); declaration = action.get("deduction", {})
            for acquire in chain["obtain"]:
                observed = self.action(acquire)
                self.check(id + "/observation/" + acquire, observed.get("kind") in {"inspect", "read", "talk", "interact"} and bool(observed.get("focus")) and bool(observed.get("grants")), {"focus": observed.get("focus"), "kind": observed.get("kind")})
            learned = {field(item) for item in declaration.get("learns", {})}
            self.check(id + "/learns", {field(item) for item in chain["learns"]} <= learned, sorted(learned))
            self.check(id + "/qualification", not chain.get("qualification_required") or bool(declaration.get("qualification", "").strip()), declaration.get("qualification"))
            proof = self.proofs.get(chain["deduction"], {})
            checked = proof.get("checked_certificate") or {}
            self.check(id + "/production_proof_design", proof.get("all_premises_status") == "proved" and proof.get("empty_premises_status") != "proved" and checked.get("Valid") is True and len(proof.get("used_premises") or []) >= 2, proof)
            evidence = declaration.get("evidence", [])
            self.check(id + "/evidence_budget", 2 <= len(evidence) <= 32, len(evidence))
            owner = self.owner.get(chain["deduction"], self.contract["viewer"])
            for item in evidence:
                for fact in compact_refs(item["when"]):
                    producers = [p for p in self.producers[(owner, fact)] if p["action"] != chain["deduction"]]
                    self.check(id + "/source/" + item["id"] + "/" + fact, bool(producers), producers)
            for unlock in chain["unlocks"]:
                target = self.action(unlock)
                self.check(id + "/cognition_unlock/" + unlock, bool(learned & self.conditions(target)), sorted(learned & self.conditions(target)))
            # Initial cognition may contain state/resources, never this chain's result.
            initial = set(objects_fields(self.actors.get(owner, {}).get("mind", {}).get("objects", {})))
            self.check(id + "/not_initially_known", not learned & initial, sorted(learned & initial))

    def check_opinions_and_spell(self):
        for id, action in self.actions.items():
            if not (proposal := action.get("suggestion")):
                continue
            profile = self.actors.get(proposal.get("target"), {}).get("opinion", {})
            self.check("opinion/profile/" + id, bool(profile) and "threshold" in profile and "values" in profile, {"target": proposal.get("target"), "threshold": profile.get("threshold")})
            self.check("opinion/evidence/" + id, bool(proposal.get("evidence")) and all(self.producers[(self.owner[id], field(item))] for item in proposal.get("evidence", [])), proposal.get("evidence"))
            self.check("opinion/cost_risk/" + id, isinstance(proposal.get("cost"), (float, int)) and proposal["cost"] >= 0 and isinstance(proposal.get("risk"), (float, int)) and proposal["risk"] >= 0 and all(-1 <= v <= 1 for v in proposal.get("appeal", {}).values()), {"cost": proposal.get("cost"), "risk": proposal.get("risk"), "appeal": proposal.get("appeal")})
        spec = self.contract["spell"]; action = self.action(spec["action"]); magic = action.get("incantation", {})
        self.check("spell/cognitive_gate", field(spec["required_knowledge"]) in compact_refs(magic.get("requires", {})), magic.get("requires"))
        self.check("spell/paid_temporary_rule", magic.get("cost", 0) > 0 and magic.get("backlash", {}).get("amount", 0) > 0 and magic.get("rule", {}).get("duration_minutes", 0) > 0 and field(magic.get("rule", {}).get("field", "missing.field")) == field(spec["rule"]), magic)
        consumers = [id for id, candidate in self.actions.items() if field(spec["rule"]) in self.conditions(candidate, True) and self.owner[id] != self.contract["viewer"]]
        self.check("spell/physical_work_consumes_rule", bool(consumers), consumers)
        for cooperation in spec["target_cooperation"]:
            candidate = self.action(cooperation); target = candidate.get("suggestion", {}).get("target")
            modifiers = self.actors.get(target, {}).get("opinion", {}).get("modifiers", [])
            relevant = [item for item in modifiers if field(spec["rule"]) in compact_refs(item.get("when", {})) and item.get("score", 0) != 0]
            self.check("spell/independent_opinion_effect/" + cooperation, bool(relevant) and target in magic.get("witnesses", []), {"target": target, "modifiers": relevant})

    def check_branches(self):
        for branch in self.contract["branches"]:
            selector = self.action(branch["selector"]); strategy = selector.get("strategy", {})
            actual = {item["id"]: item for item in strategy.get("options", [])}
            self.check(branch["id"] + "/selector", strategy.get("output") == branch["output"] and set(actual) == {v["id"] for v in branch["options"]}, strategy)
            signatures = []
            for option in branch["options"]:
                id = branch["id"] + "/" + option["id"]
                self.check(id + "/conditional_option", bool(actual.get(option["id"], {}).get("when")), actual.get(option["id"]))
                for action_key, effects_key in [("mutation_action", "effects"), ("recovery_action", "recovery_effects")]:
                    self.action(option[action_key]); writes = self.writes.get(option[action_key], {})
                    wanted = {field(key): value for key, value in option[effects_key].items()}
                    self.check(id + "/" + effects_key, all(key in writes and writes[key] == value for key, value in wanted.items()), {"required": wanted, "actual": writes})
                resolution = self.action(option["resolution"])
                self.check(id + "/resolution_matches_public_case", (field("case_day3.doc.kind"), option["id"]) in list(equals(resolution.get("precondition", {}))), option["resolution"])
                signatures.append(json.dumps(option["effects"], sort_keys=True))
            self.check(branch["id"] + "/different_material_consequences", len(set(signatures)) == len(signatures), signatures)

    def check_reports_and_exits(self):
        for report in self.contract["academy_report_sources"]:
            owner = report["actor"]; fact = field(report["field"]); trace = field(report["personal_field"])
            writers = [id for id, values in self.writes.items() if fact in values and self.owner[id] == owner]
            legitimate = [id for id in writers if trace in self.conditions(self.actions[id])]
            self.check("report/" + report["site"], bool(legitimate), {"writer": legitimate, "observation_sources": self.producers[(owner, trace)]})
            world_grants = [event["id"] for event in self.data.get("world_events", []) if fact in {field(key) for key in event.get("writes", {})}]
            self.check("report/not_timer/" + report["site"], not world_grants, world_grants)
        for route in self.contract["safe_exits"]:
            entering = self.action(route["entry"]); leaving = self.action(route["exit"])
            own = self.owner.get(route["exit"], self.contract["viewer"]); location = field(own + ".room")
            self.check("exit/route/" + route["room"], self.writes.get(route["entry"], {}).get(location) == route["room"] and self.writes.get(route["exit"], {}).get(location) == route["destination"] and (location, route["room"]) in list(equals(leaving.get("precondition", {}))), route)
            combined = self.conditions(leaving) | self.conditions(leaving, True)
            blocked_by = sorted(combined & {field(item) for item in route["forbidden_exit_facts"]})
            self.check("exit/no_expired_or_npc_gate/" + route["room"], not blocked_by and "not_after_game_minutes" not in leaving, blocked_by)
            event = field("event:" + route["exit"] + ".done")
            self.check("exit/repeatable/" + route["room"], not route["return_repeatable"] or event not in self.writes.get(route["exit"], {}), {"manual_only": leaving.get("manual_only", False), "once_event": event in self.writes.get(route["exit"], {})})
        fallback = self.actions.get("detective.day2.deduce_academy_reports", {}).get("deduction", {})
        personal = {field(r["personal_field"]) for r in self.contract["academy_report_sources"]}
        used = set().union(*(compact_refs(e["when"]) for e in fallback.get("evidence", []))) if fallback.get("evidence") else set()
        self.check("report/fallback_does_not_claim_personal_trace", len(fallback.get("evidence", [])) == 6 and not personal & used and field("detective.infer.academies_personally_verified") not in {field(k) for k in fallback.get("learns", {})}, {"evidence_count": len(fallback.get("evidence", [])), "uses_personal": sorted(personal & used)})

    def check_planning(self):
        viewer = self.contract["viewer"]; planning = self.actors[viewer].get("planning", {}); tasks = planning.get("tasks", [])
        self.check("planning/bounded", 1 <= len(tasks) <= 32, len(tasks))
        self.check("planning/physiology_separate", bool(planning.get("physiology", {}).get("needs")) and "critical_below" in planning.get("physiology", {}) and "resume_above" in planning.get("physiology", {}), planning.get("physiology"))
        for task in tasks:
            id = task["id"]; nodes = {step["id"]: step for step in task["steps"]}; visited = set(); active = set()
            def visit(step):
                if step in active or step not in nodes:
                    return False
                if step in visited:
                    return True
                active.add(step)
                if not all(visit(parent) for parent in nodes[step].get("after", [])):
                    return False
                active.remove(step); visited.add(step); return True
            self.check("planning/dag/" + id, 0 < len(nodes) <= 32 and all(visit(step) for step in nodes), len(nodes))
            self.check("planning/knowledge_gate/" + id, bool(self.conditions({"precondition": task["available_when"]})) and all(self.producers[(viewer, ref)] for ref in refs(task["available_when"])), task["available_when"])
            for step in nodes.values():
                self.check("planning/own_step/" + id + "/" + step["id"], self.owner.get(step["action"]) == viewer and bool(set(refs(step["done_when"]))) and all(self.producers[(viewer, ref)] for ref in refs(step["done_when"])), {"action": step["action"], "done_when": step["done_when"]})
            if id.startswith("d3_") and id != "d3_public_accounts":
                self.check("planning/public_branch_gate/" + id, (field("case_day3.doc.kind"), id[3:]) in list(equals(task["available_when"])), task["available_when"])
        secret = field("ruan.plan.selected")
        self.check("planning/no_private_selector_gate", all(secret not in set(refs(task["available_when"])) for task in tasks), "任务按已公开案情解锁，不读阮清月的内部选择")
        routes = planning.get("physiology", {}).get("routes", [])
        self.check("physiology/bounded_routes", 1 <= len(routes) <= 32, len(routes))
        origins = set()
        for route in routes:
            id = route["id"]; sequence = route.get("actions", []); need = route.get("need")
            gate = route.get("available_when", {})
            gate_fields = compact_refs(gate)
            self.check("physiology/known_source/" + id, field("R05.phys.drinking_water_available") in gate_fields and all(self.producers[(viewer, ref)] for ref in gate_fields), gate)
            self.check("physiology/legal_bounded_sequence/" + id, 1 <= len(sequence) <= 8 and all(self.owner.get(action) == viewer and self.actions[action].get("physiology_route_only") is True and not self.actions[action].get("manual_only", False) for action in sequence), sequence)
            recovery = self.actions.get(route.get("recovery_action"), {})
            self.check("physiology/actual_recovery/" + id, route.get("recovery_action") in sequence and recovery.get("completed_need_effects", {}).get(need, 0) > 0 and recovery.get("duration_seconds", 0) > 0, {"recovery": route.get("recovery_action"), "effects": recovery.get("completed_need_effects"), "seconds": recovery.get("duration_seconds")})
            declared_origins = [value for ref, value in equals(gate) if ref == field(viewer + ".room")]
            if not declared_origins and "op" not in gate:
                declared_origins = [value for key, value in gate.items() if field(key) == field(viewer + ".room")]
            origins.update(declared_origins)
            returns = self.writes.get(sequence[-1], {}).get(field(viewer + ".room")) if sequence else None
            self.check("physiology/returns_to_start/" + id, len(declared_origins) == 1 and (len(sequence) == 1 and declared_origins[0] == "R05" or len(sequence) > 1 and returns == declared_origins[0]), {"origin": declared_origins, "return": returns})
            if len(sequence) > 1:
                last = self.actions.get(sequence[-1], {})
                self.check("physiology/return_has_no_expiry/" + id, "not_after_game_minutes" not in last and field("backflow.phys.active") not in self.conditions(last) and field("island_environment.phys.wind_window") not in self.conditions(last), sequence[-1])
        self.check("physiology/main_workrooms_covered", {"R05", "B-P", "2F-cross-M", "F03", "T01", "B2-control", "1F-Infirmary"} <= origins, sorted(origins))


def export(args):
    sys.path.insert(0, str(ROOT / "mcp/game"))
    import runtime
    deadline = time.monotonic() + args.seconds
    host = runtime.build("cli", deadline)
    command = [runtime.resolve_dotnet(deadline=deadline), host / "DuelView.Cli.dll", "--story-authoring-view", project_path(args.content).relative_to(ROOT), output_path(args.output).relative_to(ROOT)]
    if args.overlay:
        command.append(project_path(args.overlay).relative_to(ROOT))
    process = runtime.spawn(command, stdout=runtime.subprocess.PIPE, stderr=runtime.subprocess.STDOUT)
    try:
        text, _ = process.communicate(timeout=runtime.remaining(deadline, 120))
    except runtime.subprocess.TimeoutExpired:
        runtime.terminate(process)
        text, _ = process.communicate()
        raise RuntimeError("作者导出超时，没有启动游戏。" + text[-3000:])
    output_path(args.output + ".command.txt").write_text("COMMAND " + " ".join(map(str, command)) + "\n" + text + "\nEXIT " + str(process.returncode) + "\n", encoding="utf-8")
    if process.returncode:
        raise RuntimeError(text)
    print(text.strip())


def inspect_file(args):
    view = load(project_path(args.view)); contract = load(project_path(args.contract))
    result = Inspection(view, contract).run()
    from focus_audit import inspect as inspect_focus
    presentation = inspect_focus(view, args.view)
    focus_path = output_path(str(Path(args.output).with_suffix(".focus.json")))
    save(focus_path, presentation)
    result["checks"].extend([
        {"id": "presentation/all_actor_fixed_room_targets", "passed": not presentation["missing"], "detail": [{"room": item["room"], "focus": item["focus"], "actions": item["actions"]} for item in presentation["missing"]]},
        {"id": "presentation/every_room_has_visible_exit", "passed": not presentation["rooms_missing_visible_door"], "detail": presentation["rooms_missing_visible_door"]}
    ])
    result["passed"] = sum(item["passed"] for item in result["checks"])
    result["total"] = len(result["checks"])
    result["failed"] = [item for item in result["checks"] if not item["passed"]]
    result["presentation_audit"] = {"path": str(focus_path.relative_to(ROOT)), "sha256": sha(focus_path), "dynamic_or_colocation_warnings": len(presentation["warnings"]), "scope": presentation["scope"]}
    result["author_view"] = {"path": args.view, "sha256": sha(project_path(args.view))}
    result["contract"] = {"path": args.contract, "sha256": sha(project_path(args.contract))}
    out = output_path(args.output); save(out, result)
    print(json.dumps({"output": str(out.relative_to(ROOT)), "passed": result["passed"], "total": result["total"], "failed": result["failed"], "gameplay_accepted": False, "literary_quality_automatic_verdict": None}, ensure_ascii=False, indent=2))
    return 0 if not result["failed"] else 1


def prompt(args):
    view = load(project_path(args.view)); contract = load(project_path(args.contract))
    if view.get("schema") != "story-authoring-view-v1":
        raise ValueError("先从正式CLI导出当前声明")
    lines = ["# 五日可查验剧情生成任务", "", "你是本项目的剧情作者。随附正式DV展开视图与剧情契约是可引用事实底座。先阅读指定大纲章节，再生成可执行候选；不得凭空假定玩家知道世界私有事实。", "", "## 必须交付", "", "1. 待整合的DV增量及精确源文件，不修改现有验收记录。", "2. 每条调查的获取行动→至少两项带来源证据→明确推理规则→新认知→实际新行为；说明反证、未排除项和失败后的合法续行。", "3. 前四日逐项说明人物目标、误解、反转、重读与代价；第五日只声明可选生活行为及真实反馈条件，文学回收等待实际日志，由人工审阅人物动机与节奏。", "4. NPC日常驱动力和独立意见评估分别声明；任务计划和身体需求分别声明。", "5. 提供正式CLI作者校验命令与Godot全AI/独立玩家验收计划；只有真实运行回执可证明流程通过。", "", "## 生成边界", ""]
    lines += ["- " + requirement for requirement in contract["generation_requirements"]]
    lines += ["", "## 当前正式上下文", "", "展开视图：" + args.view, "内容指纹：" + view["content_hash"], "来源集合指纹：" + view["source_fingerprint"], "契约：" + args.contract, "", "引用以下已存在的调查链；新增名称须同时声明实现、物理位置和获取途径：", "", json.dumps(contract["investigations"], ensure_ascii=False, indent=2), "", "不得给文学性自动打PASS。结构检查只能证明声明、引用和假设前提的推导设计符合契约，不能证明实际可达、节奏或有趣。", ""]
    out = output_path(args.output); out.write_text("\n".join(lines), encoding="utf-8")
    print(json.dumps({"output": str(out.relative_to(ROOT)), "bytes": out.stat().st_size, "provider_called": False, "scope": "本地作者提示词；由作者选择生成服务，工具不发送原文或私有日志"}, ensure_ascii=False))


def negative_checks(args):
    base = load(project_path(args.view)); contract = load(project_path(args.contract)); outcomes = []
    def check(name, mutate, expected):
        changed = deepcopy(base); mutate(changed)
        result = Inspection(changed, contract).run()
        failures = [item["id"] for item in result["failed"]]
        outcomes.append({"name": name, "expected_check": expected, "rejected": expected in failures, "failed_checks": failures})
    def action(view, id):
        return next(a for actor in view["declaration"]["actors"] for a in actor["actions"] if a["id"] == id)
    check("删除推演对新行为的认知门槛", lambda v: action(v, "detective.day5.archive_cases").update(precondition={"op": "all", "items": []}), "d5_archive/cognition_unlock/detective.day5.archive_cases")
    check("关窗会封死返程", lambda v: action(v, "detective.day2.return_from_W102").update(not_after_game_minutes=2460), "exit/no_expired_or_npc_gate/W-102")
    check("言灵不付代价", lambda v: action(v, "detective.day4.seal_airlock")["incantation"].update(cost=0), "spell/paid_temporary_rule")
    check("药卷布局退化为探针同名效果", lambda v: v["declaration"]["physical"]["adoptions"]["ruan.day3.shift_original_page"]["law"].update(effects=[]), "day3_selected_case/medicine/effects")
    check("假设证据为空仍宣布证明", lambda v: next(p for p in v["author_proofs"] if p["action"] == "detective.deduce_drag").update(empty_premises_status="proved"), "d1_drag/production_proof_design")
    def remote_stock_perception(view):
        actor = next(item for item in view["declaration"]["actors"] if item["id"] == "lucian")
        sensor = next(item for item in actor["perceptions"] if item["id"] == "day5:lucian.stock_F03")
        sensor["condition"] = {"op": "constant", "value": True}
    check("不在食堂也更新食堂库存", remote_stock_perception, "autonomy/local_resource_perception/day5:lucian.stock_F03/pantry.phys.dry_portions")
    def remote_stock_consumption(view):
        cook = action(view, "shen.day5.cook_batch")
        def move_room(node):
            if isinstance(node, dict):
                if node.get("op") == "equals" and field(node.get("ref", "missing.field")) == field("shen.room") and node.get("value") == "F05":
                    node["value"] = "F03"
                for child in node.values():
                    move_room(child)
            elif isinstance(node, list):
                for child in node:
                    move_room(child)
        move_room(cook)
        move_room(view["declaration"]["physical"]["adoptions"][cook["law"]]["law"])
    check("人在食堂直接消耗热厨原料", remote_stock_consumption, "autonomy/local_resource_mutation/shen.day5.cook_batch/kitchen.phys.dry_portions")
    def remove_self_awareness(view):
        owner = next(actor for actor in view["declaration"]["actors"] if actor["id"] == "moran")
        for sensor in owner.get("perceptions", []):
            sensor["refs"] = [item for item in sensor.get("refs", []) if field(item) != field("moran.phys.care_relief")]
    check("患者不知道自己是否已获护理", remove_self_awareness, "autonomy/own_state_obtainable/moran.phys.care_relief")
    def remove_loaded_route(view):
        owner = next(actor for actor in view["declaration"]["actors"] if actor["id"] == "mora")
        owner["actions"] = [item for item in owner["actions"] if item["id"] != "mora.day5.carry_dirty_cloth_from_F03"]
    check("带着旧敷料到食堂后没有运送去向", remove_loaded_route, "autonomy/loaded_route/mora.phys.carried_dirty_cloth/F03")
    result = {"schema": "story-validator-negative-checks-v1", "source_view_sha256": sha(project_path(args.view)), "scope": "内存内作者检查反例，不生成游戏回执、不启动游戏、不修改DV", "cases": outcomes, "passed": all(item["rejected"] for item in outcomes), "gameplay_accepted": False}
    save(output_path(args.output), result)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["passed"] else 1


def main():
    parser = argparse.ArgumentParser(description="通过正式DV解析结果查验剧情作者声明；不代替实机")
    commands = parser.add_subparsers(dest="command", required=True)
    item = commands.add_parser("export", help="调用正式CLI编译／初始化并导出作者视图，不推进世界")
    item.add_argument("--content", default="assets/data/scenarios/campaign.dv")
    item.add_argument("--overlay", help="项目内待整合目录，保持res相对路径；未提供的文件仍读取正式源")
    item.add_argument("--output", default=".runtime/story/author-view.json")
    item.add_argument("--seconds", type=float, default=240)
    item.set_defaults(run=export)
    for name, function, output in [("check", inspect_file, "checks.json"), ("prompt", prompt, "generator-prompt.md"), ("negative-checks", negative_checks, "negative-checks.json")]:
        item = commands.add_parser(name)
        item.add_argument("--view", default=".runtime/story/author-view.json")
        item.add_argument("--contract", default="tools/story/story-contract.json")
        item.add_argument("--output", default=".runtime/story/" + output)
        item.set_defaults(run=function)
    args = parser.parse_args()
    try:
        return args.run(args) or 0
    except (ValueError, KeyError, OSError, RuntimeError) as error:
        print("作者检查未完成：" + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
