from pathlib import Path
from PIL import Image, ImageOps

root = Path(__file__).resolve().parents[1]
with Image.open(root / "src/AniSteam/logo.png") as original:
    image = ImageOps.contain(original.convert("RGBA"), (256, 256), Image.Resampling.LANCZOS)
    square = Image.new("RGBA", (256, 256))
    square.alpha_composite(image, ((256 - image.width) // 2, (256 - image.height) // 2))
    square.save(root / "src/AniSteam/logo.ico", sizes=[(size, size) for size in [16, 20, 24, 32, 40, 48, 64, 128, 256]])
print("Created src/AniSteam/logo.ico")
