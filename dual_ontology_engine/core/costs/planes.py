from enum import Enum, auto


class CostPlane(Enum):
    EXECUTION = auto()
    BELIEF = auto()
    BURDEN = auto()
    SEARCH = auto()
    NARRATIVE = auto()


class Reversibility(Enum):
    REVERSIBLE = auto()
    COMPENSABLE = auto()
    IRREVERSIBLE = auto()


class SettlementTiming(Enum):
    IMMEDIATE = auto()
    ESCROW = auto()
    PER_BATCH = auto()
    ON_SUCCESS = auto()
    ON_ATTEMPT = auto()

