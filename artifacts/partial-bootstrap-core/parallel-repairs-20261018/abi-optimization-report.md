# ABI container optimization and neutral execution

Implemented after the third diagnostic run ended and the main agent authorized unfreezing. Production changes are confined to `language_core/compiler/Runtime.cs`; no LinkParent, DV decisions, chapters, budgets, fees, snapshot payloads or wire formats were modified.

`has_value` uses a cached ordinal string set only for a string query against a frozen, entirely string-valued list or a mutable owner with its currently valid frozen Backbone. The set belongs to the immutable frozen value and does not retain mutable owners. Any non-string list element retains the original structural equality algorithm. Owner mutation invalidates the Backbone through the existing mechanism; the previous frozen evidence and its membership set remain independent.

`record_with_many` maintains an ordinal string set alongside its original ordered keys. All-string appended keys use set membership. Once any non-string key appears, the original structural scan handles this and subsequent keys. Output iteration order, source duplicate keys, last update value, empty-update identity, branch copying and malformed-record error sequence retain the previous algorithm.

## Actual neutral execution

Executed `dotnet run` on the standalone runtime probe in Release, with `NuGetAudit=false`, disabled build servers and `DOTNET_ROLL_FORWARD=Major`. Process exit was 0 and stderr was empty. This executable directly links current Runtime/Session and uses throwing stubs for every DV decision facade; it does not compile or execute a formal game session.

Evidence:

- `abi-optimized-probe.stdout.txt`, `abi-optimized-probe.stderr.txt`
- `abi-optimized-probe-20261018/abi-differential.json`
- `abi-optimized-probe-20261018/result.json`
- `abi-optimized-probe-20261018/source-hashes.json`

`runtime-probe/AbiDifferential.cs` retains the former membership and batch-update algorithms as neutral references. The executed comparisons cover ordinal Unicode strings, duplicates, missing strings, nested structure values, exact/floating numeric equality, frozen/mutable/copied owners, cache invalidation after replacement and append, seeded ordered source/update records, empty-update identity, malformed-record first errors, and independently mutated nested branches. The serialization portion additionally exercised tagged exact rationals through 300 array layers, Save/WriteSave byte-equivalent format and Restore round trip, unchanged original trace/partial bytes, and frozen evidence rejection of mutation.

No speed claim follows from this engineering probe. Performance and behavior in a new formal session remain for the main agent's unified run. No flow regression or game acceptance was executed here.

## Contract note for the main agent's public documentation update

Runtime adds immutable-value caches and a string-only ordered-key membership shortcut. Both use ordinal string equality; mixed or non-string values retain structural/numeric comparison. Mutation and copy ownership, duplicate-key semantics, fees and evidence JSON are unchanged. Cache identity follows frozen snapshots, never a stale mutable owner.
