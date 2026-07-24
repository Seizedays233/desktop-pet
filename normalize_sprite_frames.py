from __future__ import annotations

import math
import statistics
from pathlib import Path

from PIL import Image


ASSET_DIR = Path(r"C:/Users/52965/Documents/Codex/2026-07-23/new-chat/work/OrangeCatPet/Assets")
FRAME_SIZE = 640
ANCHOR_X = 320
ANCHOR_BOTTOM = 599
MAX_EXTENT = 590
SEQUENCES = (
    "walk",
    "groom",
    "scratch",
    "sleep",
    "pat",
    "feed",
    "feed-can",
    "feed-chicken",
)


def visible_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Sprite contains no visible pixels")
    return bounds


idle = Image.open(ASSET_DIR / "cat.png").convert("RGBA")
idle_left, idle_top, idle_right, idle_bottom = visible_bounds(idle)
idle_area = (idle_right - idle_left) * (idle_bottom - idle_top)
target_area = idle_area * 0.90


for sequence in SEQUENCES:
    sources = [
        Image.open(ASSET_DIR / f"cat-{sequence}-{index:02}.png").convert("RGBA")
        for index in range(1, 9)
    ]
    bounds = [visible_bounds(image) for image in sources]
    widths = [right - left for left, _, right, _ in bounds]
    heights = [bottom - top for _, top, _, bottom in bounds]
    areas = [width * height for width, height in zip(widths, heights)]

    scale = math.sqrt(target_area / statistics.median(areas))
    scale = min(scale, MAX_EXTENT / max(widths), MAX_EXTENT / max(heights))

    final_bounds: list[tuple[int, int, int, int]] = []
    for index, (source, bounds_for_frame) in enumerate(zip(sources, bounds), start=1):
        cropped = source.crop(bounds_for_frame)
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
        output_path = ASSET_DIR / f"cat-smooth-{sequence}-{index:02}.png"
        canvas.save(output_path, optimize=True)
        final_bounds.append(visible_bounds(canvas))

    print(
        f"{sequence}: scale={scale:.3f}, "
        f"bottoms={sorted({bottom for _, _, _, bottom in final_bounds})}, "
        f"centers={sorted({round((left + right) / 2, 1) for left, _, right, _ in final_bounds})}"
    )
