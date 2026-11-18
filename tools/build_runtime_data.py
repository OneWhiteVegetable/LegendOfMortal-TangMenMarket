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
UI_SPRITE_COUNT = 164
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


def validate_ui(ui_dir, art_dir, icon_names):
    manifest_path = ui_dir / "manifest.json"
    manifest = read_json(manifest_path)
    assets = manifest.get("assets") or []
    require(len(assets) == UI_SPRITE_COUNT and manifest.get("totalSpriteCount") == UI_SPRITE_COUNT,
            "UI manifest must declare exactly 164 sprites")
    asset_paths = set()
    for asset in assets:
        relative = safe_relative_path(asset.get("file"), "UI asset")
        require(relative.suffix.casefold() == ".png", "UI asset is not PNG: " + relative.as_posix())
        require(relative.as_posix() not in asset_paths, "Duplicate UI asset: " + relative.as_posix())
        asset_paths.add(relative.as_posix())
        source = ui_dir / Path(*relative.parts)
        require(source.is_file(), "UI sprite is missing: " + relative.as_posix())
        require(source.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n", "Invalid UI PNG: " + relative.as_posix())
        require(sha256(source) == asset.get("sha256"), "UI sprite hash mismatch: " + relative.as_posix())
    ui_item_assets = {PurePosixPath(asset["file"]).name for asset in assets if asset.get("category") == "items/final"}
    require(ui_item_assets == set(icon_names), "UI final item sprites must match all 54 standard icons")
    for icon_name in icon_names:
        require(sha256(ui_dir / "items" / "final" / icon_name) == sha256(art_dir / icon_name),
                "UI/standard icon hash mismatch: " + icon_name)
    copies = source_files(ui_dir, {".png", ".json"})
    png_paths = {relative.as_posix() for source, relative in copies if source.suffix.casefold() == ".png"}
    require(png_paths == asset_paths, "Formal UI PNG files must exactly match the 164 manifest assets")
    return copies, manifest


def copy_item_icons(art_dir, output_dir, icon_names):
    copies = [(art_dir / name, PurePosixPath(name)) for name in icon_names]
    return sync_files(output_dir / "items", copies)


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
    items = read_json(output_dir / "items.json")["items"]
    news_document = read_json(output_dir / "news.json")
    news = news_document["news"]
    catalog = read_json(output_dir / "selection_catalog.json")
    require({row["id"] for row in items} == set(range(1, ITEM_COUNT + 1)), "Runtime item IDs must be 1 through 54")
    require(len(items) == ITEM_COUNT and len({row["iconPath"] for row in items}) == ITEM_COUNT, "Runtime item count/icon mismatch")
    require(all(row["iconPath"] in actual for row in items), "Runtime item icon path is missing")
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
    ui_manifest = read_json(output_dir / "ui" / "v4" / "manifest.json")
    actual_counts = {
        "items": len(items),
        "itemIcons": sum(path.startswith("items/") for path in actual),
        "newsPeriodsPerItem": len(news_document["periods"]),
        "newsDocuments": len({(row["itemId"], row["period"]) for row in news}),
        "news": len(news),
        "newsUp": directions["up"],
        "newsDown": directions["down"],
        "selectionStocks": len(catalog["stocks"]),
        "selectionIndices": len(catalog["indices"]),
        "stockWeeklyFiles": sum(path.startswith("data/") and path.casefold().endswith(".wkg") for path in actual),
        "stockDataFiles": sum(path.startswith("data/") for path in actual),
        "uiSprites": len(ui_manifest["assets"]),
        "uiFiles": sum(path.startswith("ui/v4/") for path in actual),
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
    parser.add_argument("--ui-dir", type=Path)
    parser.add_argument("--verify-only", action="store_true")
    return parser.parse_args()


def main():
    args = parse_args()
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
    ui_dir = (args.ui_dir or workspace / "UI" / "unity_assets" / "v4").resolve()

    items_document, icon_names, idea_hashes = build_items(ideas_dir, art_dir)
    item_names = {row["id"]: row["name"] for row in items_document["items"]}
    news_document, direction_counts, intensity_counts = build_news(news_dir, item_names)
    catalog, stock_manifest = build_selection_catalog(stock_data_dir)
    stock_copies = validate_stock_snapshot(stock_data_dir, stock_manifest)
    ui_copies, ui_manifest = validate_ui(ui_dir, art_dir, icon_names)

    output_dir.mkdir(parents=True, exist_ok=True)
    sync_files(output_dir / "data", stock_copies)
    copy_item_icons(art_dir, output_dir, icon_names)
    sync_files(output_dir / "ui" / "v4", ui_copies)
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
            "uiSprites": len(ui_manifest["assets"]),
            "uiFiles": len(ui_copies),
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
            "forbiddenArtifactsExcluded": True,
            "buildManifestExcludedFromOwnHash": True,
        },
        "sources": {
            "stockManifestSha256": sha256(stock_data_dir / "manifest.json"),
            "uiManifestSha256": sha256(ui_dir / "manifest.json"),
            "ideaFilesSha256": idea_hashes,
        },
        "newsIntensityLevels": dict(sorted(intensity_counts.items())),
        "files": files,
    }
    write_json(output_dir / "build_manifest.json", build_manifest)
    verified = verify_runtime(output_dir)
    print(
        "Built %d items, %d news entries, %d stocks, %d indices, %d UI sprites, %d WKG files."
        % (
            len(items_document["items"]),
            len(news_document["news"]),
            len(catalog["stocks"]),
            len(catalog["indices"]),
            len(ui_manifest["assets"]),
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
