"""Derive shallow 2D relief from existing interior albedo, preserving diffuse pixels.

python mcp/asset_pipeline/build_surface_materials.py
The normal maps approximate grain relief, not physical geometry or ray reflections.
CanvasTexture sidecars are loaded by CampusMapView; no generated scenes are edited.
"""
from pathlib import Path
import hashlib
import json
import re

import numpy as np
from PIL import Image, ImageFilter

GAME = (Path(__file__).resolve().parents[2] / "godot_csharp")
ROOMS = GAME / "ShareAsset/Internal/Props/Chapter2/rooms"


def build():
    records = []
    paths = sorted(ROOMS.glob("*.png"))
    desktop = GAME / "ShareAsset/Internal/Props/UIDesk/Layers/wood.png"
    if desktop.exists(): paths.append(desktop)
    for path in paths:
        if path.stem.endswith("-normal"):
            continue
        image = Image.open(path).convert("RGB")
        value = np.asarray(image.filter(ImageFilter.GaussianBlur(0.65)), dtype=np.float32) / 255
        height = value @ np.array([0.299, 0.587, 0.114])
        dy, dx = np.gradient(height)
        normal = np.stack((-dx * 1.35, dy * 1.35, np.ones_like(dx)), axis=-1)
        normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
        normal_path = path.with_name(path.stem + "-normal.png")
        Image.fromarray(np.round((normal * 0.5 + 0.5) * 255).astype(np.uint8)).save(normal_path)
        diffuse = "res://" + path.relative_to(GAME).as_posix()
        normal_uri = "res://" + normal_path.relative_to(GAME).as_posix()
        material = path.with_suffix(".relief.tres")
        material.write_text(f'''[gd_resource type="CanvasTexture" load_steps=3 format=3]

[ext_resource type="Texture2D" path="{diffuse}" id="diffuse"]
[ext_resource type="Texture2D" path="{normal_uri}" id="normal"]

[resource]
diffuse_texture = ExtResource("diffuse")
normal_texture = ExtResource("normal")
specular_color = Color(0.16, 0.17, 0.18, 1)
specular_shininess = 0.18
texture_filter = 1
''', encoding="utf-8")
        records.append({"diffuse": diffuse, "source_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                        "normal": normal_uri, "material": material.name})
    (ROOMS / "relief-manifest.json").write_text(json.dumps({"generator": "tools/build_surface_materials.py",
        "method": "Gaussian prefilter, shallow luminance gradient, normalized tangent vector; diffuse unchanged",
        "surfaces": records}, indent=2) + "\n", encoding="utf-8")
    print("Wrote interior relief sidecars; diffuse textures unchanged.")


if __name__ == "__main__":
    build()
