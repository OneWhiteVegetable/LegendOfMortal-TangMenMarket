"""Build a Windows .NET Framework installer from the ZIP produced by tools/pack.ps1."""
import argparse
import hashlib
import os
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

SOURCE = Path(__file__).resolve().parent
REPO = SOURCE.parent
DLL = "BepInEx/plugins/XingShangMod.dll"
MANIFEST = "BepInEx/plugins/XingShangMod/Runtime/build_manifest.json"

def digest(path):
    with path.open("rb") as stream:
        sha = hashlib.sha256()
        for part in iter(lambda: stream.read(1024*1024), b""):
            sha.update(part)
        return sha.hexdigest()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, help="Use a specific verified release ZIP.")
    parser.add_argument("--out", type=Path, default=REPO/"dist")
    args = parser.parse_args()
    current = ET.parse(REPO/"XingShangMod.csproj").findtext(".//PropertyGroup/Version")
    package = (args.package or REPO/"dist"/("XingShangMod-v"+current+".zip")).resolve(strict=True)
    match = re.fullmatch(r"XingShangMod-v(\d+\.\d+\.\d+)\.zip",package.name)
    if not match:
        raise ValueError("Expected XingShangMod-vX.Y.Z.zip")
    version = match.group(1)
    sha = digest(package)
    if sha != Path(str(package)+".sha256").read_text(encoding="utf-8-sig").split()[0].lower():
        raise ValueError("ZIP does not match its pack.ps1 SHA256 receipt.")
    with zipfile.ZipFile(package) as archive:
        for entry, build_file in [(DLL,REPO/"bin/Release/XingShangMod.dll"),(MANIFEST,REPO/"Runtime/build_manifest.json")]:
            archive.getinfo(entry)
            if not args.package and hashlib.sha256(archive.read(entry)).hexdigest()!=digest(build_file):
                raise ValueError("ZIP differs from the current build for "+entry+"; run tools/pack.ps1 first.")
    out = args.out.resolve()
    out.mkdir(parents=True,exist_ok=True)
    target = out/("XingShangMod-Setup-v"+version+".exe")
    compiler = Path(os.environ["WINDIR"])/"Microsoft.NET/Framework64/v4.0.30319/csc.exe"
    if not compiler.is_file():
        raise FileNotFoundError("Windows .NET Framework 4.x compiler is required.")
    with tempfile.TemporaryDirectory(prefix="tangmen-setup-build-",dir=out) as stage_name:
        stage = Path(stage_name).resolve(strict=True)
        if stage.parent!=out or not stage.name.startswith("tangmen-setup-build-"):
            raise ValueError("Unexpected compiler working directory")
        (stage/"package.sha256").write_text(sha,encoding="ascii")
        info = 'using System.Reflection;\n[assembly: AssemblyTitle("唐门行市安装器")]\n[assembly: AssemblyProduct("唐门行市")]\n'
        info += '[assembly: AssemblyVersion("'+version+'.0")]\n[assembly: AssemblyFileVersion("'+version+'.0")]\n'
        (stage/"version.cs").write_text(info,encoding="utf-8-sig")
        binary = stage/target.name
        argv = [compiler,"/nologo","/target:winexe","/platform:anycpu","/optimize+","/utf8output","/out:"+str(binary),
                "/win32manifest:"+str(SOURCE/"app.manifest"),"/win32icon:"+str(SOURCE/"installer.ico"),
                "/resource:"+str(package)+",package.zip","/resource:"+str(stage/"package.sha256")+",package.sha256",
                "/reference:System.Windows.Forms.dll","/reference:System.Drawing.dll","/reference:System.IO.Compression.dll","/reference:System.Web.Extensions.dll",
                SOURCE/"GameHost.cs",SOURCE/"Installation.cs",SOURCE/"Program.cs",stage/"version.cs"]
        subprocess.run(argv,check=True)
        receipt=stage/"installer.sha256"
        receipt.write_text(digest(binary)+"  "+target.name+"\n",encoding="ascii")
        os.replace(binary,target)
        os.replace(receipt,Path(str(target)+".sha256"))
    print(target, digest(target))

if __name__=="__main__":
    main()
