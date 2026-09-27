"""Collect licenses for Python packages bundled by the Windows collector."""

import importlib.metadata
from pathlib import Path
import shutil
import sys

destination = Path(sys.argv[1]) / "Python"
destination.mkdir(parents=True, exist_ok=True)
manifest = []
for package in sorted(importlib.metadata.distributions(), key=lambda p: p.metadata["Name"].lower()):
    name = package.metadata["Name"]
    version = package.version
    if not name:
        continue
    found = 0
    for item in package.files or ():
        leaf = Path(str(item)).name.lower()
        if not (leaf.startswith(("license", "licence", "copying", "notice"))):
            continue
        source = Path(package.locate_file(item))
        if not source.is_file() or source.stat().st_size > 500_000:
            continue
        target = destination / f"{name}-{version}" / source.name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        found += 1
    manifest.append(f"{name} {version}: {found} license/notice file(s)")
(destination / "PACKAGES.txt").write_text("\n".join(manifest) + "\n", encoding="utf-8")
