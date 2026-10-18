# Structural proposal audit — 2026-10-18

Scope: read-only current mechanism/content source and existing frozen02/03 records. No project design files were opened. No source edit, build, neutral benchmark or formal session occurred in this audit. OwnedList and BoolBox measurement work is stopped. The separate historical-detective-projection.jsonl is a derived read-only packet extract with original source paths/line numbers/event IDs/raw references; it does not replace original evidence. This report does not promise speedup or judge intended characterization without the design authority.

## Scheduler and time premises

`verification/session/30-runner.dv::dvs_schedule` already excludes actors with `scheduled_action` from fresh ready selection. Queued actions contribute their due time; a due actor dispatches the previously chosen action through `dvs_dispatch_due`, not another choice. The scan cancels queued work when the actor cannot act and preserves cyclic tie order. A no-action result is not permanent: eligibility also checks physical time, mind revision and pending_heard. The scheduler advances physical time through `dvs_environment_next_time` boundaries when waiting. None of this proves that every actor's cognitive state can sleep until its own mind revision changes.

`game/entry.dv::dvg_refresh_goals` currently visits only current.cursor, ignores scheduled actors, and refreshes eligible chapter residents/demon when stored campaign mind revision, mind.time or chapter differs. `dvg_prepare` calls schedule first and content refresh before `dvs_prepare_actor`. The latter subsequently updates support-event inputs, drains received speech, notices peers and runs cognition. Thus a cache signature sampled before prepare cannot assume those later changes have already happened. A fresh pending dispatch bypasses this refresh path.

Mind time is not automatically synchronized merely by the scheduler advancing physical time. `core/20-observation.dv` observation adoption computes latest_time from received observation times; a nonempty observation batch increments revision. `30-runner.dv::dvs_session_complete` also sets the executing actor's mind.time to at least the actual execution receipt time after observation. `10-subjective.dv::dvs_erosion_update` can replace memory from the first-day snapshot and explicitly set time/revision. Scheduled speech is queued then drained at dispatch/prepare; received_speeches, requests, advice and interpretation state can change alongside or separately from some other fields. A clock perception cell, mind.time, decision_time and physical.time have different roles and cannot be silently substituted.

## Existing reusable indexes/caches

Runtime already has indexed record lookup for tables with at least eight entries, using FrozenIndex or invalidation-aware MutableIndex; last duplicate string key wins for lookup. `dv_get` delegates to that ABI. Another generic dictionary implementation would duplicate existing work unless a particular still-linear case is identified.

Exact JSON strings are weakly cached by FrozenList identity in Runtime.JsonEncodings. Fresh mutable roots retain serialization; no mutable identity is cached. JSON-derived reference keys may still repeatedly encode a fresh root, but a second cache must preserve exact JSON order and numeric representation and must not turn structural-value equality into encoding equality. The current three numeric-spelling boxes, shared Bool adaptation and EqValue/SameValue helpers are already separate runtime mechanisms; none justifies changing gameplay work.

`dvp_dependency_topology` is memoized by immutable adopted laws/method inputs. `c1n_index_goal_window`, `c1n_clock_boundaries` and `c1n_without_clock` already memoize immutable syntax metadata; personal-goal/dialogue templates are also cached. Preserve these interfaces rather than rebuilding parallel AST caches.

## Goal-refresh dependency closure

A sound reuse certificate needs two independent proofs: complete construction inputs and a still-valid time interval. The current output goal list is not a complete declaration-input catalogue: a branch can suppress an opportunity/goal which later becomes available. Collect dependencies from source declaration templates/actions/rows before conditional suppression.

Reusable syntax interfaces:

- `dvs_sim_refs(proposition)` recursively covers equals, at_least, not and all, producing referenced fields. Its unknown-op fallback returns [], so it cannot by itself certify completeness. A wrapper must return `{complete, refs}` and mark unsupported/malformed syntax incomplete.
- `dvp_merge_refs`/`dvp_add_ref` preserve structural reference union. `dvp_unmet_refs(..., include_known=true)` can support extraction but must not omit an already known condition that can later change.
- `c1n_index_goal_window` provides future_clock_condition, positive opening bounds and nonclock condition. `c1n_clock_boundaries` includes negated/closing bounds. Positive bounds alone are inadequate for cache expiration.
- The new `dvs_known_clock_boundary(condition, clock, now, limit)` helper supplied by static_review returns `{complete,next}`. Unknown/malformed operators or unknown clock invalidate completeness. Its nonmatching references are not dependencies-free; the caller must retain their complete current cells. The helper knows only declared numeric threshold syntax and the existing minute execution grid; it does not synchronize a mind clock or prove dynamic content calculations.

Required declaration families include goal available_when/proposition/ref/minimum_time/refresh_until/refresh_refs/navigation requirements; action precondition and base precondition; personal choice requires/prohibits and alternatives; owned decision context_refs/context_policy_keys/request_topics; speech literal/request/received-speech requirements; safety/recheck, case gate, carryover and navigation-route inputs. Dynamic declarations and policy switches require an exact version/value input, not just an array length.

The present builder chain is broader than investigation navigation:

| Builder/subchain | Additional actual inputs a certificate must include |
|---|---|
| c1n_refresh_actor_goals / c1_inquiry_navigation | first-night/base goals, actions/grants/preconditions, current question/topic, own position and adopted route laws, completed_actions, evidence source/time, inquiry decorations |
| c2d_goals | actor-specific rows, policy manipulation flag, case gate, work concern/safety notice, actual received-speech requirements, pending requests, owned decisions, partner choice and advice readiness |
| c2_refresh_content_actions | completed actions, work request receipt contents, restriction/cooperation receipt cells, accepted plans, safety notice, context decisions, policy norm rules; it rewrites actions and norm applicability |
| c2_personal_goals / c2i_goals / c2_safety_goals | static task/goal templates, carryover, own route/gates/adoptions, closing completion, work decision receipts, sample source/epoch/location/age, safety and current support/body facts |
| c2_navigation_goals | existing goal completion, source-supported evidence, known route/grants, full local action eligibility; eligibility reaches attempt/rejected/accepted/used state, visible contexts, requests, interpretations, norms and personal alternative groups |
| c2h_refresh_actor | homecoming policy switch, received source receipts, owned decision input sources, cognitive_samples, drive/caution/trust/class_bias, actual preconditions and navigation recomputation |

`c2i_navigation_input(actor)` is a useful narrower existing projection: refs, investigated windows, adoptions, closing availability and completion flags. It explicitly does not contain the whole chain above. Do not promote it to a certificate for c2d/c2h/safety merely because it matches.

## Time windows not captured by an available_when walk

`c2_meter_fresh` directly checks `actor.mind.time - read_claim.time > 30` after checking owned reading/epoch/location claims. Its transition is the first existing execution-grid point strictly after sample.time+30; merely looking for at_least AST nodes misses it. Full sample claim identities/time, co-sampled epoch/location, observed environment epoch and the dynamic age boundary belong in a certificate. `c2_safety_goals` also constructs minimum_time using sample.time+30 or a later environment observation time.

Evidence refresh uses `dvp_refresh_current_evidence`: only while refresh_until is not personally true, and only declared known refresh_refs can raise minimum_time by their received cell times. Clock/irrelevant observation time alone is not that dependency. Preserve whole relevant cells, not just value or revision, because source/trust/claims/time can alter source-supported freshness and decision provenance.

`c1n_known_future_wait_actor` must still run each selected turn. It compares both positive/closing opportunity bounds and pending completion, and `c1n_known_wait_actor` recomputes duration from target, current mind.time, wait_chunk_limit, daytime limits and early chapter timing. Caching its final action duration within a coarse time bucket is unsafe. A reusable builder skeleton must call this dynamic tail after reuse and retain its own input/output ownership.

## What revision + heard length cannot establish

Same revision/count does not establish identical completed/used/owned-decision/advice/attempt state, request identity/source/parse receipt, received-speech ordering/content, policy trust/value order/budget, hypotheses/models/interpretations, or chapter/config/action declarations. Equal receipt length can contain replacements or different requests. Equal cell value can hide new perception evidence/time/source and changed applicability. Different revision need not mean a relevant dependency changed. A certificate must preserve exact relevant values and stable declaration identity, and unknown closure must fall back to the original refresh.

Do not manufacture a complete certificate by stripping a few fields from the entire actor: such a key is expensive, contains unrelated churn and has no demonstrated hit region. A useful implementation should separate immutable construction metadata, compact exact runtime inputs, a known next transition and the always-dynamic wait/navigation tails. At present the local-eligibility and safety/source closure above is not proved by an AST-only window extractor.

## Foolish configuration and real scoring

Current `cognitive-profiles.dv::Foolish_Detective` supplies Replay_Depth=2, plan_depth=2, resistance=0.25 and scope_reference_check=false with its value ordering. It does not set information_weight or replace hypothesis priors. Initialization samples information_weight from the existing policy's 0.95/1/1.05 values; `c1_raw_person_policy` starts it at12 for both detective configurations. The generic initialization applies sampled quantities and then the chosen profile. Current/frozen02/frozen03 declarations therefore do not support the premise that Foolish automatically has low information weight or specially wrong hypothesis priors. Any intended new priors/weights would be a behavior/design change, not a neutral scheduling optimization.

Actual scoring: `dvp_action_value_context` counts unknown/conflicted/relevant information and evidence/probe verification, then multiplies information+verification by policy.information_weight. Progress/achievement use progress_weight; private/owned utility also enters progress. Hypothesis selection in `dvs_hypotheses` gives false=-1, true=1, otherwise declared prior, then adds cached model fit and rejects inconsistent/false candidates. `dvp_answer_reward` returns1 for personally true answer or matching owned argument, otherwise only the current matching hypothesis's prior. A prior can influence selection/reward; it is not factual support or an answer-acceptance override. `dvp_eligible_base` still checks speech truth, subject topic, accepted/attempt signatures and false-answer exclusion. Do not confuse the selected action score with the downstream authoritative answer result.

## Frozen02/03 evidence: enumeration versus actual evidence work

The preserved packet extraction contains real action_selected and action_execution records, actual replay actions, hypothesis adoption and submitted/accepted answers. In frozen03 Foolish: e1074 at487 selects E7 inspection, e1188 at488 selects E8, e1251 at489 selects sound_trial; e1750–e1807 at517–521 select E1–E5 inspections; replay is selected e1837 at524 and e2149 at555. Subsequent E6, E9/E10 and latch contact are also real chosen/executed actions, rather than fabricated assumed results.

The first recorded answers in that same prefix are submitted/accepted e2696/e2697 at594 (me), e2793/e2794 at605 (V3_0), e3077/e3078 at631 (V4_0), and e3321/e3322 at654 (trust). Accepted events explicitly project literal_truth=true and epistemic_support=true. Frozen02 exhibits the same order and times with different event IDs. The Standard route differs around latch/pantry ordering and has deeper movement planning, but the Foolish prefix is not a visible sequence of trying every answer until one accepts.

Foolish factor_prune e2026 at553 reports constraints with unknown-or-not-contradicted rejections, not a completed explanation or a granted observation. Enumerating/pruning authored hypothesis candidates is a separate mechanism from brute-force answer submissions. These data justify distinguishing paid internal search from actual sourced inspections; they do not, on their own, prove each full argument chain valid. Projection omits some model/argument snapshots and detailed source cells (raw references are preserved). Full raw reconstruction and a design-aware review remain separate. No new raw historical journal scan or simulation was run here, and no missing dynamic branch is guessed.

## Recommendation to main

Retain existing queued exclusion and actual observation/time boundaries. Reuse the existing AST/ref/index facilities, but require a complete declaration/runtime dependency certificate before a general goal-refresh skip. Keep unsupported time shapes, dynamic freshness and local-eligibility dependency gaps on the original path. Do not market changing Foolish motives/priors as a performance fix or label its existing recorded evidence route brute-force merely from its available candidate catalogue. Complete the design review separately with the main agent. Engineering and game acceptance remain distinct; no acceleration or five-day completion is claimed.
