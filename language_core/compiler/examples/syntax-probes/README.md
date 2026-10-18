# Neutral syntax engineering probes

These inputs do not execute a game session. Build the current C0→C1→C2→C3 chain once, then use C3 to compile each input and combine generated C# with the current Runtime.cs. Keep each output in a new evidence directory.

`comprehension.dv` expected JSON:

```json
[1,10,[12,13],[[2,4],[4,6]],[11,12],[],[],"negative","zero","one","many"]
```

The source counter distinguishes repeated source evaluation. Nested same-name bindings distinguish correct source and body scope; the outer `x` remains 10. The excluded projection raises an error if evaluated.

Expected compiler diagnostics: `unbound-binding.dv` → unbound local; `bad-projection-type.dv` → numeric literal has incompatible type; `immutable-rebinding.dv` → assignment requires var; `malformed-elif.dv` → expected colon. Check the input path and source line in each diagnostic.

`bad-source-runtime.dv` and `bad-guard-runtime.dv` deliberately retain the existing dynamic Value boundary: compilation may succeed; execution must fail with Expected List / Expected Bool respectively. They are not successful executions or game acceptance evidence.

Initial probes were subsequently executed with the parent-provided C3 compiler; raw evidence and the corrected diagnostic expectation are recorded in artifacts/partial-bootstrap-core/parallel-repairs-20261018/syntax-probes-01. The original mismatched text expectation is retained in results.json.

`branch-boundaries.dv` expects `["nested","outer","independent","fallback",2]`; it distinguishes nested elif from the outer chain and an independent if. `orphan-elif.dv`, `while-elif.dv`, and `intervening-elif.dv` are compiler errors (`unexpected token after statement`), covering elif without an adjacent if chain.
