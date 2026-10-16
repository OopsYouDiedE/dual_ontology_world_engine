# Norm receipt and completion source review — 2026-10-16

Scope: `verification/session/20-speech.dv` and `30-runner.dv`. This review reads source only; no compiler, build, session, test, Godot or design document was opened/executed by this worker.

Before repair, `speech.norm_proposal` and `action.norm_decision` had no consumers in these session modules. Existing generic estimated-proposal handling is a different contract. `used` was appended for every completed attempt, including rejected execution, so it cannot evidence successful action completion.

The actual scheduled-speech receiver now records `norm_proposals[id]` only after a real `listener_parse`, when mapped content is structurally identical to the declared literal and the receiver's updated mind supports that literal. This retains the supplied norm, original speech source and parse event. It does not change norm rules or assent. The existing speech prediction/model functions do not call this receiver or the new receipt helper.

Successful read actions may declare `norm_receipt: {norm, content}`. The common Complete path records that content only after an actual observation and only when the updated owner's mind supports the declared content predicate. Unknown content creates no receipt. The actual action_execution ID is retained separately from the observation ID; read receipts link their source to execution and their comprehension field to observation. Content owns the declaration and actual read law/grants; the generic session contains no chapter, actor, norm or action identifiers.

Successful Complete of `norm_decision` requires a matching personally received norm. It replaces only that owner's rule with the declared mode/method/value impacts, or removes that norm on refusal, and records `norm_stances`. `norm_decision` events retain value order, declared choice, receipt links and before/after rules. Stance execution_event refers to action_execution, while observation_event separately records the following observation lineage. Content's existing execution law writes its own accepted/decided facts and the ordinary granted observation updates that owner's mind; the helper does not write another actor's decision or a global physical prohibition.

`completed_actions` is appended only when the common execution result is `applied`. Existing `used` remains an attempt-use record. The personal-choice worker uses completed_actions for completed_action_requirement and requires matching receipt provenance before norm decision eligibility; the content worker supplied norm_receipt on actual board reads. Choice remains the existing value_order comparison with positive support required for acceptance.

Unverified: compilation, formal-model behavior, propagation timing, eventual votes/signatures, UI and geometry. The main worker must run authorized validation only after all repairs are complete. Policy change/receipt events retain sources but are not gameplay acceptance evidence by themselves.
