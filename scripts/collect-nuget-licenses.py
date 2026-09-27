"""Collect notices for every resolved NuGet package in the Windows app."""

import json
import os
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile


root = Path(__file__).resolve().parents[1]
assets = root / "src/MochiLog.Windows/obj/project.assets.json"
destination = Path(sys.argv[1]) / "NuGet"
destination.mkdir(parents=True, exist_ok=True)
data = json.loads(assets.read_text(encoding="utf-8"))
packages_root = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget/packages"))
rows = ["Resolved NuGet packages used to build MochiLog Windows", ""]
upstream_licenses = {
    "Common.Logging": ("Apache-2.0", "Common.Logging-LICENSE.txt",
                       "https://github.com/net-commons/common-logging/blob/master/license.txt"),
    "Common.Logging.Core": ("Apache-2.0", "Common.Logging-LICENSE.txt",
                            "https://github.com/net-commons/common-logging/blob/master/license.txt"),
    "Makaretu.Dns": ("MIT", "Makaretu.Dns-LICENSE.txt",
                     "https://github.com/richardschneider/net-dns/blob/master/LICENSE"),
    "Makaretu.Dns.Multicast": ("MIT", "Makaretu.Dns.Multicast-LICENSE.txt",
                               "https://github.com/richardschneider/net-mdns/blob/master/LICENSE"),
}

for identifier, details in sorted(data["libraries"].items(), key=lambda item: item[0].lower()):
    if details.get("type") != "package":
        continue
    name, version = identifier.rsplit("/", 1)
    package_root = packages_root / name.lower() / version.lower()
    nuspec = package_root / f"{name.lower()}.nuspec"
    if not nuspec.is_file():
        raise FileNotFoundError(f"NuGet metadata missing for {identifier}: {nuspec}")
    metadata = ET.parse(nuspec).getroot().find("{*}metadata")
    if metadata is None:
        raise ValueError(f"NuGet metadata missing in {nuspec}")
    license_element = metadata.find("{*}license")
    license_url = metadata.findtext("{*}licenseUrl") or ""
    project_url = metadata.findtext("{*}projectUrl") or ""
    expression = (license_element.text or "").strip() if license_element is not None else ""
    license_type = license_element.get("type", "") if license_element is not None else ""
    upstream = upstream_licenses.get(name) if not expression and not license_url else None
    if upstream:
        expression, _, license_url = upstream
    if not expression and not license_url:
        raise ValueError(f"NuGet package has no license metadata: {identifier}")

    section = destination / f"{name}-{version}"
    section.mkdir(parents=True, exist_ok=True)
    if upstream:
        source = root / "resources/NuGetLicenses" / upstream[1]
        if not source.is_file():
            raise FileNotFoundError(source)
        shutil.copy2(source, section / "LICENSE.txt")
    lines = [f"Package: {name}", f"Version: {version}",
             f"License: {expression or license_url}", f"Project: {project_url}"]
    if license_url:
        lines.append(f"License URL: {license_url}")
    notice_count = 0
    archive = package_root / f"{name.lower()}.{version.lower()}.nupkg"
    if archive.is_file():
        with zipfile.ZipFile(archive) as package:
            for member in package.infolist():
                leaf = Path(member.filename).name
                if not leaf.lower().startswith(("license", "licence", "copying", "notice")):
                    continue
                if member.is_dir() or member.file_size > 500_000:
                    continue
                target = section / f"{notice_count + 1:02d}-{leaf}"
                with package.open(member) as source, target.open("wb") as output:
                    shutil.copyfileobj(source, output)
                notice_count += 1
    (section / "PACKAGE.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    rows.append(f"{identifier}: {expression or license_url}; {notice_count} bundled notice(s)")

(destination / "PACKAGES.txt").write_text("\n".join(rows) + "\n", encoding="utf-8")
