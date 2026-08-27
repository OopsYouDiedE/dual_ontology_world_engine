"""Public exception hierarchy.

Failures are classified so callers never need to infer a world-law failure from
an implementation exception or a bounded-search miss.
"""


class EngineError(Exception):
    """Base class for engine-level failures."""


class CompileError(EngineError):
    """An expression is not a closed, registered, well-typed proposition."""


class ValidationError(EngineError):
    """A scenario or operation violates a declared invariant."""


class ConflictError(ValidationError):
    """A commit would create incompatible facts or active constraints."""


class PaymentError(ValidationError):
    """A payment is absent, insufficient, unauthorized, or already consumed."""


class CapabilityBoundary(EngineError):
    """No solution was found inside the registered finite capability boundary."""


class ReplayError(EngineError):
    """A replay diverged from its recorded deterministic trace."""

