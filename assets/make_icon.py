"""Aion2Heal's icon, in two styles on the same dark teal tile:

- "wings": a green healing cross with gold wings spreading from behind it (the active icon);
- "cross": the green healing cross with a gold heartbeat line through it (the earlier icon, kept as the backup).

Each size is drawn on its own (supersampled, then reduced), so the small ones stay sharp: their cross edges sit on
whole pixels, and they leave out what would only blur (up to 40 px both styles are the plain cross).

Run from anywhere with Pillow installed: `python assets/make_icon.py` writes assets/Aion2Heal-wings.ico/.png and
assets/Aion2Heal-cross.ico/.png, and copies the active style (`--style wings` by default, or `--style cross`) to
assets/Aion2Heal.ico (the exe's and the release's icon), assets/Aion2Heal.png and the windows'
src/Aion2Flow/Assets/Images/Aion2Heal.png. `--preview DIR` also writes preview sheets there.
"""
import argparse
import math
import pathlib
import shutil

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = pathlib.Path(__file__).resolve().parent.parent
SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]

TILE_TOP = (19, 74, 85)       # deep teal
TILE_BOTTOM = (9, 30, 44)     # navy
TILE_EDGE = (78, 160, 160)
CROSS_TOP = (196, 255, 226)
CROSS_BOTTOM = (60, 200, 128)  # the overlay's HP green, a bit deeper
CROSS_EDGE = (8, 52, 44)
GLOW = (94, 240, 176)
PULSE = (242, 207, 99)         # the overlay's gold, a bit brighter
PULSE_EDGE = (10, 28, 40)
WING_TOP = (255, 241, 196)
WING_BOTTOM = (214, 158, 52)
WING_EDGE = (46, 30, 8)

# A wing's feathers, top (drawn last) to bottom: root and tip as parts of the tile (the right wing; the left one is
# its mirror), width, and how far the feather arches up (as a part of its length). They fan out and rise behind the
# cross.
FEATHERS = [
    ((0.52, 0.36), (0.93, 0.12), 0.15, 0.07),
    ((0.52, 0.43), (0.95, 0.27), 0.14, 0.06),
    ((0.52, 0.50), (0.93, 0.42), 0.13, 0.05),
    ((0.52, 0.57), (0.86, 0.56), 0.12, 0.04),
]


def gradient(size, top, bottom, diagonal=False):
    ramp = Image.linear_gradient("L")
    if diagonal:
        ramp = ramp.resize((size * 2, size * 2)).rotate(45, resample=Image.BICUBIC).crop((size // 2, size // 2, size // 2 + size, size // 2 + size))
    else:
        ramp = ramp.resize((size, size))
    return Image.composite(Image.new("RGBA", (size, size), bottom + (255,)), Image.new("RGBA", (size, size), top + (255,)), ramp)


def layer(base, colour_or_image, mask):
    fill = colour_or_image if isinstance(colour_or_image, Image.Image) else Image.new("RGBA", base.size, colour_or_image)
    base.alpha_composite(Image.composite(fill, Image.new("RGBA", base.size, (0, 0, 0, 0)), mask))


def feather(root, tip, width, arch, n, mirror, steps=32):
    # A feather from its root to its tip: a quadratic curve bent upwards, about as wide all along, its tip rounded.
    (x0, y0), (x1, y1) = root, tip
    length = math.hypot(x1 - x0, y1 - y0)
    # The control point lifted off the straight line, upwards (to smaller y).
    nx, ny = (y1 - y0) / length, -(x1 - x0) / length
    if ny > 0:
        nx, ny = -nx, -ny
    cx, cy = (x0 + x1) / 2 + nx * arch * length, (y0 + y1) / 2 + ny * arch * length
    upper, lower = [], []
    for i in range(steps + 1):
        t = i / steps
        px = (1 - t) ** 2 * x0 + 2 * (1 - t) * t * cx + t * t * x1
        py = (1 - t) ** 2 * y0 + 2 * (1 - t) * t * cy + t * t * y1
        tx = 2 * (1 - t) * (cx - x0) + 2 * t * (x1 - cx)
        ty = 2 * (1 - t) * (cy - y0) + 2 * t * (y1 - cy)
        tl = math.hypot(tx, ty)
        half = width / 2 * (1 - t ** 3) ** 0.5  # about as wide all along, rounding off at the tip
        upper.append((px - ty / tl * half, py + tx / tl * half))
        lower.append((px + ty / tl * half, py - tx / tl * half))
    points = upper + lower[::-1]
    return [((1 - x) * n if mirror else x * n, y * n) for x, y in points]


def tile_and_glow(image, size, n, u):
    tile = Image.new("L", (n, n), 0)
    ImageDraw.Draw(tile).rounded_rectangle((0, 0, n - 1, n - 1), radius=0.22 * n, fill=255)
    layer(image, gradient(n, TILE_TOP, TILE_BOTTOM, diagonal=True), tile)
    if size >= 32:
        edge = Image.new("L", (n, n), 0)
        width = max(u, 0.022 * n)
        ImageDraw.Draw(edge).rounded_rectangle((width / 2, width / 2, n - 1 - width / 2, n - 1 - width / 2), radius=0.22 * n - width / 2, outline=150, width=round(width))
        layer(image, TILE_EDGE + (255,), edge)
    if size >= 64:
        gloss = Image.new("L", (n, n), 0)
        ImageDraw.Draw(gloss).ellipse((-0.25 * n, -0.62 * n, 1.25 * n, 0.42 * n), fill=26)
        layer(image, (255, 255, 255, 255), ImageChops.multiply(gloss, tile))
    if size >= 48:
        glow = Image.radial_gradient("L").resize((n, n))
        glow = glow.point(lambda v: max(0, 255 - v * 2) * 0.55)
        layer(image, GLOW + (255,), ImageChops.multiply(glow, tile))
    return tile


def cross(image, size, n, factor, thickness, length, edge):
    # Arms `thickness` of the tile thick and `length` long, edges on whole pixels (an even number of pixels thick, so it
    # centres on a pixel line).
    mask = Image.new("L", (n, n), 0)
    draw = ImageDraw.Draw(mask)
    arm = 2 * round(size * thickness / 2 + 0.01) * factor
    low = round(size * (1 - length) / 2 + 0.01) * factor
    high = n - low
    radius = 0.06 * n
    draw.rounded_rectangle((low, (n - arm) / 2, high - 1, (n + arm) / 2 - 1), radius=radius, fill=255)
    draw.rounded_rectangle(((n - arm) / 2, low, (n + arm) / 2 - 1, high - 1), radius=radius, fill=255)
    if edge:
        layer(image, CROSS_EDGE + (255,), mask.filter(ImageFilter.MaxFilter(int(0.03 * n) | 1)))
    layer(image, gradient(n, CROSS_TOP, CROSS_BOTTOM), mask)


def render(size, style):
    factor = max(4, 1024 // size)
    n = size * factor
    u = n / size  # one target pixel
    image = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    tile = tile_and_glow(image, size, n, u)

    if style == "wings" and size >= 48:
        # Gold wings behind the cross, each feather outlined in dark brown, the upper ones over the lower.
        wing_fill = gradient(n, WING_TOP, WING_BOTTOM)
        outline = int(0.035 * n) | 1
        for root, tip, width, arch in reversed(FEATHERS):
            for mirror in (False, True):
                mask = Image.new("L", (n, n), 0)
                ImageDraw.Draw(mask).polygon(feather(root, tip, width, arch, n, mirror), fill=255)
                layer(image, WING_EDGE + (255,), ImageChops.multiply(mask.filter(ImageFilter.MaxFilter(outline)), tile))
                layer(image, wing_fill, mask)
        cross(image, size, n, factor, thickness=0.2, length=0.56, edge=True)
    else:
        cross(image, size, n, factor, thickness=0.25, length=0.75, edge=size >= 48)
        # The heartbeat: flat, a spike up, a deeper one down, back to flat; outlined so it reads on the green. Not below
        # 64 px, where it would only clutter the cross.
        if style == "cross" and size >= 64:
            points = [(0.08, 0.5), (0.34, 0.5), (0.42, 0.3), (0.52, 0.72), (0.6, 0.42), (0.66, 0.5), (0.92, 0.5)]
            line = [(x * n, y * n) for x, y in points]
            outline = Image.new("L", (n, n), 0)
            ImageDraw.Draw(outline).line(line, fill=255, width=round(0.11 * n), joint="curve")
            layer(image, PULSE_EDGE + (255,), ImageChops.multiply(outline, tile))
            stroke = Image.new("L", (n, n), 0)
            ImageDraw.Draw(stroke).line(line, fill=255, width=round(0.055 * n), joint="curve")
            layer(image, PULSE + (255,), stroke)

    return image.resize((size, size), Image.BOX if size <= 32 else Image.LANCZOS)


def save(images, ico, png):
    images[256].save(ico, format="ICO", sizes=[(s, s) for s in SIZES], append_images=[images[s] for s in SIZES if s != 256])
    images[256].save(png)


def preview(styles, folder):
    # Every size at 1x on a dark and a light ground, one block per style; and the small ones blown up 8x.
    folder.mkdir(parents=True, exist_ok=True)
    shown = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    width = sum(shown) + 24 * (len(shown) + 1)
    sheet = Image.new("RGBA", (width, len(styles) * 2 * (256 + 48)), (0, 0, 0, 255))
    top = 0
    for images in styles.values():
        for ground in [(32, 32, 32, 255), (243, 243, 243, 255)]:
            sheet.paste(Image.new("RGBA", (width, 256 + 48), ground), (0, top))
            x = 24
            for size in shown:
                sheet.alpha_composite(images[size], (x, top + 24 + (256 - size) // 2))
                x += size + 24
            top += 256 + 48
    sheet.save(folder / "preview.png")
    for name, images in styles.items():
        zoom = Image.new("RGBA", (5 * (40 * 6 + 24) + 24, 40 * 6 + 48), (32, 32, 32, 255))
        x = 24
        for size in [16, 20, 24, 32, 40]:
            zoom.alpha_composite(images[size].resize((size * 6, size * 6), Image.NEAREST), (x, 24 + (40 - size) * 3))
            x += 40 * 6 + 24
        zoom.save(folder / f"preview-small-{name}.png")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--style", choices=["wings", "cross"], default="wings")
    parser.add_argument("--preview", type=pathlib.Path)
    arguments = parser.parse_args()

    styles = {style: {size: render(size, style) for size in SIZES} for style in ["wings", "cross"]}
    assets = ROOT / "assets"
    for style, images in styles.items():
        save(images, assets / f"Aion2Heal-{style}.ico", assets / f"Aion2Heal-{style}.png")
    shutil.copyfile(assets / f"Aion2Heal-{arguments.style}.ico", assets / "Aion2Heal.ico")
    shutil.copyfile(assets / f"Aion2Heal-{arguments.style}.png", assets / "Aion2Heal.png")
    shutil.copyfile(assets / f"Aion2Heal-{arguments.style}.png", ROOT / "src" / "Aion2Flow" / "Assets" / "Images" / "Aion2Heal.png")
    if arguments.preview is not None:
        preview(styles, arguments.preview)


main()
