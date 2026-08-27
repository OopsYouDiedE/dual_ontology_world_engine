"""冻结 proposal、纯冲突解析和原子版本提交。"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum

from .budget import BudgetExceeded, BudgetMeter, BudgetProfile
from .failure import FailureCertificate, FailureCode, FailureStage
from .model import Intent, OperationKind, Proposal, WorldSnapshot, WorldVersion, stable_digest


class CommitStatus(str, Enum):
    COMMITTED = "committed"
    REJECTED = "rejected"
    STALE = "stale"


@dataclass(frozen=True, slots=True)
class CommitResult:
    status: CommitStatus
    state: WorldSnapshot
    failure: FailureCertificate | None = None

    @property
    def committed(self) -> bool:
        return self.status is CommitStatus.COMMITTED


class TransactionKernel:
    """不执行规则回调；只提交已由受限编译器产生的 proposal。"""

    def __init__(self, budget: BudgetProfile | None = None) -> None:
        self._budget = budget or BudgetProfile()

    def settle(
        self,
        state: WorldSnapshot,
        intent: Intent,
        proposals: tuple[Proposal, ...],
    ) -> CommitResult:
        meter = BudgetMeter(self._budget)
        if intent.based_on_version != state.version.version_id:
            return self._fail(state, intent, FailureCode.STALE_VERSION, FailureStage.READ_VALIDATE, "base version is stale", meter)
        try:
            meter.charge("proposals", len(proposals) or 1)
            ordered = tuple(sorted(proposals, key=lambda item: (item.phase, item.reaction_key, item.proposal_id)))
            reaction_keys: set[tuple[str, ...]] = set()
            writes: dict[tuple[str, ...], object] = {}
            for proposal in ordered:
                meter.charge("agenda_nodes")
                meter.charge("instructions", 1 + len(proposal.operations))
                if proposal.intent_id != intent.intent_id:
                    return self._fail(state, intent, FailureCode.INVALID_OPERATION, FailureStage.NORMALIZE, "proposal belongs to another intent", meter)
                if proposal.reaction_key in reaction_keys:
                    return self._fail(state, intent, FailureCode.DUPLICATE_REACTION, FailureStage.REACTION, "reaction key repeated in one batch", meter)
                reaction_keys.add(proposal.reaction_key)
                epochs = state.epoch_map()
                for token in proposal.read_tokens:
                    if epochs.get(token.index_partition, 0) != token.dependency_epoch:
                        return self._fail(state, intent, FailureCode.STALE_VERSION, FailureStage.READ_VALIDATE, "predicate read is stale", meter)
                for operation in proposal.operations:
                    meter.charge("writes")
                    if operation.kind in (OperationKind.ADD_RELATION, OperationKind.REMOVE_RELATION):
                        return self._fail(state, intent, FailureCode.INVALID_OPERATION, FailureStage.INVARIANT, "relation operations require the typed relation compiler", meter)
                    previous = writes.get(operation.path)
                    normalized = (operation.kind.value, operation.value)
                    if previous is not None and previous != normalized:
                        return self._fail(state, intent, FailureCode.WRITE_CONFLICT, FailureStage.CONFLICT, "incompatible writes target the same path", meter, ("/".join(operation.path),))
                    writes[operation.path] = normalized

            facts = state.fact_map()
            touched_partitions: set[str] = set()
            for path, (kind, value) in sorted(writes.items()):
                touched_partitions.add(path[0])
                if kind == OperationKind.PUT.value:
                    facts[path] = value  # type: ignore[assignment]
                else:
                    facts.pop(path, None)
            epochs = state.epoch_map()
            for partition in touched_partitions:
                epochs[partition] = epochs.get(partition, 0) + 1
            fact_tuple = tuple(sorted(facts.items()))
            delta_root = stable_digest(tuple(sorted((path, value) for path, value in writes.items())))
            state_root = stable_digest((fact_tuple, state.relations, tuple(sorted(epochs.items()))))
            parent = state.version
            version_id = stable_digest((parent.version_id, intent.intent_id, state_root, delta_root))
            version = WorldVersion(
                version_id=version_id, branch_id=parent.branch_id,
                parents=(parent.version_id,), generation=parent.generation + 1,
                world_time=parent.world_time, state_root=state_root,
                delta_root=delta_root, cause_id=intent.intent_id,
                ruleset_hash=parent.ruleset_hash,
                commit_sequence=parent.commit_sequence + 1,
            )
            return CommitResult(CommitStatus.COMMITTED, WorldSnapshot(
                version=version, facts=fact_tuple, relations=state.relations,
                predicate_epochs=tuple(sorted(epochs.items())),
                trace_roots=state.trace_roots + (stable_digest((intent.intent_id, tuple(p.provenance for p in ordered))),),
            ))
        except BudgetExceeded as exc:
            return self._fail(state, intent, FailureCode.BUDGET_EXHAUSTED, FailureStage.REACTION, f"budget exhausted: {exc.dimension}", meter, (exc.dimension,))

    @staticmethod
    def _fail(
        state: WorldSnapshot,
        intent: Intent,
        code: FailureCode,
        stage: FailureStage,
        reason: str,
        meter: BudgetMeter,
        witnesses: tuple[str, ...] = (),
    ) -> CommitResult:
        certificate = FailureCertificate(code, stage, intent.intent_id, state.version.version_id, reason, witnesses, meter.consumed)
        status = CommitStatus.STALE if code is FailureCode.STALE_VERSION else CommitStatus.REJECTED
        return CommitResult(status, state, certificate)
