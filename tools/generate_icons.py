#!/usr/bin/env python3
"""Generate the ScreenCurtain brand icon and tray status icons.

Requires Pillow.  The generated files are committed so the release build only
needs the in-box C# compiler.
"""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets" / "icon"
SRC = ROOT / "src"
RES = SRC / "Resources"
ASSETS.mkdir(parents=True, exist_ok=True)
RES.mkdir(parents=True, exist_ok=True)

S = 1024
NAVY = (11, 27, 43, 255)
NAVY_LIGHT = (23, 54, 75, 255)
INK = (6, 15, 24, 255)
CYAN = (76, 194, 255, 255)
IVORY = (244, 233, 213, 255)
IVORY_DARK = (218, 201, 169, 255)


def rounded(draw, box, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def make_master(seam_color=CYAN):
    image = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(image)

    # App tile.
    rounded(d, (44, 44, 980, 980), 218, NAVY, (30, 65, 88, 255), 7)
    # Soft inner edge.
    rounded(d, (60, 60, 964, 964), 202, None, (20, 45, 62, 255), 3)

    # Monitor outline.
    rounded(d, (104, 154, 920, 804), 82, (9, 25, 39, 255), (55, 97, 121, 255), 10)
    rounded(d, (136, 184, 888, 772), 58, INK)

    # Curtain panels inside the screen.
    rounded(d, (162, 210, 490, 744), 28, IVORY)
    rounded(d, (534, 210, 862, 744), 28, IVORY_DARK)
    # Fold highlights, kept broad enough to survive 16 px downsizing.
    d.rounded_rectangle((190, 238, 218, 716), radius=14, fill=(255, 250, 239, 255))
    d.rounded_rectangle((560, 238, 588, 716), radius=14, fill=(238, 226, 202, 255))
    # A slim cyan seam signals "screen off".
    rounded(d, (500, 202, 524, 752), 12, seam_color)

    # Monitor stand and base.
    rounded(d, (456, 794, 568, 878), 26, (18, 44, 61, 255))
    rounded(d, (338, 868, 686, 916), 24, (18, 44, 61, 255), (55, 97, 121, 255), 5)
    return image


def save_ico(image, path, sizes):
    image.save(path, format="ICO", sizes=[(s, s) for s in sizes])


def main():
    master = make_master()
    master.save(ASSETS / "icon-1024.png")
    save_ico(master, SRC / "App.ico", [16, 20, 24, 32, 40, 48, 64, 128, 256])
    save_ico(master, ASSETS / "App.ico", [16, 20, 24, 32, 40, 48, 64, 128, 256])

    tray_specs = [
        ("tray-active", (63, 185, 80, 255)),
        ("tray-degraded", (227, 160, 8, 255)),
        ("tray-off", (139, 148, 158, 255)),
    ]
    for name, color in tray_specs:
        tray = make_master(color)
        save_ico(tray, RES / (name + ".ico"), [16, 20, 24, 32])
        save_ico(tray, ASSETS / (name + ".ico"), [16, 20, 24, 32])

    # Preview sheet on light and dark surfaces.
    for mode, bg in (("light", (243, 243, 243, 255)), ("dark", (22, 28, 34, 255))):
        sheet = Image.new("RGBA", (1000, 300), bg)
        for i, size in enumerate((16, 24, 32, 48, 64, 96, 128)):
            icon = master.resize((size, size), Image.LANCZOS)
            x = 40 + i * 132
            y = (300 - size) // 2
            sheet.alpha_composite(icon, (x, y))
        sheet.save(ASSETS / ("preview-" + mode + ".png"))

    print("Generated:")
    for p in sorted(ASSETS.glob("*")) + sorted(RES.glob("tray-*.ico")):
        print(" ", p.relative_to(ROOT))


if __name__ == "__main__":
    main()
