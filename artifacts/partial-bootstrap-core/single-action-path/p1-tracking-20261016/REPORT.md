# P1 repair and four-district tracking audit

Date: 2026-10-16. Scope: the requested runner/simulation repairs, native campaign helper and Session bridges. This is engineering evidence, not level 1, 2 or 3 gameplay acceptance. The existing REPORT.txt is preserved unchanged.

## Changes

- Removed the incomplete idle can_skip branch and idle metadata writes. Eligible autonomous actors execute peer notice, cognition, topic/argument/speech preparation and dvs_choose. Existing declared budgets and the explicit human-input path remain intact. No actor selection is based on the tracking list.
- Restored action_execution physical_before/physical_after and full receipt; both requested observation paths' mind_before/mind_after; answer_submitted mind/physical; full question_transition; model_recheck actor_before; mind_simulation actor_before and result.predicted. Existing revision metadata remains supplemental. dvs_event uses dv_with, which clones the payload; these are not mutable live-state aliases.
- Added dvg_tracked_actors and the requested append-order chronological projection, and dvg_run_campaign(max_steps). The helper opens once, uses the existing dvg_prepare/complete formal-model protocol, preserves chapter extension/state, and returns live state. false means the authored session action limit; zero means no advancement. It neither raises budgets nor supplies answers or synthetic knowledge.
- Added internal C# CampaignTrackedActors, CampaignChronologicalTrace and CampaignRun bridges. All world behavior remains compiled DV.
- The explicitly requested generation destination creates Session/DVProgram.cs, while the existing project already compiles obj/dv/DVProgram.cs. Excluded the inspectable root copy to prevent duplicate generated classes; both copies still come from build_session.py. Generated root files are locally ignored.
- Updated relevant maintenance/interface documents. Removed the superseded idle-cache guidance and unsupported performance/completion summary from current AGENTS guidance; historical reports remain intact. The design standard itself was not changed.

Tracking mapping, from docs/design-standard.md resident table: Jia / quer (Que'er, R-03), Yi / detective (R-05), Bing / ruan (R-10), Ding / mora (R-16). This list selects audit output only.

## Actual engineering execution

1. python language_core/tools/build_session.py --out language_core/Session completed with exit 0; stdout/stderr retained.
2. dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false completed with exit 0, no compiler warnings or errors. It emitted DuelView.Session.dll and DualOntologyWorldEngine.dll. The build log records 68.37 seconds; this is build time, not game performance.
3. DOTNET_ROLL_FORWARD=Major dotnet run --project artifacts/partial-bootstrap-core/single-action-path/p1-tracking-20261016/Probe/Probe.csproj -p:NuGetAudit=false --disable-build-servers -- artifacts/partial-bootstrap-core/single-action-path/p1-tracking-20261016 32 completed with exit 0. This directly calls the generated campaign helper from a fresh authored session, without modifying state, budgets or decisions.

The probe's caller bound was 32 completed actions. It returned an active chapter-1 session at minute 486 (08:06), starting at 480 (08:00), after 47.4785 seconds of helper execution and output processing. This is a short engineering window, not a speedup comparison or full-session duration. All four selected actors appear and adjacent extracted event times are nondecreasing using exact rational comparison; equal-time events retain append order and event IDs. tracked-trace.json preserves the projection including action data.

Every requested resident has an actual action_selected event: detective e27, huo e35, shen e43, su e51, quer e60, lucian e69, ward e78, gu e87, ruan e96, elena e105, selma e114, moran e123, mora e132, bertrand e141, veronica e150, margaret e159. The world also retains demon. These are participation observations, not a claim that everyone has completed deduction or that all behavior is equivalent. The probe selected no actions itself.

Execution event e23 contains physical_before, physical_after and receipt. Answer submission, question transition, model recheck and mind simulation were absent from this bounded window; their repaired fields were reviewed in source and compiled, not exercised here. The probe did not inspect observation snapshot payloads, so their runtime contract is also unverified. No claims of complete independent provenance verification are made.

## Remaining audit findings and limits

The report also points beyond the user's enumerated fixes: session/35-model-argument.dv still records actor_id instead of actor_before while checks/15-argument-provenance.dv consumes the snapshot; other stripped producers, including physical_process, were not repaired in this scope. The chapter2_goals_mind_revision cache remains and its full input equivalence is not established. Therefore these specific repairs do not close every evidence/caching issue identified in REPORT.txt.

No chapter-2 expansion occurred in this window. Continuous extension is wired in the helper and reviewed in source, but two-chapter execution/completion and Godot movement, collision, input, display and experience remain unverified. No flow/flow2 or other regression suite was invoked. No assertion totals or pass rates are used as game evaluation. No oracle, prewritten solution, injected world state or actor-skipping shortcut was introduced.

Existing untracked workspace artifacts were preserved. A documentation-edit helper initially failed to match a Unicode heading through PowerShell's stdin encoding; it wrote no documents before the exception and generation did not start in that attempt. It was corrected using ASCII anchors. A default git whitespace check flagged CR bytes on the edited lines of the already-CRLF simulation source; the CR-aware whitespace review reported no whitespace defects. Source encoding/content was not altered to suppress a semantic error.

Evidence: generate/build/probe stdout and stderr; probe-result.json; tracked-trace.json; Probe source; fingerprints.json (source and assembly hashes captured for the run). Final documentation-only edits after the run are recorded separately in final-fingerprints.json. The original raw evidence is not overwritten.
