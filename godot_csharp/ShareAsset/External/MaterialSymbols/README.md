# Material Symbols Outlined

The mind reasoning UI uses Google's Material Symbols Outlined variable font for control icons. The font, codepoint map, and Apache 2.0 `LICENSE.txt` were downloaded from the [official Material Design Icons repository](https://github.com/google/material-design-icons/tree/master/variablefont) on 2026-09-27.

The current UI themes in `godot_csharp/scenes/UI/Styles/` reference this font for control icons. Their source is `mcp/asset_pipeline/build_ui_theme.py`; the runtime font remains at `res://ShareAsset/External/MaterialSymbols/MaterialSymbolsOutlined[FILL,GRAD,opsz,wght].ttf`. This asset is not used for prose text.
