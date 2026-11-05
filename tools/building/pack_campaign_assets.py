"""Deterministically package only the published campaign's necessary GLBs.

Run after publish_campaign_rooms.py. The normal environment/MCP launcher
verifies and expands these small versioned parts before Godot imports them.
"""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "assets/packs"
PART_BYTES = 240000


def main():
    publication = json.loads((ROOT / "assets/scenes/campaign_rooms/publication.json").read_text())
    names = {name.removeprefix("res://") for name in publication["outputs"] if name.endswith(".glb")}
    animations = ROOT / "assets/imported/kaykit/animations"
    source = json.loads((animations / "source.json").read_text())
    names.update((animations / item["file"]).relative_to(ROOT).as_posix() for item in source["files"])
    cache = ROOT / ".runtime/asset-packages"
    cache.mkdir(parents=True, exist_ok=True)
    archive = cache / "campaign-assets-authoring.zip"
    files = []
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
        for name in sorted(names):
            content = (ROOT / name).read_bytes()
            info = zipfile.ZipInfo(name, date_time=(2026, 11, 4, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            bundle.writestr(info, content)
            files.append({"path": name, "bytes": len(content), "sha256": hashlib.sha256(content).hexdigest()})
    DEST.mkdir(parents=True, exist_ok=True)
    payload = archive.read_bytes()
    parts = []
    for index, start in enumerate(range(0, len(payload), PART_BYTES)):
        name = f"campaign-models-{index:03d}.bin"
        content = payload[start:start + PART_BYTES]
        (DEST / name).write_bytes(content)
        parts.append({"name": name, "bytes": len(content), "sha256": hashlib.sha256(content).hexdigest()})
    manifest = {"schema": "dowe-campaign-assets-v1", "archive_sha256": hashlib.sha256(payload).hexdigest(),
                "archive_bytes": len(payload), "expanded_bytes": sum(item["bytes"] for item in files),
                "files": files, "parts": parts}
    previous = DEST / "campaign_assets.json"
    if previous.is_file():
        for item in json.loads(previous.read_text()).get("parts", []):
            if item["name"] not in {part["name"] for part in parts}:
                path = DEST / item["name"]
                if path.parent == DEST and path.is_file():
                    path.unlink()
    previous.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    (DEST / ".gdignore").write_text("")
    print(json.dumps({"files": len(files), "parts": len(parts), "raw_bytes": manifest["expanded_bytes"], "stored_bytes": len(payload)}))


if __name__ == "__main__":
    main()
