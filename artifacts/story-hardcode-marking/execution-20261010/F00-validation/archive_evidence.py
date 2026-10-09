"""Retain byte-identical copies of newly produced formal verify logs."""
from pathlib import Path
import hashlib
import json
import re
import shutil

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
SOURCE_ROOT = ROOT / "artifacts" / "project-audit" / "verify-logs"
records = []
for capture in sorted(OUT.glob("verify*.stdout.log")):
    content = capture.read_bytes().decode("utf-8", errors="replace")
    ids = sorted(set(re.findall(r"verify-logs[\\/]([0-9-]+)[\\/]", content)))
    for identifier in ids:
        source = SOURCE_ROOT / identifier
        if source.parent.resolve() != SOURCE_ROOT.resolve():
            raise RuntimeError("unexpected log path")
        target = OUT / "verify-raw" / identifier
        target.mkdir(parents=True, exist_ok=True)
        for raw in sorted(source.glob("*.log")):
            copied = target / raw.name
            original_bytes = raw.read_bytes()
            if copied.exists() and copied.read_bytes() != original_bytes:
                raise RuntimeError(f"refusing to overwrite archived evidence: {copied}")
            if not copied.exists():
                shutil.copy2(raw, copied)
            records.append({
                "source": raw.relative_to(ROOT).as_posix(),
                "copy": copied.relative_to(OUT).as_posix(),
                "bytes": len(original_bytes),
                "source_sha256": hashlib.sha256(original_bytes).hexdigest(),
                "copy_sha256": hashlib.sha256(copied.read_bytes()).hexdigest(),
                "identical": original_bytes == copied.read_bytes(),
            })
(OUT / "verify-log-provenance.json").write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
manifest = []
for raw in sorted(OUT.rglob("*.log")):
    manifest.append(f"{hashlib.sha256(raw.read_bytes()).hexdigest()}  {raw.relative_to(OUT).as_posix()}")
(OUT / "raw-log-sha256.txt").write_text("\n".join(manifest) + "\n", encoding="utf-8")
print(f"Archived {len(records)} channel logs; byte-identical: {all(record['identical'] for record in records)}")
