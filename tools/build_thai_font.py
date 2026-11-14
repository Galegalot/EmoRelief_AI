"""
Builds Assets/Resources/Fonts/EmoThai-Regular.ttf from Mitr-Regular (SIL OFL 1.1).

Unity IMGUI renders text without OpenType shaping, so Thai tone marks collide with
upper vowels and marks overlap the ascenders of ป ฝ ฟ ฬ. This script exposes the
font's positional variants (normally reachable only through GPOS/GSUB) at fixed
private-use codepoints. ThaiText.cs on the Unity side substitutes them per cluster.

Private-use layout (must match ThaiText.cs):
  U+E000..E004  tone marks 0E48..0E4C, high (stacked above an upper vowel)
  U+E010..E014  tone marks, high + shifted left (ascender consonant)
  U+E020..E024  tone marks, low + shifted left (ascender consonant)
  U+E030..E036  upper vowels 0E31,0E34,0E35,0E36,0E37,0E47,0E4D shifted left
  U+E040..E043  ฎ ฏ without descender, ญ ฐ without lower part (before a lower vowel)

Usage:  python Tools/build_thai_font.py
"""

import os
import urllib.request

from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CACHE = os.path.join(ROOT, "Tools", "cache")
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Fonts")
SOURCE_URL = "https://github.com/google/fonts/raw/main/ofl/mitr/Mitr-Regular.ttf"
LICENSE_URL = "https://github.com/google/fonts/raw/main/ofl/mitr/OFL.txt"
FAMILY = "EmoRelief Thai"

# Horizontal shift (font units) that moves an upper mark clear of an ascender,
# derived from Mitr's GPOS base anchors for ป/ฝ (362-626+60 = -204, 384-628+60 = -184).
ASCENDER_SHIFT = -195


def fetch(url, path):
    if not os.path.exists(path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        urllib.request.urlretrieve(url, path)
    return path


def main():
    source = fetch(SOURCE_URL, os.path.join(CACHE, "Mitr-Regular.ttf"))
    license_path = fetch(LICENSE_URL, os.path.join(CACHE, "OFL-Mitr.txt"))

    font = TTFont(source)
    glyf = font["glyf"]
    hmtx = font["hmtx"]
    cmap = font.getBestCmap()
    order = list(font.getGlyphOrder())
    glyf.glyphOrder = order
    mapping = {}

    def add_shifted(name, base, dx):
        glyph = Glyph()
        component = GlyphComponent()
        component.glyphName = base
        component.x, component.y = dx, 0
        component.flags = 0x4  # ROUND_XY_TO_GRID
        glyph.components = [component]
        glyph.numberOfContours = -1
        glyf[name] = glyph
        glyph.recalcBounds(glyf)
        hmtx[name] = (0, getattr(glyph, "xMin", 0))
        if name not in order:
            order.append(name)
        return name

    tones = [0x0E48, 0x0E49, 0x0E4A, 0x0E4B, 0x0E4C]
    for index, tone in enumerate(tones):
        small = cmap[tone] + ".small"
        if small not in glyf:
            raise SystemExit("Missing glyph " + small)
        mapping[0xE000 + index] = small
        mapping[0xE010 + index] = add_shifted("emo.toneHighLeft%d" % index, small, ASCENDER_SHIFT)
        mapping[0xE020 + index] = add_shifted("emo.toneLowLeft%d" % index, cmap[tone], ASCENDER_SHIFT)

    uppers = [0x0E31, 0x0E34, 0x0E35, 0x0E36, 0x0E37, 0x0E47, 0x0E4D]
    for index, vowel in enumerate(uppers):
        mapping[0xE030 + index] = add_shifted("emo.upperLeft%d" % index, cmap[vowel], ASCENDER_SHIFT)

    for code, name in zip(range(0xE040, 0xE044), ["doChadathai.short", "toPatakthai.short", "yoYingthai.less", "thoThanthai.less"]):
        if name not in glyf:
            raise SystemExit("Missing glyph " + name)
        mapping[code] = name

    font.setGlyphOrder(order)
    for table in font["cmap"].tables:
        if table.isUnicode():
            table.cmap.update(mapping)

    # OFL: modified versions must not reuse the original name.
    for record in font["name"].names:
        if record.nameID in (1, 16):
            record.string = FAMILY
        elif record.nameID == 4:
            record.string = FAMILY + " Regular"
        elif record.nameID == 6:
            record.string = "EmoReliefThai-Regular"

    os.makedirs(OUT_DIR, exist_ok=True)
    font.save(os.path.join(OUT_DIR, "EmoThai-Regular.ttf"))
    with open(license_path, "rb") as src, open(os.path.join(OUT_DIR, "OFL-EmoThai.txt"), "wb") as dst:
        dst.write(b"EmoThai-Regular.ttf is a modified version of Mitr-Regular.ttf (renamed per the OFL).\n\n")
        dst.write(src.read())
    print("Built EmoThai-Regular.ttf with %d positional glyphs." % len(mapping))


if __name__ == "__main__":
    main()
