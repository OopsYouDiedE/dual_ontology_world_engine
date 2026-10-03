"""Publish chapter-one evidence art (《门的这一侧》) from native 1x recipes.

Run from the repository root:
    python godot_csharp/tools/build_awakening_evidence.py

Recipes: first_awakening_art/awakening_evidence.py. The same native pixels are
written to AssetsSource (production source) and ShareAsset (runtime). A 2x
contact sheet goes to artifacts/remake-belief-replay/ for review.
"""
from pathlib import Path
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from first_awakening_art import awakening_evidence  # noqa: E402

GAME = Path(__file__).resolve().parents[1]
FAMILY = "Internal/Props/AwakeningEvidence"


def publish() -> None:
    canvases = awakening_evidence.build()
    for root in ("AssetsSource", "ShareAsset"):
        (GAME / root / FAMILY).mkdir(parents=True, exist_ok=True)
    for name, canvas in canvases.items():
        for root in ("AssetsSource", "ShareAsset"):
            canvas.save(GAME / root / FAMILY / (name + ".png"))
    cell = 120
    columns = 6
    rows = (len(canvases) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell, rows * cell), (224, 217, 200, 255))
    draw = ImageDraw.Draw(sheet)
    for index, (name, canvas) in enumerate(canvases.items()):
        x, y = (index % columns) * cell, (index // columns) * cell
        draw.text((x + 4, y + 3), name, fill=(48, 43, 39, 255))
        image = canvas.image()
        scale = max(1, min((cell - 8) // image.width, (cell - 20) // image.height))
        image = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
        sheet.alpha_composite(image, (x + (cell - image.width) // 2, y + 16))
    output = GAME.parent / "artifacts" / "remake-belief-replay"
    output.mkdir(parents=True, exist_ok=True)
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.Resampling.NEAREST).save(output / "evidence-art-sheet-2x.png")
    print(f"Published {len(canvases)} native textures to AssetsSource and ShareAsset/{FAMILY}.")


if __name__ == "__main__":
    publish()
