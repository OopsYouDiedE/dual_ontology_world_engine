# R01 neutral-fixture harness failure, retained

The first independent-case attempt stopped at command 0092: `git mv oracle Oracle` cannot perform this directory case-only rename directly on the Windows filesystem. The command's original output remains in `raw/0092.stdout.bin` and `raw/0092.stderr.bin`. This is a fixture setup failure, not a guard result.

The first five completed cases and all original command captures remain under `fixtures/` and `raw/`. The next run uses new `fixtures-run02/` directories and a two-step neutral directory rename. Frozen safety expectations and guard bytes are unchanged.
