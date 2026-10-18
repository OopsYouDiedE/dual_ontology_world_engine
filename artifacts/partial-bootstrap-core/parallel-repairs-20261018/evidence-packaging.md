# Byte-preserving packaging preparation

Only `.gitattributes`, this note and `package_frozen_run.py` were changed for this packaging task. No old raw evidence, archive bytes, index entries or production source were modified. No formal run was started.

Updated attribute files:

- `artifacts/partial-bootstrap-core/parallel-repairs-20261018/.gitattributes`
- `artifacts/rejection-probes-01/.gitattributes`
- `artifacts/public-projection-probes-01/.gitattributes`
- `artifacts/public-projection-probes-02/.gitattributes`
- `artifacts/public-projection-probes-03/.gitattributes`
- `artifacts/wait-window-probes-01/.gitattributes`

JSON preserves bytes with `-text` while retaining text review. Original logs, generated debug projections and generated Program/Runtime/bundle copies use `-text -diff`. The authored `runtime-probe/Program.cs` explicitly remains text and reviewable; other authored C#/DV/Python/Markdown probes and reports remain reviewable. No broad `*.cs` or `*.dv` binary rule was introduced.

After the ONE240 child and its run_debug postprocessing have exited, run this packaging command with the actual new run directory name:

```powershell
python artifacts/partial-bootstrap-core/parallel-repairs-20261018/package_frozen_run.py --run run-240s
```

The script requires result.json and generated source inputs. It validates all old evidence-manifest entries before accepting new evidence, exclusively creates a new `<run>-frozen-program.zip`, includes generated program files plus built assemblies (excluding obj), verifies input hashes remained stable while archiving, saves the exact old manifest under a new pre-run name, and then refreshes evidence-manifest.json. Existing archives/backup names cause refusal. It does not delete raw generated folders or change old zip bytes. Missing final trace remains missing evidence, regardless of package creation. Failure after archive creation leaves that archive for inspection; do not blindly rerun against the same name.

Suggested explicit index refresh after the run/package is final (not executed by this agent):

```powershell
git add --renormalize -- artifacts/partial-bootstrap-core/parallel-repairs-20261018 artifacts/rejection-probes-01 artifacts/public-projection-probes-01 artifacts/public-projection-probes-02 artifacts/public-projection-probes-03 artifacts/wait-window-probes-01
git add -- artifacts/partial-bootstrap-core/parallel-repairs-20261018/.gitattributes artifacts/rejection-probes-01/.gitattributes artifacts/public-projection-probes-01/.gitattributes artifacts/public-projection-probes-02/.gitattributes artifacts/public-projection-probes-03/.gitattributes artifacts/wait-window-probes-01/.gitattributes artifacts/partial-bootstrap-core/parallel-repairs-20261018/package_frozen_run.py artifacts/partial-bootstrap-core/parallel-repairs-20261018/evidence-packaging.md
```

`--renormalize` refreshes tracked/staged files, not all new untracked outputs. Add the new finalized run evidence, new archive, pre-run manifest backup and refreshed manifest explicitly as needed; do not force-add raw program build directories when the archive already seals them. Check index attributes and compare staged raw-file SHA-256 against the refreshed manifest before committing.

实际本次封存：run-240s-frozen-program.zip还含trace.json.partial原始778118000字节；原partial仍在本地，manifest以封存ZIP引用这份不完整原始证据，不把partial当成完整trace。脚本在本次执行前扩充horizon-probes-01范围与partial归档，旧ZIP及旧manifest副本保持原字节。
