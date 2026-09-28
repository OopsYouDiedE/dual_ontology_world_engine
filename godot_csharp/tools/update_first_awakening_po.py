"""Refresh FirstAwakening PO sources from Dialogue, preserving matching translations.

Run with --check to verify source freshness and translation completeness without writing.
Changed source text intentionally loses its old translation until a translator updates it.
"""
import argparse
import ast
import json
import re
from pathlib import Path

SCENE = Path(__file__).resolve().parents[1] / "scenes" / "FirstAwakening"


def read_entries(path):
    entries = {}
    current = {}
    field = None
    for line in (path.read_text(encoding="utf-8-sig") + "\n\n").splitlines():
        if not line.strip():
            if "msgctxt" in current:
                entries[current["msgctxt"]] = (current.get("msgid", ""), current.get("msgstr", ""))
            current, field = {}, None
        elif line.startswith(("msgctxt ", "msgid ", "msgstr ")):
            field, value = line.split(" ", 1)
            current[field] = ast.literal_eval(value)
        elif line.startswith('"') and field:
            current[field] += ast.literal_eval(line)
    return entries


def dialogue_entries():
    entries = {}
    for line in (SCENE / "first_awakening.dialogue").read_text(encoding="utf-8-sig").splitlines():
        match = re.search(r"\s+\[ID:(FA_\d+(?:_\d+)*)\]\s*$", line)
        if not match:
            continue
        text = line[:match.start()].strip()
        if text.startswith("- "):
            text = re.sub(r"\s+\[if .*\]$", "", text[2:])
        else:
            text = text.split(": ", 1)[1]
        tag = match.group(1)
        if tag in entries:
            raise ValueError(f"Duplicate Dialogue ID: {tag}")
        entries[tag] = text
    return entries


def render(language, sources, existing):
    plural = "nplurals=2; plural=(n != 1);" if language == "en" else "nplurals=1; plural=0;"
    quote = lambda value: json.dumps(value, ensure_ascii=False)
    lines = ['msgid ""', 'msgstr ""']
    lines += [quote(value + "\n") for value in (
        f"Language: {language}", "Content-Type: text/plain; charset=UTF-8",
        "Content-Transfer-Encoding: 8bit", f"Plural-Forms: {plural}")]
    missing = []
    for tag, source in sources.items():
        old_source, translation = existing.get(tag, ("", ""))
        if old_source != source:
            translation = ""
        if not translation:
            missing.append(tag)
        lines += ["", f"msgctxt {quote(tag)}", f"msgid {quote(source)}", f"msgstr {quote(translation)}"]
    for tag, (source, translation) in existing.items():
        if tag not in sources:
            lines += ["", f"msgctxt {quote(tag)}", f"msgid {quote(source)}", f"msgstr {quote(translation)}"]
    return "\n".join(lines) + "\n", missing


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    sources = dialogue_entries()
    failed = False
    for language in ("en", "ja"):
        path = SCENE / "i18n" / f"{language}.po"
        existing = read_entries(path)
        content, missing = render(language, sources, existing)
        stale = any(existing.get(tag, ("", ""))[0] != source for tag, source in sources.items())
        if not args.check:
            path.write_text(content, encoding="utf-8")
        print(f"{language}: {len(sources)} entries; stale sources: {stale}; missing translations: {len(missing)}")
        if missing:
            print("  " + ", ".join(missing))
        failed |= bool(missing) or (args.check and stale)
    return int(failed)


if __name__ == "__main__":
    raise SystemExit(main())
