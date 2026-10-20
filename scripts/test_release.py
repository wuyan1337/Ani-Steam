import argparse
import hashlib
import os
from pathlib import Path
import subprocess
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]
CFG_SHA256 = "6952d7df31df89d46e08be831a6139963cf2613d1dcde9ee4e1da3079adddd8f"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("release", type=Path, help="Path to a disposable extracted official Ani directory")
    args = parser.parse_args()
    release = args.release.resolve()
    cfg = release / "app/Ani.cfg"
    original = cfg.read_bytes()
    if hashlib.sha256(original).hexdigest() != CFG_SHA256:
        raise RuntimeError("Expected pristine Animeko 6.3.0 x64 configuration; use a disposable release copy")
    classes = ROOT / "build/test-classes"
    classes.mkdir(parents=True, exist_ok=True)
    subprocess.run(["javac", "--release", "17", "-encoding", "UTF-8", "-d", str(classes),
                    str(ROOT / "tests/agent/ReleaseSmokeTest.java")], check=True)
    fixture = release / "app/ani-steam-test.jar"
    with zipfile.ZipFile(fixture, "w") as jar:
        for path in classes.rglob("*.class"):
            jar.write(path, path.relative_to(classes).as_posix())
    agent_dir = release / "app/ani-steam"
    agent_dir.mkdir(exist_ok=True)
    shutil.copy2(ROOT / "build/ani-steam-agent.jar", agent_dir)
    output = ROOT / "build/release-snapshots.jsonl"
    report = ROOT / "build/release-smoke.txt"
    output.write_text("", encoding="utf-8")
    report.unlink(missing_ok=True)
    text = original.decode("utf-8").replace("\r\n", "\n")
    text = text.replace("app.mainclass=me.him188.ani.app.desktop.AniDesktop", "app.mainclass=ReleaseSmokeTest")
    text = text.replace("[JavaOptions]", "app.classpath=$APPDIR\\ani-steam-test.jar\n\n[JavaOptions]")
    text += "\njava-options=-javaagent:$APPDIR\\ani-steam\\ani-steam-agent.jar\n"
    text += f"java-options=-Danisteam.test.output={output}\njava-options=-Danisteam.test.report={report}\n"
    try:
        cfg.write_text(text, encoding="utf-8", newline="\r\n")
        with (ROOT / "build/release-launcher.log").open("wb") as log:
            result = subprocess.run([str(release / "Ani.exe")], cwd=release, timeout=40,
                                    stdout=log, stderr=log, env={**os.environ, "JPACKAGE_DEBUG": "true"})
        if result.returncode != 0 or not report.exists() or not report.read_text().startswith("PASS"):
            raise RuntimeError(f"Release integration failed: exit {result.returncode}, report {report}")
        print(report.read_text().strip())
        print(f"Snapshots: {output}")
    finally:
        cfg.write_bytes(original)
        fixture.unlink(missing_ok=True)


if __name__ == "__main__":
    main()
