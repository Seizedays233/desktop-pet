from pathlib import Path

from PIL import Image


ASSET_DIR = Path(r"C:/Users/52965/Documents/Codex/2026-07-23/new-chat/work/OrangeCatPet/Assets")


def alpha_bounds(path: Path) -> tuple[int, int, int, int]:
    image = Image.open(path).convert("RGBA")
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError(f"No visible pixels in {path.name}")
    return bounds


for pattern in ("cat.png", "cat-walk-??.png", "cat-groom-??.png", "cat-scratch-??.png",
                "cat-sleep-??.png", "cat-pat-??.png", "cat-feed-??.png",
                "cat-feed-can-??.png", "cat-feed-chicken-??.png"):
    files = sorted(ASSET_DIR.glob(pattern))
    print(pattern)
    for path in files:
        left, top, right, bottom = alpha_bounds(path)
        print(
            f"  {path.name}: bbox=({left},{top})-({right},{bottom}) "
            f"size={right-left}x{bottom-top} bottom={bottom}"
        )
