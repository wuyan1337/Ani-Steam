import argparse
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ASM_URL = "https://repo.maven.apache.org/maven2/org/ow2/asm/asm/9.8/asm-9.8.jar"
ASM_SHA256 = "876eab6a83daecad5ca67eb9fcabb063c97b5aeb8cf1fca7a989ecde17522051"


def run(*args):
    subprocess.run([str(a) for a in args], cwd=ROOT, check=True)


def build_agent():
    deps = ROOT / "build/deps"
    deps.mkdir(parents=True, exist_ok=True)
    asm = deps / "asm-9.8.jar"
    if not asm.exists():
        urllib.request.urlretrieve(ASM_URL, asm)
    if hashlib.sha256(asm.read_bytes()).hexdigest() != ASM_SHA256:
        raise RuntimeError("ASM download checksum mismatch")
    classes = ROOT / "build/agent-classes"
    classes.mkdir(parents=True, exist_ok=True)
    javac = shutil.which("javac")
    if not javac:
        raise RuntimeError("JDK 17+ javac is required")
    run(javac, "--release", "17", "-encoding", "UTF-8", "-cp", asm, "-d", classes,
        *sorted((ROOT / "agent/src").rglob("*.java")))
    jar = ROOT / "build/ani-steam-agent.jar"
    with zipfile.ZipFile(jar, "w", zipfile.ZIP_DEFLATED) as out:
        out.writestr("META-INF/MANIFEST.MF", "Manifest-Version: 1.0\r\nPremain-Class: dev.anisteam.Agent\r\n\r\n")
        for path in sorted(classes.rglob("*.class")):
            out.write(path, path.relative_to(classes).as_posix())
        with zipfile.ZipFile(asm) as source:
            for name in source.namelist():
                if name.startswith("org/objectweb/asm/") and name.endswith(".class"):
                    out.writestr(name, source.read(name))
    print(f"Built {jar}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET", "dotnet"))
    parser.add_argument("--agent-only", action="store_true")
    args = parser.parse_args()
    build_agent()
    if not args.agent_only:
        run(args.dotnet, "publish", "src/AniSteam/AniSteam.csproj", "-c", "Release", "-r", "win-x64",
            "--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:DebugType=None", "-p:DebugSymbols=false", "-o", ROOT / "build/helper")


if __name__ == "__main__":
    main()
