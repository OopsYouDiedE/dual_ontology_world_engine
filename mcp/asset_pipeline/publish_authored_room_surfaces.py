"""Publish exact source-image crops; do not repaint, resize or repair their seams.

The references are room illustrations, not periodic material atlases. Their
lighting and perspective remain in the crop and are documented explicitly.
Godot applies a single declared scale and phase independently of 32px coverage.
"""
from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "mcp/asset_pipeline/sources/Internal/Props/AuthoredRooms"
OUTPUT = ROOT / "godot_csharp/ShareAsset/Internal/Props/AuthoredRooms"
ROOMS = {
    "central_hall": {
        "reference": "artifacts/chapter12-room-design/12-central-hall.png",
        "scale": 0.64,
        "phase": [0, 0],
        "occlusion": "The source pillar obscures the medallion center and a small north arc. Runtime separates its source silhouette with authored_medallion.gdshader; an unobscured blue-gray sample fills the interior only. Hidden radial ornament and north gold arc are not reconstructed.",
        "crops": {
            "floor": [380, 332, 500, 164],
            "medallion_ring": [462, 498, 338, 266],
            "medallion_field": [536, 610, 24, 24],
        },
    },
    "granary": {
        "reference": "artifacts/chapter12-room-design/06-granary.png",
        "scale": 0.25,
        "phase": [32, 96],
        "crops": {"floor": [370, 438, 600, 320]},
    },
    "shura_gallery": {
        "reference": "mcp/asset_pipeline/sources/Internal/Props/Highlights/Backgrounds/shura_gallery.png",
        "scale": 7 / 24,
        "phase": [140.20833333333334, 204.625],
        "crops": {
            "floor": [370, 512, 152, 111],
            "border_south": [370, 960, 152, 20],
            "border_west": [272, 450, 20, 152],
            "border_corner": [270, 830, 50, 40],
            "border_turn": [269, 399, 60, 60],
            "center_diamond": [706, 566, 124, 116],
        },
    },
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def publish():
    SOURCES.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    manifest = {"schema": "authored-room-surfaces/v1", "publication": "exact rectangular source crop; no resampling, recoloring, alpha masking or seam synthesis", "limitation": "Illustration samples retain authored perspective, illumination and irregular spacing. Repeated samples are not claimed to be seamless native tiles.", "rooms": {}}
    for room, spec in ROOMS.items():
        reference = ROOT / spec["reference"]
        archived = SOURCES / (room + ".png")
        shutil.copyfile(reference, archived)
        image = Image.open(archived)
        entries = {}
        for name, (x, y, width, height) in spec["crops"].items():
            target = OUTPUT / (room + "_" + name + ".png")
            image.crop((x, y, x + width, y + height)).save(target)
            entries[name] = {"source_rect_px": [x, y, width, height], "output": str(target.relative_to(ROOT)).replace("\\", "/"), "sha256": digest(target), "size_px": [width, height]}
        manifest["rooms"][room] = {"reference": spec["reference"], "source": str(archived.relative_to(ROOT)).replace("\\", "/"), "source_sha256": digest(archived), "source_size_px": list(image.size), "world_pixels_per_source_pixel": spec["scale"], "world_phase_px": spec["phase"], "crops": entries}
        if "occlusion" in spec:
            manifest["rooms"][room]["occlusion"] = spec["occlusion"]
    (OUTPUT / "publication.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    publish()
