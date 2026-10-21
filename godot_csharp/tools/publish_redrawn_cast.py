"""Publish reviewed ImageGen 3x6 sheets to the existing native atlas contract.

Requires --source and --review (a new output directory). Source alpha >=200 is
the explicit opaque coverage mask, used for both gutter checks and publication.
All poses share one scale and the existing cell/standing height. Runtime .tres,
collision, animation order and timing are unchanged. No historical masters needed.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

from PIL import Image
from build_anime_cast import bounds

GAME = Path(__file__).resolve().parents[1]
RUNTIME = GAME / "ShareAsset/Internal/Characters/PixelCast"
ALPHA = 200


def prepare(path):
    key = path.name.removesuffix("-compact.png")
    resource = (RUNTIME / f"{key}.tres").read_text(encoding="utf-8")
    cell = re.search(r"region = Rect2\(0, 0, (\d+), (\d+)\)", resource)
    if not cell:
        raise ValueError(f"{key}: no existing 3x6 cell contract")
    cw, ch = map(int, cell.groups())
    old = Image.open(RUNTIME / f"{key}.png").convert("RGBA")
    if old.size != (cw * 6, ch * 3):
        raise ValueError(f"{key}: runtime atlas layout is not 3x6")
    recipe_path = path.with_name(key + ".publication.json")
    recipe = json.loads(recipe_path.read_text(encoding="utf-8")) if recipe_path.exists() else {}
    if recipe and recipe["cell"] != [cw, ch]: raise ValueError(f"{key}: publication cell changed")
    target_height = recipe.get("standing_height") or max(old.crop((0, y * ch, cw, (y + 1) * ch)).getbbox()[3] - old.crop((0, y * ch, cw, (y + 1) * ch)).getbbox()[1] for y in range(3))
    boxes = bounds(path, ALPHA)
    source = Image.open(path).convert("RGBA")
    scale = min(target_height / max(row[0][3] - row[0][1] for row in boxes),
                (cw - 2) / max(b[2] - b[0] for row in boxes for b in row),
                (ch - 2) / max(b[3] - b[1] for row in boxes for b in row))
    sheet = Image.new("RGBA", old.size)
    for y, row in enumerate(boxes):
        for x, box in enumerate(row):
            pose = source.crop(box)
            pose.putalpha(pose.getchannel("A").point(lambda a: 255 if a >= ALPHA else 0))
            pose = pose.resize((max(1, round(pose.width * scale)), max(1, round(pose.height * scale))), Image.Resampling.NEAREST)
            opaque = [pixel[:3] for pixel in pose.getdata() if pixel[3]]
            swatches = Image.new("RGB", (len(opaque), 1))
            swatches.putdata(opaque)
            palette = swatches.quantize(colors=24, method=Image.Quantize.MEDIANCUT)
            reduced = pose.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE).convert("RGBA")
            reduced.putalpha(pose.getchannel("A"))
            sheet.alpha_composite(reduced, (x * cw + (cw - pose.width) // 2, y * ch + ch - 1 - pose.height))
    return key, sheet, {"source": str(path), "source_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                         "alpha_coverage": ALPHA, "cell": [cw, ch], "target_standing_height": target_height, "source_bounds": boxes, "scale": scale,
                         "previous_runtime_sha256": hashlib.sha256((RUNTIME / f"{key}.png").read_bytes()).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--review", type=Path, required=True)
    parser.add_argument("--only", nargs="+")
    args = parser.parse_args()
    if args.review.exists(): raise FileExistsError(args.review)
    inputs = sorted(args.source.glob("*-compact.png"))
    if args.only: inputs = [path for path in inputs if path.name.removesuffix("-compact.png") in args.only]
    if not inputs: raise FileNotFoundError("No redrawn masters supplied")
    # Validate the complete selected set before replacing any runtime atlas.
    prepared = [prepare(path) for path in inputs]
    args.review.mkdir(parents=True)
    manifest = {}
    for key, sheet, record in prepared:
        sheet.save(RUNTIME / f"{key}.png")
        preview = Image.new("RGBA", sheet.size, "#b7b8b1")
        preview.alpha_composite(sheet)
        preview.resize((sheet.width * 5, sheet.height * 5), Image.Resampling.NEAREST).save(args.review / f"{key}.png")
        record["runtime_sha256"] = hashlib.sha256((RUNTIME / f"{key}.png").read_bytes()).hexdigest()
        recipe_path = args.source / f"{key}.publication.json"
        if not recipe_path.exists():
            recipe_path.write_text(json.dumps({"cell": record["cell"], "standing_height": record["target_standing_height"]}, indent=2) + "\n", encoding="utf-8")
        manifest[key] = record
        print(key, sheet.size, "published; all existing animation resources preserved")
    (args.review / "asset-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
