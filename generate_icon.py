#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""为「文件台账工具 Ava」生成应用图标。
设计：蓝色 Fluent 圆角磁贴 + 白色公文（表头 + 台账横线）+ 右下角扫描徽标。
输出：Assets/AvaIcon.ico（多尺寸 16~256）+ Assets/AvaIcon_512.png。
重新运行即可，改下面的配色/尺寸即可微调。
"""
from PIL import Image, ImageDraw

SS = 2048  # 主画布（超采样，保证缩小后边缘平滑）

# ── 配色（与 App 的蓝系主题一致）──
BG_TOP = (59, 130, 246)    # #3B82F6
BG_BOT = (29, 78, 216)     # #1D4ED8
DOC = (255, 255, 255, 255)
BAND = (12, 68, 124, 255)  # #0C447C 表头
LINE = (203, 213, 225, 255) # #CBD5E1 台账横线
ACCENT = (12, 68, 124, 255)  # 徽标内图形

# ── 1) 背景渐变磁贴（圆角）──
grad = Image.new("RGBA", (1, SS))
gp = grad.load()
for y in range(SS):
    t = y / (SS - 1)
    gp[0, y] = (
        int(BG_TOP[0] + (BG_BOT[0] - BG_TOP[0]) * t),
        int(BG_TOP[1] + (BG_BOT[1] - BG_TOP[1]) * t),
        int(BG_TOP[2] + (BG_BOT[2] - BG_TOP[2]) * t),
        255,
    )
grad = grad.resize((SS, SS), Image.BICUBIC)

mask = Image.new("L", (SS, SS), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, SS - 1, SS - 1], radius=380, fill=255)
img = Image.new("RGBA", (SS, SS))
img.paste(grad, (0, 0), mask)

d = ImageDraw.Draw(img)

# ── 2) 白色公文 ──
doc = [560, 470, 1488, 1620]
d.rounded_rectangle(doc, radius=90, fill=DOC)

# 表头块（内缩，避免与公文圆角冲突）
band = [doc[0] + 70, doc[1] + 70, doc[2] - 70, doc[1] + 70 + 300]
d.rounded_rectangle(band, radius=70, fill=BAND)

# 台账横线
ly = band[3] + 150
while ly < doc[3] - 130:
    d.rounded_rectangle([doc[0] + 150, ly, doc[2] - 520, ly + 34], radius=17, fill=LINE)
    ly += 175

# ── 3) 右下角扫描徽标（白底圆 + 蓝色放大镜）──
cx, cy, r = 1360, 1510, 250
d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=DOC)
# 放大镜圆环
rr, w = 120, 56
d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=ACCENT, width=w)
# 放大镜手柄（45°）
import math
hx = cx + rr * math.cos(math.radians(45))
hy = cy + rr * math.sin(math.radians(45))
d.line([(hx, hy), (hx + 130, hy + 130)], fill=ACCENT, width=w)

# ── 4) 导出 ──
master = img
png512 = master.resize((512, 512), Image.LANCZOS)
ico_sizes = [(s, s) for s in (16, 24, 32, 48, 64, 128, 256)]

import os
out_dir = os.path.join(os.path.dirname(__file__), "Assets")
os.makedirs(out_dir, exist_ok=True)
ico_path = os.path.join(out_dir, "AvaIcon.ico")
png_path = os.path.join(out_dir, "AvaIcon_512.png")

master.save(ico_path, sizes=ico_sizes)
png512.save(png_path)

print("已生成：")
print(" ", ico_path)
print(" ", png_path)
print(" ICO 内置尺寸：", [f"{w}x{h}" for w, h in ico_sizes])
