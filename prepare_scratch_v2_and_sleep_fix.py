from __future__ import annotations

import math
import statistics
from pathlib import Path

from PIL import Image


WORK_DIR = Path(r"C:/Users/52965/Documents/Codex/2026-07-23/new-chat/work/imagegen")
ASSET_DIR = Path(r"C:/Users/52965/Documents/Codex/2026-07-23/new-chat/work/OrangeCatPet/Assets")
FRAME_SIZE = 640
ANCHOR_X = 320
ANCHOR_BOTTOM = 599
MAX_EXTENT = 590


def visible_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Sprite contains no visible pixels")
    return bounds


def prepare_scratch_sequence() -> None:
    sheet = Image.open(WORK_DIR / "scratch-v2-sheet-alpha.png").convert("RGBA")
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

    bounds = [visible_bounds(frame) for frame in raw_frames]
    widths = [right - left for left, _, right, _ in bounds]
    heights = [bottom - top for _, top, _, bottom in bounds]
    areas = [width * height for width, height in zip(widths, heights)]
    scale = math.sqrt(target_area / statistics.median(areas))
    scale = min(scale, MAX_EXTENT / max(widths), MAX_EXTENT / max(heights))

    for index, (frame, frame_bounds) in enumerate(zip(raw_frames, bounds), start=1):
        cropped = frame.crop(frame_bounds)
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
        output = ASSET_DIR / f"cat-smooth-scratch-v2-{index:02}.png"
        canvas.save(output, optimize=True)

        final_left, final_top, final_right, final_bottom = visible_bounds(canvas)
        print(
            f"scratch {index}: size={final_right-final_left}x{final_bottom-final_top}, "
            f"center={(final_left+final_right)/2:.1f}, bottom={final_bottom}"
        )


def prepare_sleep_sequence() -> None:
    for index in range(1, 8):
        source = ASSET_DIR / f"cat-smooth-sleep-{index:02}.png"
        target = ASSET_DIR / f"cat-smooth-sleep-v2-{index:02}.png"
        Image.open(source).convert("RGBA").save(target, optimize=True)

    clean_loop_frame = Image.open(ASSET_DIR / "cat-smooth-sleep-01.png").convert("RGBA")
    clean_loop_frame.save(ASSET_DIR / "cat-smooth-sleep-v2-08.png", optimize=True)
    print("sleep 8: replaced with clean frame 1 to close the loop without the tail bulge")


prepare_scratch_sequence()
prepare_sleep_sequence()
