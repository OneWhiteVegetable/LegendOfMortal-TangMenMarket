import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ICONS = ROOT / "UnityUI/Assets/Art/V4/icons"
TEXTURES = ROOT / "UnityUI/Assets/Art/V4/textures"


def draw_stepper_icons():
    minus = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    ImageDraw.Draw(minus).line((56, 128, 200, 128), fill=(255, 255, 255, 255), width=20)
    minus.save(ICONS / "icon_minus.png")
    plus = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    draw = ImageDraw.Draw(plus)
    draw.line((128, 56, 128, 200), fill=(255, 255, 255, 255), width=20)
    draw.line((56, 128, 200, 128), fill=(255, 255, 255, 255), width=20)
    plus.save(ICONS / "icon_plus.png")


def draw_halftone():
    image = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    for y in range(7, 512, 29):
        for x in range(7, 512, 29):
            draw.ellipse((x - 2, y - 2, x + 2, y + 2), fill=(255, 255, 255, 255))
    image.save(TEXTURES / "texture_celadon_halftone.png")


def strengthen_hatch_alpha():
    path = TEXTURES / "texture_ink_diagonal_hatch_tile.png"
    image = Image.open(path).convert("RGBA")
    if image.getchannel("A").getextrema()[1] <= 18:
        alpha = image.getchannel("A").point(lambda value: min(255, round(value * 255 / 18)))
        image.putalpha(alpha)
        image.save(path)


def update_manifest_hashes():
    files = {
        "icons/icon_minus.png",
        "icons/icon_plus.png",
        "textures/texture_celadon_halftone.png",
        "textures/texture_ink_diagonal_hatch_tile.png",
    }
    path = ROOT / "UnityUI/Assets/Art/V4/manifest.json"
    manifest = json.loads(path.read_text(encoding="utf-8"))
    for asset in manifest["assets"]:
        if asset["file"] in files:
            asset["sha256"] = hashlib.sha256((ROOT / "UnityUI/Assets/Art/V4" / asset["file"]).read_bytes()).hexdigest()
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    draw_stepper_icons()
    draw_halftone()
    strengthen_hatch_alpha()
    update_manifest_hashes()
