# Neutral runtime probe

This project links the current `Runtime.cs` and `Session.cs` directly. It does not build or link generated game code. All DV decision facade stubs throw if called. The only DvSession operations used are Restore, Save and WriteSave on neutral nested arrays with a rational leaf.

Run after the complete repair batch is frozen, from the repository root, with an unused output directory:

```powershell
dotnet run --project artifacts/partial-bootstrap-core/parallel-repairs-20261018/runtime-probe/RuntimeProbe.csproj -c Release --disable-build-servers -p:NuGetAudit=false -- artifacts/partial-bootstrap-core/parallel-repairs-20261018/runtime-probe-evidence
```

If the installed .NET runtime requires it, use the repository's documented `DOTNET_ROLL_FORWARD=Major` environment setting. The probe itself has not been run by its author. Build/execute logs should be retained separately by the main agent. `result.json` appears only after all requested engineering checks finish; an exception is a failure requiring inspection, not a game verdict.

The executable exercises refusal of existing complete/partial evidence and verifies their bytes, string trace rational format, tagged Save rational format at 300 nested arrays, Save/WriteSave equivalence and Restore round trip, plus immutable frozen evidence and independent mutable copies. It preserves its output directory and refuses reuse.
