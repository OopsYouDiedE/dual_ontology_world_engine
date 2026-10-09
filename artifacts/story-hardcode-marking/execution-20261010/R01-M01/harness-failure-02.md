# R01 Windows symlink setup limitation, retained

The second independent-case attempt reached the neutral filesystem symlink setup after command 0237. `os.symlink('item.txt', .../allowed/link.txt)` raised `WinError 1314` because this Windows session does not have the required symbolic-link privilege. No filesystem symlink guard result is claimed.

All earlier completed independent cases remain in `fixtures-run02/` and are recorded in `independent-guard-partial-results.json`. The continuation uses fresh `fixtures-run03/` directories for the remaining cases, creates a neutral Git `120000` symlink entry for Git-tree rejection, and separately attempts a real Windows junction. Safety expectations and frozen guard bytes are unchanged.
