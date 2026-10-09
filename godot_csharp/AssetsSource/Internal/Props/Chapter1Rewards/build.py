"""Native 1x wall-side furniture; publish without scaling or quantization."""
from pathlib import Path
import hashlib
import json
import sys

SOURCE = Path(__file__).resolve().parent
GAME = SOURCE.parents[3]
sys.path.insert(0, str(GAME / "tools"))

from first_awakening_art.palette import BRASS, IRON, LINEN, WALNUT  # noqa: E402
from first_awakening_art.pixel import Canvas  # noqa: E402
from first_awakening_art.props import _finish, leg, slab  # noqa: E402


def reward_locker() -> Canvas:
    c = Canvas(80, 32)
    # Low cornice and four individually hinged doors; light comes from above-left.
    slab(c, 1, 1, 78, 5, 23, WALNUT, seed=41)
    c.hline(1, 7, 78, WALNUT[1])
    c.vline(2, 7, 20, WALNUT[5])
    c.vline(77, 7, 20, WALNUT[1])
    for x in (5, 23, 41, 59):
        c.rect(x, 9, 16, 16, WALNUT[2])
        c.frame(x, 9, 16, 16, WALNUT[0])
        c.hline(x + 1, 10, 14, WALNUT[4])
        c.vline(x + 1, 10, 14, WALNUT[4])
        c.rect(x + 3, 17, 10, 5, WALNUT[3])
        c.hline(x + 4, 21, 8, WALNUT[2])
        # Paper slips in brass holders: ink marks remain abstract at native size.
        c.rect(x + 4, 12, 8, 4, BRASS[1])
        c.hline(x + 4, 12, 8, BRASS[3])
        c.rect(x + 5, 13, 6, 2, LINEN[4])
        c.hline(x + 6, 14, 3, WALNUT[2])
        for y in (12, 21):
            c.vline(x, y, 2, BRASS[2])
            c.px(x, y, BRASS[4])
        c.rect(x + 12, 18, 2, 3, BRASS[2])
        c.px(x + 12, 18, BRASS[4])
    c.rect(1, 26, 78, 3, WALNUT[2])
    c.hline(1, 26, 78, WALNUT[4])
    for x in (4, 73):
        c.rect(x, 29, 3, 2, WALNUT[1])
        c.px(x, 29, WALNUT[3])
    return _finish(c)


def cylinder_player() -> Canvas:
    c = Canvas(32, 40)
    # Small separate stand, with a compact wooden mechanism case on its top.
    for x in (5, 25):
        leg(c, x, 28, 11, WALNUT)
    c.hline(7, 35, 18, WALNUT[2])
    slab(c, 2, 22, 28, 5, 3, WALNUT, seed=42)
    slab(c, 5, 15, 21, 5, 4, WALNUT, seed=43)
    c.hline(7, 21, 17, WALNUT[5])
    # Horizontal mandrel, drive wheel, and a hand crank on the right.
    c.rect(8, 13, 2, 6, IRON[2])
    c.rect(21, 13, 2, 6, IRON[2])
    c.hline(9, 15, 13, IRON[4])
    c.ellipse(23, 16, 3, 4, BRASS[1])
    c.ellipse(23, 15, 2, 3, BRASS[3])
    c.vline(22, 13, 4, BRASS[4])
    c.hline(24, 17, 5, BRASS[3])
    c.vline(28, 17, 4, BRASS[2])
    c.rect(27, 20, 4, 2, WALNUT[1])
    c.hline(27, 20, 3, WALNUT[5])
    # Restrained brass horn and curved reproducer arm above the bare mandrel.
    c.line(17, 13, 18, 6, BRASS[2])
    c.line(18, 6, 14, 5, BRASS[4])
    c.poly([(14, 5), (7, 2), (6, 11), (14, 8)], BRASS[2])
    c.line(7, 2, 14, 5, BRASS[4])
    c.ellipse(7, 6, 3, 5, BRASS[3])
    c.ellipse(6, 7, 2, 3, BRASS[0])
    c.vline(5, 3, 4, BRASS[5])
    c.px(17, 13, IRON[4])
    return _finish(c)


def main() -> None:
    runtime = GAME / "ShareAsset/Internal/Props/Chapter1Rewards"
    runtime.mkdir(parents=True, exist_ok=True)
    manifest = {"cell": 32, "recipe": str(Path(__file__).relative_to(GAME)).replace("\\", "/"), "assets": {}}
    for name, canvas in {"reward_locker": reward_locker(), "cylinder_player": cylinder_player()}.items():
        canvas.save(SOURCE / f"{name}.png")
        (runtime / f"{name}.png").write_bytes((SOURCE / f"{name}.png").read_bytes())
        manifest["assets"][name] = {
            "size": [canvas.w, canvas.h], "anchor": [canvas.w // 2, canvas.h],
            "source": f"AssetsSource/Internal/Props/Chapter1Rewards/{name}.png",
            "runtime": f"ShareAsset/Internal/Props/Chapter1Rewards/{name}.png",
            "sha256": hashlib.sha256((SOURCE / f"{name}.png").read_bytes()).hexdigest(),
        }
    (runtime / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Published 2 native pixel sprites to " + str(runtime.relative_to(GAME)))


if __name__ == "__main__":
    main()
