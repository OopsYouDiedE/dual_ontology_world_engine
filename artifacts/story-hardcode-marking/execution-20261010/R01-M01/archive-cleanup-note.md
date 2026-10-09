# Neutral fixture archive and cleanup status

`neutral-fixtures.zip` was fully written and every archived regular file was verified against its original SHA256 before cleanup. Junctions were recorded as metadata and not followed. The original live junction was removed without traversing its target.

Cleanup then stopped on read-only Git object files (`WinError 5`). A native PowerShell recursive removal was rejected by automatic approval review with `blocked by policy`; no approval was requested. The remaining review-owned fixture folders stay inside `R01-M01` and are excluded by the local `.gitignore`, so they cannot accidentally become committed Git links. The byte-preserving ZIP is the deliverable fixture evidence.

The archive manifest's fixture-directory list identifies the cleanup targets, not a claim that all targets were removed. No worker clone, production source, test or expected result was changed by this cleanup.
