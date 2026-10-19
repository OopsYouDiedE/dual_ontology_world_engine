# Historical directory cleanup request

2026-10-19 user explicitly selected deletion of all old artifacts, tmp and scratch, retaining only artifacts/partial-bootstrap-core/core-ui-connection-20261019. The proposed PowerShell operation resolved the workspace and retained directory, enumerated old sibling paths, checked every target remains inside the workspace and cannot contain the retained task, then used Remove-Item -LiteralPath -Recurse.

The exec tool rejected the command before process creation with `rejected: blocked by policy`; no more specific reason was returned. No deletion, move or bypass was attempted. Historical directories remain. The VS Code discovery exclusions still apply independently of this blocked cleanup.
