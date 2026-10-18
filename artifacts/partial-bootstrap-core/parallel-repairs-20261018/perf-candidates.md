# Read-only performance candidates — 2026-10-18

Scope: only current mechanism source and the in-progress diagnostic sample. language_core remains frozen and this review made no source changes. No design document was read. This report is engineering reasoning, not a same-condition speed claim or game acceptance result.

## Observed diagnostic window

profile-round3/profile-raw.json read at process_ticks=1200093732, frequency=10000000 (120.009 diagnostic seconds). Raw SHA-256 at read: 9606aad42f5079c28eb2c8400f9082ba4e6c7f8a09405da497543430b7d385f5. This is a cumulative partial sample and includes instrumentation overhead; the final tool summary can supersede its ranking.

| Function | Actual calls in sample | Exclusive diagnostic seconds | Inclusive diagnostic seconds |
| --- | ---: | ---: | ---: |
| dv_get | 74013700 | 21.799 | 21.799 |
| dv_truth | 7035965 | 15.313 | 50.479 |
| dv_has | 2563371 | 9.363 | 9.363 |
| dv_find | 20659639 | 8.848 | 8.848 |
| dv_with_many | 627262 | 8.309 | 8.309 |
| dv_has_key | 13881826 | 4.995 | 4.995 |
| dv_clone | 4017172 | 4.538 | 4.538 |
| dv_truth_uncached | 1909758 | 3.733 | 35.166 |
| dv_pair | 14108667 | 2.562 | 2.562 |
| dv_with | 419140 | 2.524 | 2.524 |
| dv_record_like | 726950 | 2.517 | 2.592 |
| dvs_support_event_index | 4190 | 2.228 | 5.741 |
| dv_stage_store_owned | 15690 | 1.769 | 2.081 |
| dvp_topic | 567548 | 1.737 | 11.863 |
| dv_read | 2251174 | 1.628 | 15.130 |

## Candidate A: topic pure-result memoization (highest actionable DV candidate)

Location: verification/session/15-action-planning.dv, dvp_topic and dvp_question. Current dvp_topic repeatedly scans complete topic_bindings and recomputes the same heard/completed truths for each item with a common topic. Do not replace eager dv_and/dv_or with short-circuit operators: that changes error evaluation for malformed binding declarations.

Use complete actor.mind as memo owner and a partitioned key containing the complete item, presence and value of question_ref, complete policy.topic_bindings, and complete heard_topics. This fully covers the current function's reads; no actor id, world or future state is inferred. A still safer first implementation can key the complete actor as owner with complete item, accepting lower reuse before narrowing from an explicit read audit. Cache only after all original reads/validations succeed. A personal-scope item can keep its existing immediate return. Cache returns only Bool and does not alter costs, candidate ordering, planning nodes or event publication.

Neutral comparison: old/new output and first error for absent question_ref, unknown/contested question, ordinary and personal goals, heard topics added/removed, binding edits, completed_when truth changes, basis empty/nonempty, duplicate topics and malformed binding fields. Mutate nested mind cells/bindings/heard records and ensure old cache entries do not release stale results. Exercise distinct actor views sharing mind but different topic bindings/heard topics. Record actual cache input ownership and aliases.

## Candidate B: constant proposition fast path

Location: core/00-data.dv dv_truth. Sample dv_truth calls greatly exceed dv_truth_uncached calls, so existing memoization already eliminates much evaluation; adding another general cache is unlikely to help. A constant proposition does not inspect the world even in the original uncached branch. Read proposition.op at entry and return proposition.value directly for op=constant before building the memo key/snapshot; retain the existing path otherwise.

This preserves all constant values, including an unconventional nonstandard value: do not newly validate or normalize it. Missing op/value must retain the same missing-field error and order. No short circuit inside all: original evaluation visits every child, including malformed later children after false. Neutral checks should include false/unknown/true constants, missing fields, invalid later all children, conflicted cells, and world/proposition mutation between calls. Benefit depends on actual constant-call frequency, not inferred from all dv_truth calls.

## Candidate C: reuse goal/window metadata without repeated record rebuilding

Location: scenarios/chapter1/opening.dv c1n_index_goal_window and c1n_known_future_wait_actor. The first helper already caches parsed condition metadata but reconstructs a goal with dv_with_many on every call. The second helper redoes c1n_without_clock/c1n_clock_boundaries in its second scan although the first scan already has equivalent condition metadata. Extend the existing static condition memo result with complete clock-boundaries metadata and reuse it, keeping the full condition as owner. This changes no game clock or wait target; pending-goal status, mind.time, nonclock truth and original goal order still run each time.

Do not trust future_clock_bounds merely because present: current code requires future_clock_condition equal available_when. Preserve that freshness test and fall back for pre-existing records. A complete goal memo may reuse exact output but must include the whole goal, not just available_when, because basis and arbitrary other fields must survive. Neutral checks cover nested all/not clock expressions, empty bounds, changed conditions with stale metadata, different basis records sharing a condition, duplicate conditions and identical target ties. This ranked below topic/truth in the sample, so apply only if scope remains small.

## Lower-priority or rejected ideas

- dv_local already has a subject-specific index and per-physical memo; dv_validate_bindings already caches only successful validation. Do not replace them with another cache without evidence; existing duplicate/empty-field first-error behavior is explicit.
- dvp_input_signature fully reads selected refs, reward, question, claim and action support. Its current output is itself an action-attempt semantic input. A cache must include complete mind, selected refs, action, question_ref and all argument/hypothesis reads of answer_reward; caching only revision/action.id is unsafe. Sample did not show it among dominant exclusive costs, so defer.
- rt_has_value linear structural membership costs are visible. A text-only membership set keyed by a frozen full list could accelerate repeated string membership, but mixed structural/numeric values must retain rt_same_value semantics and child-mutation invalidation. Do not rely on snapshot hash inequality: exact rational/double equivalents can have different storage hashes. This is runtime-agent territory and should not overlap edits.
- Do not reduce planning depth/budgets, remove events/snapshots, omit active actors, skip decision stages or change goals/answers to obtain lower time.

## Execution boundary

No proposed optimization is implemented here. After parent unfreezes: choose a narrowly scoped candidate, retain the original algorithm as neutral comparison input, implement one complete cache, run engineering equivalence/error/alias probes, update core reference and module AGENTS, then freeze before the next user-authorized formal session. Instrumented sampling is never used as a game throughput comparison.


## Authorized implementation and neutral execution

After profile-round3 ended and the parent explicitly unfroze edits, implemented:

- dvp_topic retains the personal early return and original topic/question/bindings read order. With a List mind only, cache owner is the complete mind and key contains complete item, question_ref presence/value, complete topic_bindings and complete heard_topics. A missing/nonlist mind takes the original scan without new validation. The original eager binding scan is extracted as dvp_topic_scan; success is cached only after it returns. No dv_and operand or decision is skipped on a cache miss.
- dv_truth directly returns constant.value only when world is a List; a nonlist world still reaches the original memo owner check before reading proposition. Other proposition evaluation is unchanged.
- On subsequent explicit parent authorization, dv_read caches full cells with complete world owner plus complete ref key. Original subject check, world.objects and ref.object reads precede the cache. The remaining original lazy object/facet/field traversal is extracted as dv_read_fields. A missing object still returns unknown without facet/field reads; memo hits retain the platform's independent-copy return.

The neutral runner run_pure_cache_probe.py embeds the old implementations from Git HEAD as separately named comparison functions, builds the current production declaration bundle plus neutral calls, and invokes no session entry. Probe inputs are in pure-cache-probe.dv. Initial pure-cache-probe-01 generation failed because the runner wrote CRLF after calculating LF source-range offsets; the raw error remains. Runner now writes the bundle with explicit LF. pure-cache-probe-02 executes topic/constant cases; pure-cache-probe-03 adds dv_read equivalence, alias and leading-error cases. Each directory preserves its own original stdout/stderr, commands, original comparison source and source manifest.

Observed normal output in pure-cache-probe-03 matches the declared sequence, including repeated topic hits, nested question/contested-cell edits, heard_topics change, completion truth changes, nested binding-basis edits, distinct heard views over equivalent minds, a missing-mind empty-binding actor, personal early return, unconventional constants and mutated proposition/world. dv_read results equal complete old cells including extra nested metadata; modifying a miss return or a hit return does not modify a later result, and nested world/ref mutations invalidate the old result. Missing-object refs without facet/field match old unknown output.

Separate old/new processes produced identical first errors for nonlist world (Expected List), missing op/value, malformed later all child after false, irrelevant malformed binding (Missing field: completed_when), invalid question before missing policy (Missing field: objects), cross-subject read, missing leading objects, nonlist objects, and missing field on an existing object/facet. No error is cached. Extended runner exit was 0; results.json records actual values and error text. These are engineering equivalence facts for these neutral inputs, not a performance result or game acceptance.

No opening.dv edits, budget/cost/correct-condition changes, snapshot deletion or formal session execution were made by this agent. Parent owns public documentation and any next session. No measured speedup is claimed.


## Constant List return-boundary correction before freeze4

Parent source review identified that constant List values must retain memo's original ownership behavior: a miss returns the proposition's original mutable List, while a later hit returns an independent copy. Constant early return is therefore restricted to non-List values under a List world; a List-valued constant follows the original memo path. This adds no validation or normalization, and unconventional scalar values still use the fast path.

pure-cache-probe-04 recompiles the corrected source and preserves new original logs/manifest/results. The neutral comparison now reads the same world/proposition twice, changes the second returned List, and compares proposition, first return and subsequent truth values against the original implementation. It also changes the first return to verify the original alias contract and mutation invalidation are retained. Actual normal output ends with `list-constant-return-contract`; all prior scalar, nested mutation and first-error comparisons also match the original implementation in this run. Runner exit was 0. No formal session was invoked. Source edits are complete for parent freeze4.
