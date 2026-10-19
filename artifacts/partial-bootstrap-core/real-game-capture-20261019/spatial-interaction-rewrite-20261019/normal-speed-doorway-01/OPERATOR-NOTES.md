# Normal speed doorway operator notes

This was the requested one-times-speed spatial interaction check from the bedroom. I used only the allowed `frames.jsonl`, PNG screenshots, and input-consumption confirmations.

Screenshots inspected:

- `run/frame-000021362.png` at 21,362 ms: initial bedroom view; the detective is beside the bed and the open south doorway is visible at the bottom.
- `run/frame-000033477.png` at 33,477 ms: after the first downward movement, the detective is near the room's south side.
- `run/frame-000043859.png` at 43,859 ms: after a second downward movement, the detective remains near the south-east side of the bedroom.
- `run/frame-000052861.png` at 52,861 ms: after clicking the south doorway area, the detective is still in the bedroom near its lower edge.
- `run/frame-000063163.png` at 63,163 ms: after moving left, the detective is positioned in front of the south doorway.
- `run/frame-000080642.png` at 80,642 ms: the detective is standing on the corridor's wooden floor beside doorway marker 05. The HUD reads 8:02 and the dialogue says “沿通路走到相连的房间。” This confirms reaching the corridor.
- `run/frame-000082280.png` at 82,280 ms: the same corridor dialogue remains visible. I inspected it before the parent requested that input stop.

Inputs injected and confirmed consumed:

- `S` held for 900 ms; consumed at real_ms 27,369, released at 28,043.
- `S` held for 1,000 ms; consumed at 39,052, released at 40,040.
- Click at screen pixel (592, 820); consumed at 49,657.
- `A` held for 500 ms; consumed at 60,789, released at 61,222.
- `S` held for 700 ms; consumed at 75,916, released at 76,545.

Observed result: the detective reached the corridor by 80,642 ms, with the transition dialogue visible. No further inputs were sent after reaching the corridor. Later behavior is unknown.
