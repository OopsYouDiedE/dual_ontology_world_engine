# 音乐源码与导出

本目录保留可编辑曲谱和音频生成源码：`cues.py`、九首 `Blessed Sleepers - *.mscz`、`score_io.py`、合成/后处理模块、SoundFont、谱面样式与视图设置。`music1.mscz` 是单独保留的原谱，不属于九首发行导出清单。

## 生成入口

从仓库根目录运行：

```sh
python music/build_soundtrack.py --scores
python music/build_soundtrack.py --export --scores
python music/build_soundtrack.py --from-mscz
```

第一条核对曲谱与 `cues.py`，缺少曲谱时会生成。第二条通过 MuseScore 4 导出乐谱和试听文件；第三条在安装 MuseScore 4 时以已有 `.mscz` 为输入生成游戏音频。常规运行会检查已有曲谱与 `cues.py` 的差异；只有明确传入 `--rewrite-scores` 才从代码改写已有曲谱，`--from-mscz` 跳过该比对。

PDF、MusicXML、MIDI、MP3 默认写入仓库根目录的 `artifacts/releases/soundtrack/`，可通过 `--export-dir <目录>` 修改。生成器默认不会把这些发行副本重新写回 `music/`。该默认目录已被 Git 忽略。

需要 Python、NumPy，以及写入 OGG 时使用的 `soundfile`。安装 MuseScore 4 后，可通过 `MUSESCORE` 环境变量指定程序路径；未发现 MuseScore 时脚本会提示使用内置合成器，并跳过相应发行导出。该回退使用 `cues.py`，即使传入 `--from-mscz` 也不会改成读取原谱渲染；若要求以现有曲谱为唯一输入，须先配置 MuseScore。内置合成器与 MuseScore 的音色和渲染结果可能不同。

## 游戏使用的音频

游戏音乐与音效写入 `godot_csharp/ShareAsset/Internal/Props/Audio/Music/` 和 `Audio/Sfx/` 下的 `.ogg`；人物短音写入 `godot_csharp/ShareAsset/Internal/Characters/voices/`。首次准备工作副本时，应先生成需要的音频，再让 Godot 导入。独立发行 MP3 不替代这些运行时路径。

## 历史发行副本

2026-10-20 的仓库清理将九首曲目的 36 份 PDF、MusicXML、MIDI、MP3 从当前源码树移除，原谱和生成链保留。原始导出可从清理前提交 [`6166be88`](https://github.com/OopsYouDiedE/dual_ontology_world_engine/tree/6166be88b96864271b33a1624c0568cb728581c7/music) 取回；Git 历史未重写。重新生成的文件是否与旧导出逐字一致，取决于 MuseScore、SoundFont 和环境版本。
