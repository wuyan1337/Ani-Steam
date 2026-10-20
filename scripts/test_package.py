import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("patch", type=Path)
    parser.add_argument("--animeko-zip", type=Path, required=True)
    args = parser.parse_args()
    with zipfile.ZipFile(args.animeko_zip) as official:
        original = official.read("Ani/app/Ani.cfg")
    with zipfile.ZipFile(args.patch) as patch:
        expected_files = {"README.txt", "app/Ani.cfg", "app/ani-steam/AniSteam.exe", "app/ani-steam/ani-steam-agent.jar"}
        assert set(patch.namelist()) == expected_files, "Install archive must contain only the three runtime files and README"
        readme = patch.read("README.txt").decode("utf-8-sig")
        assert "怎么用" in readme and "v6.3.0 / Windows x64" in readme, "README must describe use and the supported release"
        config = patch.read("app/Ani.cfg")
        assert config.startswith(original), "Official config bytes must be preserved"
        assert config.count(b"-javaagent:") == 1, "Agent must be installed exactly once"
        assert b"\r\r\n" not in config, "Invalid Windows line endings"
        assert len(patch.read("app/ani-steam/AniSteam.exe")) > 40_000_000, "Expected self-contained helper"
        metadata = json.loads(args.patch.with_suffix(".manifest.json").read_text(encoding="utf-8"))
        assert set(metadata["files"]) == expected_files, "External manifest must cover every install file"
        for name, digest in metadata["files"].items():
            assert hashlib.sha256(patch.read(name)).hexdigest() == digest, name
        assert hashlib.sha256(args.patch.read_bytes()).hexdigest() == args.patch.with_suffix(".zip.sha256").read_text().split()[0]
        with zipfile.ZipFile(args.patch.parent / f"AniSteam-{metadata['patchVersion']}-source.zip") as source:
            assert {"LICENSE", "THIRD-PARTY-NOTICES.md", "scripts/package-readme.txt"}.issubset(source.namelist())
            assert any(name.startswith("licenses/") for name in source.namelist()), "Companion must retain third-party licenses"
        with tempfile.TemporaryDirectory(prefix="uninstall test 中文 ", dir=ROOT / "build") as temp:
            root = Path(temp).resolve()
            assert root.is_relative_to((ROOT / "build").resolve())
            patch.extractall(root)
            uninstall = root / "Uninstall-AniSteam.ps1"
            uninstall.write_text((ROOT / "scripts/Uninstall-AniSteam.ps1").read_text(encoding="utf-8"), encoding="utf-8-sig")
            cfg = root / "app/Ani.cfg"
            custom = b"java-options=-Duser.custom.option=preserved\r\n"
            cfg.write_bytes(config + custom)
            unrelated = root / "app/keep-me.txt"
            unrelated.write_text("unrelated file")
            command = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(root / "Uninstall-AniSteam.ps1")]
            subprocess.run(command, check=True, capture_output=True)
            restored = cfg.read_bytes()
            expected = original + custom
            if restored != expected:
                difference = next((i for i, (a, b) in enumerate(zip(restored, expected)) if a != b), min(len(restored), len(expected)))
                raise AssertionError(f"Uninstall changed config at {difference}: actual={restored[difference:difference+100]!r} expected={expected[difference:difference+100]!r}; lengths={len(restored)}/{len(expected)}")
            assert not (root / "app/ani-steam").exists(), "Patch directory should be removed"
            assert unrelated.read_text() == "unrelated file", "Unrelated file must survive"
            subprocess.run(command, check=True, capture_output=True)
            assert cfg.read_bytes() == original + custom, "Uninstall must be idempotent"
    print("PASS: exactly four install files, external hashes, source/licenses companion, original config, custom options and uninstall")


if __name__ == "__main__":
    main()
