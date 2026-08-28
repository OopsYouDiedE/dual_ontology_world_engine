"""唯一权威写入路径。"""
from .proposals import OperationKind,StateOperation,PointRead,PredicateReadToken,Precondition,Posting,ChargeKey,make_charge_key,ChargeGroup,ConflictStrategy,ConflictRank,Proposal,make_proposal
from .conflicts import ConflictComponent,ConflictResolution,resolve_conflicts
from .ledger import Ledger
from .commit import CommitStatus,CommitTrace,CommitResult,TimeAdvanceResult,Kernel,AuthoritativeKernel,VersionStore
from .intents import Intent,make_intent
from .runtime import IntentExecutionResult,execute_intent
from .invariants import InvariantWitness,InvariantViolation,validate_world_invariants
from .conservation import ConversionRule,ConservationPotential,make_conservation_potential,ConversionWitness,ConservationCertificate,validate_conversion_domain
TransactionProposal=Proposal
__all__=("OperationKind","StateOperation","PointRead","PredicateReadToken","Precondition","Posting","ChargeKey","make_charge_key","ChargeGroup","ConflictStrategy","ConflictRank","Proposal","TransactionProposal","make_proposal","ConflictComponent","ConflictResolution","resolve_conflicts","Ledger","CommitStatus","CommitTrace","CommitResult","TimeAdvanceResult","Kernel","AuthoritativeKernel","VersionStore","Intent","make_intent","IntentExecutionResult","execute_intent","InvariantWitness","InvariantViolation","validate_world_invariants","ConversionRule","ConservationPotential","make_conservation_potential","ConversionWitness","ConservationCertificate","validate_conversion_domain")
