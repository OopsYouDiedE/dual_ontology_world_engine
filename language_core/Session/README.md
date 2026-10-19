# DV host session

`DvSession` builds from the real DV compiler and `core/*.dv` plus `verification/session/*.dv`. It does not depend on the old Core or Godot.

`Open(scenarioJson)` starts a new session. `Prepare()` returns a conventional JSON intent containing the actor, action, execution identifier and cost bounds. Preparing again before completion returns the same pending intent. The host executes the interaction, then calls `Complete(receiptJson, viewer)` with `action_event`, `outcome` (`applied` or `rejected`), `actual` (`time`, `computation`, `stamina`) and its executor name. DV applies authority laws and updates knowledge only after successful execution.

`Save()` serializes the complete internal state, including a pending action. `Restore(snapshot)` resumes that state without repeating initialization or perception. These snapshots are internal save data, not player-visible information. `Trace()` seals the logic transcript after execution.

`Save()`/`Restore(string)` retain the in-memory JSON exchange contract only. New on-disk `WriteTrace(path)` and `WriteSave(path)` use checked binary DAG v2 files (.dvdag/.dvsave), with typed rational atoms and shared immutable nodes. `RestoreFile(path)` requires a valid complete seal. Writers exclusively create .partial, publish after sealing and refuse JSON/JSONL/gzip destinations; failures retain their partial bytes. No state fields are omitted.

`Snapshot(viewer).chapter_summary` is false until the actual second-chapter closing condition occurs. Afterwards it is a system record of actual votes, signatures, resources and unresolved results; it never becomes actor knowledge. The field supplies host data, and does not establish that a Godot closing card has been rendered or verified.

Build from the repository root:

```powershell
dotnet build language_core/Session/DuelView.Session.csproj -c Release -p:DvPython=<python-path>
```

The existing continuous logic runner uses the same prepare/complete boundary with the `formal-model` executor. Godot geometry and input must supply their own actual completion receipts.

Presentation is filtered by the requested subject: its own completed action, an actually parsed utterance, or an actual observation. Remote private action text is not published to that subject.

`OpenCampaign()` composes production modules from `game/entry.dv`; `Snapshot(viewer)` supplies native location and visual bindings. Content availability does not count as crossing a doorway. The generated source manifest records module and generated-code hashes. Exact rational receipt values use the tagged `$dv_rational` representation.

`OpenCampaign(player)` optionally assigns the named subject to human control. An empty player keeps autonomous decisions. `Prepare(selection)` accepts an action ID only while DV awaits that subject's choice, validates the same subject-local eligibility, and prepares the normal receipt. Waiting and resuming do not repeat cognition or execute a second action. Presentation names come from the native campaign binding; the host does not supply story text.

`OpenConfiguredCampaign(optionsJson)` calls native `dvg_host_open` with the parsed options record. Its host contract is `{controller: "player" | "ai", viewer?: string, campaign?: record}`; campaign configuration, defaults, and validation belong to DV. `Viewer` reads native `dvg_host_viewer` from the current or restored state. The older `OpenCampaign(player)` entry remains available. Both controller modes use the same production prepare/complete chain.

Godot `GameEngine.ConfigureSession(options)` accepts opening options only before a session starts. Its command pump serializes dispatches, queues reentrant input, and deduplicates successful completions by DV `action_event`; an unrelated receipt leaves the pending intention intact. A worker failure freezes dispatch rather than retrying a potentially committed transition. Opening, preparation, and completion build the viewer snapshot on that same worker. A prepared `dv_action` also carries an `execution_snapshot` for the actual actor's Godot geometry adapter; this is execution data, not an additional player observation. `Snapshot()` and `ReadMilestoneProgressJson()` return the latest completed cached projection without traversing state concurrently.

Internal awaited `SaveAsync` and `RestoreAsync` pause the dispatch pump and include the complete persistence operation in the work awaited by shutdown. All host save/restore entries reject an outstanding Godot execution receipt; awaited entries recheck after previous work finishes, since Prepare can produce a pending action during that wait. The legacy synchronous methods also require an idle host. Restore preserves DV's pending action from an earlier shutdown save and the next normal Prepare redispatches that saved intention for real execution; queued commands for the previous state are discarded while their input records remain. Shutdown can preserve a pending action as evidence: it stops new preparation, awaits current work, preserves queued inputs and worker errors, and records `session_opened`, unprocessed command count and pending action in `close-result.json`. Closing the title screen without opening a session records `session_opened: false` without inventing a trace/save error. Trace/save completeness remains separate from execution or game acceptance.

## Campaign audit bridges

Internal DVProgram.CampaignTrackedActors() and CampaignChronologicalTrace(state) call the native DV reporting functions. CampaignRun(maxSteps) returns live campaign state through the formal-model adapter, with all actors scheduled normally. Pass false for the authored action bound or zero for an unadvanced opening; this is not a Godot execution adapter.

`python language_core/tools/build_session.py --out language_core/Session` generates an inspectable copy. The project excludes this root DVProgram.cs from compilation and continues generating/building obj/dv/DVProgram.cs. Both copies are generated, never hand edited.

Production sources are composed by `game/entry.dv` using relative `#include` lines. The build and editor share `tools/dv_sources.py`. Diagnostic selection lives in `dvg_debug_config`; an empty actor list includes all actors without changing scheduling. `CampaignChronologicalTrace` now returns the generic read-only event projection with IDs, parents, action identity, source and selected details. Full snapshots remain in WriteTrace.

Use `python language_core/tools/run_debug.py --out <new-directory>` for one formal-model output run. It preserves raw output and source fingerprints and exports readable debug/player text. It does not perform Godot execution or certify a full level-one acceptance by itself.

Level-one testing is windowless DV logic only; it does not start Godot or require a UI replay. Read-only projections are independent copies, and presentation surface_voice is distinct from the supplementary voice.

`OpenConfiguredCampaign`接受`audit: bool`，普通Godot游玩默认false；完整采集显式true。策略按实例在每次DV调用时建立执行上下文作用域，不跨会话改全局默认。`AuditMode`可读取；Save/WriteSave在边界写入audit_mode，Restore沿用（旧档缺失默认true）。普通游玩的部分审计大字段未保存，宿主不会自动导出session.dvdag；WriteTrace仍是显式接口，不会恢复已省略的历史字段。

Binary record framing is implemented in compiler/Runtime.cs (`BinaryEvidence`): `DVLOG2\r\n`, then little-endian uint32 length, SHA-256 and typed payload. Raw DAG frames also carry sequence/predecessor checksums and node/event/checkpoint/seal records. Integers, finite IEEE double values (including negative zero), UTF-8 text, Bool, maps and ordered arrays are distinct; DV Rational atoms retain their canonical exact text as a typed DAG atom. `tools/binary_evidence.py` and `tools/raw_dag.py` read the same format, with old JSON evidence supported only for read-only inspection. This changes storage, not decisions or receipts; no gameplay validation is claimed by the codec checks.
