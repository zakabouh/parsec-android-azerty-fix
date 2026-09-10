"""Assemble the application icon from existing PNG files.

No shapes, colors, logos, or badge artwork are generated here. The official
Parsec PNG is kept at its original 256 x 256 size and the existing 72 x 72
Twemoji check-mark PNG is composited unchanged in the lower-right corner.
"""

from pathlib import Path

from PIL import Image


REPO_ROOT = Path(__file__).resolve().parent.parent
ASSETS = REPO_ROOT / "assets"
BASE_PATH = ASSETS / "parsec-official-256.png"
BADGE_PATH = ASSETS / "twemoji-check-button-72.png"
MASTER_PATH = ASSETS / "icon-master.png"
ICO_PATH = ASSETS / "ParsecAzertyFix.ico"

BASE_SIZE = (256, 256)
BADGE_SIZE = (72, 72)
BADGE_POSITION = (178, 178)
ICO_SIZES = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
             (48, 48), (64, 64), (128, 128), (256, 256)]


def load_rgba(path: Path, expected_size: tuple[int, int]) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    if image.size != expected_size:
        raise ValueError(f"{path.name} must be {expected_size}, got {image.size}")
    return image


def main() -> None:
    base = load_rgba(BASE_PATH, BASE_SIZE)
    badge = load_rgba(BADGE_PATH, BADGE_SIZE)

    icon = base.copy()
    icon.alpha_composite(badge, BADGE_POSITION)
    icon.save(MASTER_PATH, format="PNG", optimize=True)
    icon.save(ICO_PATH, format="ICO", sizes=ICO_SIZES)

    print(f"Created {MASTER_PATH.relative_to(REPO_ROOT)}")
    print(f"Created {ICO_PATH.relative_to(REPO_ROOT)}")


if __name__ == "__main__":
    main()
