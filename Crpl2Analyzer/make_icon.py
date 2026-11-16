import sys
from PIL import Image, ImageDraw

S = 256
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# rounded background
d.rounded_rectangle([8, 8, S - 8, S - 8], radius=56, fill=(29, 33, 44, 255))
d.rounded_rectangle([8, 8, S - 8, S - 8], radius=56, outline=(58, 66, 84, 255), width=3)

# donut slices
cx = cy = S // 2
outer, inner = 88, 52
colors = [(61, 190, 122), (244, 206, 96), (232, 138, 62), (224, 91, 79)]
vals = [0.62, 0.18, 0.12, 0.08]
start = -90.0
for c, v in zip(colors, vals):
    sweep = 360.0 * v
    d.pieslice([cx - outer, cy - outer, cx + outer, cy + outer], start, start + sweep, fill=c + (255,))
    start += sweep
# hole
d.ellipse([cx - inner, cy - inner, cx + inner, cy + inner], fill=(29, 33, 44, 255))
d.ellipse([cx - inner, cy - inner, cx + inner, cy + inner], outline=(58, 66, 84, 255), width=2)
# center tick
d.rounded_rectangle([cx - 3, cy - 26, cx + 3, cy + 6], radius=3, fill=(232, 236, 244, 255))
d.ellipse([cx - 6, cy + 10, cx + 6, cy + 22], fill=(86, 166, 226, 255))

img.save(sys.argv[1], sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
img.resize((256, 256)).save(sys.argv[2])
print("ok")
