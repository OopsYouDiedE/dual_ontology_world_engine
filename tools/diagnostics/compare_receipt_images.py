"""Compare original receipt images without modifying either file."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageStat

parser = argparse.ArgumentParser()
parser.add_argument("left", type=Path)
parser.add_argument("right", type=Path)
arguments = parser.parse_args()
with Image.open(arguments.left) as source_left, Image.open(arguments.right) as source_right:
    left, right = source_left.convert("RGBA"), source_right.convert("RGBA")
    result = {"left": str(arguments.left.resolve()), "right": str(arguments.right.resolve()),
              "left_size": left.size, "right_size": right.size,
              "identical_pixels": left.size == right.size and left.tobytes() == right.tobytes(),
              "left_sha256": hashlib.sha256(arguments.left.read_bytes()).hexdigest(),
              "right_sha256": hashlib.sha256(arguments.right.read_bytes()).hexdigest()}
    if left.size == right.size:
        result["mean_absolute_channel_difference"] = ImageStat.Stat(ImageChops.difference(left, right)).mean
print(json.dumps(result, ensure_ascii=False, indent=2))
