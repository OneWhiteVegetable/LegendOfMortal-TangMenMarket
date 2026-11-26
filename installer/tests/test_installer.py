"""Integration audit against temporary game folders. The real game is only a read-only source for identity checks."""
import argparse
import ctypes
from ctypes import wintypes
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import uuid
import zipfile

DLL = "BepInEx/plugins/XingShangMod.dll"
RT = "BepInEx/plugins/XingShangMod/Runtime/"
MAN = RT + "build_manifest.json"
WRONG = {"BepInEx/config/com.lom.mod.uncapsixstats.cfg": b"Keep1",
         "BepInEx/plugins/LOMAssistant/LOMAssistant.dll": b"Keep2", "UserSave/002.dat": b"Copy of a user-owned file"}
LOADER_SHA = "e7ab96aa80f77119335fca0e99e978460d422f4a76befb4deb9c2d36380614d0"

def h(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def put(root, path, value):
    f=root/path
    f.parent.mkdir(parents=True,exist_ok=True)
    f.write_bytes(value)
    return f

def locked_for_delete(path):
    kernel=ctypes.WinDLL("kernel32",use_last_error=True)
    create=kernel.CreateFileW
    create.argtypes=[wintypes.LPCWSTR,wintypes.DWORD,wintypes.DWORD,wintypes.LPVOID,wintypes.DWORD,wintypes.DWORD,wintypes.HANDLE]
    create.restype=wintypes.HANDLE
    handle=create(str(path),0x80000000,1,None,3,0,None)
    if handle==ctypes.c_void_p(-1).value:raise ctypes.WinError(ctypes.get_last_error())
    return lambda:kernel.CloseHandle(wintypes.HANDLE(handle))

def audit(exe, package, previous, game, official_zip, output, online):
    results=[]
    def run(*args, ok=True):
        process=subprocess.run([str(exe),*map(str,args)],capture_output=True,timeout=240)
        if (process.returncode==0)!=ok:
            raise AssertionError((args,process.returncode,process.stdout[-600:],process.stderr[-600:]))
        return process.stdout,process.stderr
    def note(name, **more):
        results.append({"case":name,"status":"passed",**more})
        print("PASS "+name,flush=True)
    def known_game(root):
        for f in ["Mortal.exe","UnityPlayer.dll","Mortal_Data/Managed/Mortal.Core.dll"]:
            p=root/f;p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(game/f,p)
        for f,b in WRONG.items():put(root,f,b)
    def check_others(root):
        assert all((root/f).read_bytes()==b for f,b in WRONG.items())
    def archive_matches(root, spec):
        with zipfile.ZipFile(spec) as z:
            files=[n for n in z.namelist() if not n.endswith("/")]
            for n in files:
                assert hashlib.sha256(z.read(n)).hexdigest()==h(root/n),n
            return len(files)
    assert h(official_zip)==LOADER_SHA
    output.parent.mkdir(parents=True,exist_ok=True)
    assert exe.name.startswith("XingShangMod-Setup-v")
    with tempfile.TemporaryDirectory(prefix="tangmen-installer-qa-") as d:
        dest=Path(d).resolve()
        if dest.parent!=Path(tempfile.gettempdir()).resolve() or not dest.name.startswith("tangmen-installer-qa-"):
            raise AssertionError("Unexpected audit path")
        # Keep a realistic long Chinese Steam-library path: the original mirrored
        # transaction subtrees overflowed the Framework path limit before installing.
        root=dest/"玩家使用的较长中文 Steam Library"/"LegendOfMortal"
        known_game(root)
        run("--check",root)
        located,_=run("--detect")
        note("Steam discovery read-only",detected=bool(located.strip()))
        bad=dest/"wrong";bad.mkdir()
        run("--install",bad,ok=False)
        assert not (bad/".XingShangModInstaller").exists()
        original=(root/"Mortal.exe").read_bytes()
        (root/"Mortal.exe").write_bytes(b"MZ"+b"\x00"*199)
        run("--install",root,ok=False);(root/"Mortal.exe").write_bytes(original)
        note("wrong game and wrong architecture rejected")
        run("--install",root,ok=False)
        fake=put(dest,"wrong-loader.zip",b"wrong archive")
        run("--install",root,"--loader-zip",fake,ok=False)
        put(root,"winhttp.dll",b"existing unrelated proxy")
        run("--install",root,"--loader-zip",official_zip,ok=False)
        assert (root/"winhttp.dll").read_bytes()==b"existing unrelated proxy"
        (root/"winhttp.dll").unlink()
        assert not (root/DLL).exists()
        check_others(root)
        note("missing, corrupt and conflicting loaders retained or rejected")
        if online:
            run("--install",root,"--download-loader")
        else:
            run("--install",root,"--loader-zip",official_zip)
        with zipfile.ZipFile(official_zip) as z:
            for x in z.infolist():
                if x.is_dir():continue
                n="BepInEx/BepInEx-changelog.txt" if x.filename=="changelog.txt" else x.filename
                assert z.read(x)==(root/n).read_bytes(),n
        count=archive_matches(root,package)
        core=h(root/"BepInEx/core/BepInEx.Core.dll");check_others(root)
        note("first installation including verified official loader",mode="upstream download" if online else "offline original ZIP",payloadFiles=count)
        history=root/".XingShangModInstaller"
        old_receipts=len(list(history.glob("*/committed.json")))
        run("--install",root)
        assert len(list(history.glob("*/committed.json")))==old_receipts
        note("identical reinstall is idempotent")
        custom=put(root,RT+"player-notes.txt",b"never owned")
        run("--uninstall",root)
        assert not(root/DLL).exists() and not (root/MAN).exists() and custom.read_bytes()==b"never owned"
        check_others(root);assert h(root/"BepInEx/core/BepInEx.Core.dll")==core
        run("--restore",root)
        assert archive_matches(root,package)==count
        note("uninstall and restore preserve loader, unrelated mods, settings, saved data and extra runtime file")
        with zipfile.ZipFile(previous) as z:
            # Both inputs are locally verified publisher release archives.
            for info in z.infolist():
                if info.is_dir():continue
                assert (info.filename==DLL or info.filename.startswith(RT)) and ".." not in Path(info.filename).parts
                put(root,info.filename,z.read(info))
        previous_dll=h(root/DLL)
        oldmanifest=json.loads((root/MAN).read_text(encoding="utf-8-sig"))
        obsolete=put(root,RT+"audit-obsolete.txt",b"old content")
        edited=put(root,RT+"audit-edited.txt",b"customized after install")
        for n in [obsolete.name,edited.name]:
            oldmanifest["files"].append({"path":n,"bytes":11,"sha256":h(obsolete)})
        (root/MAN).write_text(json.dumps(oldmanifest),encoding="utf-8")
        before_manifest=h(root/MAN);old_bundle=h(root/(RT+"bundles/xingshang_ui.bundle"))
        release_lock=locked_for_delete(root/DLL)
        try:run("--install",root,ok=False)
        finally:release_lock()
        assert h(root/DLL)==previous_dll and h(root/MAN)==before_manifest
        assert h(root/(RT+"bundles/xingshang_ui.bundle"))==old_bundle
        assert obsolete.is_file() and edited.is_file() and list(history.glob("*/rolled-back.json"))
        note("mid-upgrade locked DLL rolls earlier replaced and retired files back")
        # Represent an interrupted transaction after its first atomic rename, before the second.
        txn=history/("interrupted-"+uuid.uuid4().hex)
        oldRel="old/"+DLL; staged="stage/"+DLL
        template=put(txn,staged,b"new bytes");backup=txn/oldRel;backup.parent.mkdir(parents=True,exist_ok=True)
        (root/DLL).replace(backup)
        (txn/"pending.json").write_text(json.dumps({"version":"2.0.0","operation":"Install","files":[{"path":DLL,"before":previous_dll,"after":h(template)}]}),encoding="utf-8")
        run("--install",root)
        assert (txn/"rolled-back.json").exists() and not(txn/"pending.json").exists()
        assert archive_matches(root,package)==count and not obsolete.exists() and edited.is_file() and custom.is_file()
        check_others(root)
        note("interrupted transaction recovers; upgrade cleans retired files but preserves user edits")
        run("--restore",root)
        assert h(root/DLL)==previous_dll and h(root/MAN)==before_manifest and obsolete.exists()
        check_others(root)
        note("upgraded version restores previous DLL and Runtime")
        run("--install",root)
        file=root/(RT+"items.json");file.write_bytes(b"modified by user")
        run("--restore",root,ok=False);assert file.read_bytes()==b"modified by user"
        note("restore stops before overwriting changed user content")
    output.write_text(json.dumps({"installer":str(exe),"installerSha256":h(exe),"package":str(package),"packageSha256":h(package),
                                  "officialLoaderSha256":LOADER_SHA,"caseCount":len(results),"cases":results},indent=2,ensure_ascii=False),encoding="utf-8")
    print("Verified",len(results),"installer scenarios")

if __name__=="__main__":
    p=argparse.ArgumentParser()
    for arg in ["exe","package","previous","game","official_zip","output"]:p.add_argument(arg,type=Path)
    p.add_argument("--online",action="store_true")
    a=p.parse_args()
    audit(a.exe.resolve(),a.package.resolve(),a.previous.resolve(),a.game.resolve(),a.official_zip.resolve(),a.output.resolve(),a.online)
