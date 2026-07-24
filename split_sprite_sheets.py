from pathlib import Path

from PIL import Image


WORK_DIR = Path(r"C:/Users/52965/Documents/Codex/2026-07-23/new-chat/work/imagegen")
ASSET_DIR = Path(r"C:/Users/52965/Documents/Codex/2026-07-23/new-chat/work/OrangeCatPet/Assets")
ANIMATIONS = ("feed", "feed-can", "feed-chicken", "walk", "groom", "scratch", "sleep", "pat")
FRAME_SIZE = (640, 640)


def split_sheet(animation: str) -> None:
    sheet = Image.open(WORK_DIR / f"{animation}-sheet-alpha.png").convert("RGBA")
    width, height = sheet.size

    for index in range(8):
        row, column = divmod(index, 4)
        left = round(column * width / 4)
        right = round((column + 1) * width / 4)
        top = round(row * height / 2)
        bottom = round((row + 1) * height / 2)
        frame = sheet.crop((left, top, right, bottom))
        frame.thumbnail(FRAME_SIZE, Image.Resampling.LANCZOS)

        canvas = Image.new("RGBA", FRAME_SIZE, (0, 0, 0, 0))
        offset = ((FRAME_SIZE[0] - frame.width) // 2, (FRAME_SIZE[1] - frame.height) // 2)
        canvas.alpha_composite(frame, offset)
        canvas.save(ASSET_DIR / f"cat-{animation}-{index + 1:02}.png", optimize=True)


for animation_name in ANIMATIONS:
    split_sheet(animation_name)

