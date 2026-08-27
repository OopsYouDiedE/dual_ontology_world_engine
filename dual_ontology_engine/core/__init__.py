"""有界、声明式、不可变的权威微内核。

默认入口只导出权威世界与事务协议。``non_authoritative`` 不在默认
导入图中，也不存在到 Kernel 的桥接。
"""
from .foundation.failures import FailureCertificate,FailureKind,CoreError,ValidationError
from .foundation.ids import BranchId,VersionId,EntityId,SubjectId,RuleId,FactId,WorldRef
from .foundation.numbers import Exact,exact
from .foundation.provenance import Provenance,make_provenance,validate_provenance_dag
from .world import WorldState,WorldVersion,Entity,Subject,Fact,Relation,EpistemicView,PermissionGrant,IdentityTransitionPlan,Obligation,ScopeOp,ScopeExpr
from .transactions import Kernel,AuthoritativeKernel,VersionStore,Proposal,TransactionProposal,StateOperation,OperationKind,CommitResult,CommitStatus,Intent,make_intent,IntentExecutionResult,execute_intent

__all__=("FailureCertificate","FailureKind","CoreError","ValidationError","BranchId","VersionId","EntityId","SubjectId","RuleId","FactId","WorldRef","Exact","exact","Provenance","make_provenance","validate_provenance_dag","WorldState","WorldVersion","Entity","Subject","Fact","Relation","EpistemicView","PermissionGrant","IdentityTransitionPlan","Obligation","ScopeOp","ScopeExpr","Kernel","AuthoritativeKernel","VersionStore","Proposal","TransactionProposal","StateOperation","OperationKind","CommitResult","CommitStatus","Intent","make_intent","IntentExecutionResult","execute_intent")
