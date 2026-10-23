# Clear-room stale presence correction

Baseline: `2cf698eb4`. Candidate changes only the generic same-room observation path in `30-session.dv` and its contracts. No content chapter or authority condition changed.

## Source finding

`dvs_notice_peers` granted newly visible peers' locations but never withdrew a remembered location when that peer was absent from the actual room. Its unchanged-visible-context early return also ignored stale location memories newly supplied to the mind. Rejected attempts separately retain an input signature; they do not supply authoritative destination information and are not used to guess a failure cause.

The correction applies only when same-room perception is enabled, the actual room is clear, the missing peer has an omitted or literal-true observable-presence condition, a subject-specific binding exists, and the subject's uncontested remembered location equals this room. It replaces that cell with unknown, clears current claims, stamps the actual observation time and source event, and increments cognition revision. The event preserves the complete prior cell/claims and the actual room-observation parent. No remote destination is granted.

## Finite mechanism evidence

The attached fixture runs the previous function and candidate against the same miniature physical world and bound personal mind. It contains no session advancement, content inclusion, or autonomous AI loop.

- Actual visible same-room peer: retained.
- Peer leaves room-a: baseline retains stale room-a; candidate produces unknown at time 12, never room-secret.
- Original perception cell time and claim time: both 10; exact old cell retained in retraction event.
- Conditional visibility or unclear observer visibility: remembered cell retained.
- Unchanged visible context with a stale room memory: still retracts.
- Refresh of old source at original time 10: `older-than-current`; unknown remains time 12.
- Actual re-encounter: location reacquired at time 14, with a new claim at time 14.
- Original parent mind: unchanged.

The real DV compiler generated the fixture executable from current 20/30 plus this fixture and the exact baseline function. .NET Release build succeeded; executing the fixture exited 0 with the attached output. Production `check_dv.py --format json dv_core/modules/30-session.dv` reported success with no diagnostics. `git diff --check` reported no errors.

The first fixture compile failed because Windows newline conversion invalidated the generated source offsets. The fixture writer was corrected to emit LF, then compilation and execution succeeded; production logic did not change to satisfy that harness repair.

`dvs_refresh_derivations` only restores an old write if the current complete cell still equals its `change.after`; the new unknown cell does not match. Existing `dv_update_mind` rejects observations older than the cell time. Later fresh but inaccurate testimony is checked again by the ordinary same-room observation path.

## Limits

No formal story session, medical outcome, Godot geometry, game UI, full AI behavior, or performance comparison was executed. Parent task owns integration and authorized CLI acceptance. This correction does not choose a search route or infer where the missing peer went; it only removes a locally disproved location premise. Conditional visibility is conservatively excluded even if its condition might be true in a particular world. Memories of other rooms are unchanged.
