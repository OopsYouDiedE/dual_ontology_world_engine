# auto-03 independent automatic regression

No project design, README or prior play reports were read. No implementation or C# changes were made and no build was repeated. The check script is regression_auto03.gd, copied from regression_v5.gd with only the output path changed.

CLI persistent driver: PASS. start stayed alive, snapshot printed opening and initial state, send 帮助 consumed exactly one input and printed help. The independent session transcript remains in artifacts/remake-chapter2/auto03-cli-driver.

Godot window regression: exit 0, ended=true, 35 fixture commands, 225 output entries. Nine screenshot saves returned status 0. All nine PNGs were opened with view_image.

First blocking failure: compare.exit=1. CLI and GUI each have 264 visible lines, but 7 differ. Rule aliases are shifted (CLI 苏泠/霍铎认甲, 阮清越认乙; GUI 苏泠/霍铎认乙, 阮清越认丙). Replay header differs 苏泠认甲 vs 苏泠认乙. Preserve transcript-comparison.txt plus both visible-output files as exact evidence. Automated play to an ending is not blind-play validation.

Screenshot findings: 01-intro contains the field and HUD with no visible opening dialogue. 02-field is also the field. Notebook, five-cell board editor, replay manuscript/stage, confrontation and ending panels render legibly. The replay results show two checked matching sites and five unchecked sites. 06b reports an expired selection because this check directly sends the replay command and does not set last_run_vars; this is not a verified host defect. 08-ending initially shows older log paragraphs and the final outcome is below the scroll region, so this image alone does not verify the ending text.

Exact SHA256 copies: Paper, HintOverlay and FieldNavigation all match their Awakening sources. Chapter2Dialogue directly references the shared StoryDialogue.gd rather than copying it. Hash evidence is in copied-ui-hashes.txt.

Per first-block instruction, dependent replay same-origin/context-reset and navigation/WASD checks were stopped and are NOT verified this round. No news-specific visible UI check was run. A new independent agent must verify the repaired build.
