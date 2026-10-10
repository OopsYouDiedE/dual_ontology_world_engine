import hashlib
import json
import pathlib
import shutil

HERE = pathlib.Path(__file__).resolve().parent
ROOT = pathlib.Path(__file__).resolve().parents[5]
destination_root = HERE / "source-snapshot-route-context"
manifest_path = HERE / "source-and-evidence-hashes-route-context.json"
if destination_root.exists() or manifest_path.exists():
    raise SystemExit("Refusing to overwrite sealed route-context evidence")
source_manifest = json.loads((HERE / "source-and-evidence-hashes.json").read_text(encoding="utf-8"))
manifest = {"source": [], "evidence": []}
for item in source_manifest["source"]:
    name = item["path"]
    source = ROOT / name
    destination = destination_root / name
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    payload = source.read_bytes()
    manifest["source"].append({"path": name, "sha256": hashlib.sha256(payload).hexdigest(), "bytes": len(payload), "snapshot": str(destination.relative_to(HERE))})
for path in sorted(HERE.glob("*")):
    if path.is_file() and path.suffix in {".log", ".jsonl", ".py", ".gd"}:
        payload = path.read_bytes()
        manifest["evidence"].append({"path": path.name, "sha256": hashlib.sha256(payload).hexdigest(), "bytes": len(payload)})
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
print("Final route-context source and original evidence bytes sealed separately")
