# Blipsmith 0.1.0 音频工具测试与技术验收综合报告

> 本文档为 `artifacts/blipsmith-test/` 唯一权威报告，系统归纳整理了 Blipsmith 0.1.0 在 Windows 环境下的原生界面功能测试、文字转语音音效工作流探索、以及导出音频的底层 PCM 波形技术指标。

---

## 一、 测试背景与工作流概述

- **测试日期**：2026-10-01
- **测试对象**：Blipsmith 0.1.0 Windows 安装包（测试程序安装于 `tmp/blipsmith-test/installed/`）
- **工具用途**：面向复古与独立游戏，为对白文字逐字显示提供可辨识、富有节奏感的短音效（Blip 音效）。
- **核心操作流**：
  1. **预设起点**：Folk、Monsters、Machines、Magic、Objects 五类预设（Folk 包含 7 种角色）；
  2. **核心调制滑块（9 个）**：Size（体型）、Brightness（明亮度）、Wobble（起伏）、Grit（颗粒/粗糙）、Talking speed（语速）、Blip length（单音长度）、Thickness（厚度）、Warble（颤音）、Space（混响空间）；
  3. **离散与高级参数**：Tone（Chip/Soft/Buzzy/Pure/Breathy）、Mouth（Click/Pluck/Blip/Hum）、Pitch slide（Flat/Rises/Falls/Bends）、Letter-to-letter pattern（Random/By letter/Vowels up/One note）、Sentence intonation（语调开关）；
  4. **探索与导出**：通过 `ROLL A VOICE` 随机采样、`NUDGE IT` 微调、保存至内部库并打包导出包含 6 个短音变体、整句示例及 `params.json` 的 ZIP 资产。

---

## 二、 界面操作与功能验证 (UI Test)

| 功能项 | 实际操作 | 结果与判定 |
| :--- | :--- | :--- |
| **程序启动** | 执行静默安装并启动主窗口 | 通过，像素风主窗口正常显示，默认 Villager 与示例英语对白可见 |
| **文字预览** | 输入默认英语句子、以及“你好，旅人！先休息一下。ABC 123 😊”并点击 `TYPE IT` | 通过，文字逐字流式显示，UI 动画与响应正常 |
| **随机探索** | 点击 `ROLL A VOICE` 与 `NUDGE IT` | 通过，滑块位置、参数及 speaker 显示产生预期随机与微调变动 |
| **内部声音库保存** | 命名为“中文旅人”并保存；清空名称时保存 | 通过，生成 `LOAD/DEL` 控件；空名称正常回退为缺省占位符名称 |
| **单音色导出** | 导出默认 Villager 与中文混合句子 | 通过，成功弹出保存对话框并输出完整 Voice ZIP 资产 |
| **批量导出** | 点击 `EXPORT ALL VOICES` | 渲染 2 条声音并弹出保存对话框，随后按指令中止退出 |

### 界面缺陷记录
- **低严重度（导出反馈提示文字过期）**：导出时用户在对话框中自定义文件名（如 `default-villager.zip` 或 `chinese-villager.zip`），实际文件已按新名称正确落盘，但界面 Toast 成功提示仍固定显示建议文件名 `Saved villager_blips.zip`。已记录待上游工具修复。

---

## 三、 导出音频底层 PCM 技术核验 (Technical Audio Verification)

共对导出的 14 个 WAV 音频样本进行了底层二进制、RIFF 头部与 PCM 波形数据核验：

| 样本文件 | 编码格式 | 采样率/位深/声道 | 时长 (s) | 峰值 dBFS | RMS dBFS | 满幅硬削波 | 首尾采样跃变 |
| :--- | :--- | :--- | ---:| ---:| ---:| ---:| :--- |
| `blip_01.wav` ~ `blip_06.wav` | PCM | 44,100 Hz / 16-bit / 单声道 | 0.244014 | -10.50 ~ -10.56 | -20.33 ~ -20.39 | 0 | < 0.001 满幅 |
| `chinese-villager/line_demo.wav` | PCM | 44,100 Hz / 16-bit / 单声道 | 1.166440 | -4.50 | -14.16 | 0 | 0.000061 满幅 |
| `default-villager/line_demo.wav` | PCM | 44,100 Hz / 16-bit / 单声道 | 3.971361 | -4.44 | -14.03 | 0 | 0.000061 满幅 |

### 技术一致性指标
1. **头部与数据结构**：所有 WAV 文件的 RIFF 块结构、帧对齐、字节率全部合法，100% 可解码；
2. **唯一性与随机性**：各 take 变体的 PCM SHA-256 哈希互不相同，全部非静音，符合逐字随机变化设计意图；
3. **失真与爆音控制**：无整数满幅样本，未发生满幅硬削波；首尾相对零点跃变均严格控制在 0.001 以内，无直流跳变导致的杂音/爆音；
4. **元数据**：`params.json` 完整记录合成参数与界面状态。

---

## 四、 归档交付物索引

- **权威总报告**：[`report.md`](report.md)
- **底层数据证据**：
  - [`audio-validation.json`](audio-validation.json)：14 个 WAV 样本的峰值、RMS、直流均值、前后 5ms 采样详细度量；
  - [`installation-evidence.json`](installation-evidence.json)：安装路径与进程验证记录；
  - `all-voices.zip`：测试导出的完整音色样本包（含 6 组 blip take、示例整句及参数配置）。
