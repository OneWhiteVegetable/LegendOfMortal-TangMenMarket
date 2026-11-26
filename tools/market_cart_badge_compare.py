import argparse
import json
import math
import re
from collections import deque
from pathlib import Path

from PIL import Image


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--pair", action="append", required=True, metavar="NAME=HTML,UNITY")
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def gold(pixel):
    r, g, b = pixel[:3]
    return r >= 140 and g >= 105 and b <= 180 and r >= g and r - b >= 45 and g - b >= 30


def bounds(points):
    if not points:
        return {"x": 0, "y": 0, "width": 0, "height": 0}
    left = min(x for x, y in points)
    top = min(y for x, y in points)
    right = max(x for x, y in points) + 1
    bottom = max(y for x, y in points) + 1
    return {"x": left, "y": top, "width": right - left, "height": bottom - top}


def components(points):
    remaining = set(points)
    result = []
    while remaining:
        seed = remaining.pop()
        queue = deque([seed])
        component = {seed}
        while queue:
            x, y = queue.popleft()
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if dx == 0 and dy == 0:
                        continue
                    neighbor = (x + dx, y + dy)
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        component.add(neighbor)
                        queue.append(neighbor)
        result.append(component)
    return result


def has_gold(mask, x, y, radius, dx, dy):
    return any((x + dx * distance, y + dy * distance) in mask for distance in range(1, radius + 1))


def pixel_rotation(mask, rect):
    if not mask:
        return 0.0
    rows = []
    for y in range(rect["y"], rect["y"] + rect["height"]):
        xs = [x for x, yy in mask if yy == y]
        if xs:
            rows.append((y, min(xs), max(xs)))
    if len(rows) < 2:
        return 0.0
    max_width = max(row[2] - row[1] + 1 for row in rows)
    minimum_width = math.ceil(max_width * 0.9)
    rows = [row for row in rows if row[2] - row[1] + 1 >= minimum_width]
    if len(rows) < 2:
        return 0.0
    mean_y = sum(row[0] for row in rows) / len(rows)
    mean_left = sum(row[1] for row in rows) / len(rows)
    mean_right = sum(row[2] for row in rows) / len(rows)
    denominator = sum((row[0] - mean_y) ** 2 for row in rows)
    if denominator == 0:
        return 0.0
    left = sum((row[0] - mean_y) * (row[1] - mean_left) for row in rows) / denominator
    right = sum((row[0] - mean_y) * (row[2] - mean_right) for row in rows) / denominator
    return math.degrees(math.atan(left) + math.atan(right)) / 2


def measure(path):
    image = Image.open(path).convert("RGB")
    width, height = image.size
    scale = 1.5 if width >= 2304 and height >= 1296 else 1.0
    compact = width / scale <= 1450 or height / scale <= 790
    rail_top = (118 if compact else 136) * scale
    probe = {
        "x": max(0, math.floor(width - 115 * scale)),
        "y": max(0, math.floor(rail_top - 12 * scale)),
    }
    probe["width"] = width - probe["x"]
    probe["height"] = min(height, math.ceil(rail_top + 78 * scale)) - probe["y"]
    points = []
    for y in range(probe["y"], probe["y"] + probe["height"]):
        for x in range(probe["x"], probe["x"] + probe["width"]):
            if gold(image.getpixel((x, y))):
                points.append((x, y))
    candidates = components(points)
    window_right = (14 if compact else 20) * scale
    expected_center = (
        width - window_right - (28 if compact else 32) * scale,
        rail_top + (27 if compact else 34) * scale,
    )
    if candidates:
        candidates.sort(key=lambda item: (
            -len(item),
            (sum(x for x, y in item) / len(item) - expected_center[0]) ** 2
            + (sum(y for x, y in item) / len(item) - expected_center[1]) ** 2,
        ))
        mask = candidates[0]
    else:
        mask = set()
    gold_bounds = bounds(mask)
    radius = max(gold_bounds["width"], gold_bounds["height"])
    digit = set()
    if mask:
        for y in range(gold_bounds["y"], gold_bounds["y"] + gold_bounds["height"]):
            for x in range(gold_bounds["x"], gold_bounds["x"] + gold_bounds["width"]):
                r, g, b = image.getpixel((x, y))
                gray = (r * 77 + g * 150 + b * 29) >> 8
                if gray > 130:
                    continue
                if (has_gold(mask, x, y, radius, -1, 0)
                        and has_gold(mask, x, y, radius, 1, 0)
                        and has_gold(mask, x, y, radius, 0, -1)
                        and has_gold(mask, x, y, radius, 0, 1)):
                    digit.add((x, y))
    digit_bounds = bounds(digit)
    gold_center = [
        gold_bounds["x"] + gold_bounds["width"] * 0.5,
        gold_bounds["y"] + gold_bounds["height"] * 0.5,
    ] if mask else list(expected_center)
    rotation = pixel_rotation(mask, gold_bounds)
    mean = [0, 0, 0]
    if mask:
        mean = [round(sum(image.getpixel(point)[channel] for point in mask) / len(mask)) for channel in range(3)]
    touching_probe = bool(mask) and (
        gold_bounds["x"] <= probe["x"]
        or gold_bounds["y"] <= probe["y"]
        or gold_bounds["x"] + gold_bounds["width"] >= probe["x"] + probe["width"]
        or gold_bounds["y"] + gold_bounds["height"] >= probe["y"] + probe["height"]
    )
    touching_gold = bool(digit) and (
        digit_bounds["x"] <= gold_bounds["x"]
        or digit_bounds["y"] <= gold_bounds["y"]
        or digit_bounds["x"] + digit_bounds["width"] >= gold_bounds["x"] + gold_bounds["width"]
        or digit_bounds["y"] + digit_bounds["height"] >= gold_bounds["y"] + gold_bounds["height"]
    )
    return {
        "sourcePng": str(path.resolve()),
        "width": width,
        "height": height,
        "nativePixelsPerCssPixel": scale,
        "compact": compact,
        "expectedCenter": list(expected_center),
        "goldCenterDeltaFromExpected": [
            gold_center[0] - expected_center[0],
            gold_center[1] - expected_center[1],
        ],
        "pixelRotationZ": rotation,
        "probeRect": probe,
        "goldPaintPixels": len(mask),
        "goldMeanColor": mean,
        "goldBounds": gold_bounds,
        "goldTouchingProbeEdge": touching_probe,
        "digitInkPixels": len(digit),
        "digitBounds": digit_bounds,
        "digitTouchingGoldEdge": touching_gold,
    }


def crop(image, rect, margin, path):
    if not rect["width"] or not rect["height"]:
        return None
    left = max(0, rect["x"] - margin)
    top = max(0, rect["y"] - margin)
    right = min(image.width, rect["x"] + rect["width"] + margin)
    bottom = min(image.height, rect["y"] + rect["height"] + margin)
    image.crop((left, top, right, bottom)).save(path)
    return {"x": left, "y": top, "width": right - left, "height": bottom - top}


def capture_crops(pair_name, key, source, measured, directory):
    image = Image.open(source).convert("RGB")
    scale = measured["nativePixelsPerCssPixel"]
    badge = crop(image, measured["goldBounds"], round(10 * scale), directory / f"{key}-badge.png")
    digit = crop(image, measured["digitBounds"], max(2, round(4 * scale)), directory / f"{key}-digit.png")
    return image, {"badge": badge, "digit": digit}


def comparison(images, directory):
    native = []
    for key, image in zip(("html", "unity"), images):
        path = directory / f"{key}-badge.png"
        if path.exists():
            native.append(Image.open(path).convert("RGB"))
    if not native:
        return
    canvas = Image.new("RGB", (sum(image.width for image in native), max(image.height for image in native)), (32, 35, 33))
    x = 0
    for image in native:
        canvas.paste(image, (x, 0))
        x += image.width
    canvas.save(directory / "comparison-badge.png")


def main():
    args = parse_args()
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    report = {
        "schemaVersion": 1,
        "coordinateSpace": "nativePixelsTopLeft",
        "measurement": "Largest thresholded gold component; exclusive-right/bottom framebuffer bounds. Rotation uses rows spanning at least 90% of the maximum gold row, never RectTransform size.",
        "pairs": [],
    }
    for specification in args.pair:
        name, paths = specification.split("=", 1)
        html_path, unity_path = paths.split(",", 1)
        if not re.fullmatch(r"[A-Za-z0-9_.-]+", name):
            raise ValueError(f"Unsafe pair name: {name}")
        directory = output / name
        directory.mkdir(parents=True, exist_ok=True)
        html = measure(Path(html_path))
        unity = measure(Path(unity_path))
        images = []
        crops = {}
        for key, source, measured in (("html", Path(html_path), html), ("unity", Path(unity_path), unity)):
            image, crop_paths = capture_crops(name, key, source, measured, directory)
            images.append(image)
            crops[key] = crop_paths
        comparison(images, directory)
        html_size = html["goldBounds"]
        unity_size = unity["goldBounds"]
        report["pairs"].append({
            "name": name,
            "html": html,
            "unity": unity,
            "crops": crops,
            "goldSizeDeltaUnityMinusHtml": [
                unity_size["width"] - html_size["width"],
                unity_size["height"] - html_size["height"],
            ],
            "digitSizeDeltaUnityMinusHtml": [
                unity["digitBounds"]["width"] - html["digitBounds"]["width"],
                unity["digitBounds"]["height"] - html["digitBounds"]["height"],
            ],
        })
    (output / "badge-paint.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
