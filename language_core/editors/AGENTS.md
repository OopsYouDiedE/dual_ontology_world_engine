# DV editor integration

This directory inherits `language_core/AGENTS.md`. Editor clients transport source buffers and display compiler diagnostics. Language semantics remain in `compiler/compiler.dv`; clients must not maintain a second parser or numeric checker.

`tools/check_dv.py` checks a `dv-check.json` compilation group without executing content. `tools/dv_toolchain.py` caches the C3 compiler after the C2/C3 fixed point. `tools/dv_lsp.py` exposes diagnostics over standard input/output for Visual Studio. The Godot addon calls the JSON checker directly.

Build the Visual Studio client with its project file in Release, then run `visualstudio/package.py --out <vsix>`. Generated assemblies belong in bin/obj and release packages in artifacts/releases. Record protocol verification separately from actual editor rendering. Compilation success does not establish gameplay or character correctness.
