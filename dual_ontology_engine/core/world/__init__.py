"""权威不可变世界协议。"""
from .entities import Entity,Subject,Fact,PermissionGrant,IdentityFacet
from .relations import Relation,RelationEndpoint
from .versions import WorldState,WorldVersion,genesis,validate_version_dag
from .epistemic import BeliefValue,EpistemicView
from .identity import FacetTransfer,FacetInstruction,IdentityTransitionPlan
from .obligations import ObligationState,Obligation,transition_obligation
from .scopes import ScopeOp,ScopeExpr,ScopeResolution,resolve_scope
__all__=("Entity","Subject","Fact","PermissionGrant","IdentityFacet","Relation","RelationEndpoint","WorldState","WorldVersion","genesis","validate_version_dag","BeliefValue","EpistemicView","FacetTransfer","FacetInstruction","IdentityTransitionPlan","ObligationState","Obligation","transition_obligation","ScopeOp","ScopeExpr","ScopeResolution","resolve_scope")
