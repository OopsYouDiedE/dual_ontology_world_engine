# Neutral compiler and Core checks

These cases exercise the real DV compiler and generic Core functions with small,
constructed values. They do not import `30-session.dv`, chapter files,
`60-game.dv`, or `70-checks.dv`; they never start a formal session or a retired
flow. Their results are engineering evidence, not gameplay or UI acceptance.

## Run after the source batch is frozen

From the repository root, choose a directory that does not exist:

```sh
python artifacts/core-syntax-20261021/checks/run_checks.py --out artifacts/core-syntax-20261021/runs/neutral-001
```

`--timeout` controls the maximum duration of each compiler, build, or neutral
program command; its default is 180 seconds. A timeout fails the batch and
retains the available outputs. Existing output directories are rejected.

The runner copies the exact code inputs and cases into the new directory, writes
a SHA-256 manifest, and loads the copied `dv_toolchain.py`. The real seed builds
C1, C2, and C3 in that isolated copy. C2/C3 generated sources must be identical.
Both `--check` and `--emit` run for each syntax case; positive cases are then built
and executed as small .NET programs. No product `obj` directory is used.

The runner rechecks the working and frozen inputs before and after every command.
A source change ends the batch. Command arguments, exact stdout and stderr,
exit status, generated C#, source boundaries, and the C3 fingerprint remain in
the run directory. `summary.json` describes the completed stages and any failure;
only a completed batch receives a `COMPLETE` marker. Rejections must include the
specified diagnostic, so an unrelated compiler or runtime failure is not counted
as an expected rejection.

## Evidence boundaries

- `syntax-positive.dv` covers formula declarations, named outputs, ordered list
  outputs, explicit `end` blocks, bare returns, and compatibility with `fn`.
  Inline negative cases in `cases.json` cover uninitialized paths, reading an
  output before assignment, incompatible return syntax, duplicate outputs,
  missing `end`, and conflicting return annotations.
- `core-boundaries.dv` covers snapshot-only verification, recomputed execution,
  event publication, atomic failed postconditions, unknown/conflicted input
  diagnostics, identity-only binding and returned-value ownership, prediction
  and inference domains, exact fractional clock boundaries, and safe fallback.
  Separate program invocations check stale/repeated/backwards requests and
  attempts to execute a verification candidate.
- `finite-solvers.dv` compares reference enumeration and interval branch pruning
  on the same model, candidate prefix, and work charge. It includes signed exact
  fractions, equality boundaries, ties, a later cheaper option, inconsistent
  constraints, zero budget, a zero-dimensional domain, exact work just below a
  candidate boundary, and legacy/exchange certificate compatibility. It rejects
  missing, repeated, malformed, stale, and suboptimal certificate claims.

The solver case prints candidate-evaluation counts only to demonstrate that a
prunable model avoids full candidate reconstruction. These counters exclude
preprocessing, bound evaluation, and proof checking; they do not establish an
elapsed-time speedup or a production performance result. An incomplete prefix
must remain `Feasible` or `Unresolved`, even when an incumbent appears globally
promising.
