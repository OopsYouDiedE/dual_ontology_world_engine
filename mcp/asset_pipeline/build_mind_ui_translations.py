"""Build gettext resources for the FirstAwakening mind UI from one TSV source."""
from __future__ import annotations

import csv
import json
from pathlib import Path


SOURCE = (Path(__file__).resolve().parents[2] / "godot_csharp") / "scenes/Shared/Locale/i18n/mind_ui.tsv"


def quote(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


with SOURCE.open("r", encoding="utf-8-sig", newline="") as handle:
    rows = list(csv.DictReader(handle, delimiter="\t"))

keys = [row["key"] for row in rows]
if len(keys) != len(set(keys)):
    raise ValueError("Duplicate mind UI translation keys")

for locale in ("zh_CN", "en", "ja"):
    lines = [
        'msgid ""',
        'msgstr ""',
        f'"Language: {locale}\\n"',
        '"Content-Type: text/plain; charset=UTF-8\\n"',
        "",
    ]
    for row in rows:
        value = row[locale]
        if not value:
            raise ValueError(f"Missing {locale} translation: {row['key']}")
        lines.extend((f"msgid {quote(row['key'])}", f"msgstr {quote(value)}", ""))
    (SOURCE.parent / f"mind_ui.{locale}.po").write_text("\n".join(lines), encoding="utf-8")
