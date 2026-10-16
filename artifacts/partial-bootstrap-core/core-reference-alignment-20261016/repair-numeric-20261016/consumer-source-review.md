# Personal-choice numeric consumer source review — 2026-10-16

Scope: `language_core/verification/session/16-personal-choice.dv`. Static source review only; no compiler, build, test, or session was run. `docs/design-standard.md` was not read. Existing music changes were left untouched.

## Change

Value-domain guards now use `rt_is_numeric_value` in direct owned quantities, policy quantities, uncontested owner cell values, owner requirement reference collection, and physical erosion exposure. `rt_is_number` remains the text parser predicate elsewhere; this file no longer calls it. The ABI contract supplied by the parallel runtime repair accepts finite double and Rational and rejects text, booleans, and lists.

## Declaration branches

`dvp_owned_quantity` still returns the original numeric value unchanged. Explicit `false` remains the existing optional zero. Other values must satisfy the original record requirement; text including numeric-looking text and `true` reach that explicit error. Policy-backed declarations retain the existing absent-key zero fallback and require any present policy value to be numeric. Reference-backed declarations retain required `ref` presence and record checks; an unknown, contested, or nonnumeric observed cell retains the original zero result. No fallback was added around malformed required declarations.

`dvp_owner_requirement_refs` retains the same optional false branch, passive resonance branch, policy-key exclusion, required reference checks, and reference insertion. Numeric literals no longer enter the text parser; records reach validation and reference extraction. Direct required field access (drive, relevance, truth_bonus, caution, perceived_risk) remains unchanged so missing declarations still fail.

Erosion still treats absence of erosion as optional, obtains exposure only from an uncontested numeric cell, and retains all arithmetic, thresholds, rank, memory period, and time operations unchanged. No qualification formula, balance, clock, or content was edited.

## Receipt and completion source shapes

`20-speech.dv:dvs_receive_norm` constructs an actor-local record indexed by `norm.id`, whose values contain `norm`, `source_event`, `parse_event`, and `received_time`. This matches `dvp_normative_ready` lookup by `decision.id`, provenance checks, and full `dv_same(proposal.norm, decision.norm)` comparison. Chapter-two `norm_decision` declares id, accepted and the same norm content object; read receipts declare norm/content. The candidate consumer does not conflate a receipt with assent.

`30-runner.dv` appends the text `action.id` to actor `completed_actions` only when execution status is applied. The completion prerequisite is a record containing `action_id` and uses `dv_has` on that list. This consumer shape agrees with the actual producer, rather than expecting receipt records or using failed attempts in `used`.

## Separate static limitation for parent review

`private_execution` reads owner quantities for benefit and resource_cost, but `dvp_owner_requirement_refs` currently extracts only resonance and night-call references. A future reference-backed private-execution declaration would not be added by this helper. No active `private_execution` content declaration was found in the source searches performed here, so this was reported rather than expanded as part of the numeric ABI repair. No runtime reachability or gameplay outcome is claimed.