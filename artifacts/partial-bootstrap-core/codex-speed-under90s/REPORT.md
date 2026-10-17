# Formal Level-1 session — 2026-10-17

Command: `python language_core/tools/run_debug.py --out artifacts/partial-bootstrap-core/codex-speed-under90s --timeout 120`

This was one new `formal-model` logic session. [`result.json`](result.json) records `elapsed_seconds: 106.125`, `exit_code: 0`, `trace_written: true`, and `output_well_formed: true`; the build source remained consistent and unchanged. The 60–85 second target was **not met**. [`events.jsonl`](events.jsonl) records `debug_end.steps: 536`, below the configured 550-action limit, and `session_stopped.status: no-admissible-action-or-budget` at game minute 2029. Its stop evidence says `no-scheduled-or-admissible-action`, so the requested completion of all 550 steps was **not met**.

The following are actual `applied` action executions, not planning candidates (game minute in parentheses):

| Milestone | Execution |
| --- | --- |
| `shen.opening.serve-tea`; `demon.opening` | 491; 550 |
| `detective.answer.me`; `detective.answer.V3_0`; `detective.answer.V4_0`; `detective.answer.trust` | 610; 646; 695; 733 — each has an `answer_accepted` presentation receipt |
| `demon.departure.release-outer`; `detective.departure.open-door` | 735; 738 |
| `detective` / `huo` / `shen` / `su.departure.cross-out` | All four at 740 |
| `quer.c2.purge.corridor.north`; `quer.c2.open.corridor.north` | 2022; 2023 |

All 16 residents had an `applied` move into `living_hall`: `quer` and `lucian` by minute 703; `detective`, `huo`, `shen`, and `su` at 740; `ward` and `gu` at 863; `ruan`, `elena`, `selma`, and `moran` at 1902; `mora`, `bertrand`, `veronica`, and `margaret` at 2018. `quer` later moved onward to `central_hall` at 2025.

This Level-1 evidence covers logical execution and player-visible text, not Godot geometry, input, or display. The milestone list was reached despite the runtime and 550-step targets remaining unmet. No Git commit was made.
