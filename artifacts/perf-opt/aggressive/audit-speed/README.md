# Read-only causal membership indexing — 2026-10-22

Scope: continuation of 77e31772 on opt/fix-audit-speed. No new session, production content changes, merge or push.

- Runtime retains each ancestor closure's existing BFS list and its already-built HashSet. Membership reads the set; ordered closure consumers receive the same frozen list. The one-owner/eight-closure bound remains.
- Integrity checks replace repeated linear ID-prefix membership with each text ID's first event position. Last-match event lookup, per-event/per-parent checks, finding overwrite order and evidence-reference order remain unchanged. Unsupported/malformed graphs fall back to original DV evaluation.
- Both paths use the existing tool-only opt-causal-index switch. No change to opt-plancache; chapter 50 genuinely consumes that switch.

Engineering verification: generated the complete 70-checks closure, then compiled the temporary harness and the unchanged CausalCheckBridge with .NET 8, zero warnings/errors. The first temporary harness build lacked StartupObject and failed with CS0017; specifying Probe fixed the harness configuration, without source changes.

Finite on/off comparison used the actual generated v_integrity, v_ancestor and v_ancestors functions: empty events, valid graph, duplicate ID, forward/missing parent, child-before-parent, absent fields, boolean/numeric IDs, malformed parent list/record and a cycle; a seeded 60-node graph compared ordered closures and membership for five child/ancestor IDs, including missing IDs. All results or exception type/message matched. Appending to a mutable graph invalidated the index correctly.

One warmed synthetic 2,000-event chain integrity call measured 216.8219 ms off and 4.9727 ms on after checking complete finding equality. This is limited synthetic timing, not a formal-session, full-domain or game-speed claim.

Full historical sealed DAG: read-only existing isolation-depth-two evidence, one invocation only, 300-second total process bound; started after the parallel Foolish run_debug process exited. Result recorded below.

Full historical result: DAG intake completed in 32,572.6154 ms; domain-check started but did not finish before the 300-second total bound (300.171 s measured). No report completed. The earlier 17,395.4705 ms intake/300-second timeout remains historical, not a matched benchmark. Full-domain completion, verdict equivalence on this DAG and overall speed improvement remain unverified. No retry ran.
Temporary cleanup: automatic command policy rejected recursive removal of the verified task-only .scratch/causal-membership path. Ignored temporary harness/generated files remain there; no cleanup workaround was attempted. Full stderr is retained beside this report.
