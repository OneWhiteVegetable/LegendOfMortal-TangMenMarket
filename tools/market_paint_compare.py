import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def top_rect(rect, height):
    return (
        float(rect["x"]),
        float(height - rect["y"] - rect["height"]),
        float(rect["x"] + rect["width"]),
        float(height - rect["y"]),
    )


def dom_rect(data, selector, dpr=1.0):
    rect = data["elements"][selector]["rect"]
    return tuple(
        value * dpr
        for value in (rect["x"], rect["y"], rect["x"] + rect["width"], rect["y"] + rect["height"])
    )


def find(rows, parent, leaf):
    return next((row for row in rows if parent in row["path"] and leaf in row["path"]), None)


class Source:
    def __init__(self, label, png_path, json_path):
        self.label = label
        self.png_path = Path(png_path)
        self.json_path = Path(json_path)
        self.data = read_json(json_path)
        self.image = Image.open(png_path).convert("RGB")
        self.width, self.height = self.image.size
        self.html = "elements" in self.data
        self.dpr = float(self.data.get("viewport", {}).get("devicePixelRatio", 1.0)) if self.html else (
            1.5 if self.width >= 2304 and self.height >= 1296 else 1.0
        )

    def unity_image(self, parent, leaf):
        return find(self.data.get("images", []), parent, leaf)

    def title(self):
        if self.html:
            return dom_rect(self.data, ".goods-name", self.dpr)
        return top_rect(self.unity_image("/GoodsCard00#", "/NameBack#")["rect"], self.height)

    def red_line(self):
        if self.html:
            box = self.title()
            return (box[2], box[1], box[2] + 3 * self.dpr, box[3])
        row = self.unity_image("/GoodsCard00#", "/NameRedLine#")
        if row is None:
            box = self.title()
            return (box[2], box[1], box[2], box[3])
        return top_rect(row["rect"], self.height)

    def title_text(self):
        if self.html:
            style = self.data["elements"][".goods-name"]
            fonts = self.data.get("fonts", {}).get(".goods-name", [])
            return {
                "font": fonts[0].get("postScriptName", "") if fonts else "",
                "fontSize": float(style.get("computedStyle", {}).get("fontSize", "0px").rstrip("px")),
                "fontStyle": style.get("computedStyle", {}).get("fontWeight", ""),
            }
        row = find(self.data.get("texts", []), "/GoodsCard00#", "/NameText#")
        return {key: row.get(key) for key in ("font", "fontSize", "fontStyle", "pixelsPerUnit", "nativeFontSize")}

    def chart(self):
        if self.html:
            return dom_rect(self.data, ".goods-chart-bg", self.dpr), {}
        row = next(row for row in self.data["charts"] if row["mode"] == "Card" and "/GoodsCard00#" in row["path"])
        if row.get("rect"):
            rect = top_rect(row["rect"], self.height)
        else:
            face = self.unity_image("/GoodsCard00#", "/VisualBackground#")["rect"]
            face = top_rect(face, self.height)
            bottom = (35 if self.height <= 790 else 42) + 1
            rect = (face[0] + self.dpr, face[1] + self.dpr, face[2] - self.dpr, face[3] - bottom * self.dpr)
        keys = (
            "totalPointCount", "visiblePointCount", "visibleRange", "segmentCount", "lineWidth",
            "pixelsPerUnit", "nativeLineWidth", "graphicAlpha", "mainStrokeOpacity",
            "underStrokeOpacity", "effectiveMainStrokeAlpha", "effectiveUnderStrokeAlpha",
            "underStrokeAdditionalWidth", "risingColor", "fallingColor", "dryBrushTexture", "plotShader",
        )
        return rect, {key: row.get(key) for key in keys}

    def rail(self):
        if self.html:
            return dom_rect(self.data, ".cart-rail", self.dpr)
        return top_rect(self.unity_image("/PageHost#", "/CartRail#")["rect"], self.height)

    def baseline(self):
        if self.html:
            tabs = dom_rect(self.data, ".cart-tabs", self.dpr)
            return (tabs[0], tabs[3] - self.dpr, tabs[2], tabs[3])
        return top_rect(self.unity_image("/CartTabs#", "/Baseline#")["rect"], self.height)

    def cart_list_top(self):
        if self.html:
            return dom_rect(self.data, ".cart-list", self.dpr)[1]
        scroll = next((row for row in self.data.get("scrolls", []) if "/CartListScroll#" in row["path"]), None)
        if scroll and scroll.get("rect"):
            return top_rect(scroll["rect"], self.height)[1]
        return self.rail()[1] + 110 * self.dpr

    def empty_text(self, leaf):
        if self.html:
            selector = ".cart-empty strong" if leaf == "title" else ".cart-empty p"
            if selector not in self.data["elements"]:
                return None, {}
            return dom_rect(self.data, selector, self.dpr), {}
        row = find(self.data.get("texts", []), "/CartEmpty#", "/TitleText#" if leaf == "title" else "/BodyText#")
        if row is None:
            return None, {}
        keys = ("font", "fontSize", "fontStyle", "pixelsPerUnit", "nativeFontSize")
        return top_rect(row["rect"], self.height), {key: row.get(key) for key in keys}


def clamp_box(box, image):
    left = max(0, math.floor(box[0]))
    top = max(0, math.floor(box[1]))
    right = min(image.width, math.ceil(box[2]))
    bottom = min(image.height, math.ceil(box[3]))
    return left, top, max(left, right), max(top, bottom)


def expand(box, horizontal, vertical, image):
    return clamp_box((box[0] - horizontal, box[1] - vertical, box[2] + horizontal, box[3] + vertical), image)


def ink(image, box, dark):
    box = clamp_box(box, image)
    crop = image.crop(box)
    points = []
    for y in range(crop.height):
        for x in range(crop.width):
            pixel = crop.getpixel((x, y))
            maximum, minimum = max(pixel), min(pixel)
            match = maximum < 150 and maximum - minimum < 45 if dark else minimum > 90 and maximum - minimum < 75
            if match:
                points.append((x + box[0], y + box[1]))
    if not points:
        return {"pixels": 0, "width": 0, "height": 0, "right": 0}
    xs, ys = zip(*points)
    return {
        "pixels": len(points),
        "width": max(xs) - min(xs) + 1,
        "height": max(ys) - min(ys) + 1,
        "right": max(xs) + 1,
    }


def stripe_columns(image, rail, left):
    sample_width = max(12, round((rail[2] - rail[0]) * 0.045))
    start_x = round(rail[0]) if left else round(rail[2]) - sample_width
    end_x = start_x + sample_width
    start_y = round(rail[1] + (rail[3] - rail[1]) * 0.08)
    end_y = round(rail[3] - (rail[3] - rail[1]) * 0.08)
    columns = 0
    for x in range(start_x, end_x):
        light = total = 0
        for y in range(start_y, end_y, 2):
            r, g, b = image.getpixel((x, y))
            light += r > 130 and g > 120 and b > 100
            total += 1
        columns += bool(total and light * 2 >= total)
    return columns


def detail_crop(source, chart):
    if source.width == 1920 and source.height == 1080:
        return 68, 474, 202, 535
    x0, y0, x1, y1 = chart
    width, height = x1 - x0, y1 - y0
    return (
        x0 + width * 0.0442,
        y0 + height * 0.7807,
        x0 + width * (0.0442 + 0.2821),
        y0 + height * (0.7807 + 0.1683),
    )


def detail_contrast(image, box):
    roi = image.convert("L").crop(clamp_box(box, image))
    contrast = ImageChops.subtract(roi.filter(ImageFilter.MedianFilter(9)), roi)
    return sum(contrast.histogram()[21:])


def save_crop(image, box, path):
    box = clamp_box(box, image)
    image.crop(box).save(path)
    return {"x": box[0], "y": box[1], "width": box[2] - box[0], "height": box[3] - box[1]}


def capture(source, output):
    output.mkdir(parents=True, exist_ok=True)
    prefix = source.label
    title = source.title()
    title_ink = ink(source.image, title, True)
    red_line = source.red_line()
    chart, chart_layers = source.chart()
    lower_left = detail_crop(source, chart)
    rail = source.rail()
    baseline = source.baseline()
    baseline_y = (baseline[1] + baseline[3]) / 2
    empty_title, empty_title_style = source.empty_text("title")
    result = {
        "label": prefix,
        "sourcePng": str(source.png_path),
        "sourceJson": str(source.json_path),
        "width": source.width,
        "height": source.height,
        "nativePixelsPerCssPixel": source.dpr,
        "firstTitle": {
            **source.title_text(),
            **{f"neutralInk{key.title()}": value for key, value in title_ink.items() if key != "right"},
            "titleToRedLineGap": red_line[0] - title_ink["right"] if title_ink["pixels"] else -1,
        },
        "firstChart": {**chart_layers, "detailContrastOver20": detail_contrast(source.image, lower_left)},
        "cart": {
            "leftLightStripeColumns": stripe_columns(source.image, rail, True),
            "rightLightStripeColumns": stripe_columns(source.image, rail, False),
            "tabsBaselineFromRailTop": baseline_y - rail[1],
            "cartListFromRailTop": source.cart_list_top() - rail[1],
            "baselineToListGap": source.cart_list_top() - baseline_y,
        },
        "crops": {},
    }
    if empty_title is not None:
        empty_ink = ink(source.image, empty_title, False)
        result["cart"]["emptyTitle"] = {
            **empty_title_style,
            **{f"lightInk{key.title()}": value for key, value in empty_ink.items() if key != "right"},
        }
    crops = {
        "first-title": expand(title, 8 * source.dpr, 8 * source.dpr, source.image),
        "first-chart": chart,
        "first-chart-lower-left": lower_left,
        "cart-rail": rail,
        "cart-tabs": expand(baseline, 0, 30 * source.dpr, source.image),
    }
    if empty_title is not None:
        crops["cart-empty-title"] = expand(empty_title, 4 * source.dpr, 4 * source.dpr, source.image)
    for name, box in crops.items():
        result["crops"][name] = save_crop(source.image, box, output / f"{prefix}-{name}.png")
    return result, {name: source.image.crop(clamp_box(box, source.image)) for name, box in crops.items()}


def stitch(sources, crops, name, output):
    images = [crops[source.label][name] for source in sources if name in crops[source.label]]
    if len(images) != len(sources) or not images:
        return
    margin = 8
    width = sum(image.width for image in images) + margin * (len(images) + 1)
    height = max(image.height for image in images) + margin * 2
    result = Image.new("RGB", (width, height), (23, 28, 26))
    x = margin
    for image in images:
        result.paste(image, (x, margin))
        x += image.width + margin
    result.save(output / f"comparison-{name}.png")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--html-png", required=True)
    parser.add_argument("--html-json", required=True)
    parser.add_argument("--unity-png", required=True)
    parser.add_argument("--unity-json", required=True)
    parser.add_argument("--before-png")
    parser.add_argument("--before-json")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    output = Path(args.output)
    sources = [Source("html", args.html_png, args.html_json)]
    if args.before_png or args.before_json:
        if not args.before_png or not args.before_json:
            parser.error("--before-png and --before-json must be supplied together")
        sources.append(Source("before", args.before_png, args.before_json))
    sources.append(Source("after", args.unity_png, args.unity_json))
    reports = []
    crops = {}
    for source in sources:
        report, source_crops = capture(source, output)
        reports.append(report)
        crops[source.label] = source_crops
    for name in ("first-title", "first-chart-lower-left", "cart-tabs", "cart-empty-title"):
        stitch(sources, crops, name, output)
    report_path = output / "paint-comparison.json"
    report_path.write_text(json.dumps(reports, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(report_path)


if __name__ == "__main__":
    main()
