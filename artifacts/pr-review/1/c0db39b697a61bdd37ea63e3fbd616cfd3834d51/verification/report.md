# PR #1 独立自动回归报告

准确提交：`c0db39b697a61bdd37ea63e3fbd616cfd3834d51`。构建/导入/运行位于隔离 checkout `tmp/pr1-review-checkout`；报告目录中的源码快照没有被直接 build。本代理未读取项目设计规范、未修改生产源码。以下是注入状态的自动回归与定向运行检查，不是盲玩。

用户已要求停止测试。之后仅收存日志、截图与复现源码，没有新启动任何测试或有声进程。

## 环境与声音缓存条件

Godot `4.6.1.stable.mono.official.14d19694e`，窗口渲染 OpenGL Compatibility，NVIDIA RTX 3070。Core与宿主dotnet构建通过。

干净检出只有五份 `voice_*.wav.import`，五份目标 WAV 源文件缺失；`StoryDialogue.gd:10-14` preload 引发 Parse Error，后续 `active` 访问报错。干净导入虽 exit 0，但 **资源重建/可运行性 FAIL**（`import.log`、`blind-start-smoke.log`）。主代理核对基线亦存在同问题，因此此项不归为 PR 新增。

后续运行明确条件为 **cached voice resources supplied**：逐一核对 main 和两个检出中 .import 的 SHA256 相同，只复制与目标导入路径匹配的五份 .sample 及对应 .md5 到 .godot/imported。散列保存在 `cached-voices-sha256.txt`。没有冒名 zip 样本，没有合成替代音频，没有改生产源码，没有声称干净检出或源资源重建通过。缓存条件默认启动 smoke 无 SCRIPT ERROR。

## 已完成结果

| 检查 | 状态 | 证据及边界 |
|---|---|---|
| Core Release build | PASS | `verify.log`，0 warning/0 error |
| Core scope regression | PASS | `verify.log`，21 checks |
| Godot host build | PASS | `verify.log`，23 warnings/0 errors |
| 干净导入/启动 | FAIL，既有缺陷 | WAV源缺失；`import.log`、`blind-start-smoke.log` |
| 缓存条件启动 | PASS | `cached-start-smoke.log` |
| story_flow_check | PASS | `cached-flow-nav-gallery.log`，243 checks/0 failures；注入状态 |
| navigation_check | PASS，有限覆盖 | 4322 routes/0 blocked cells；脚本明确禁用可通行门叶碰撞与physics，不能代替实机通关 |
| gallery | PASS生成，部分实际看图QA | 14 PNG保存于 `gallery/`，无SCRIPT ERROR |
| 侦探宿舍门实时碰撞 | PASS | 初始门叶solid/openness0、physics运行；20路点入室、20路点出室；过程中门叶自行开放，无人工禁碰撞。8px/4px剩余在合法stop radius内 |
| 厨房北门洞双向 | PASS | 11路点入/出，最终距目标0px |
| 厨房南口双向 | PASS | 14路点入/出，最终距目标8px，合法stop radius |
| 按住物理D键 | PASS | 15个physics帧前进22.5px |
| 同伴挡厨房北口后重新点击 | PASS（较长绕行） | 初始600帧不足，仍行进在148点替代路线；随后已有独立2400帧预算的 `companion.log` 收尾0px、failures0。不能把最初预算失败称为卡住 |
| S28完整退场 | PASS | 四人各自到达(580/620/660/700,936)，travelling=false；并非只断言对白已开始 |
| E05“返回检视” | **FAIL，确认新增** | 真实Inspect/ObservationReader记录；渲染按钮实际mouse input前后均mode=evidence、E05；截图 `e05-before-return.png` / `e05-after-return.png` |
| verify.py无Godot边界 | **FAIL，确认新增** | mock find_godot=None，三项build成功，main仍return0，只有core/scope/host；`verify-boundary.log`。这是工具逻辑边界测试，未运行build/引擎 |

定向动态完整运行的终端结果保存在 `console-transcript.txt`（同内容附件 `dynamic-completed-observed.txt`），来源为已运行工具返回的转存，非原始完整日志；未依据截图或上下文拼造完整日志。后来2400帧全量复核的 `dynamic.log` 只有门初始状态，运行提前结束，没有全部断言；标记 **中止/不完整**，不以exit0冒称通过。专项同伴测试独立 `companion.log` 实际已完整结束，可据其最终PASS。

## UI专项与截图QA

已实际查看：gallery的 `02_opening_revealed`、`06_evidence_notebook`、`08_deduction_sequence`、`11_restart_confirm`，以及E05前后证物页与 `s10-longest-dialogue`。这几张图中正文、证物翻页/目录、推演选项与重启确认可读，无明显遮挡、溢出或按钮叠压。其余gallery图仅确认生成，**未逐张人工看图验收**。

S10最长中文句实际渲染 content_height=96，正文区域84；截图显示三行完整可读，最后“上都对得上。”可见。当前未确认裁切bug；未覆盖全部句子、全部语言与所有窗口尺寸。

证物目录的grab_focus被Godot拒绝（只允许屏幕阅读器），实际focus=null；右键按下后E05正常切至E06。**未确认焦点吞键**，未覆盖开启屏幕阅读器和展开popup状态。见 `ui.log`、`evidence-focused-right-key.png`。

异步同伴重复请求拒绝/返程碰撞没有独立覆盖，不将静态疑点记为确认失败。键盘在同伴挡门情形的脱困没有单独覆盖；已覆盖公共区按住D与挡门重新点击。

README提及的 `res://tools/check_asset_layout.gd` 在该提交不存在，旧 Reasoning.Check 项目未执行；依据用户停止测试要求，标记 **未覆盖/入口缺失**，没有继续扩展。

## 确定发现的机制

1. `RedTokenDemo.gd:685-687` 对inspect_only证物直接调用 `_inspect_red`；已记录证物再次打开同一EvidenceNotebook。`_open_recorded_evidence:977-978` 仍配置“返回检视”，经 `_return_to_inspection:679-681` 回到前述逻辑，形成同页返回。真实按钮复现确认。
2. `tools/verify.py:105-113` 未找到Godot时不添加flow/nav/gallery，最后130行仅all(results)返回成功。因此默认完整验收命令可能没有引擎测试却exit0。受控mock确认。

复现源码归隔离检出 `godot_csharp/scenes/FirstAwakening/check/review_*`，副本同本报告目录；这些脚本是报告附件，不是生产改动。

