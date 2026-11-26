import argparse
import hashlib
import json
import re
import shutil
import sys
from collections import Counter
from pathlib import Path, PurePosixPath

ITEM_COUNT = 54
NEWS_COUNT = 6329
UI_BUNDLE_NAME = "xingshang_ui.bundle"
UI_PREFAB_COUNT = 15
UI_FONT_ASSETS = {
    "regular": "NotoSansCJKsc-Regular",
    "bold": "NotoSansCJKsc-Bold",
    "serif": "NotoSerifSC-Regular",
    "display": "BarlowCondensed-SemiBoldItalic",
}
UI_BUNDLE_RUNTIME_PATH = PurePosixPath("bundles") / UI_BUNDLE_NAME
FONT_LICENSE_RUNTIME_PATH = PurePosixPath("licenses/OFL.txt")
INSTALL_GUIDE_RUNTIME_PATH = PurePosixPath("docs/安装说明.txt")
STOCK_WEEKLY_COUNT = 6024
STOCK_DATA_FILE_COUNT = 6028
SELECTION_STOCK_COUNT = 6008
SELECTION_INDEX_COUNT = 11
PERIODS = (
    "1年4-6月",
    "1年7-9月",
    "1年10-12月",
    "2年1-3月",
    "2年4-6月",
    "2年7-9月",
    "2年10-12月",
    "3年1-3月",
    "3年4-6月",
    "3年7-9月",
    "3年10-12月",
    "4年1-2月",
)
ITEM_FILE_RE = re.compile(r"^(?:选定)?创意(?P<number>\d{2})_(?P<name>.+)\.md$")
ITEM_DIR_RE = re.compile(r"^(?:选定)?创意(?P<number>\d{2})_(?P<name>.+)$")
NEWS_ENTRY_RE = re.compile(
    r"^###\s+(多|空)\s*(\d+)\s*[·・.、]?\s*(.*?)\s*$"
    r"(.*?)(?=^###\s+(?:多|空)\s*\d+|^##\s|\Z)",
    re.MULTILINE | re.DOTALL,
)
NEWS_TEXT_RE = re.compile(r"^\s*-?\s*\*\*快讯文字\*\*\s*[：:]\s*(.+?)\s*$", re.MULTILINE)
NEWS_INTENSITY_RE = re.compile(r"^\s*-?\s*\*\*强度\*\*\s*[：:]\s*(.+?)\s*$", re.MULTILINE)
SUMMARY_LINE_RE = re.compile(r"^\s*(\d+)[.、．]\s*(.+?)\s*$")
FORBIDDEN_PARTS = {"work", "_work", "previews", "history", "rework_history"}


class BuildError(RuntimeError):
    pass


def require(condition, message):
    if not condition:
        raise BuildError(message)


def read_text(path):
    return path.read_text(encoding="utf-8-sig")


def read_json(path):
    return json.loads(read_text(path))


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def write_json(path, value):
    payload = json_bytes(value)
    if not path.exists() or path.read_bytes() != payload:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(payload)


def safe_relative_path(value, label):
    require(isinstance(value, str) and value.strip(), label + " path is empty")
    require("\\" not in value, label + " path must use forward slashes: " + value)
    path = PurePosixPath(value)
    require(not path.is_absolute() and ".." not in path.parts, label + " path is unsafe: " + value)
    require(not any(part.casefold() in FORBIDDEN_PARTS for part in path.parts), label + " path is excluded: " + value)
    return path


def is_excluded(path, suffixes=None):
    if any(part.casefold() in FORBIDDEN_PARTS for part in path.parts):
        return True
    if path.name.casefold().endswith("_4x.png"):
        return True
    return suffixes is not None and path.suffix.casefold() not in suffixes


def source_files(root, suffixes=None):
    require(root.is_dir(), "Source directory does not exist: " + str(root))
    result = []
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        relative = path.relative_to(root)
        if not is_excluded(relative, suffixes):
            result.append((path, relative))
    result.sort(key=lambda pair: pair[1].as_posix())
    require(result, "No source files found under " + str(root))
    return result


def sync_files(destination, copies):
    destination.mkdir(parents=True, exist_ok=True)
    expected = set()
    for source, relative in copies:
        relative_text = relative.as_posix()
        require(relative_text not in expected, "Duplicate output path: " + relative_text)
        expected.add(relative_text)
        target = destination / Path(*relative.parts)
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists() or target.stat().st_size != source.stat().st_size or sha256(target) != sha256(source):
            shutil.copyfile(source, target)
        require(sha256(target) == sha256(source), "Copied file hash mismatch: " + relative_text)
    for path in sorted(destination.rglob("*"), key=lambda item: len(item.parts), reverse=True):
        if path.is_file():
            relative = path.relative_to(destination).as_posix()
            if relative not in expected:
                path.unlink()
        elif path.is_dir() and not any(path.iterdir()):
            path.rmdir()
    return expected


def extract_one(pattern, text, label):
    values = pattern.findall(text)
    require(len(values) == 1, label + " must occur exactly once; found " + str(len(values)))
    return values[0]


def build_items(ideas_dir, art_dir):
    ideas = {}
    for path in ideas_dir.iterdir():
        if not path.is_file():
            continue
        match = ITEM_FILE_RE.match(path.name)
        if match:
            number = int(match.group("number"))
            require(number not in ideas, "Duplicate item idea number: " + str(number))
            ideas[number] = (path, match.group("name"))
    require(set(ideas) == set(range(1, ITEM_COUNT + 1)), "Formal ideas must contain exactly 01 through 54")

    art_files = {}
    for number, (idea_path, name) in ideas.items():
        image = art_dir / ("%02d_%s.png" % (number, name))
        require(image.is_file(), "Missing canonical standard item art: " + image.name)
        art_files[number] = image

    items = []
    source_hashes = {}
    for number in range(1, ITEM_COUNT + 1):
        idea_path, name = ideas[number]
        text = read_text(idea_path)
        description = extract_one(
            re.compile(r"^-\s*\*\*物品简介\*\*\s*[：:]\s*(.+?)\s*$", re.MULTILINE),
            text,
            idea_path.name + " 物品简介",
        ).strip()
        price_text = extract_one(
            re.compile(r"参考(?:价|买入价)\s*[：:]\s*(\d[\d,]*)"),
            text,
            idea_path.name + " 参考价",
        )
        weight_text = extract_one(
            re.compile(r"重量\s*[：:]\s*(\d+)"),
            text,
            idea_path.name + " 重量",
        )
        price = int(price_text.replace(",", ""))
        weight = int(weight_text)
        require(description, "Empty item description: " + idea_path.name)
        require(price > 0, "Reference price must be positive: " + idea_path.name)
        require(weight > 0, "Weight must be positive: " + idea_path.name)
        image = art_files[number]
        require(image.name == "%02d_%s.png" % (number, name), "Item/art name mismatch: " + image.name)
        require(image.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n", "Invalid PNG: " + str(image))
        items.append(
            {
                "id": number,
                "name": name,
                "description": description,
                "referencePrice": price,
                "weight": weight,
                "iconPath": "items/" + image.name,
            }
        )
        source_hashes[idea_path.name] = sha256(idea_path)
    return {"schemaVersion": 1, "items": items}, [art_files[number].name for number in range(1, ITEM_COUNT + 1)], source_hashes


def clean_news_text(value):
    text = value.strip()
    text = re.sub(r"^\d+\s*[.、．]\s*", "", text)
    text = re.sub(r"^【[^】]*】\s*", "", text)
    return text.strip()


def parse_summary(text, label):
    section = None
    result = []
    for line in text.splitlines():
        stripped = line.strip()
        if stripped == "## 利多":
            section = "up"
            continue
        if stripped == "## 利空":
            section = "down"
            continue
        match = SUMMARY_LINE_RE.match(line)
        if match:
            require(section is not None, "Summary entry outside direction section: " + label)
            result.append((section, clean_news_text(match.group(2))))
    return result


def period_metadata():
    result = []
    for index, name in enumerate(PERIODS):
        match = re.match(r"^(\d+)年(\d+)-(\d+)月$", name)
        require(match is not None, "Invalid period: " + name)
        result.append(
            {
                "index": index,
                "name": name,
                "year": int(match.group(1)),
                "startMonth": int(match.group(2)),
                "endMonth": int(match.group(3)),
            }
        )
    return result


def build_news(news_dir, item_names):
    directories = {}
    for path in news_dir.iterdir():
        if not path.is_dir():
            continue
        match = ITEM_DIR_RE.match(path.name)
        if match:
            number = int(match.group("number"))
            require(number not in directories, "Duplicate news item directory: " + str(number))
            directories[number] = (path, match.group("name"))
    require(set(directories) == set(item_names), "News directories must cover exactly all 54 items")

    news = []
    direction_counts = Counter()
    intensity_counts = Counter()
    for number in range(1, ITEM_COUNT + 1):
        item_dir, source_name = directories[number]
        require(source_name == item_names[number], "News/item name mismatch for %02d" % number)
        for period_index, period in enumerate(PERIODS):
            full_path = item_dir / period / "完整构思.md"
            summary_path = item_dir / period / "异动解读.md"
            require(full_path.is_file(), "Missing full news document: " + str(full_path))
            require(summary_path.is_file(), "Missing news summary document: " + str(summary_path))
            full_text = read_text(full_path)
            entries = NEWS_ENTRY_RE.findall(full_text)
            require(entries, "No news entries in " + str(full_path))
            full_bodies = Counter()
            parsed_entries = []
            for direction_mark, ordinal, event_name, block in entries:
                text_values = NEWS_TEXT_RE.findall(block)
                intensity_values = NEWS_INTENSITY_RE.findall(block)
                require(len(text_values) == 1 and len(intensity_values) == 1,
                        "News fields must occur once: %s %s %s%s" % (full_path, direction_mark, ordinal, event_name))
                direction = "up" if direction_mark == "多" else "down"
                body = clean_news_text(text_values[0])
                intensity = intensity_values[0].strip()
                event_name = event_name.strip()
                require(body and event_name and intensity, "Empty news field: " + str(full_path))
                require(not body.startswith("【") and not re.match(r"^\d+\s*[.、．]", body),
                        "News prefix was not removed: " + str(full_path))
                require(intensity[0] in "强中轻", "Unknown intensity: " + intensity)
                full_bodies[(direction, body)] += 1
                parsed_entries.append((direction, body, event_name, intensity))
            summary_bodies = Counter(parse_summary(read_text(summary_path), str(summary_path)))
            require(full_bodies == summary_bodies, "Full/summary news body mismatch: " + str(full_path))
            for direction, body, event_name, intensity in parsed_entries:
                news.append(
                    {
                        "itemId": number,
                        "period": period,
                        "periodIndex": period_index,
                        "direction": direction,
                        "text": body,
                        "eventName": event_name,
                        "intensity": intensity,
                        "intensityLevel": intensity[0],
                    }
                )
                direction_counts[direction] += 1
                intensity_counts[intensity[0]] += 1
    require(len(news) == NEWS_COUNT, "Expected 6329 news entries; found " + str(len(news)))
    require(direction_counts == Counter({"up": 3175, "down": 3154}), "News direction counts do not match the formal index")
    return {
        "schemaVersion": 1,
        "periods": period_metadata(),
        "news": news,
    }, direction_counts, intensity_counts


def selection_entry(row, kind):
    identifier = row.get("securityId") if kind == "stock" else row.get("indexId")
    result = {
        "securityId": identifier,
        "kind": kind,
        "name": row.get("name"),
        "path": row.get("path"),
        "status": row.get("status"),
        "universeStatus": row.get("universeStatus") or "active",
        "rowCount": row.get("rowCount"),
        "firstWeek": row.get("firstWeek"),
        "lastWeek": row.get("lastWeek"),
        "observedRows": row.get("observedRows"),
        "noTradeRows": row.get("noTradeRows"),
        "sourceMissingRows": row.get("sourceMissingRows"),
        "rawScale": row.get("rawScale"),
        "currency": row.get("currency"),
        "sha256": row.get("sha256"),
    }
    if kind == "stock":
        result.update(
            {
                "code": row.get("code"),
                "assetType": row.get("assetType"),
                "category": row.get("category"),
                "exchange": row.get("exchange"),
                "listingDate": row.get("listingDate"),
                "delistingDate": row.get("delistingDate"),
            }
        )
    else:
        result.update({"windCode": row.get("windCode"), "market": row.get("market")})
    require(all(result[field] is not None for field in ("securityId", "name", "path", "status", "rowCount", "observedRows", "sha256")),
            "Incomplete selection entry: " + repr(identifier))
    return result


def build_selection_catalog(stock_data_dir):
    manifest_path = stock_data_dir / "manifest.json"
    manifest = read_json(manifest_path)
    selected = {"stock": [], "index": []}
    for kind, key in (("stock", "securities"), ("index", "indices")):
        for row in manifest[key]:
            if row.get("status") != "success" or not row.get("path"):
                continue
            if (row.get("observedRows") or 0) <= 0 or (row.get("sourceMissingRows") or 0) != 0:
                continue
            relative = safe_relative_path(row["path"], row.get("securityId") or row.get("indexId") or kind)
            source = stock_data_dir / Path(*relative.parts)
            require(source.is_file(), "Selected market file is missing: " + str(source))
            require(sha256(source) == row.get("sha256"), "Selected market hash mismatch: " + row["path"])
            selected[kind].append(selection_entry(row, kind))
    stocks = sorted(selected["stock"], key=lambda row: row["securityId"])
    indices = sorted(selected["index"], key=lambda row: row["securityId"])
    require(len(stocks) == SELECTION_STOCK_COUNT, "Expected 6008 selectable stocks; found " + str(len(stocks)))
    require(len(indices) == SELECTION_INDEX_COUNT, "Expected 11 selectable indices; found " + str(len(indices)))
    all_rows = stocks + indices
    require(len({row["securityId"] for row in all_rows}) == len(all_rows), "Duplicate selection securityId")
    require(len({row["path"] for row in all_rows}) == len(all_rows), "Duplicate selection path")
    catalog = {
        "schemaVersion": 1,
        "sourceSchemaVersion": manifest.get("schemaVersion"),
        "counts": {"stocks": len(stocks), "indices": len(indices)},
        "indices": indices,
        "stocks": stocks,
    }
    return catalog, manifest


def validate_stock_snapshot(stock_data_dir, manifest):
    copies = source_files(stock_data_dir)
    require(len(copies) == STOCK_DATA_FILE_COUNT, "Expected 6028 stock data files; found " + str(len(copies)))
    weekly = [pair for pair in copies if pair[0].suffix.casefold() == ".wkg"]
    require(len(weekly) == STOCK_WEEKLY_COUNT, "Expected 6024 WKG files; found " + str(len(weekly)))
    for key in ("calendars", "securities", "indices"):
        for row in manifest[key]:
            if not row.get("path"):
                continue
            relative = safe_relative_path(row["path"], row.get("securityId") or row.get("indexId") or row.get("calendarId") or key)
            source = stock_data_dir / Path(*relative.parts)
            require(source.is_file(), "Manifest data file is missing: " + row["path"])
            if row.get("sha256"):
                require(sha256(source) == row["sha256"], "Manifest data hash mismatch: " + row["path"])
    return copies


def validate_shared_font_license(font_license_path):
    text = read_text(font_license_path)
    markers = (
        "Noto Sans CJK SC Regular and Bold",
        "Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'",
        "Noto Serif SC Regular",
        "(c) 2017-2024 Adobe (http://www.adobe.com/).",
        "Copyright 2012 Google Inc. All Rights Reserved.",
        "Barlow Condensed SemiBold Italic",
        "Copyright 2017 The Barlow Project Authors (https://github.com/jpt/barlow)",
    )
    for marker in markers:
        require(marker in text, "Shared font OFL is missing copyright/family marker: " + marker)
    require(text.count("SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007") == 3,
            "Shared font OFL must contain three complete OFL 1.1 texts")
    for section in ("PREAMBLE", "DEFINITIONS", "PERMISSION & CONDITIONS", "TERMINATION", "DISCLAIMER"):
        require(text.count(section) == 3, "Shared font OFL section must occur once per family: " + section)


def validate_ui_bundle(bundle_path, binding_path, font_license_path, install_guide_path, icon_names):
    require(bundle_path.is_file(), "UI bundle does not exist: " + str(bundle_path))
    require(bundle_path.name == UI_BUNDLE_NAME, "Unexpected UI bundle name: " + bundle_path.name)
    with bundle_path.open("rb") as stream:
        require(stream.read(7) == b"UnityFS", "UI bundle is not a UnityFS AssetBundle")

    binding = read_json(binding_path)
    require(binding.get("schemaVersion") == 1, "Unsupported UI binding schema")
    require(binding.get("bundleName") == UI_BUNDLE_NAME, "UI binding bundle name mismatch")
    require(binding.get("unityVersion"), "UI binding Unity version is missing")

    prefabs = binding.get("prefabs") or []
    require(len(prefabs) == UI_PREFAB_COUNT,
            "UI binding must declare exactly %d prefabs" % UI_PREFAB_COUNT)
    require(all(row.get("name") and row.get("asset") and row.get("nodes") for row in prefabs),
            "UI binding contains an incomplete prefab")
    require(len({row["name"] for row in prefabs}) == len(prefabs), "Duplicate UI prefab name")
    require(len({row["asset"] for row in prefabs}) == len(prefabs), "Duplicate UI prefab asset")
    for prefab in prefabs:
        keys = [node.get("key") for node in prefab["nodes"]]
        require(all(keys) and len(set(keys)) == len(keys), "Invalid or duplicate node key in " + prefab["name"])

    sprites = binding.get("sprites") or []
    sprite_assets = [row.get("asset") for row in sprites]
    require(sprite_assets and all(sprite_assets), "UI binding has no complete sprite assets")
    require(len(set(sprite_assets)) == len(sprite_assets), "Duplicate UI sprite asset")
    icon_assets = {PurePosixPath(name).stem for name in icon_names}
    require(icon_assets <= set(sprite_assets), "UI binding does not contain all 54 item icons")

    fonts = binding.get("fonts") or []
    font_assets = [row.get("asset") for row in fonts]
    font_map = {row.get("key"): row.get("asset") for row in fonts}
    require(len(fonts) == len(UI_FONT_ASSETS) and font_map == UI_FONT_ASSETS,
            "UI binding must declare regular, bold, serif and display with stable asset names")
    require(len(font_assets) == len(UI_FONT_ASSETS) and all(font_assets) and
            len(set(font_assets)) == len(UI_FONT_ASSETS),
            "UI binding font assets are incomplete or duplicated")

    bundle_manifest_path = bundle_path.with_name(bundle_path.name + ".manifest")
    bundle_manifest = read_text(bundle_manifest_path)
    require(re.search(r"(?m)^\s*TypeTreeHash:\s*$", bundle_manifest) is not None,
            "UI bundle manifest has no TypeTree hash; rebuild without disabling TypeTree")
    required_assets = {
        "Assets/Binding/ui_map.json",
        "Assets/Fonts/OFL.txt",
        "Assets/Fonts/NotoSansCJKsc-Regular.otf",
        "Assets/Fonts/NotoSansCJKsc-Bold.otf",
        "Assets/Fonts/NotoSerifSC-Regular.ttf",
        "Assets/Fonts/NotoSerifSC-OFL.txt",
        "Assets/Fonts/BarlowCondensed-SemiBoldItalic.ttf",
        "Assets/Fonts/BarlowCondensed-OFL.txt",
    }
    required_assets.update("Assets/Prefabs/" + row["asset"] + ".prefab" for row in prefabs)
    for asset in sorted(required_assets):
        require(asset in bundle_manifest, "UI bundle manifest is missing: " + asset)

    validate_shared_font_license(font_license_path)
    require(UI_BUNDLE_NAME in read_text(install_guide_path) and "Runtime" in read_text(install_guide_path),
            "Install guide does not identify the Runtime UI bundle")
    return binding


def remove_legacy_runtime_assets(output_dir):
    for name in ("items", "ui"):
        path = output_dir / name
        if path.exists():
            require(path.is_dir(), "Legacy runtime path is not a directory: " + str(path))
            shutil.rmtree(str(path))


def runtime_file_manifest(output_dir):
    files = []
    for path in output_dir.rglob("*"):
        if not path.is_file() or path.name == "build_manifest.json":
            continue
        relative = path.relative_to(output_dir).as_posix()
        files.append({"path": relative, "bytes": path.stat().st_size, "sha256": sha256(path)})
    files.sort(key=lambda row: row["path"])
    return files


def validate_forbidden_artifacts(output_dir):
    for path in output_dir.rglob("*"):
        if not path.is_file():
            continue
        relative = path.relative_to(output_dir)
        require(not any(part.casefold() in FORBIDDEN_PARTS for part in relative.parts),
                "Forbidden runtime artifact: " + relative.as_posix())
        require(not path.name.casefold().endswith("_4x.png"), "4x master in runtime: " + relative.as_posix())
        require("demo" not in path.name.casefold(), "Demo artifact in runtime: " + relative.as_posix())


def verify_runtime(output_dir):
    manifest_path = output_dir / "build_manifest.json"
    require(manifest_path.is_file(), "Missing build manifest: " + str(manifest_path))
    manifest = read_json(manifest_path)
    expected = {row["path"]: row for row in manifest["files"]}
    require(len(expected) == len(manifest["files"]), "Duplicate path in build manifest")
    actual = {}
    for path in output_dir.rglob("*"):
        if path.is_file() and path.name != "build_manifest.json":
            actual[path.relative_to(output_dir).as_posix()] = path
    require(set(actual) == set(expected), "Runtime file set differs from build manifest")
    for relative, path in actual.items():
        row = expected[relative]
        require(path.stat().st_size == row["bytes"], "Runtime file size mismatch: " + relative)
        require(sha256(path) == row["sha256"], "Runtime file hash mismatch: " + relative)

    ui = manifest.get("ui") or {}
    documents = manifest.get("documents") or {}
    declared_files = (
        (ui, "bundlePath", "bundleSha256", UI_BUNDLE_RUNTIME_PATH, "UI bundle"),
        (ui, "fontLicensePath", "fontLicenseSha256", FONT_LICENSE_RUNTIME_PATH, "Font OFL"),
        (documents, "installGuidePath", "installGuideSha256", INSTALL_GUIDE_RUNTIME_PATH, "Install guide"),
    )
    for owner, path_key, hash_key, expected_path, label in declared_files:
        relative = safe_relative_path(owner.get(path_key), label).as_posix()
        require(relative == expected_path.as_posix(), label + " must be installed at " + expected_path.as_posix())
        require(relative in actual, label + " is missing: " + relative)
        declared_hash = owner.get(hash_key)
        require(isinstance(declared_hash, str) and re.fullmatch(r"[0-9a-f]{64}", declared_hash) is not None,
                label + " has no valid SHA256 in the build manifest")
        require(sha256(actual[relative]) == declared_hash, label + " SHA256 differs from its manifest field")
    require(re.fullmatch(r"[0-9a-f]{64}", ui.get("bindingSha256") or "") is not None,
            "UI binding SHA256 is missing from the build manifest")
    with actual[UI_BUNDLE_RUNTIME_PATH.as_posix()].open("rb") as stream:
        require(stream.read(7) == b"UnityFS", "Runtime UI bundle is not a UnityFS AssetBundle")
    validate_shared_font_license(actual[FONT_LICENSE_RUNTIME_PATH.as_posix()])
    require(UI_BUNDLE_NAME in read_text(actual[INSTALL_GUIDE_RUNTIME_PATH.as_posix()]),
            "Runtime install guide does not name the UI bundle")

    prefab_assets = ui.get("prefabs") or []
    sprite_assets = ui.get("sprites") or []
    font_assets = ui.get("fonts") or []
    require(len(prefab_assets) == UI_PREFAB_COUNT and len(set(prefab_assets)) == UI_PREFAB_COUNT and all(prefab_assets),
            "Runtime UI manifest must contain %d unique prefabs" % UI_PREFAB_COUNT)
    require(sprite_assets and all(sprite_assets) and len(set(sprite_assets)) == len(sprite_assets),
            "Runtime UI sprite assets are missing or duplicated")
    require(len(font_assets) == len(UI_FONT_ASSETS) and all(font_assets) and
            len(set(font_assets)) == len(UI_FONT_ASSETS) and set(font_assets) == set(UI_FONT_ASSETS.values()),
            "Runtime UI manifest must contain the four registered font assets")
    require(ui.get("unityVersion"), "Runtime UI Unity version is missing")
    require(not any(path.startswith("ui/") or path.startswith("items/") for path in actual),
            "Runtime must not contain legacy loose ui/items assets")
    require(not any(path.casefold().endswith(".png") for path in actual),
            "Runtime PNG assets must be embedded in the UI bundle")

    items = read_json(output_dir / "items.json")["items"]
    news_document = read_json(output_dir / "news.json")
    news = news_document["news"]
    catalog = read_json(output_dir / "selection_catalog.json")
    require({row["id"] for row in items} == set(range(1, ITEM_COUNT + 1)), "Runtime item IDs must be 1 through 54")
    require(len(items) == ITEM_COUNT and len({row["iconPath"] for row in items}) == ITEM_COUNT, "Runtime item count/icon mismatch")
    icon_paths = [safe_relative_path(row.get("iconPath"), "Runtime item icon") for row in items]
    require(all(len(path.parts) == 2 and path.parts[0] == "items" and path.suffix.casefold() == ".png"
                for path in icon_paths), "Runtime item icon keys must retain the items/<name>.png convention")
    icon_assets = [path.stem for path in icon_paths]
    require(set(icon_assets) <= set(sprite_assets), "Runtime item icon is missing from the UI bundle binding")
    require(all(row["referencePrice"] > 0 and row["weight"] > 0 for row in items), "Runtime contains an invalid item price/weight")
    require(len(news) == NEWS_COUNT and len(news_document["periods"]) == len(PERIODS), "Runtime news/period count mismatch")
    require(len({(row["itemId"], row["period"]) for row in news}) == ITEM_COUNT * len(PERIODS),
            "Runtime news must cover every item/period pair")
    require(all(not row["text"].startswith("【") and not re.match(r"^\d+\s*[.、．]", row["text"]) for row in news),
            "Runtime news contains a number or source prefix")
    require(len(catalog["stocks"]) == SELECTION_STOCK_COUNT and len(catalog["indices"]) == SELECTION_INDEX_COUNT,
            "Runtime selection counts mismatch")
    require(all(row["status"] == "success" and row["path"] and row["observedRows"] > 0 and row["sourceMissingRows"] == 0
                for row in catalog["stocks"] + catalog["indices"]), "Runtime selection contains an ineligible entry")
    require(all("data/" + row["path"] in actual for row in catalog["stocks"] + catalog["indices"]),
            "Runtime selection path is missing")
    directions = Counter(row["direction"] for row in news)
    actual_counts = {
        "items": len(items),
        "itemIcons": len(set(icon_assets)),
        "newsPeriodsPerItem": len(news_document["periods"]),
        "newsDocuments": len({(row["itemId"], row["period"]) for row in news}),
        "news": len(news),
        "newsUp": directions["up"],
        "newsDown": directions["down"],
        "selectionStocks": len(catalog["stocks"]),
        "selectionIndices": len(catalog["indices"]),
        "stockWeeklyFiles": sum(path.startswith("data/") and path.casefold().endswith(".wkg") for path in actual),
        "stockDataFiles": sum(path.startswith("data/") for path in actual),
        "uiBundles": sum(path.startswith("bundles/") and path.casefold().endswith(".bundle") for path in actual),
        "uiPrefabs": len(prefab_assets),
        "uiSprites": len(sprite_assets),
        "uiFonts": len(font_assets),
        "runtimePayloadFiles": len(actual),
    }
    require(manifest.get("counts") == actual_counts, "Build manifest counts differ from runtime contents")
    require(all(manifest.get("checks", {}).values()), "Build manifest contains a failed check")
    require(manifest.get("newsIntensityLevels") == dict(sorted(Counter(row["intensityLevel"] for row in news).items())),
            "Build manifest intensity counts differ from runtime news")
    validate_forbidden_artifacts(output_dir)
    return manifest


def parse_args():
    script_dir = Path(__file__).resolve().parent
    default_workspace = script_dir.parent.parent
    parser = argparse.ArgumentParser(description="Build complete XingShangMod runtime data from formal sources")
    parser.add_argument("--workspace-root", type=Path, default=default_workspace)
    parser.add_argument("--output-dir", type=Path, default=script_dir.parent / "Runtime")
    parser.add_argument("--ideas-dir", type=Path)
    parser.add_argument("--item-art-dir", type=Path)
    parser.add_argument("--news-dir", type=Path)
    parser.add_argument("--stock-data-dir", type=Path)
    parser.add_argument("--ui-bundle", type=Path)
    parser.add_argument("--ui-map", type=Path)
    parser.add_argument("--font-license", type=Path)
    parser.add_argument("--install-guide", type=Path)
    parser.add_argument("--verify-only", action="store_true")
    return parser.parse_args()


def main():
    args = parse_args()
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent
    workspace = args.workspace_root.resolve()
    output_dir = args.output_dir.resolve()
    if args.verify_only:
        manifest = verify_runtime(output_dir)
        print("Verified %d runtime payload files; all hashes and counts match." % len(manifest["files"]))
        return
    ideas_dir = (args.ideas_dir or workspace / "ideas").resolve()
    art_dir = (args.item_art_dir or workspace / "art" / "final_items").resolve()
    news_dir = (args.news_dir or workspace / "text" / "glm").resolve()
    stock_data_dir = (args.stock_data_dir or workspace / "stock_market" / "data").resolve()
    ui_bundle = (args.ui_bundle or project_root / "UnityUI" / "Build" / UI_BUNDLE_NAME).resolve()
    ui_map = (args.ui_map or project_root / "UnityUI" / "Assets" / "Binding" / "ui_map.json").resolve()
    font_license = (args.font_license or project_root / "UnityUI" / "Assets" / "Fonts" / "OFL.txt").resolve()
    install_guide = (args.install_guide or project_root / "安装说明.txt").resolve()

    items_document, icon_names, idea_hashes = build_items(ideas_dir, art_dir)
    item_names = {row["id"]: row["name"] for row in items_document["items"]}
    news_document, direction_counts, intensity_counts = build_news(news_dir, item_names)
    catalog, stock_manifest = build_selection_catalog(stock_data_dir)
    stock_copies = validate_stock_snapshot(stock_data_dir, stock_manifest)
    binding = validate_ui_bundle(ui_bundle, ui_map, font_license, install_guide, icon_names)

    output_dir.mkdir(parents=True, exist_ok=True)
    sync_files(output_dir / "data", stock_copies)
    sync_files(output_dir / "bundles", [(ui_bundle, PurePosixPath(UI_BUNDLE_NAME))])
    sync_files(output_dir / "licenses", [(font_license, PurePosixPath("OFL.txt"))])
    sync_files(output_dir / "docs", [(install_guide, PurePosixPath("安装说明.txt"))])
    remove_legacy_runtime_assets(output_dir)
    write_json(output_dir / "items.json", items_document)
    write_json(output_dir / "news.json", news_document)
    write_json(output_dir / "selection_catalog.json", catalog)
    validate_forbidden_artifacts(output_dir)

    files = runtime_file_manifest(output_dir)
    build_manifest = {
        "schemaVersion": 1,
        "counts": {
            "items": len(items_document["items"]),
            "itemIcons": len(icon_names),
            "newsPeriodsPerItem": len(PERIODS),
            "newsDocuments": len(items_document["items"]) * len(PERIODS),
            "news": len(news_document["news"]),
            "newsUp": direction_counts["up"],
            "newsDown": direction_counts["down"],
            "selectionStocks": len(catalog["stocks"]),
            "selectionIndices": len(catalog["indices"]),
            "stockWeeklyFiles": STOCK_WEEKLY_COUNT,
            "stockDataFiles": len(stock_copies),
            "uiBundles": 1,
            "uiPrefabs": len(binding["prefabs"]),
            "uiSprites": len(binding["sprites"]),
            "uiFonts": len(binding["fonts"]),
            "runtimePayloadFiles": len(files),
        },
        "checks": {
            "itemPricesFromOfficialIdeas": True,
            "noDemoPrices": True,
            "positivePricesAndWeights": True,
            "newsBodiesMatchedByText": True,
            "newsPrefixesRemoved": True,
            "selectionExcludesNonSuccess": True,
            "selectionExcludesEmptyPaths": True,
            "selectionExcludesSourceMissing": True,
            "selectionRequiresObserved": True,
            "sourceAndCopiedHashesMatch": True,
            "uiBundleHashVerified": True,
            "uiBindingComplete": True,
            "uiTypeTreeRetained": True,
            "fontOflIncluded": True,
            "installGuideIncluded": True,
            "looseUiAssetsExcluded": True,
            "forbiddenArtifactsExcluded": True,
            "buildManifestExcludedFromOwnHash": True,
        },
        "sources": {
            "stockManifestSha256": sha256(stock_data_dir / "manifest.json"),
            "ideaFilesSha256": idea_hashes,
        },
        "ui": {
            "bundlePath": UI_BUNDLE_RUNTIME_PATH.as_posix(),
            "bundleSha256": sha256(output_dir / Path(*UI_BUNDLE_RUNTIME_PATH.parts)),
            "bindingSha256": sha256(ui_map),
            "unityVersion": binding["unityVersion"],
            "prefabs": [row["asset"] for row in binding["prefabs"]],
            "sprites": [row["asset"] for row in binding["sprites"]],
            "fonts": [row["asset"] for row in binding["fonts"]],
            "fontLicensePath": FONT_LICENSE_RUNTIME_PATH.as_posix(),
            "fontLicenseSha256": sha256(output_dir / Path(*FONT_LICENSE_RUNTIME_PATH.parts)),
        },
        "documents": {
            "installGuidePath": INSTALL_GUIDE_RUNTIME_PATH.as_posix(),
            "installGuideSha256": sha256(output_dir / Path(*INSTALL_GUIDE_RUNTIME_PATH.parts)),
        },
        "newsIntensityLevels": dict(sorted(intensity_counts.items())),
        "files": files,
    }
    write_json(output_dir / "build_manifest.json", build_manifest)
    verified = verify_runtime(output_dir)
    print(
        "Built %d items, %d news entries, %d stocks, %d indices, one %d-prefab UI bundle, %d WKG files."
        % (
            len(items_document["items"]),
            len(news_document["news"]),
            len(catalog["stocks"]),
            len(catalog["indices"]),
            len(binding["prefabs"]),
            STOCK_WEEKLY_COUNT,
        )
    )
    print("Verified %d runtime payload files under %s" % (len(verified["files"]), output_dir))


if __name__ == "__main__":
    try:
        main()
    except (BuildError, OSError, ValueError, json.JSONDecodeError) as error:
        print("ERROR: " + str(error), file=sys.stderr)
        sys.exit(1)
