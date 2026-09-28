# Academy 48px TileSet archive

This directory preserves the earlier `AcademyTerrain48.tres`, its expanded A2/A4 atlas PNGs, and the original 48px atlas inputs. The TileSet reports atlas coordinates outside the available textures during Godot load, and no current scene references it. It is retained as historical production material, not as a runtime resource and not as a supported 32px game asset. Do not repair it by changing atlas dimensions or artwork as part of the asset relocation.

`Academy_Inside_A2.png`, `Academy_Inside_A4.png`, and `academy_objects_48.png` remain available here as inputs to the current deterministic 32px story/shot prop builders. Those builders write their runtime outputs to `ShareAsset/Internal/Props/`.
