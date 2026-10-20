import argparse
import hashlib
import json
from pathlib import Path
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CFG_SHA256 = "6952d7df31df89d46e08be831a6139963cf2613d1dcde9ee4e1da3079adddd8f"
AGENT_LINE = r"java-options=-javaagent:$APPDIR\ani-steam\ani-steam-agent.jar"
VERSION = ET.parse(ROOT / "src/AniSteam/AniSteam.csproj").findtext(".//Version")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--animeko-zip", type=Path, required=True)
    args = parser.parse_args()
    with zipfile.ZipFile(args.animeko_zip) as release:
        original = release.read("Ani/app/Ani.cfg")
        if hashlib.sha256(original).hexdigest() != CFG_SHA256:
            raise RuntimeError("This build only supports the official Animeko v6.3.0 Windows x64 zip")
        config = original + (AGENT_LINE + "\r\n").encode("utf-8")
        release.getinfo("Ani/app/desktop-6.3.0.jar")
    dist = ROOT / "dist"
    dist.mkdir(exist_ok=True)
    destination = dist / f"AniSteam-{VERSION}-animeko-6.3.0-win-x64-minimal.zip"
    source_zip = dist / f"AniSteam-{VERSION}-source.zip"
    readme = (ROOT / "scripts/package-readme.txt").read_text(encoding="utf-8").replace("{SOURCE_ARCHIVE}", source_zip.name)
    files = {
        "app/Ani.cfg": config,
        "app/ani-steam/ani-steam-agent.jar": (ROOT / "build/ani-steam-agent.jar").read_bytes(),
        "app/ani-steam/AniSteam.exe": (ROOT / "build/helper/AniSteam.exe").read_bytes(),
        "README.txt": readme.replace("\n", "\r\n").encode("utf-8-sig"),
    }
    with zipfile.ZipFile(source_zip, "w", zipfile.ZIP_DEFLATED) as source:
        roots = ["agent", "src", "tests", "scripts", "docs", "licenses"]
        for folder in roots:
            for path in sorted((ROOT / folder).rglob("*")):
                if path.is_file() and not set(path.parts) & {"bin", "obj", "__pycache__"}:
                    source.write(path, path.relative_to(ROOT).as_posix())
        for name in ["README.md", "LICENSE", "THIRD-PARTY-NOTICES.md", ".gitignore"]:
            source.write(ROOT / name, name)
    metadata = {"patchVersion": VERSION, "animekoVersion": "6.3.0", "architecture": "x64",
                "originalConfigSha256": CFG_SHA256,
                "files": {path: hashlib.sha256(data).hexdigest() for path, data in files.items()}}
    destination.with_suffix(".manifest.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as out:
        for name, data in files.items():
            out.writestr(name, data)
    checksum = hashlib.sha256(destination.read_bytes()).hexdigest()
    destination.with_suffix(".zip.sha256").write_text(f"{checksum}  {destination.name}\n", encoding="ascii")
    print(f"Created {destination} ({destination.stat().st_size / 1024 / 1024:.1f} MiB)")


if __name__ == "__main__":
    main()
