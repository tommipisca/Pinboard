"""Generates the Pinboard icon (1024 PNG and multi-resolution .ico). Usage: python3 assets/make_icon.py"""
from PIL import Image, ImageDraw, ImageFilter
import os

S = 2048           # drawn at high resolution, then scaled down
OUT = os.path.dirname(os.path.abspath(__file__))
ICO = os.path.join(OUT, "..", "src", "Pinboard", "Assets", "Pinboard.ico")


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def gradient(size, top, bottom):
    img = Image.new("RGB", (size, size))
    px = img.load()
    for y in range(size):
        c = lerp(top, bottom, y / (size - 1))
        for x in range(size):
            px[x, y] = c
    return img


def rounded_mask(size, radius):
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size - 1, size - 1], radius, fill=255)
    return m


# Background: ink-blue rounded square with a light gradient.
base = gradient(S, (0x2A, 0x3A, 0x6B), (0x0F, 0x17, 0x28)).convert("RGBA")
icon = Image.new("RGBA", (S, S), (0, 0, 0, 0))
icon.paste(base, (0, 0), rounded_mask(S, int(S * 0.225)))

# Three "widget" cards: two squares on top, a wide one below.
pad = int(S * 0.16)
gap = int(S * 0.05)
w = S - 2 * pad
sq = (w - gap) // 2
h_top = sq
h_bot = int(S * 0.20)
r = int(S * 0.055)
y0 = (S - (h_top + gap + h_bot)) // 2  # group centered vertically

cards = [
    ((pad, y0, pad + sq, y0 + h_top), (0xEC, 0xE8, 0xDD)),                     # paper
    ((pad + sq + gap, y0, pad + w, y0 + h_top), (0x4F, 0xE3, 0xA6)),           # emerald
    ((pad, y0 + h_top + gap, pad + w, y0 + h_top + gap + h_bot), (0x9A, 0xA5, 0xFF)),  # indigo
]

# Soft shadows under the cards.
shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
sd = ImageDraw.Draw(shadow)
for box, _ in cards:
    x0, y0_, x1, y1 = box
    sd.rounded_rectangle([x0, y0_ + int(S * 0.012), x1, y1 + int(S * 0.012)], r, fill=(0, 0, 0, 120))
shadow = shadow.filter(ImageFilter.GaussianBlur(S * 0.012))
icon = Image.alpha_composite(icon, shadow)

d = ImageDraw.Draw(icon)
for box, color in cards:
    d.rounded_rectangle(box, r, fill=color + (255,))

# Detail on the paper card: the hands of a clock.
cx0, cy0, cx1, cy1 = cards[0][0]
cx, cy = (cx0 + cx1) // 2 - int(S * 0.03), (cy0 + cy1) // 2 + int(S * 0.035)
ink = (0x0F, 0x17, 0x28, 255)
lw = int(S * 0.018)
d.line([cx, cy, cx, cy - int(S * 0.095)], fill=ink, width=lw)
d.line([cx, cy, cx + int(S * 0.065), cy + int(S * 0.02)], fill=ink, width=lw)
d.ellipse([cx - lw, cy - lw, cx + lw, cy + lw], fill=(0xFF, 0x9F, 0x0A, 255))

# On the emerald card: a check mark.
sx0, sy0, sx1, sy1 = cards[1][0]
mx, my = (sx0 + sx1) // 2, (sy0 + sy1) // 2
tw = int(S * 0.024)
pts = [(mx - int(S * 0.07), my + int(S * 0.005)), (mx - int(S * 0.02), my + int(S * 0.055)), (mx + int(S * 0.08), my - int(S * 0.06))]
d.line(pts, fill=ink, width=tw, joint="curve")
for p in (pts[0], pts[2]):
    d.ellipse([p[0] - tw // 2, p[1] - tw // 2, p[0] + tw // 2, p[1] + tw // 2], fill=ink)

# On the wide card: two lines of text.
bx0, by0, bx1, by1 = cards[2][0]
lh = int(S * 0.026)
d.rounded_rectangle([bx0 + int(S * 0.06), by0 + int(S * 0.045), bx0 + int(S * 0.45), by0 + int(S * 0.045) + lh], lh // 2, fill=ink)
d.rounded_rectangle([bx0 + int(S * 0.06), by0 + int(S * 0.045) + int(S * 0.05), bx0 + int(S * 0.30), by0 + int(S * 0.045) + int(S * 0.05) + lh], lh // 2, fill=(0x0F, 0x17, 0x28, 150))

# Red push pin in the corner of the wide card, like on a pinboard.
px_, py_ = bx1 - int(S * 0.075), by0 + int(S * 0.075)
pr = int(S * 0.04)
d.ellipse([px_ - pr, py_ - pr + int(S * 0.006), px_ + pr, py_ + pr + int(S * 0.006)], fill=(0x0F, 0x17, 0x28, 70))
d.ellipse([px_ - pr, py_ - pr, px_ + pr, py_ + pr], fill=(0xFF, 0x3B, 0x30, 255))
d.ellipse([px_ - pr // 2, py_ - pr // 2 - int(S * 0.006), px_ - pr // 8, py_ - pr // 8 - int(S * 0.006)], fill=(255, 255, 255, 140))

final = icon.resize((1024, 1024), Image.LANCZOS)
final.save(os.path.join(OUT, "pinboard-icon-1024.png"))

os.makedirs(os.path.dirname(ICO), exist_ok=True)
sizes = [16, 24, 32, 48, 64, 128, 256]
final.save(ICO, format="ICO", sizes=[(s, s) for s in sizes])
print("ok:", ICO)
