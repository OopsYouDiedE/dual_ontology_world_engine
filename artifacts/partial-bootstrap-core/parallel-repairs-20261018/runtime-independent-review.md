# Independent read-only review of retry and wait changes

Review scope: `session/15-action-planning.dv`, `session/30-runner.dv`, `scenarios/chapter1/opening.dv`, `scenarios/chapter2/content.dv`, and their immediate cache, observation and cost consumers. No design files were read; no production files were changed during this review; no formal session or build was executed.

## Material concern

The new failure observation path checks `receipt.outcome == "applied"` and the post-rejection physical world's `failure_observe_when`. For chapter2 moves, this means the physical actor remains at the declared origin after the authority rejection, and that preserved logical position authorizes observing the gate.

This is consistent for a formal-model rejected crossing with no geometry. For a real host receipt that actually reached the target before the authority rejection, the logical origin is not evidence of the execution's actual location. Current new code does not inspect the actual host endpoint before claiming direct perception. Such receipts need either explicit actual-location/contact validation for the failure grant or restriction of this logical-at-origin recovery to the formal-model adapter. This concern was sent to the main agent for implementation judgment; no new observation authorization was invented here.

## Other reviewed boundaries

- `dvp_memo_input` retains `rejected_attempt_signatures`; adding a rejection changes the memo key. `dvp_actor_view` retains the same field in predicted views. Rejected actions are filtered before repeatable actions can bypass the used-list check. No stale-plan bypass was found in these paths.
- Failure signatures store known/value/conflicted inputs for equality predicates and truth values for numeric thresholds. Thus unrelated passage of time does not by itself re-enable a failed numerical-threshold action; personally acquired changed gate input or changed location can. The selected prediction adoption is also signed. The generic signature's precondition language remains bounded by the current predicate implementation; this is source review, not an exhaustive semantic proof.
- Rejection signatures are written after the actual failure observation, so the immediately resulting knowledge is the blocked baseline. Successful execution clears the entry. Original charging and ledger settlement are unchanged.
- Failure observations call the ordinary physical-to-local binding projection, retain a real execution-event parent and explicit grants, and include full before/after mind snapshots. They do not directly assign arbitrary authority truth to mind. The actual-location concern above remains separate.
- Wait lengths are selected from the preexisting `[180,120,60,30,15,5,1]` list. The same list drives cost-kind registration and wait-law installation, so the newly used short lengths have real fees and adopted laws. There is no new unpriced arbitrary-length wait.
- A selected wait must now end at or before the next owned boundary. Exact integral-minute window starts/ends are not crossed by the old 15-minute floor. Closing thresholds nested under negation are collected for reconsideration; future-start selection still uses positive lower bounds. The new helper reads declared conditions, not future physical state.
- The chapter2 policy limit is 30, but the preexisting first-night special case in `c1n_known_wait_actor` overrides it to 60 before minute 810 for its selected living-hall actors. This does not cross the chosen boundary, but a claim that every chapter2 wait is capped at 30 would be inaccurate. Fractional gaps below one minute generate no new wait because the smallest registered duration is one; no unsupported fractional fee was introduced.
- Navigation now prefers a locally eligible task with a personally known route over a preceding ready but unreachable task. The fallback remains authored ordering when none is reachable. This does not prove global task completion or optimal routing.

## Executable engineering evidence preparation

`runtime-probe/RuntimeProbe.csproj` and `Program.cs` form an executable neutral probe against the actual Runtime/Session source. Its README gives the command for the main agent to run once the repair batch is frozen. It checks no-overwrite evidence, exact rational wire formats, deep Save/WriteSave/Restore compatibility, and immutable evidence independence without running DV decisions. It has not been executed during this review.
