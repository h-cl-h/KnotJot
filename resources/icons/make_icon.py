# -*- coding: utf-8 -*-
"""生成 KnotJot App 图标 (icon.ico + 多尺寸 PNG)。
图形:蓝色圆角方块 + 白色线条(长斜线 + 短斜线与垂直线连成的圆角连续笔画)。
纯几何绘制,原创美术。"""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = HERE
os.makedirs(ASSETS, exist_ok=True)

BLUE = (91, 108, 240, 255)   # #5B6CF0
WHITE = (255, 255, 255, 255)
R = 1024
s = R / 280.0


def P(x, y):
    return (x * s, y * s)


def draw_icon(size):
    img = Image.new("RGBA", (R, R), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, R - 1, R - 1], radius=int(64 * s), fill=BLUE)
    w = int(round(22 * s))
    r = w / 2.0
    path = [P(224, 144), P(140, 60), P(140, 220), P(56, 136)]
    d.line(path, fill=WHITE, width=w, joint="curve")
    longline = [P(222, 58), P(58, 222)]
    d.line(longline, fill=WHITE, width=w)
    for (x, y) in path + longline:           # 圆角端点/接点
        d.ellipse([x - r, y - r, x + r, y + r], fill=WHITE)
    if size != R:
        img = img.resize((size, size), Image.LANCZOS)
    return img


base = draw_icon(R)
ico_path = os.path.join(HERE, "icon.ico")
base.save(ico_path, sizes=[(16, 16), (24, 24), (32, 32), (48, 48),
                           (64, 64), (128, 128), (256, 256)])
print("wrote", ico_path)

for px in (1024, 512, 256, 128):
    p = os.path.join(ASSETS, f"icon-{px}.png")
    draw_icon(px).save(p)
    print("wrote", p)
