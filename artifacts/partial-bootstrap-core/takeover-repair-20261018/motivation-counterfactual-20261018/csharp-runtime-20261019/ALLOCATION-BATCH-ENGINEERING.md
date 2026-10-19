# C# allocation batch (2026-10-19)

Runtime shares validated ordered-key layout metadata. Compiler emits known literal capacities; pair/single/list3 use fresh literals. Normal values, aliases, first ordinary errors, exact values and full evidence remain. Early allocation changes OOM ordering; helper profiling attribution changes.

Serial neutral engineering compared duplicate/malformed/mixed records, mutation isolation, Save/Restore/Trace/HostJSON and raw DAG bytes. comparison.json records exact original results; no observed differences in these samples. Layout first-index allocation for 1000 roots: 8 slots 1016000 to 25200 bytes; 128 slots 10216000 to 35360. Construction/freeze CPU varied and sometimes regressed; no stable whole-game CPU claim. Literal construction (30000 roots) allocation 15840000 to 12240000 bytes.

Production build: dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false completed in 25.10 seconds. This is engineering evidence only. Current compiler() rebuild completed and enforces C2/C3 byte fixed point; the original logs/cache hashes are retained.

Current Main default remains playback_speed=4.0, physics ticks=240 at that speed. The following 240-second formal-model window performs no Godot geometry/playback and therefore has no 4x time-scale multiplier. Actual game-day coverage, plot nodes and full sealing must be reported separately.
