# Godot DV checker engineering record

2026-10-14. This is editor-plugin verification, not a gameplay acceptance result.

The actual Godot 4.6.1 .NET editor loaded the addon in an isolated project. The real OpenGL compatibility renderer used AMD Radeon 780M Graphics. The isolated capture hook opened the panel, invoked the shared compiler service through the plugin worker thread, and captured its actual viewport. Production plugin code contains no capture hook.

`valid-checker.json` is the real compiler response for the existing Chinese `define` alias example. The panel displayed `Compiler check completed.`. `sample-checker.json` is the real response for an Acceptance literal of 1.2: `literal outside Acceptance`, line 2, column 30. Activating the diagnostic positioned the actual CodeEdit caret at 2:30; the document remained unmodified (`dirty:false`). These calls use check_dv.py, not a diagnostic mock.

The first rendered error view exposed an excessively long path pushing the status row and hiding the error message. The addon now puts the diagnostic message first, uses a short filename in the status, and retains the full path in tooltips and diagnostic metadata. Before-layout-fix screenshots preserve that observation. Final screenshots are `godot-dv-valid.png` and `godot-dv-checker.png`; both were opened and visually inspected.

`render-final.stdout.log` and `render-final.stderr.log` preserve the final real editor process output. `render-force.*` preserve the earlier rendered layout run. `render.*` record the initial hidden-window capture attempt; hidden windows did not emit the awaited frame_post_draw, so the isolated capture hook was changed to RenderingServer.force_draw. No compiler or production story behavior was changed for capture.

Earlier headless engineering work loaded and closed the normal editor successfully, and exercised the real checker and caret navigation. A temporary SceneTree driver terminated with editor RID cleanup warnings; the normal editor entry did not. Those earlier outputs were returned through the command tool, not separately saved as raw files. The real-editor logs in this directory provide new independently captured evidence; they are not reconstructed earlier logs. A temporary capture hook indentation error was also corrected before rendering.
