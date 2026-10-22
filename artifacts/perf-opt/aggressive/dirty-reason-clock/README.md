# Dirty reasoning queue and static clock plans — 2026-10-23

Base: cba7317c, fast-forwarded into opt/fix-audit-speed before manual integration of the previously reviewed dirty-reason-clock candidate. No design files were read. No formal session was started; no gameplay or performance outcome is claimed.

## Implementation

- Reasoning uses the existing Runtime reverse dependency queue, isolated by adoption table, external inference signatures and profile. It tracks complete premise and output cells, object existence and adoption changes, preserving forward/current-pass and backward/next-pass ordering. Budget exhaustion does not consume the next queued candidate.
- Inactive/non-infer declarations retain positions with empty dependencies; their condition/effect operators and references are not inspected during indexing. Zero budget does not build the index. Unsupported active operators explicitly fail; no production scan fallback was added. Active declarations may still be validated while initially indexing, before their original execution turn.
- Clock plans cache declaration structure, clock relationship and static thresholds. Equality/ambiguous clock references retain conservative minute sampling; numeric thresholds preserve the original integer-minute lattice. Reversed due<now intervals now return due, matching the prior environment entry.
- Explicit opt_dirty_verify sampling runs an independent uncached reference scan with the original live-process early return. Both optimized return paths compare to that reference. Default production calls do not invoke this scan. Remaining environment truth/stage scans and threshold-list iteration remain; this is not a fully event-driven environment scheduler.
- No Runtime ABI changes, role whitelist, production content change or additional switch.

## Finite mechanism comparison

The committed Mechanisms.cs harness uses the generated real DV functions, with opt_dirty=false versus opt_dirty=true and opt_dirty_verify=1. It compares full actors (only the two configuration flag fields removed), signatures, derivations and ordered events after every call; no gameplay state injection or formal AI scheduler is involved.

Observed equivalent results:

- Forward cascade and an earlier rule awakened for the next pass.
- One-step budget exhausted and replenished across four calls.
- Unchanged source value with changed claims, across five calls; an explicit assertion confirmed that the changed claims woke work.
- Inactive and non-infer malformed declarations with unknown operators/cross-subject references remain unvisited.
- Zero-budget active malformed declaration remains unvisited; positive-budget unsupported active operator raises an explicit error.
- Reversed interval: now=10,due=8 gives 8.
- Static threshold 15 gives 15; threshold 15.2 gives 16 on the original minute lattice.
- Equality at current clock gives 11; a live increment gives 11 through the early-return verification branch.
- Absent clock gives due=30; inactive malformed clock declaration remains untouched.

The first temporary harness build used an incorrect constructor name F_dv_equals and failed. Correcting the harness to the actual F_dv_fact constructor produced a Release build with zero warnings/errors; the finite run exited 0. This was a harness naming error, not a production-source failure.

Full Release engineering build: `dotnet build dv_core/LanguageCore.sln -c Release --disable-build-servers -p:NuGetAudit=false` exited 0, zero warnings/errors, 35.55 seconds. This checks compilation of Seed/Core/Session/CLI and does not execute AI scheduling. `git diff --check` reported no whitespace errors.

Reproduce the finite harness by generating 70-checks.dv with build_session.py --main false into an isolated ignored build directory, copying compiler/Runtime.cs and Mechanisms.cs there, and using the usual net8.0 project with StartupObject=Mechanisms. The checks module is only a compilation closure; the harness calls mechanism functions, not oracle or judgment entry points.

Formal integration/session validation remains the main agent's next authorized phase. No new speed measurement or full-domain check was performed.

## Subject-isolation follow-up

The reasoning memo key is now `dirty-reason-index-v2` plus `mind.subject`, external signatures and profile, owned by the frozen adoption table. A hit can no longer skip the reference-scope validation for a different subject sharing that table. Runtime queue state already keys by subject; its complete-cell dependencies are unchanged.

The finite SubjectIsolation.cs probe deliberately shares one frozen adoption table between alpha and beta. Unscoped references yield stable index values and independent first-candidate/consumed queue states. After alpha caches an alpha-scoped declaration, beta is rejected with `Cross-subject reference access` rather than returning the cached result. Final probe exited 0; its isolated Release harness compile had zero warnings/errors. No full solution rebuild or formal session was repeated.

Two initial probe runs failed because the harness incorrectly required CLR reference identity to distinguish or stabilize returned DV index values. The ABI permits snapshot interning and independently owned memo return values. Those harness-only assertions were replaced with DV value equality and actual independent-queue/access-control behavior; production code was not changed in response to either harness failure.
