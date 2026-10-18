# Exact numeric-text boxed constants — isolated engineering results, 2026-10-18

The main agent confirmed all formal 06 windows had stopped before these isolated operations began. Number3 ran first, followed by FreshMembership8. Each original/candidate build, neutral differential process and microbenchmark process exited normally with empty stderr. Production source was not edited. Both patches passed git apply --check against production immediately before execution; the source copies and fingerprints remain in the parent directory.

The typed neutral stdout and corresponding raw journals are byte-identical; exact hashes/byte sizes are in differential.json. This includes first-error type/message and signed floating-point bits. The existing raw_dag.py separately read both versions: sealed journals retain complete final checkpoints; the membership duplicate-ID partial retains a committed prefix without a seal. All original raw bytes remain unchanged.

The benchmark used Release, DOTNET_TieredCompilation=0, one process at a time. Forward order was original then candidate; reverse order candidate then original. The table reports per-case sample medians, not summed game timings. All samples are retained in original/ and candidate/ stdout. Allocation totals include the constant 40-byte Stopwatch overhead per sample.

| Neutral case | Calls/sample | Forward ms old → candidate | Reverse ms old → candidate | Reverse allocated bytes old → candidate |
|---|---:|---:|---:|---:|
| zero | 1000000 | 36.3637 → 2.3921 | 32.8264 → 2.8135 | 48000040 → 40 |
| one | 1000000 | 39.4340 → 2.5668 | 32.6612 → 2.7679 | 48000040 → 40 |
| minus-one | 1000000 | 53.9905 → 3.4690 | 37.8085 → 3.1672 | 48000040 → 40 |
| fallback-negative-zero | 1000000 | 51.8651 → 36.6495 | 33.1394 → 38.6577 | 48000040 → 48000040 |
| fallback-two | 1000000 | 41.4662 → 32.1036 | 32.7398 → 38.7776 | 48000040 → 48000040 |
| fallback-decimal | 1000000 | 49.3775 → 41.4179 | 45.0686 → 57.9432 | 48000040 → 48000040 |
| mixed-common | 1000000 | 38.9254 → 12.1007 | 41.0848 → 13.4769 | 48000040 → 12000040 |
| mixed-fallback | 1000000 | 42.2293 → 43.4187 | 49.0007 → 50.6634 | 48000040 → 48000040 |

Number3 recommendation: the exact-spelling path is a reasonable small candidate for central adoption. Selected spellings remove 48 observed allocated bytes per call after warmup in this harness, rather than an assumed single output box. Mixed-common input removes the corresponding selected fraction; inputs are fixed strings and do not measure compiler literal frequency in a campaign. Fallback mixture allocation is unchanged and its candidate timing is slightly higher in both process orders (42.2293→43.4187ms and 49.0007→50.6634ms). Individual fallback case timing changed direction with process order, so no fallback speedup is claimed. Keep the scope at the three constants rather than adding a general cache.

Typed output includes negative zero, alternate spellings, subnormal/overflow/invalid inputs, current-culture variation, numeric key/memo/snapshot/serialization and public numeric operations. Scalar identity outside the DV ABI is not part of equivalence; no mutable box operation exists in that ABI. Cold initialization and retained static memory are outside this warm timing.

These are engineering measurements of neutral Runtime operations. They neither run formal prepare/complete nor establish five-day/two-chapter completion or game improvement. A combined production build/neutral check and later authorized formal profile remain separate evidence.
