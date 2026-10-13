# DV Checker for Godot 4.6

Enable **DV Checker** in Project → Project Settings → Plugins. Open the **DV** bottom
panel, or Project → Tools → **Open DV source…**. The panel opens `.dv` files anywhere
on disk, including the repository's `language_core` directory outside the Godot project.

Set **Python** to the Python executable (an absolute path works) and **Checker** to
`language_core/tools/check_dv.py`. These editor settings are remembered locally.
Arguments are passed directly, without shell interpretation; do not add command-line
arguments or surrounding quotes to either path field.

The panel contains a source editor with line numbers. **Save & check** saves the file
and invokes the real compiler checker in a worker thread. **Check on save** is enabled
by default. Double-click a diagnostic to open its file and position the caret at the
reported line and column. Unsaved changes are marked; switching files asks before
discarding them. Editing during a check labels its results as belonging to the saved
version. The lower text field retains the exact checker output, including startup errors.

The command is:

```text
<python> <checker> --format json <absolute-source-path>
```

Expected stdout is a JSON object containing `success` and `diagnostics`. Each diagnostic
contains `path`, `line`, `column`, `severity` and `message`; positions are one-based.
The service resolves the compilation context. This addon does not substitute its own
syntax or type rules. Completion means that the compiler accepted the source, not that
the story or character knowledge is correct. Gameplay requires a real session.

This is an editor tool, with no game runtime dependencies. Disabling the plugin removes
the panel and menu. If a check is running, shutdown waits for the checker to exit.
