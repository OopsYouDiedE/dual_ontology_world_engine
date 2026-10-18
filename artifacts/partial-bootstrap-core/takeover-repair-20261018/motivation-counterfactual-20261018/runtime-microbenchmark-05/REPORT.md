# Neutral runtime microbenchmark, 2026-10-18

No formal game session was executed. The main agent reported the fifth formal window had stopped before this benchmark began; no formal session ran concurrently. No production sources were edited. Frozen input fingerprints are in prepared-source.json. Both isolated Release builds and all benchmark processes exited normally with empty stderr.

Each named with_many case uses 8,000 measured calls per sample; each has_value case uses 40,000. Each case first warms 2,000 calls, then runs three serial timed samples with explicit collection outside timing. DOTNET_TieredCompilation=0 was used. Forward process order was frozen04 then current; reverse order was current then frozen04. Tables show median sample milliseconds, retaining all raw samples in the adjacent stdout files. Allocated bytes are current-thread totals; the small constant Stopwatch allocation is included in both variants. This is a warm-index benchmark, not index-build or retained-memory measurement.

| Neutral case | Forward ms old → current | Reverse ms old → current | Reverse allocated bytes/call old → current |
|---|---:|---:|---:|
| with/n=2/u=1/nested=False | 2.116 → 2.778 | 1.880 → 2.420 | 448.005 → 560.005 |
| with/n=4/u=1/nested=False | 2.386 → 2.867 | 2.616 → 3.272 | 672.005 → 768.005 |
| with/n=8/u=1/nested=True | 4.858 → 8.597 | 5.214 → 6.739 | 1288.005 → 1352.005 |
| with/n=8/u=2/nested=True | 13.731 → 10.151 | 7.512 → 10.563 | 1681.938 → 1608.005 |
| with/n=16/u=1/nested=True | 11.490 → 6.776 | 8.529 → 7.140 | 1416.005 → 1416.005 |
| with/n=128/u=8/nested=True | 82.669 → 57.583 | 71.583 → 51.827 | 12171.871 → 11107.871 |
| with-duplicates/n=4/u=4/nested=True | 10.290 → 12.669 | 8.541 → 10.402 | 1361.938 → 1449.938 |
| has/n=128/query=frozen-hit | 227.019 → 13.629 | 217.050 → 13.719 | 15656.001 → 536.001 |
| has/n=128/query=fresh-hit | 223.764 → 217.953 | 219.563 → 216.704 | 15656.001 → 15656.001 |
| has/n=512/query=frozen-hit | 815.052 → 13.893 | 853.022 → 13.186 | 61736.001 → 536.001 |

Small-record setup overhead is visible in both process orders. The current fast path validates both tables and creates update/presence indexes; small tables previously used short reverse scans. Threshold.patch retains the old algorithm unless base count is at least 16, or base count is at least 8 and updates count at least 2. Threshold.Runtime.cs is an isolated candidate, not applied or separately benchmarked. This removes the clearly observed small-record/one-update regressions while keeping larger-table examples. The n8/u2 nested case changed direction across orders, so that boundary remains uncertain and must not be described as a universal improvement.

Structured membership on frozen/backbone queries shows a large warm-index reduction in this synthetic workload. Fresh mutable queries deliberately remain on the original scan and show similar costs. The structured examples contain differing text identifiers and duplicate nested Rational/double values; all-numeric buckets, cold indexes and one-off queries are not represented by that benefit. The benchmark does not establish the proportion of eligible queries in a formal session.

The four full process wall times were approximately 22.000s (forward frozen04), 10.779s (forward current), 10.378s (reverse current), 21.632s (reverse frozen04). These are engineering microbenchmark process times; they do not measure campaign progress or two-chapter/five-day completion.

Full per-case samples are preserved in frozen04/bench.stdout.jsonl, current-prepared/bench.stdout.jsonl and their bench-reverse counterparts. comparison.csv/json and comparison-reverse.csv/json contain all case medians and allocation details. Semantic equivalence is a separate question covered by the earlier neutral differential probes; timing alone is not a correctness claim.

Sweep32/ArrayPool64 remain isolated, unbuilt and unexecuted in runtime-snapshot-candidates-05; neither was included in this measurement.
