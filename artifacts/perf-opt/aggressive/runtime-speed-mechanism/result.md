# Runtime speed mechanism check

Source branch: opt/fix-runtime-speed, base 6ce6d1332.
Runtime: DV_OPT_RUNTIMESPEED=0 and =1, each in a fresh R.ValueContext.
Standalone Release harness compiles compiler/Runtime.cs directly; no DV session,
AI scheduling, design files, storyline, Godot, or formal measurement is used.

Actual result: on/off values are same_value identical after 20,000 neutral
copy / nested read / nested write / snapshot iterations per mode.
Explicit invariants: unchanged independent sibling; nested parent snapshot
invalidation; external List alias invalidation; duplicate last-match lookup
and subsequent pair-key mutation. Initial fractions are exact 1/3 and 2/7.
SHA-256 of identical JSON projection:
8BE314FED634B900607B2E5183CEAEBD0DF572D313C35A1FE4977543A0238538

Program.cs is the actual finite harness source. Build it in an independent
net8.0 console project with ImplicitUsings and Nullable enabled and include
compiler/Runtime.cs. The temporary project is removed after completion.

Scope: value/COW mechanism equivalence; supplemental complete binary evidence checks are recorded below. No formal-session speed claim, gameplay, UI, or whole-program acceptance.
CLI integration owner must add runtimespeed to run_debug.py optimization_names
and to its DV_OPT environment loop. No content config is necessary because
this runtime option is sampled independently by R.ValueContext.

Final frozen-source Release LanguageCore.sln build: succeeded, 0 warnings, 0 errors (38.31 seconds).


## Supplemental binary evidence validation

EvidenceProgram.cs was compiled as an independent net8.0 Release console
program against the unchanged Runtime.cs. It creates actual OwnedList roots
when on and ordinary List roots when off, confirms the public List ABI,
performs nested updates, independent sibling reads and updates through shared
external parent aliases, and includes exact 1/3, 2/7, 19/23, 29/31 and 37/41.
Both modes write initial/final checkpoints plus actual e1/e2 raw_event values
through RawDagWriter/FrozenNode. Both modes also WriteEvidenceFile, then use
the production C# ReadEvidenceFile checksum/seal reader, create a mutable
restored-owner copy, write it again, and require exact same_value roundtrips.

check_evidence.py independently uses the existing read-only RawDagIndex to
validate all six binary files, checksum chains, definition references,
checkpoints and seals. Every expanded tagged field/item/rational value and
ordering in both checkpoints and both events is identical on/off. All six
files have complete_sealing=true and zero residual/uncommitted tails.
Only the envelope process_elapsed_ms timing is excluded from equivalence;
there is no requirement that nondeterministic timestamps or bytes match.
The decoded checkpoint/event digest and per-file sizes are in evidence-result.txt.

Static type-path inspection: Runtime.TryItems uses `case List<object>`;
IsDvList/snapshot/copy_value/RawDagWriter.Node use subtype-compatible `is`
patterns. FrozenNode accepts the resulting FrozenList, not the mutable CLR
runtime type. BinaryProjection and JSON emission use TryItems. ReadEvidenceFile
constructs standard mutable List values from decoded nodes before freezing;
subsequent copies can become OwnedList and their serialization was exercised.
No production `GetType()==typeof(List<object>)` exclusion was found.

No production source change was needed for this supplemental validation.
The temporary standalone project was removed. These checks do not start a
formal session, infer game performance, or prove arbitrary concurrent mutation.
