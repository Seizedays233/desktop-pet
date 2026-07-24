from __future__ import annotations

import math
import statistics
from pathlib import Path

from PIL import Image


WORK_DIR = Path(r"C:/Users/52965/Documents/Codex/2026-07-23/new-chat/work/imagegen")
ASSET_DIR = Path(r"C:/Users/52965/Documents/Codex/2026-07-23/new-chat/work/OrangeCatPet/Assets")
SHEET_PATH = WORK_DIR / "pat-seated-v2-sheet-alpha.png"
FRAME_SIZE = 640
ANCHOR_X = 320
ANCHOR_BOTTOM = 599
MAX_EXTENT = 590


def visible_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Sprite contains no visible pixels")
    return bounds


sheet = Image.open(SHEET_PATH).convert("RGBA")
sheet_width, sheet_height = sheet.size
raw_frames: list[Image.Image] = []

for index in range(8):
    row, column = divmod(index, 4)
    left = round(column * sheet_width / 4)
    right = round((column + 1) * sheet_width / 4)
    top = round(row * sheet_height / 2)
    bottom = round((row + 1) * sheet_height / 2)
    raw_frames.append(sheet.crop((left, top, right, bottom)))

old_idle = Image.open(ASSET_DIR / "cat.png").convert("RGBA")
idle_left, idle_top, idle_right, idle_bottom = visible_bounds(old_idle)
target_area = (idle_right - idle_left) * (idle_bottom - idle_top) * 0.90

raw_bounds = [visible_bounds(frame) for frame in raw_frames]
widths = [right - left for left, _, right, _ in raw_bounds]
heights = [bottom - top for _, top, _, bottom in raw_bounds]
areas = [width * height for width, height in zip(widths, heights)]
scale = math.sqrt(target_area / statistics.median(areas))
scale = min(scale, MAX_EXTENT / max(widths), MAX_EXTENT / max(heights))

normalized_frames: list[Image.Image] = []
for index, (frame, bounds) in enumerate(zip(raw_frames, raw_bounds), start=1):
    cropped = frame.crop(bounds)
    resized = cropped.resize(
        (
            max(1, round(cropped.width * scale)),
            max(1, round(cropped.height * scale)),
        ),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    left = round(ANCHOR_X - resized.width / 2)
    top = ANCHOR_BOTTOM - resized.height
    canvas.alpha_composite(resized, (left, top))
    output_path = ASSET_DIR / f"cat-smooth-pat-seated-{index:02}.png"
    canvas.save(output_path, optimize=True)
    normalized_frames.append(canvas)

normalized_frames[0].save(ASSET_DIR / "cat-smooth-idle-v2.png", optimize=True)
normalized_frames[1].save(ASSET_DIR / "cat-smooth-blink-v2.png", optimize=True)

print(f"scale={scale:.3f}")
for index, frame in enumerate(normalized_frames, start=1):
    left, top, right, bottom = visible_bounds(frame)
    print(
        f"frame {index}: size={right-left}x{bottom-top}, "
        f"center={(left+right)/2:.1f}, bottom={bottom}"
    )
