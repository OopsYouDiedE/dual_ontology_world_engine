"""Publish selected imagegen masters without repainting their material detail."""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "AssetsSource/Internal/Props/UIDesk/ReferenceRedraw"
DEST = ROOT / "ShareAsset/Internal/Props/UIDesk/ReferenceRedraw"

def main():
    DEST.mkdir(parents=True, exist_ok=True)
    Image.open(SOURCE / "desk.png").save(DEST / "desk.png")
    cover = Image.open(SOURCE / "cover.png").convert("RGBA")
    # Mechanical transparent-gutter trimming; preserve every material pixel.
    box = cover.getchannel("A").getbbox()
    cover = cover.crop(box)
    cover.save(DEST / "cover.png")
    # The inner paper is a material sample, not the full cover stretched white.
    w, h = cover.size
    cover.crop((round(w*.20), round(h*.16), round(w*.85), round(h*.81))).save(DEST / "paper.png")
    print("Published desk, trimmed cover and inner-paper material", cover.size)

if __name__ == "__main__":
    main()
