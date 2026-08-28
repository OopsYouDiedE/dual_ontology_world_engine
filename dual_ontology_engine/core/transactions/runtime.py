from __future__ import annotations

from dataclasses import dataclass, replace

from ..foundation.budgets import BudgetExceeded, BudgetMeter, BudgetProfile
from ..foundation.codec import content_id
from ..foundation.failures import FailureCertificate, FailureKind
from ..foundation.provenance import make_provenance
from ..rules.dependencies import Dependency, Polarity, analyze_dependencies
from ..rules.ir import EffectKind, RuleIR, RulePhase, TargetMode
from ..rules.triggers import ClosureResult, Trigger, monotone_closure
from ..tracing.proofs import TraceDag, TraceNode
from ..world.entities import Fact
from ..world.versions import WorldVersion
from ..world.scopes import resolve_scope
from .commit import CommitResult, Kernel
from .intents import Intent,make_intent
from .proposals import (
    OperationKind,
    PointRead,
    PredicateReadToken,
    Precondition,
    StateOperation,
    make_proposal,
)


@dataclass(frozen=True, slots=True)
class IntentExecutionResult:
    intent_id: str
    commit: CommitResult | None
    failure: FailureCertificate | None
    closure: ClosureResult | None
    trace: TraceDag
    follow_up_rule_ids: tuple[str, ...] = ()
    follow_up_intents: tuple[Intent, ...] = ()

    @property
    def committed(self) -> bool:
        return self.commit is not None and self.commit.committed


def _trace_node(
    kind: str,
    payload: tuple[tuple[str, str], ...],
    parents: tuple[str, ...] = (),
    *,
    objective_only: bool = False,
) -> TraceNode:
    identifier = content_id((kind, payload, parents, objective_only))
    return TraceNode(identifier, kind, payload, parents, objective_only)


def _failure(base: WorldVersion, intent: Intent, code: str, *witnesses: str) -> FailureCertificate:
    return FailureCertificate(
        FailureKind.INVALID,
        code,
        str(base.version_id),
        subject_ref=intent.actor_ref,
        witness_refs=tuple(witnesses),
        ruleset_hash=base.ruleset_hash,
        input_id=intent.intent_id,
        state_digest=base.state.root_hash,
    )


def _rule_dependencies(rules: tuple[RuleIR, ...]) -> tuple[Dependency, ...]:
    producers: dict[str, list[str]] = {}
    for rule in rules:
        for key in rule.outputs:
            producers.setdefault(key, []).append(rule.rule_id)
    return tuple(
        sorted(
            (
                Dependency(source, target.rule_id, Polarity.POSITIVE)
                for target in rules
                for requirement in target.requires
                for source in producers.get(requirement, ())
            ),
            key=lambda edge: (edge.source_rule, edge.target_rule, edge.polarity.value),
        )
    )


def _fact_id(intent: Intent, target_local_id: str, target_epoch: int, predicate: str) -> str:
    return content_id(
        ("fact", intent.branch_id, target_local_id, target_epoch, predicate)
    )


def execute_intent(
    base: WorldVersion,
    intent: Intent,
    rules: tuple[RuleIR, ...],
    *,
    budget: BudgetProfile | None = None,
    kernel: Kernel | None = None,
) -> IntentExecutionResult:
    """执行封闭的 Intent→规则闭包→Proposal→Commit 参考流水线。"""

    intent_node = _trace_node(
        "intent_received",
        (("intent_id", intent.intent_id), ("action", intent.action)),
    )
    nodes = [intent_node]
    if intent.branch_id != str(base.branch_id) or intent.based_on_version != str(base.version_id):
        failure = FailureCertificate(
            FailureKind.STALE,
            "intent-version-mismatch",
            str(base.version_id),
            subject_ref=intent.actor_ref,
            ruleset_hash=base.ruleset_hash,
            input_id=intent.intent_id,
            state_digest=base.state.root_hash,
        )
        return IntentExecutionResult(intent.intent_id, None, failure, None, TraceDag(tuple(nodes)))
    subject = base.state.subjects.get(intent.actor_ref)
    if subject is None or base.state.identity_epochs.get(intent.actor_ref) != intent.actor_identity_epoch:
        failure = FailureCertificate(
            FailureKind.UNAUTHORIZED,
            "intent-actor-or-epoch-invalid",
            str(base.version_id),
            subject_ref=intent.actor_ref,
            ruleset_hash=base.ruleset_hash,
            input_id=intent.intent_id,
            state_digest=base.state.root_hash,
        )
        return IntentExecutionResult(intent.intent_id, None, failure, None, TraceDag(tuple(nodes)))

    selected_rules = tuple(sorted((rule for rule in rules if rule.action == intent.action), key=lambda rule: rule.rule_id))
    if not selected_rules or len({rule.rule_id for rule in selected_rules}) != len(selected_rules):
        failure = _failure(base, intent, "missing-or-duplicate-intent-rule")
        return IntentExecutionResult(intent.intent_id, None, failure, None, TraceDag(tuple(nodes)))

    meter = BudgetMeter(budget or BudgetProfile())
    try:
        scope = resolve_scope(base.state, intent.scope, meter)
    except BudgetExceeded as exc:
        failure = FailureCertificate(FailureKind.BUDGET_EXHAUSTED,"scope-budget",str(base.version_id),subject_ref=intent.actor_ref,witness_refs=(str(exc),),ruleset_hash=base.ruleset_hash,input_id=intent.intent_id,budget_usage=(("instructions",meter.used_instructions),("nodes",meter.used_nodes),("proposals",meter.used_proposals)),state_digest=base.state.root_hash)
        return IntentExecutionResult(intent.intent_id, None, failure, None, TraceDag(tuple(nodes)))
    except (ValueError, TypeError) as exc:
        failure = _failure(base, intent, "scope-resolution-failed", str(exc))
        return IntentExecutionResult(intent.intent_id, None, failure, None, TraceDag(tuple(nodes)))
    meter = scope.meter
    scope_node = _trace_node(
        "scope_resolved",
        (("scope_id", scope.scope_id), ("target_count", str(len(scope.refs)))),
        (intent_node.trace_id,),
    )
    nodes.append(scope_node)

    try:
        analyze_dependencies(
            tuple(rule.rule_id for rule in selected_rules),
            _rule_dependencies(selected_rules),
            frozenset(rule.rule_id for rule in selected_rules if rule.monotone),
        )
    except (ValueError, TypeError) as exc:
        failure = _failure(base, intent, "rule-stratification-failed", str(exc))
        return IntentExecutionResult(intent.intent_id, None, failure, None, TraceDag(tuple(nodes)))

    initial = set(base.state.facts)
    initial.add(f"intent.action:{intent.action}")
    initial.update(f"intent.target:{ref.local_id}" for ref in scope.refs)
    domain = set(initial)
    for rule in selected_rules:
        domain.update(rule.requires)
        domain.update(rule.outputs)
    triggers = tuple(
        Trigger(
            rule.rule_id,
            scope.scope_id,
            str(base.version_id),
            intent.intent_id,
            rule.phase.value,
            rule.requires,
            rule.outputs,
        )
        for rule in selected_rules
    )
    closure = monotone_closure(frozenset(initial), triggers, frozenset(domain), meter)
    fired_ids = {key[0] for key in closure.fired_reaction_keys}
    for key in closure.fired_reaction_keys:
        nodes.append(
            _trace_node(
                "rule_fired",
                (("rule_id", key[0]), ("phase", key[4])),
                (scope_node.trace_id,),
                objective_only=True,
            )
        )
    if closure.failure is not None:
        failure=replace(
            closure.failure,
            subject_ref=intent.actor_ref,
            ruleset_hash=base.ruleset_hash,
            input_id=intent.intent_id,
            reaction_keys=tuple(content_id(key) for key in closure.fired_reaction_keys),
            budget_usage=(("instructions",closure.meter.used_instructions),("nodes",closure.meter.used_nodes),("proposals",closure.meter.used_proposals)),
            state_digest=base.state.root_hash,
        )
        closure=replace(closure,failure=failure)
        return IntentExecutionResult(intent.intent_id, None, failure, closure, TraceDag(tuple(nodes)))

    parameters = dict(intent.parameters)
    known_refs = {entity.ref for entity in base.state.entities.values()} | {
        value.ref for value in base.state.subjects.values()
    }
    proposals = []
    for rule in selected_rules:
        if rule.rule_id not in fired_ids or rule.phase is not RulePhase.PRE_COMMIT or not rule.effects:
            continue
        provenance = make_provenance(rule_id=rule.rule_id, event_id=intent.intent_id)
        operations: dict[str, StateOperation] = {}
        point_reads: dict[str, PointRead] = {}
        preconditions: dict[str, Precondition] = {}
        for effect in rule.effects:
            if effect.target_mode is TargetMode.ACTOR:
                targets = (subject.ref,)
            elif effect.target_mode is TargetMode.EACH_SCOPE_TARGET:
                targets = scope.refs
            else:
                targets = (effect.fixed_target,)  # type: ignore[assignment]
            for target in targets:
                if target not in known_refs or str(target.branch) != intent.branch_id:
                    failure = _failure(base, intent, "effect-target-invalid", str(target))
                    return IntentExecutionResult(intent.intent_id, None, failure, closure, TraceDag(tuple(nodes)))
                fact_id = _fact_id(intent, target.local_id, target.epoch, effect.predicate)
                address = f"fact:{fact_id}"
                previous = base.state.facts.get(fact_id)
                if effect.kind is EffectKind.DELETE_FACT:
                    if previous is None:
                        continue
                    operation = StateOperation(OperationKind.DELETE_FACT, address, None, provenance.provenance_id)
                else:
                    value = effect.literal_value
                    if effect.parameter_name is not None:
                        if effect.parameter_name not in parameters:
                            failure = _failure(base, intent, "missing-intent-parameter", effect.parameter_name)
                            return IntentExecutionResult(intent.intent_id, None, failure, closure, TraceDag(tuple(nodes)))
                        value = parameters[effect.parameter_name]
                    fact = Fact(fact_id, target, effect.predicate, value or "", provenance.provenance_id)
                    operation = StateOperation(OperationKind.SET_FACT, address, fact, provenance.provenance_id)
                old = operations.get(address)
                if old is not None and old != operation:
                    failure = _failure(base, intent, "rule-produced-internal-conflict", address)
                    return IntentExecutionResult(intent.intent_id, None, failure, closure, TraceDag(tuple(nodes)))
                operations[address] = operation
                point_reads[address] = PointRead(address, base.state.key_epochs.get(address, 0))
                preconditions[address] = Precondition(
                    address, None if previous is None else content_id(previous)
                )
        if not operations:
            continue
        proposal = make_proposal(
            branch_id=intent.branch_id,
            base_version=intent.based_on_version,
            actor_ref=intent.actor_ref,
            actor_identity_epoch=intent.actor_identity_epoch,
            permission_action=rule.permission_action,
            preconditions=tuple(preconditions[key] for key in sorted(preconditions)),
            point_reads=tuple(point_reads[key] for key in sorted(point_reads)),
            predicate_reads=(
                PredicateReadToken(scope.normalized_query, scope.dependency_epoch, range_read=True),
            ),
            operations=tuple(operations[key] for key in sorted(operations)),
            charge_groups=(),
            provenance_nodes=(provenance,),
            provenance_root=provenance.provenance_id,
            atomic_group_id=intent.intent_id,
        )
        proposals.append(proposal)
        nodes.append(
            _trace_node(
                "proposal_created",
                (("proposal_id", proposal.proposal_id), ("rule_id", rule.rule_id)),
                tuple(node.trace_id for node in nodes if node.kind == "rule_fired" and dict(node.public_payload).get("rule_id") == rule.rule_id),
                objective_only=True,
            )
        )

    if not proposals:
        failure = _failure(base, intent, "intent-produced-no-state-progress")
        return IntentExecutionResult(intent.intent_id, None, failure, closure, TraceDag(tuple(nodes)))

    commit = (kernel or Kernel()).commit(base, tuple(proposals))
    commit_node = _trace_node(
        "commit_result",
        (
            ("status", commit.status.value),
            ("version", str(commit.version.version_id)),
            ("failure", "" if commit.failure is None else commit.failure.code),
        ),
        tuple(node.trace_id for node in nodes if node.kind == "proposal_created"),
        objective_only=True,
    )
    nodes.append(commit_node)
    follow_up_rules=tuple(rule for rule in selected_rules if commit.committed and rule.rule_id in fired_ids and rule.phase is RulePhase.POST_COMMIT)
    follow_up_intents=tuple(make_intent(branch_id=intent.branch_id,based_on_version=str(commit.version.version_id),actor_ref=intent.actor_ref,actor_identity_epoch=intent.actor_identity_epoch,action=rule.follow_up_action,scope=intent.scope,ability_source=intent.ability_source,parameters=intent.parameters,proof_hints=(rule.rule_id,intent.intent_id)) for rule in follow_up_rules)
    follow_up=tuple(rule.rule_id for rule in follow_up_rules)
    return IntentExecutionResult(
        intent.intent_id,
        commit,
        commit.failure,
        closure,
        TraceDag(tuple(nodes)),
        follow_up,
        follow_up_intents,
    )
