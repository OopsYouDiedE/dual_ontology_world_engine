# DV editor checker

This addon is an editor-only client of `mcp/editor_lsp/check_dv.py`.
Keep parsing, elaboration, type rules and proof checking in the shared DV compiler.
The Godot client owns file editing, process execution and diagnostic navigation only.
Use a worker thread for the checker process; access editor controls only on the main thread.
The JSON contract is `{success: bool, diagnostics: [{path, line, column, severity, message}]}`.
Paths are absolute; line and column are one-based. Preserve raw tool output for engineering errors.
The addon does not run story sessions or perform gameplay acceptance.

Create the editor panel when the user opens the tool menu command. Loading the
plugin during asset import must not construct an unused CodeEdit or load its font
resources. The menu command still opens the same compiler-backed checker.

For isolated engineering diagnosis, copy this addon to a temporary Godot 4.6 project,
enable its plugin.cfg in that project's editor_plugins section and start the editor.
Do not run flow/flow2 or any aggregate regression command.
