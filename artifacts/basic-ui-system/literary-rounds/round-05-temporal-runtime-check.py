from pathlib import Path

base = Path(__file__).resolve().parent
engine = (base / "round-05-02-engine-actions.txt").read_text(encoding="utf-8-sig")
ui = (base / "round-05-03-ui-actions.txt").read_text(encoding="utf-8-sig")

morning_quer = "补记记录：第四日上午，雀儿补记了第三日夜里的取用 1 份。"
morning_mora = "补记记录：第四日上午，茉拉补记了第三日夜里的取用 1 份。"
noon_mora = "补记记录：第四日午间，茉拉补记了第三日夜里的取用 1 份。"
partial = "那夜我一共取了 2 份，已经补记 1 份，还有 1 份没补。"
post_news = "沃德说他在第四日傍晚，在中央厅贴了规矩。"

checks = {
    "02 morning two distinct named ledger rows": morning_quer in engine and morning_mora in engine,
    "02 no noon row before partial testimony": engine.index(noon_mora) > engine.index(partial),
    "02 partial testimony agrees with morning ledger": partial in engine,
    "02 noon Mora row distinct": noon_mora in engine,
    "02 retell has event time": "茉拉说她在第四日上午，到台账上补记了昨夜取的一份" in engine and post_news in engine,
    "02 no first-day false Ward post retell": post_news not in engine[:engine.index("第四日 · 傍晚")],
    "03 morning ledger rows and partial testimony drawn": morning_quer in ui and morning_mora in ui and partial in ui,
    "03 noon ledger row and dated post retell drawn": noon_mora in ui and post_news in ui,
}

for label, ok in checks.items():
    print(f"{'PASS' if ok else 'FAIL'} {label}")
if not all(checks.values()):
    raise SystemExit(1)
