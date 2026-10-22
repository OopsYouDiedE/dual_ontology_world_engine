# Evidence postprocessing optimization (2026-10-23)

Scope: read-only tools only, based on 5b0aeb0dc, independent opt/fix-postprocess-speed worktree. No world/design files read or changed. No formal session started; no production DAG scanned.

Static findings:
- RawDagIndex already reused decoded envelopes with evidence-index on, but re-encoded every payload to validate the chain. The decoder now exposes the exact validated payload byte span and SHA-256 consumes it directly. All frame/type/chain/reference/commit/seal validation remains active.
- Event metadata previously reread and decoded record/field nodes from the journal after the scan had just decoded those nodes. A read-only LRU retains up to 8192 nodes and 16 MiB of wire bytes. Python object overhead is additional. Cache misses use the unchanged SQLite offset reader.
- Both additions belong to the existing opt-evidence-index mechanism. Off retains the reference path. No fields, order, sources, checksums, output records, or tail reporting are removed.

Finite synthetic measurements (same host, no session; 3,588,574 bytes, 6005 nodes, 1500 events):
- Existing HEAD implementation: index off 2.015669 s; existing index on 0.686943 s.
- Candidate final implementation: index off 2.009529 s; candidate index on 0.316726 s.
- Relevant existing-on versus candidate-on ratio is about 2.17x (0.370 s saved), rather than the 6.34x off/on ratio. These are short single measurements, not a production estimate. Each measurement includes index creation, full summary/index bytes, and temporary index cleanup; journal construction is outside timing.

Verification:
- test_postprocess_speed: exact byte spans for all atom kinds including negative zero, Unicode, nested records, and cache count/size eviction.
- Existing test_tool_hardening raw DAG differential: normal/duplicate/missing/nested fields, invalid rational, bad ID, negative timestamp, duplicate commit, bad seal, forward reference, after-seal frame, bad chain, truncation, and frame corruption. On/off summaries, event-index bytes and valid selected binary exports are identical.
- The complete existing finite tool-hardening suite also completed successfully (5 s). It uses synthetic evidence and finite process/file fixtures; no DV session or build. After the final decoder micro-adjustment, payload/cache checks and the raw-DAG differential were rerun successfully.

Boundaries:
- No actual 828 MiB trace or whole-session run timed here. No claim of full postprocessing or end-to-end <=180 seconds.
- run_debug still separately exports stdout and runs the isolated causal checker after raw DAG validation. Those costs remain and are not inferred from this microbenchmark. Complete stdout and unprojected raw tail semantics remain unchanged.
- Parent agent owns actual full-run timing, merge, and validation under current authorization.
