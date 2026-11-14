"""Convert the existing MochiLog icon to Windows icon formats without redesigning it."""

from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = Image.open(root / "assets" / "MochiLogIcon.png").convert("RGBA")
destination = root / "src" / "MochiLog.Windows" / "Assets"
source.save(destination / "AppIcon.ico", sizes=[
    (16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)
])
for filename, size in {
    "Square150x150Logo.scale-200.png": 300,
    "Square44x44Logo.scale-200.png": 88,
    "Square44x44Logo.targetsize-24_altform-unplated.png": 24,
    "Square44x44Logo.targetsize-48_altform-lightunplated.png": 48,
    "LockScreenLogo.scale-200.png": 48,
    "StoreLogo.png": 50,
}.items():
    source.resize((size, size), Image.Resampling.LANCZOS).save(destination / filename)
