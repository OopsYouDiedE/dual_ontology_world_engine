# DV for Visual Studio 2022

The extension sends open `.dv` buffers to the DV compiler through the common language server. Errors appear in Visual Studio's editor and Error List, with source positions. `define` aliases, numeric ranges, names and calls are checked by the same compiler used to build DV programs. Checking does not execute the program.

Build and package from the repository root:

```powershell
dotnet build language_core/editors/visualstudio/DuelView.VisualStudio.csproj -c Release
python language_core/editors/visualstudio/package.py --out artifacts/releases/dv/DuelView.VisualStudio.vsix
```

Install the resulting VSIX in Visual Studio 2022, then reopen Visual Studio. Python 3 and the .NET SDK must be available. Set `DV_PYTHON` to the Python executable if it is not on PATH. A source file inside this repository finds `language_core/tools/check_dv.py` automatically. For files elsewhere, set `DV_CHECKER` to that script's absolute path before starting Visual Studio.

`language_core/dv-check.json` groups files that compile together. Each file retains its own `define` scope. The first check builds and caches the self-hosted compiler; subsequent edits reuse it until compiler inputs change. Unsaved edits are checked, and obsolete results are discarded when the buffer changes again.

This is a compiler diagnostics client. It does not yet provide completion, navigation or proof-state views. The compiler's current type checking is described in `language_core/SYNTAX.md`; the extension does not add claims about properties the compiler cannot prove.

The client follows Microsoft's [Visual Studio language server extension contract](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension). The shared transport is `language_core/tools/dv_lsp.py`. The Godot client uses the same checker directly.

Local evidence: `artifacts/partial-bootstrap-core/editor-checker-01/`. The client was built and packaged, and the language server was exercised with real unsaved edits. Installation and diagnostic rendering inside Visual Studio have not yet been verified.
