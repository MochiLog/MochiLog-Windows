"""Collect licenses for Python packages bundled by the Windows collector."""

import importlib.metadata
from pathlib import Path
import shutil
import sys

destination = Path(sys.argv[1]) / "Python"
if destination.exists():
    shutil.rmtree(destination)
destination.mkdir(parents=True, exist_ok=True)
manifest = []
for package in sorted(importlib.metadata.distributions(), key=lambda p: p.metadata["Name"].lower()):
    name = package.metadata["Name"]
    version = package.version
    if not name:
        continue
    package_directory = destination / f"{name}-{version}"
    package_directory.mkdir(parents=True, exist_ok=True)
    metadata_lines = [f"Package: {name}", f"Version: {version}"]
    for field in ("License-Expression", "License", "Home-page"):
        for value in package.metadata.get_all(field, []):
            metadata_lines.append(f"{field}: {value}")
    for value in package.metadata.get_all("Project-URL", []):
        metadata_lines.append(f"Project-URL: {value}")
    for value in package.metadata.get_all("Classifier", []):
        if value.startswith("License ::"):
            metadata_lines.append(f"License classifier: {value}")
    found = 0
    for item in package.files or ():
        leaf = Path(str(item)).name.lower()
        if not (leaf.startswith(("license", "licence", "copying", "notice"))):
            continue
        source = Path(package.locate_file(item))
        if not source.is_file() or source.stat().st_size > 500_000:
            continue
        target = package_directory / source.name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        found += 1
    if found == 0:
        metadata_lines.append("No license text was included in the installed package metadata.")
    (package_directory / "PACKAGE.txt").write_text(
        "\n".join(metadata_lines) + "\n", encoding="utf-8")
    manifest.append(f"{name} {version}: {found} license/notice file(s)")
(destination / "PACKAGES.txt").write_text("\n".join(manifest) + "\n", encoding="utf-8")
