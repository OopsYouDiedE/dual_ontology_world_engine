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


Pre-merge finite follow-up:
- Explicit encode(decode(payload)) byte equality now covers signed int64 minimum/maximum, negative zero, Unicode/NUL, nested lists/maps, and both orders of the same map keys. Opposite key orders remain different byte strings and each round-trips exactly.
- Audited all RawDagIndex consumers found by repository Python searches: raw_dag methods record_fields/event_metadata/record_field/select/write_binary/write_value and debug_checkpoints.checkpoint_metadata. They only iterate, index, compare, encode, or read length; none appends, assigns, sorts, pops, clears, or otherwise mutates borrowed node items or nested references. record_fields creates its own map; event metadata is immediately encoded or read for output; select rebinds its local selected variable. Thus no copying boundary is required for current consumers. node() now explicitly documents the borrowed read-only contract. Future callers that need mutation must copy the returned value first.
- The updated focused payload/cache tests and existing complete/corrupt raw-DAG differential completed successfully. No production DAG read or session started.


Production read-only follow-up (concurrent survival formal run; not final uncontended timing):
- Exactly one complete scan of integration-S4 raw-events.dvdag: 828315953 bytes, 1126020 nodes, 38942 events; new index/validation 91.452087 s. Summary exactly matches the original, including SHA-256, sealing and zero residual tail. Raw event index bytes exactly match.
- Original stdout projection path on sealed 52179634-byte stdout: 26.251017 s. Bounded 4 MiB cProfile prefix identifies Windows Path.resolve as 1.993 s of 4.086 s instrumented prefix export, and binary parsing as another 1.103 s. Instrumented profiles are not speed comparisons.
- Cached immutable source resolution and direct binary append now use the existing evidence-index switch. One complete optimized stdout export took 6.351675 s (about 4.13x for this stdout pass). Visible/debug text, actual-event index, reasoning index and notices match original bytes. Summary differs only in required projector_sha256. On/off synthetic projections also match exactly.
- Separate 4 MiB raw prefix profile identifies binary decoding as dominant (0.829 s of 1.166 s instrumented scan); SQLite insertion/cache management are much smaller. No second full DAG scan was performed.
- Future run_debug results include postprocess_stages for raw validation/index, stdout export/index, profile summary, causal build/check, and source checks, including failure durations.
- Original recorded post was 145.953 s and included a causal checker timeout at its explicitly configured one second; no old raw-scan split exists. New measured raw+optimized-stdout is 97.804 s BEFORE causal build/check and source checks. Do not present this sum as total post or combine another run as final acceptance. Complete-command <=180 remains unestablished; dominant raw decoding cost remains unresolved.
- Metadata/prefix profiles are in production-measurement/. Derived comparison projections are removed after hashing; original evidence is unchanged. No formal session or build started here.


Indexed decoder follow-up:
- New Python cursor decoder removes per-atom BytesIO/read closures. Exact fixed-shape node-envelope matching avoids repeatedly decoding constant schema keys and values; all fixed bytes, lengths and type tags are matched, variables remain decoded at their original depths. Scalar/list node-reference fast paths retain signed int64, finite double, negative zero and strict UTF-8. Unknown shapes use the generic cursor decoder; format failures use the original reference decoder. Frame SHA, ordered chain SHA and all RawDagIndex semantic validation remain unchanged. Off still uses the old decoder; stdout optimization is already explicitly passed the same on/off flag.
- Three paired uninstrumented 4 MiB prefix reads: old/new 0.343916/0.205400 s, 0.400053/0.204908 s, 0.377245/0.199137 s; 12080 validated frames in every case. These are bounded diagnostic prefixes, not whole-run acceptance.
- After finite differential checks, exactly one new complete read of the same sealed 828315953-byte DAG took 42.340066 s, including the full event-index output and temporary index cleanup. Complete summary matches the original exactly; event-index bytes SHA-256 remains 1434016e3e7a5bc1a0f70689a45f182e3872054951428bc8f8955884e6a93d8b. 1126020 nodes / 38942 events and complete sealing preserved.
- Relative to the previously measured indexed path 91.452087 s, this observation is 2.16x / 53.7% less wall time. Parallel workload was not held constant; report the two actual durations and do not treat this as an isolated controlled speed ratio. Whole-command <=180 remains unverified: formal, build and causal stages were not run here.
- Added byte-level differential cases for valid nested values, every prefix truncation and byte inversion of representative encodings (including the fast node shape), deterministic random malformed bytes, duplicate map keys, nonfinite doubles, invalid UTF-8 and format depth limit. Exact successful re-encoding or exact format-error class/message matches the reference. Depth-limit comparison raises the interpreter stack ceiling only inside that finite test so the documented 512 format limit can be reached, then restores it.
- During implementation, an overbroad text substitution briefly inserted cursor code into the reference decoder; focused tests caught it, the reference was restored byte-for-byte from HEAD, and verification was rerun. An initial depth test hit interpreter recursion wording rather than the format limit; the explicit stack-ceiling fixture above resolved that test setup. Neither intermediate implementation was used for the full production read.
- Full decoder measurement and prefix pairs are saved in production-measurement/decoder-*.json. No formal session or build was started, original evidence unchanged, temporary prefix/index/script files removed.
