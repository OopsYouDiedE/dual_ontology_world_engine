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

Scope: value/COW mechanism equivalence only. No formal-session speed claim,
raw-evidence writer equivalence, gameplay, UI, or whole-program acceptance.
CLI integration owner must add runtimespeed to run_debug.py optimization_names
and to its DV_OPT environment loop. No content config is necessary because
this runtime option is sampled independently by R.ValueContext.

Final frozen-source Release LanguageCore.sln build: succeeded, 0 warnings, 0 errors (38.31 seconds).
