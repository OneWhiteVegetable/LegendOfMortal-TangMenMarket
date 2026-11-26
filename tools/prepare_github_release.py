"""Stage the public repository tree, four Release attachments and the GitHub release body."""
import hashlib
import mmap
import os
import re
import shutil
import stat
import subprocess
import tempfile
import uuid
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from urllib.parse import quote, unquote, urlsplit

from build_runtime_data import read_json, sha256, verify_runtime


REPO = Path(__file__).resolve().parents[1]
DIST = REPO / "dist"
ROOT_FILES = [
    ".gitignore", "XingShangMod.csproj", "README.md", "安装说明.txt", "UnityUI/.gitignore",
    "tests/fixtures/demo-market.json",
]
SOURCE_TREES = {
    "src": {".cs"},
    "tests": {".cs", ".csproj"},
    "tools": {".py", ".ps1"},
    "installer": {".cs", ".ico", ".manifest", ".md", ".py"},
    "docs": {".md", ".png", ".jpg", ".jpeg", ".webp", ".svg"},
    "UnityUI/Assets": {".cs", ".meta", ".png", ".json", ".svg", ".prefab", ".txt", ".ttf", ".otf", ".mat", ".shader"},
    "UnityUI/Packages": {".json"},
    "UnityUI/ProjectSettings": {".asset", ".txt"},
}
SKIP_DIRS = {"bin", "obj", "dist", "library", "logs", "temp", "usersettings", "__pycache__"}
MOD_DLL = "BepInEx/plugins/XingShangMod.dll"
RUNTIME_PREFIX = "BepInEx/plugins/XingShangMod/Runtime/"
MARKDOWN_LINK = re.compile(r"(!?\[[^\]\n]*\])\((<[^>\n]+>|[^()\n]+)\)")


def is_link(path):
    return path.is_symlink() or bool(getattr(path.lstat(), "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT)


def source_files():
    for name in ROOT_FILES:
        yield REPO / name
    for name, suffixes in SOURCE_TREES.items():
        root = REPO / name
        if not root.is_dir() or is_link(root):
            raise ValueError(f"Missing source directory or unexpected link: {root}")
        for here, folders, files in os.walk(root):
            parent = Path(here)
            folders[:] = sorted(d for d in folders if not d.startswith(".") and d.casefold() not in SKIP_DIRS
                                and not is_link(parent / d))
            for filename in sorted(files):
                path = parent / filename
                if not filename.startswith(".") and path.suffix.lower() in suffixes:
                    yield path


def copy_into_repo(source, destination):
    rel = source.relative_to(REPO)
    if source.resolve(strict=True) != source or is_link(source) or not source.is_file():
        raise ValueError(f"Expected a regular repository file: {rel}")
    target = destination / rel
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)


def origin_slug():
    origin = subprocess.check_output(["git", "remote", "get-url", "origin"], cwd=REPO, text=True).strip()
    match = re.fullmatch(r"(?:https://github\.com/|git@github\.com:|ssh://git@github\.com/)"
                         r"([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+?)(?:\.git)?/?", origin)
    if not match:
        raise ValueError("Set origin to the GitHub repository that will own this release.")
    return match.group(1)


def render_markdown(repository, document, published_paths, slug, tag, rewrite=False):
    local_images = set()

    def resolve_link(match):
        label, target = match.groups()
        target = target.strip().removeprefix("<").removesuffix(">")
        url = urlsplit(target)
        if url.scheme in {"https", "http"} or target.startswith("#"):
            return match.group(0)
        decoded = unquote(url.path)
        if url.scheme or url.netloc or not decoded or decoded.startswith("/") or "\\" in decoded:
            raise ValueError(f"Non-portable Markdown link in {document.name}: {target}")
        linked = Path(os.path.abspath(document.parent / decoded))
        rel = linked.relative_to(repository).as_posix()
        # Windows is usually case-insensitive; GitHub paths must match the staged spelling.
        if rel not in published_paths:
            raise ValueError(f"Missing Markdown destination from {document.name}: {target}")
        image = label.startswith("!")
        if image:
            local_images.add(rel)
        if not rewrite:
            return match.group(0)
        prefix = f"https://raw.githubusercontent.com/{slug}/{tag}/" if image else f"https://github.com/{slug}/blob/{tag}/"
        suffix = ("?" + url.query if url.query else "") + ("#" + url.fragment if url.fragment else "")
        return f"{label}({prefix}{quote(rel, safe='/')}{suffix})"

    content = MARKDOWN_LINK.sub(resolve_link, document.read_text(encoding="utf-8-sig"))
    return content, local_images


def verify_archive(repository, archive_path, manifest):
    expected = {RUNTIME_PREFIX + row["path"]: (row["bytes"], row["sha256"]) for row in manifest["files"]}
    for name, path in [(MOD_DLL, REPO / "bin/Release/XingShangMod.dll"),
                       (RUNTIME_PREFIX + "build_manifest.json", repository / "Runtime/build_manifest.json")]:
        expected[name] = (path.stat().st_size, sha256(path))
    with zipfile.ZipFile(archive_path) as archive:
        files = [entry for entry in archive.infolist() if not entry.is_dir()]
        if len(files) != len(expected) or {entry.filename for entry in files} != expected.keys():
            raise ValueError("Player ZIP contains unexpected, missing or duplicate members.")
        for entry in files:
            size, digest = expected[entry.filename]
            with archive.open(entry) as stream:
                h = hashlib.sha256()
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    h.update(block)
            if size != entry.file_size or digest != h.hexdigest():
                raise ValueError(f"ZIP differs from the staged Runtime or current DLL: {entry.filename}")
    return len(expected)


def verify_embedded_package(installer, package, digest):
    # The Framework compiler stores /resource:package.zip as a length-prefixed contiguous stream.
    with installer.open("rb") as f, mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ) as exe, package.open("rb") as z:
        offset = exe.find(z.read(512))
        size = package.stat().st_size
        if offset < 4 or int.from_bytes(exe[offset-4:offset], "little") != size or offset + size > len(exe):
            raise ValueError("Installer is missing the embedded player ZIP.")
        with memoryview(exe)[offset:offset+size] as embedded:
            if hashlib.sha256(embedded).hexdigest() != digest:
                raise ValueError("Installer embeds a different player ZIP.")


def tree_hash(folder):
    fingerprint = hashlib.sha256()
    for path in sorted(folder.rglob("*")):
        if is_link(path):
            return None
        rel = path.relative_to(folder).as_posix().encode("utf-8")
        value = sha256(path).encode("ascii") if path.is_file() else b"directory"
        fingerprint.update(rel + b"\0" + value + b"\n")
    return fingerprint.hexdigest()


def publish(stage, target):
    if target.parent.resolve(strict=True) != DIST or target.resolve() != target:
        raise ValueError(f"Unexpected output path: {target}")
    receipt = DIST / ("." + target.name + "-tree.sha256")
    if receipt.is_symlink() or (receipt.exists() and is_link(receipt)):
        raise ValueError(f"Unexpected link at the export receipt: {receipt}")
    new_hash = tree_hash(stage)
    if not new_hash:
        raise ValueError("Unexpected file-system link in the staged export.")
    previous = None
    if target.exists():
        if not target.is_dir() or is_link(target):
            raise ValueError(f"Expected a generated output directory: {target}")
        old_hash = tree_hash(target)
        if old_hash == new_hash:
            receipt.write_text(new_hash + "\n", encoding="ascii")
            return
        # Replace known generated contents; keep a previous export with any local edits.
        if receipt.is_file() and old_hash == receipt.read_text(encoding="ascii").strip():
            previous = stage.parent / "previous"
        else:
            previous = DIST / f"{target.name}-previous-{uuid.uuid4().hex[:8]}"
        target.rename(previous)
    try:
        stage.rename(target)
    except BaseException:
        if previous is not None:
            previous.rename(target)
        raise
    receipt.write_text(new_hash + "\n", encoding="ascii")
    if previous is not None and previous.parent == DIST:
        print("Previous edited export retained at", previous)


def main():
    version = ET.parse(REPO / "XingShangMod.csproj").findtext(".//PropertyGroup/Version")
    if not version or not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ValueError("Invalid project version.")
    tag = "v" + version
    slug = origin_slug()
    package = f"XingShangMod-{tag}.zip"
    installer = f"XingShangMod-Setup-{tag}.exe"
    verified = {}
    for name in (package, installer):
        path = DIST / name
        receipt = Path(str(path) + ".sha256").read_text(encoding="utf-8-sig").split()
        digest = sha256(path)
        if receipt != [digest, name]:
            raise ValueError(f"SHA256 receipt/version mismatch: {name}")
        verified[name] = digest

    with tempfile.TemporaryDirectory(prefix=".github-stage-", dir=DIST) as temp:
        work = Path(temp).resolve()
        if work.parent != DIST or not work.name.startswith(".github-stage-"):
            raise ValueError(f"Unexpected staging location: {work}")
        stage = work / "export"
        repo, release = stage / "Repository", stage / "Release"
        repo.mkdir(parents=True)
        release.mkdir()
        for path in source_files():
            copy_into_repo(path, repo)
        runtime = "Runtime/build_manifest.json"
        copy_into_repo(REPO / runtime, repo)
        manifest = read_json(repo / runtime)
        for item in manifest["files"]:
            rel = Path(item["path"])
            # Publish only production data from the verified Runtime manifest.
            if rel.is_absolute() or rel.drive or ".." in rel.parts or rel.suffix.lower() not in {
                ".json", ".csv", ".wkg", ".bundle", ".txt"
            }:
                raise ValueError(f"Unexpected Runtime member: {rel}")
            copy_into_repo(REPO / "Runtime" / rel, repo)
        verify_runtime(repo / "Runtime")

        paths = {p.relative_to(repo).as_posix() for p in repo.rglob("*") if p.is_file()}
        notes = repo / f"docs/releases/{tag}.md"
        if not notes.is_file():
            raise ValueError(f"Missing release notes for {tag}.")
        for document in repo.rglob("*.md"):
            body, images = render_markdown(repo, document, paths, slug, tag, document == notes)
            if document == repo / "README.md":
                required = {f"docs/images/{name}.jpg" for name in ("market", "late-history", "inventory", "profit")}
                if not required.issubset(images):
                    raise ValueError("The player README must reference its four gameplay screenshots.")
            if document == notes:
                if len(images) < 2:
                    raise ValueError("Release notes must reference their two screenshots.")
                (stage / "Release正文.md").write_text(body, encoding="utf-8")

        for name, digest in verified.items():
            shutil.copy2(DIST / name, release / name)
            shutil.copy2(DIST / (name+".sha256"), release / (name+".sha256"))
            if sha256(release / name) != digest:
                raise ValueError(f"Release file changed during staging: {name}")
        members = verify_archive(repo, release / package, manifest)
        verify_embedded_package(release / installer, release / package, verified[package])
        target = DIST / ("GitHub-" + tag)
        publish(stage, target)
        print(f"{target}\n{len(paths)} repository files; 4 Release attachments; {members} verified ZIP members.")


if __name__ == "__main__":
    main()
