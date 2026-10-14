# DV host session

`DvSession` builds from the real DV compiler and `core/*.dv` plus `verification/session/*.dv`. It does not depend on the old Core or Godot.

`Open(scenarioJson)` starts a new session. `Prepare()` returns a conventional JSON intent containing the actor, action, execution identifier and cost bounds. Preparing again before completion returns the same pending intent. The host executes the interaction, then calls `Complete(receiptJson, viewer)` with `action_event`, `outcome` (`applied` or `rejected`), `actual` (`time`, `computation`, `stamina`) and its executor name. DV applies authority laws and updates knowledge only after successful execution.

`Save()` serializes the complete internal state, including a pending action. `Restore(snapshot)` resumes that state without repeating initialization or perception. These snapshots are internal save data, not player-visible information. `Trace()` seals the logic transcript after execution.

Build from the repository root:

```powershell
dotnet build language_core/Session/DuelView.Session.csproj -c Release -p:DvPython=<python-path>
```

The existing continuous logic runner uses the same prepare/complete boundary with the `formal-model` executor. Godot geometry and input must supply their own actual completion receipts.

Presentation is filtered by the requested subject: its own completed action, an actually parsed utterance, or an actual observation. Remote private action text is not published to that subject.

`OpenCampaign()` composes production modules from `game/modules.json`; `Snapshot(viewer)` supplies native location and visual bindings. Content availability does not count as crossing a doorway. The generated source manifest records module and generated-code hashes. Exact rational receipt values use the tagged `$dv_rational` representation.
