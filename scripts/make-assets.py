"""Convert the existing MochiLog icon to Windows icon formats without redesigning it."""

from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

root = Path(__file__).resolve().parents[1]
source = Image.open(root / "assets" / "MochiLogIcon.png").convert("RGBA")
# iOS clips the square artwork to the app icon silhouette. Apply the same
# silhouette to Windows so the taskbar, tray and installer show the same mark.
mask = Image.new("L", source.size)
ImageDraw.Draw(mask).rounded_rectangle(
    (0, 0, source.width - 1, source.height - 1),
    radius=round(source.width * 0.22), fill=255)
source.putalpha(ImageChops.multiply(source.getchannel("A"), mask))
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
