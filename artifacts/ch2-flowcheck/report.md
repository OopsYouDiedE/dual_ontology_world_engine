# Chapter 2 unified-host flow check

## Result

The check now parses and executes through the unified Main host, including the Chapter 1 ending, C-key continuation, Chapter 2 opening, daily actions, evidence, interviews, replay, confrontation and council ending. The final independent run **failed: 104 assertions, 103 passed, 1 failed; verify exit 1**. It does not prove an error-free playable handoff: the C-key handler errors after switching views, and the first Chapter 2 spawn is outside a walkable cell. Game code was deliberately left unchanged.

Only automatic regression was performed. Level 1 transcript/literary acceptance, Level 2 visual UI acceptance and Level 3 blind play were not performed; character behavior and player experience are unverified. No design document was read. No commit or push was made, as instructed for this worktree.

## Changes

- [check_flow.gd](../../godot_csharp/scenes/Chapter2/check/check_flow.gd#L74): uses the Chapter 1 fixture to start Main, advance waking dialogue, jump to `answer_done`, then restore `investigate` so the formal interaction menu is available. Selecting `answer` runs the real finale, save and ending card. It checks the door, ending state, current save, continuation label/key, same engine instance, replacement world view, inherited player name, authored opening lines and field controls. C is delivered to the actual UI key handler. There is no `publish` probe or reconstructed inheritance JSON.
- [chapter2_fixture.gd](../../godot_csharp/scenes/Chapter2/check/chapter2_fixture.gd#L46): extends the existing Chapter 1 Main fixture, drives `Main.dispatch`, advances the formal dialogue UI, selects current menu credentials and lets UI/field presentations issue their own acknowledgements. Waits include Main's queue, dialogue, replay and actor movement; required endpoints can be specified. Missing host/view/map resources and timeouts fail explicitly. Read-only Chapter 2 state uses the existing `ChapterProbe` bridge.
- [check_flow.gd](../../godot_csharp/scenes/Chapter2/check/check_flow.gd#L119): keeps the 17-map walkability sweep and subsequent schedule, rule, investigation, replay, confrontation and council checks. After recording the defective initial spawn, the automatic map sweep and its existing fixture positioning allow remaining checks to execute. Replay Escape uses `CampusMap._input` and the normal UI acknowledgement. Dependent replay checks stop if presentation fails.
- [godot_csharp/AGENTS.md](../../godot_csharp/AGENTS.md#L101): documents this unified-host regression route. No C#, game/UI/field code, story sources or generated content was changed. Existing edited files keep LF endings.

## Assertion updates and retired assumptions

No existing gameplay assertion was intentionally removed. The following checks or their observation mechanisms changed; implementation assumptions tied to the retired host were removed.

| Previous check / assumption | Current check | Reason |
| --- | --- | --- |
| `第二章真实继承启动`, tested via `engine.Started()` on `Chapter2.tscn` after probe `publish` | Real ending card, fresh save, C action, same Main engine, replacement campus view, inherited name and first field state | The separate ChapterEngine/Chapter2 host and `Started()` interface no longer exist. |
| A separate `begin` request must produce semantic lines | Opening `arrival`, `lesson`, `free` line IDs must pass through the formal UI | Main sends `view_ready`; GameSession starts the chapter automatically. A second `begin` is invalid. `skip_intro` metadata has no current reader. |
| `章节事件在有限步骤内完成`, synchronous queue capped at 256 iterations | `章节事件在有限时间内完成`, bounded real UI/host waiting | Dispatch is asynchronous and includes movement, fades and presentation acknowledgements. |
| `章节选择有实际可用候选`, counted during direct event draining | Fixture rejects an empty/unresolvable current menu and records a failure; planned confrontation pairs retain their own assertion | Candidates and tokens are now obtained from the displayed menu and submitted through the UI. |
| `正式 .world 编译文件可读取` / `正式 .world 章节产生语义台词` | `.story` compile-file and authored UI line-ID checks | The authored source is `.story`; the generated world-script JSON remains the runtime input. This is a naming/observation update, not reduced coverage. |
| `清晨提到台账`, matching a text substring | Current `chapter2.dawn.*` line must be observed in the UI | Uses the stable authored scene ID rather than hardcoded story text. |
| `实际候选含所需交谈入口`, selected by a label substring | Select the real available `ask` candidate, then verify completion and `cancel` if the follow-up menu remains | Current Talk API has stable option IDs; it may legitimately close a follow-up when an actor becomes unavailable. |
| Replay steps collected directly from a raw `replay_play` packet | Steps read from the current replay result after the formal UI playback acknowledgement; manuscript return additionally checked | Main owns event draining and UI presentation. |
| `对账发生`, matching a location-name substring | Current `chapter2.evening.*` and `chapter2.confront_begin.*` lines observed; all valid pairs additionally checked | Preserves the confrontation check without embedding dialogue. |
| `议事日：章节到议事`, checking only the probe's simulation-ended flag | Probe ended, displayed chapter ended and actual ending card all required | The simulation flag alone does not establish UI completion. |
| `议事结果`, hardcoded row-prefix comparison | Prefix read from compiled `design.text["终局.名录库"]` | Preserves the authored result-row assertion using its source template. |
| Synthetic `move` inputs with only `place`; forced empty modal context | Fixture map loads emit the normal field `move` through Main; actor interactions use current positions and actual modal context | Current spatial context comes from WorldView. `stay` uses the current location, so an action's `place` override no longer relocates the player. |

All original map keys, required chapter sections, first-night/day progression, rule telling, case-day timing, four interview targets, replay steps, confrontation pairs and council result criteria remain covered. The new initial-spawn assertion remains failing rather than substituting the map sweep's valid `from_central` spawn.

## Final verification and defects

[Round-05 independent report](round-05/validation-report.md), [raw stdout](round-05/flow2.stdout.log), [raw stderr](round-05/flow2.stderr.log), [verify output](round-05/verify.stdout.log), and [continuous trace](round-05/20261008T211348202-378a2b461eba4139afbd31e6fedddd1f.jsonl). Logs and the new trace were copied byte-for-byte; sizes and hashes are recorded in [run metadata](round-05/run-metadata.json).

Key output:

```text
  ok  章一门开、结束状态与章末卡同时成立
  ok  章一终局写入本轮 user://chapter1_end.json
  ok  章一章末卡提供继续进入章二（C）
  ok  同一 Main 引擎替换章一现场为章二视图
  ok  第二章 view_ready 后正式开场完成
  ok  章一继续后到达章二第一个可操作状态
    handoff field: map=lecture position=(64.0, 67.1407) walkable=false stage=field
  ok  正式回放回执后回到重演手稿
  ok  对账发生（5 对有效）
  ok  有效陈述/证据对全部通过真实候选提交
  ok  议事日：章节到议事与正式章末卡
TOTAL 104 checks, 103 passed, 1 failed
FAILED: 章二第一个可操作状态的出生点可走
```

1. **C-key continuation runtime error.** `UISystem.gd:398` calls `_navigate`, whose `ui_event` synchronously switches Main's view. `Main.gd:41` removes the old UI from the tree; `UISystem.gd:399` then accesses its now-null viewport. Exact error: `SCRIPT ERROR: Cannot call method 'set_input_as_handled' on a null value.` Backtrace includes this check's `handoff` at line 102. The same engine and new chapter do proceed, but the route is not error-free. This was not suppressed or bypassed with a button route.
2. **Wrong first playable spawn.** `Chapter2InteractionSession.cs:254` sends `south` for the Lecture scene. `lecture.tscn:382` declares only `Spawns/from_central` at `(640, 336)`. `CampusMapView.gd:70` falls back to `(64, 64)` when the marker is absent; the actual post-opening position is `(64.0, 67.1407)`, with `walkable=false`. The original 17-map sweep passes when loading lecture with `from_central`. The new handoff-spawn failure is retained; game resources and code were not repaired.
3. **Environment error.** Final stderr also contains `ERROR: Failed to read the root certificate store.` at `platform/windows/os_windows.cpp:2570`. It is separate from the 1 failed assertion. No other final script/engine errors or exit leaks were observed. Verify exits on the runner's nonzero status; its error scan would also reject these errors if assertions all passed.

## Build, sandbox and earlier evidence

- [Baseline report](baseline/baseline-validation.txt): `compile_story.py --check` passed, 7 outputs current. Host build passed with `dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 -nr:false -v:minimal`, 0 errors, 3 warnings (two NU1900 network/audit warnings and existing CS0414). Default parallel MSBuild failed without actionable diagnostics; this was not established to be a filesystem-write restriction.
- Default Godot user data failed to open `user://logs/godot2026-10-08T20.59.31.log`, followed by a signal-11 crash. It produced no assertion result; only that agent's spawned process was terminated. Godot was then imported and run with process-only `APPDATA=C:\Users\Administrator\dowe-codex-flow2\scratch\ch2-flowcheck\appdata`, keeping user writes in the worktree. Final command remained `python tools/verify.py --only flow2`; build/import had already prepared its prerequisites.
- Baseline with the writable APPDATA reproduced the original deleted `Shared/ChapterEngine.cs` preload parse failure at line 134.
- Earlier independent snapshots are retained in `round-01` through `round-04`. Their parse/setup/wait/replay-driver defects were corrected in the check. Round-04's downstream presentation errors followed the fixture's wrong replay callback and are not reported as separate game bugs; the final run removes that callback error and all dependent failures.
- Final `git diff --check` passed. The working tree contains the check, new fixture, module rules and task evidence only. No other regression selections or gameplay acceptance levels were run.
