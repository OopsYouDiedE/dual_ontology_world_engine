# Fresh shallow structural membership — isolated engineering results, 2026-10-18

The main agent confirmed all formal 06 windows had stopped before these isolated operations began. Number3 ran first, followed by FreshMembership8. Each original/candidate build, neutral differential process and microbenchmark process exited normally with empty stderr. Production source was not edited. Both patches passed git apply --check against production immediately before execution; the source copies and fingerprints remain in the parent directory.

The typed neutral stdout and corresponding raw journals are byte-identical; exact hashes/byte sizes are in differential.json. This includes first-error type/message and signed floating-point bits. The existing raw_dag.py separately read both versions: sealed journals retain complete final checkpoints; the membership duplicate-ID partial retains a committed prefix without a seal. All original raw bytes remain unchanged.

The benchmark used Release, DOTNET_TieredCompilation=0, one process at a time. Forward order was original then candidate; reverse order candidate then original. The table reports per-case sample medians, not summed game timings. All samples are retained in original/ and candidate/ stdout. Allocation totals include the constant 40-byte Stopwatch overhead per sample.

| Neutral case | Calls/sample | Forward ms old → candidate | Reverse ms old → candidate | Reverse allocated bytes old → candidate |
|---|---:|---:|---:|---:|
| has/n=4/query=fresh-hit | 40000 | 18.5076 → 19.4553 | 22.2734 → 17.3219 | 31040040 → 31040040 |
| has/n=8/query=fresh-hit | 40000 | 26.6072 → 22.8633 | 27.3348 → 17.1064 | 50240040 → 21440040 |
| has/n=8/query=fresh-miss | 40000 | 12.5984 → 2.5033 | 13.1261 → 2.2498 | 39360040 → 960040 |
| has/n=128/query=fresh-hit | 40000 | 255.7046 → 17.2398 | 255.1254 → 16.0892 | 626240040 → 21440040 |
| has/n=128/query=fresh-miss | 40000 | 242.6872 → 2.3141 | 198.9043 → 2.1552 | 615360040 → 960040 |
| has/n=128/query=frozen-hit | 40000 | 14.4659 → 14.3511 | 15.3076 → 14.6667 | 21440040 → 21440040 |
| has/n=512/query=fresh-hit | 40000 | 858.1627 → 17.0681 | 859.2801 → 17.0198 | 2469440040 → 21440040 |
| has/n=512/query=fresh-miss | 40000 | 836.6150 → 2.5531 | 827.7720 → 2.1383 | 2458560040 → 960040 |
| has/n=512/query=mutable-backbone-hit | 40000 | 14.5529 → 15.8980 | 14.5759 → 18.2008 | 21440040 → 21440040 |

FreshMembership8 recommendation: central adoption is reasonable after the current integration review. The synthetic large structured lists show stable improvements for fresh shallow hit/miss queries and much lower per-call allocation; the indexed buckets still invoke same_value for equality. Existing frozen/backbone paths retain identical allocations, but their timing varies: n512 backbone-hit reverse samples are 14.5759→18.2008ms. Small n4 paths cannot use the index; no improvement should be inferred there.

The fresh query objects are constructed before timing and reused without ever freezing or caching them; thus the candidate recomputes their fingerprint each call. Construction costs in real calls are excluded equally. Each case warms the index before timing, so cold index building and retained-memory changes remain unmeasured. Workloads with very wide/deep queries, unsupported atoms or numeric-only collision buckets can have different costs. Depth/cycle/fresh-mutation differential samples preserved original values and first errors.

These are engineering measurements of neutral Runtime operations. They neither run formal prepare/complete nor establish five-day/two-chapter completion or game improvement. A combined production build/neutral check and later authorized formal profile remain separate evidence.
