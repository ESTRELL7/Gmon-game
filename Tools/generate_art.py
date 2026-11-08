"""Generates the prototype's pixel art and sounds using only the Python standard library.

Run from the project root:  python Tools/generate_art.py
The dwarf frames come from Art/Source/dwarf.aseprite, everything else is placeholder art
that can be redrawn by hand: keep the file names and sizes and Unity picks the new art up.
"""
import math, os, random, struct, sys, wave, zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, 'Assets', 'Resources', 'Art')
AUDIO = os.path.join(ROOT, 'Assets', 'Resources', 'Audio')
TILE = 24
CLEAR = (0, 0, 0, 0)


class Img:
    def __init__(self, w, h, fill=CLEAR):
        self.w, self.h = w, h
        self.px = [fill] * (w * h)

    def get(self, x, y):
        return self.px[y * self.w + x]

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[y * self.w + x] = c if len(c) == 4 else (*c, 255)

    def rect(self, x, y, w, h, c):
        for j in range(y, y + h):
            for i in range(x, x + w):
                self.set(i, j, c)

    def paste(self, other, ox, oy):
        for y in range(other.h):
            for x in range(other.w):
                c = other.get(x, y)
                if c[3]:
                    self.set(ox + x, oy + y, c)

    def save(self, name):
        rows = bytearray()
        for y in range(self.h):
            rows.append(0)
            for x in range(self.w):
                rows.extend(self.px[y * self.w + x])

        def chunk(t, b):
            return struct.pack('>I', len(b)) + t + b + struct.pack('>I', zlib.crc32(t + b))

        data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', self.w, self.h, 8, 6, 0, 0, 0))
        data += chunk(b'IDAT', zlib.compress(bytes(rows), 9)) + chunk(b'IEND', b'')
        with open(os.path.join(ART, name + '.png'), 'wb') as f:
            f.write(data)


def from_rows(rows, palette):
    img = Img(max(len(r) for r in rows), len(rows))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in palette:
                img.set(x, y, palette[ch])
    return img


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c[:3])


def read_aseprite(path):
    """Returns the first frame of an RGBA .aseprite file, composited."""
    d = open(path, 'rb').read()
    frames, w, h, depth = struct.unpack_from('<HHHH', d, 6)
    if depth != 32:
        sys.exit('dwarf.aseprite must use RGBA color mode')
    img = Img(w, h)
    pos = 128
    fbytes, _, oldchunks, _ = struct.unpack_from('<IHHH', d, pos)
    n = struct.unpack_from('<I', d, pos + 12)[0] or oldchunks
    p = pos + 16
    for _ in range(n):
        csize, ctype = struct.unpack_from('<IH', d, p)
        if ctype == 0x2005:
            _, x, y, _, ct = struct.unpack_from('<HhhBH', d, p + 6)
            cw, ch = struct.unpack_from('<HH', d, p + 22)
            raw = d[p + 26:p + csize]
            raw = zlib.decompress(raw) if ct == 2 else raw
            for cy in range(ch):
                for cx in range(cw):
                    o = (cy * cw + cx) * 4
                    if raw[o + 3]:
                        img.set(x + cx, y + cy, tuple(raw[o:o + 4]))
        p += csize
    return img


def dwarf_frames():
    src = read_aseprite(os.path.join(ROOT, 'Art', 'Source', 'dwarf.aseprite'))
    xs = [i % src.w for i, c in enumerate(src.px) if c[3]]
    ys = [i // src.w for i, c in enumerate(src.px) if c[3]]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    bw, bh = x1 - x0 + 1, y1 - y0 + 1
    body = Img(bw, bh)
    for y in range(bh):
        for x in range(bw):
            body.set(x, y, src.get(x0 + x, y0 + y))
    ox, oy = (TILE - bw) // 2, TILE - bh

    def frame(fn):
        out = Img(TILE, TILE)
        for y in range(bh):
            for x in range(bw):
                c = body.get(x, y)
                if c[3]:
                    dx, dy = fn(x, y)
                    out.set(ox + x + dx, oy + y + dy, c)
        return out

    legs = bh - 5
    mid = bw // 2
    frame(lambda x, y: (0, 0)).save('dwarf_idle')
    # Breathing: everything above the legs sinks by a pixel.
    frame(lambda x, y: (0, 1 if y < legs else 0)).save('dwarf_breath')
    # Walking: one foot lifts while the body bobs.
    frame(lambda x, y: (0, -1 if y >= legs and x < mid else 0)).save('dwarf_walk1')
    frame(lambda x, y: (0, -1 if y >= legs and x >= mid else 0)).save('dwarf_walk2')


# name, base color, speck color, style
LAYERS = [
    ('soil', (122, 78, 44), (92, 56, 32), 'soft'),
    ('clay', (160, 94, 58), (128, 70, 44), 'strata'),
    ('stone', (112, 112, 118), (84, 84, 92), 'cracked'),
    ('granite', (136, 112, 112), (92, 72, 78), 'speck'),
    ('slate', (70, 82, 104), (50, 60, 82), 'strata'),
    ('basalt', (56, 54, 66), (38, 36, 48), 'cracked'),
    ('magma', (84, 44, 40), (232, 96, 36), 'veins'),
    ('crystal', (70, 58, 112), (150, 120, 220), 'speck'),
    ('abyss', (30, 24, 48), (96, 40, 140), 'veins'),
]


def make_tile(base, speck, style, seed):
    r = random.Random(seed)
    img = Img(TILE, TILE)
    for y in range(TILE):
        for x in range(TILE):
            n = r.random()
            k = 1.0 + (0.06 if n > 0.8 else -0.06 if n < 0.2 else 0)
            if style == 'strata':
                k *= 1.08 if (y + (x // 8 + seed) % 2) % 6 < 2 else 0.96
            img.set(x, y, shade(base, k))
    if style in ('soft', 'speck'):
        for _ in range(16 if style == 'speck' else 9):
            x, y = r.randrange(1, TILE - 2), r.randrange(1, TILE - 2)
            img.rect(x, y, 2, 1 + r.randrange(2), speck)
    if style in ('cracked', 'veins'):
        for _ in range(3):
            x, y = r.randrange(TILE), r.randrange(TILE)
            for _ in range(r.randrange(6, 13)):
                img.set(x, y, speck)
                x += r.choice((-1, 0, 1))
                y += r.choice((0, 1))
    # Bevel: blocks read as separate chunks.
    for i in range(TILE):
        img.set(i, 0, shade(img.get(i, 0), 1.2))
        img.set(0, i, shade(img.get(0, i), 1.12))
        img.set(i, TILE - 1, shade(img.get(i, TILE - 1), 0.72))
        img.set(TILE - 1, i, shade(img.get(TILE - 1, i), 0.8))
    return img


def make_grass(seed):
    r = random.Random(seed)
    img = make_tile(*LAYERS[0][1:], seed)
    for x in range(TILE):
        h = 4 + r.randrange(3)
        for y in range(h):
            img.set(x, y, (98, 168, 62) if y < h - 2 else (70, 130, 48))
        img.set(x, 0, (136, 204, 84))
    return img


# id: (dark, mid, light)
ORES = [
    ('coal', (24, 24, 28), (46, 46, 54), (84, 84, 96), 'chunk'),
    ('copper', (140, 74, 34), (206, 120, 60), (246, 176, 112), 'chunk'),
    ('iron', (120, 96, 88), (182, 158, 146), (232, 220, 212), 'chunk'),
    ('silver', (120, 134, 150), (190, 206, 220), (248, 252, 255), 'chunk'),
    ('gold', (176, 112, 20), (244, 190, 40), (255, 240, 140), 'chunk'),
    ('ruby', (130, 16, 44), (222, 38, 72), (255, 150, 170), 'gem'),
    ('sapphire', (20, 48, 140), (44, 104, 232), (160, 208, 255), 'gem'),
    ('emerald', (12, 102, 60), (34, 190, 104), (170, 255, 200), 'gem'),
    ('diamond', (70, 150, 190), (150, 226, 244), (255, 255, 255), 'gem'),
    ('mithril', (40, 130, 130), (80, 226, 200), (220, 255, 244), 'bar'),
    ('adamantite', (110, 20, 130), (200, 50, 220), (255, 170, 255), 'bar'),
]


def make_gem(dark, mid, light, x, y, size):
    img = Img(size, size)
    c = (size - 1) / 2
    for j in range(size):
        for i in range(size):
            if abs(i - c) + abs(j - c) <= c + 0.1:
                col = light if i + j < size - 2 else mid if i + j < size + 1 else dark
                img.set(i, j, col)
    return img


def make_ore(dark, mid, light, kind, seed):
    r = random.Random(seed)
    img = Img(TILE, TILE)
    spots = [(3, 3), (13, 2), (8, 10), (2, 15), (15, 14)]
    for sx, sy in spots:
        sx += r.randrange(-1, 2)
        sy += r.randrange(-1, 2)
        if kind == 'chunk':
            img.rect(sx, sy + 1, 5, 2, mid)
            img.rect(sx + 1, sy, 3, 4, mid)
            img.rect(sx + 1, sy, 2, 1, light)
            img.set(sx, sy + 1, light)
            img.rect(sx + 1, sy + 3, 3, 1, dark)
            img.set(sx + 4, sy + 2, dark)
        elif kind == 'gem':
            img.paste(make_gem(dark, mid, light, 0, 0, 7), sx, sy)
        else:
            img.rect(sx, sy, 6, 4, mid)
            img.rect(sx, sy, 6, 1, light)
            img.rect(sx, sy + 3, 6, 1, dark)
    return img


def make_ore_icon(dark, mid, light, kind):
    if kind == 'gem':
        return make_gem(dark, mid, light, 0, 0, 16)
    img = Img(16, 16)
    img.rect(2, 4, 12, 9, mid)
    img.rect(4, 2, 8, 12, mid)
    img.rect(4, 2, 7, 2, light)
    img.rect(2, 4, 2, 4, light)
    img.rect(4, 13, 9, 1, dark)
    img.rect(13, 8, 1, 5, dark)
    return img


def make_cracks():
    r = random.Random(7)
    lines = []
    for stage in range(3):
        for _ in range(2 + stage):
            x, y = r.randrange(6, 18), r.randrange(6, 18)
            pts = []
            for _ in range(5 + stage * 3):
                pts.append((x, y))
                x += r.choice((-1, 0, 1))
                y += r.choice((-1, 0, 1))
            lines.append(pts)
        img = Img(TILE, TILE)
        for pts in lines:
            for x, y in pts:
                img.set(x, y, (10, 8, 12, 220))
        img.save(f'crack_{stage}')


def make_ladder():
    wood, dark = (168, 116, 62), (104, 66, 34)
    img = Img(TILE, TILE)
    for x in (6, 16):
        img.rect(x, 0, 2, TILE, wood)
        img.rect(x + 1, 0, 1, TILE, dark)
    for y in (3, 11, 19):
        img.rect(6, y, 12, 2, wood)
        img.rect(6, y + 1, 12, 1, dark)
    img.save('ladder')


PICK = [
    '.......aaaaa.....',
    '.....aabbbbbaa...',
    '...aabbbbbbbbbaa.',
    '..abbbaaaaabbbbba',
    '..aaa...hh..aabba',
    '........hd....aaa',
    '........hd.......',
    '........hd.......',
    '........hd.......',
    '........hd.......',
    '........hd.......',
    '........hd.......',
    '........hd.......',
    '........aa.......',
]


def make_pick():
    img = from_rows(PICK, {'a': (70, 78, 92), 'b': (176, 190, 206),
                           'h': (156, 100, 52), 'd': (98, 60, 30)})
    out = Img(TILE, TILE)
    out.paste(img, (TILE - img.w) // 2, (TILE - img.h) // 2)
    out.save('pick')


def make_dark():
    """Lantern light: a dithered hole in the dark. 8 px = 1 world unit at scale 1."""
    size, hole = 128, 16.0
    bayer = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
    img = Img(size, size)
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - size / 2 + .5, y - size / 2 + .5) / hole
            t = (d - 0.75) / 0.6
            if t >= (bayer[y % 4][x % 4] + .5) / 16:
                img.set(x, y, (8, 6, 14, 255))
    img.save('dark')


def make_ui():
    # 9-sliced panel and button frames; borders are 4 px wide in the importer.
    def frame(name, fill, light, dark, edge=(16, 12, 20)):
        img = Img(16, 16, (*fill, 255))
        img.rect(1, 1, 14, 2, light)
        img.rect(1, 1, 2, 14, light)
        img.rect(1, 13, 14, 2, dark)
        img.rect(13, 1, 2, 14, dark)
        for i in range(16):
            for x, y in ((i, 0), (i, 15), (0, i), (15, i)):
                img.set(x, y, edge)
        for x, y in ((0, 0), (15, 0), (0, 15), (15, 15)):
            img.set(x, y, CLEAR)
        img.save(name)

    frame('ui_panel', (58, 44, 42), (92, 72, 62), (38, 28, 30))
    frame('ui_button', (150, 96, 50), (214, 160, 88), (98, 58, 30))
    frame('ui_button_down', (112, 70, 36), (98, 58, 30), (150, 96, 50))
    frame('ui_button_green', (70, 136, 66), (124, 196, 100), (42, 88, 50))
    frame('ui_button_off', (72, 68, 72), (98, 94, 98), (50, 46, 52))
    frame('ui_bar_back', (24, 20, 26), (14, 12, 16), (40, 34, 40))
    white = Img(4, 4, (255, 255, 255, 255))
    white.save('ui_white')
    # Arrow pointing up; the D-pad rotates it.
    arrow = Img(16, 16)
    for y in range(9):
        arrow.rect(7 - y // 1 if y < 7 else 1, 2 + y, (y * 2 + 2) if y < 7 else 14, 1, (250, 238, 210))
    arrow.rect(5, 9, 6, 5, (250, 238, 210))
    arrow.rect(1, 9, 4, 2, CLEAR)
    arrow.rect(11, 9, 4, 2, CLEAR)
    arrow.save('ui_arrow')


ICONS = {
    'coin': (['....aaaaaa....', '..aabbbbbbaa..', '.abbccccccbbd.', '.abccbbbbbbbd.',
              'abccbbaabbbbbd', 'abcbbbaabbbbbd', 'abcbbbaabbbbbd', 'abcbbbaabbbbbd',
              'abcbbbaabbbbbd', 'abbbbbaabbbbbd', '.abbbbbbbbbbd.', '.abbbbbbbbbdd.',
              '..ddbbbbbbdd..', '....dddddd....'],
             {'a': (176, 112, 20), 'b': (244, 190, 40), 'c': (255, 240, 140), 'd': (140, 84, 16)}),
    'bag': (['.....aaaa.....', '....a....a....', '...aaaaaaaa...', '..abbbbbbbba..',
             '.abbbbbbbbbba.', '.abccbbbbbbba.', 'abccbbddbbbbba', 'abcbbbddbbbbba',
             'abcbbbbbbbbbba', 'abbbbbbbbbbbba', 'abbbbbbbbbbbba', '.abbbbbbbbbba.',
             '..aaaaaaaaaa..'],
            {'a': (70, 42, 24), 'b': (156, 100, 52), 'c': (204, 148, 88), 'd': (240, 200, 90)}),
    'bolt': (['.......aaa', '......aba.', '.....aba..', '....abba..', '...abba...',
              '..abbaaaa.', '.abbbbbba.', '.aaaabba..', '...abba...', '..abba....',
              '..aba.....', '.aba......', '.aa.......'],
             {'a': (176, 112, 20), 'b': (255, 226, 90)}),
    'lantern': (['....aaaa....', '...a....a...', '..aaaaaaaa..', '..abccccba..', '..abcddcba..',
                 '..acddddca..', '..acddddca..', '..abcddcba..', '..abccccba..', '..aaaaaaaa..'],
                {'a': (70, 62, 66), 'b': (240, 150, 40), 'c': (255, 210, 90), 'd': (255, 250, 200)}),
    'lift': (['...aaaaaaaa...', '...a......a...', '...a.bbbb.a...', '...a.bbbb.a...',
              '.aaa.bbbb.aaa.', '.c..........c.', '.c.dddddddd.c.', '.c.deeeeeed.c.',
              '.c.deeeeeed.c.', '.c.dddddddd.c.', '.c..........c.', '.cccccccccccc.'],
             {'a': (176, 190, 206), 'b': (120, 128, 140), 'c': (98, 60, 30),
              'd': (156, 100, 52), 'e': (70, 42, 24)}),
    'clover': (['...aa..aa...', '..abba.bba..', '..abbaabba..', '...aabbaa...', '..aabbbbaa..',
                '.abbaabbbba.', '.abba..abba.', '..aa.c..aa..', '.....cc.....', '......cc....'],
               {'a': (42, 112, 54), 'b': (100, 200, 90), 'c': (70, 130, 48)}),
    'swing': (['.......aaa..', '.....aabba..', '....abbba...', '...abba.....', '.ccc.a......',
               '.....cc.....', '..ccc..c....', '.......cc...', '...c....c...', '....cc..c...'],
              {'a': (70, 78, 92), 'b': (176, 190, 206), 'c': (255, 240, 170)}),
    'sound': (['....a.....', '...aa..b..', 'aaaaa...b.', 'aaaaa.b.b.', 'aaaaa.b.b.',
               'aaaaa...b.', '...aa..b..', '....a.....'],
              {'a': (250, 238, 210), 'b': (250, 238, 210)}),
}


def make_icons():
    for name, (rows, pal) in ICONS.items():
        from_rows(rows, pal).save('icon_' + name)
    make_pick_icon()


def make_pick_icon():
    img = from_rows(PICK, {'a': (70, 78, 92), 'b': (176, 190, 206),
                           'h': (156, 100, 52), 'd': (98, 60, 30)})
    img.save('icon_pick')


def make_shop():
    wall, wall2, roof, roof2, dark = (156, 100, 52), (120, 74, 38), (170, 56, 44), (120, 36, 36), (46, 30, 28)
    img = Img(60, 52)
    img.rect(8, 22, 44, 30, wall)
    for y in range(26, 52, 6):
        img.rect(8, y, 44, 1, wall2)
    for i in range(22):
        img.rect(4 + i, 21 - i, 52 - i * 2, 1, roof if i % 3 else roof2)
    img.rect(14, 32, 16, 12, (250, 220, 130))
    img.rect(21, 32, 2, 12, dark)
    img.rect(14, 37, 16, 2, dark)
    img.rect(36, 34, 11, 18, (98, 60, 30))
    img.rect(38, 36, 7, 7, dark)
    img.rect(44, 44, 2, 2, (240, 190, 60))
    img.save('shop')


def make_cloud():
    img = Img(48, 16)
    white, soft = (246, 250, 255), (210, 226, 244)
    img.rect(6, 8, 36, 6, white)
    img.rect(12, 4, 14, 6, white)
    img.rect(24, 2, 12, 8, white)
    img.rect(6, 13, 36, 1, soft)
    img.save('cloud')


# 5x7 glyphs. Atlas cells are 6x8 and laid out in FONT_CHARS order (see PixelFont.cs).
GLYPHS = {
    'А': '01110 10001 10001 11111 10001 10001 10001', 'Б': '11111 10000 10000 11110 10001 10001 11110',
    'В': '11110 10001 10001 11110 10001 10001 11110', 'Г': '11111 10000 10000 10000 10000 10000 10000',
    'Д': '00110 01010 01010 01010 01010 11111 10001', 'Е': '11111 10000 10000 11110 10000 10000 11111',
    'Ё': '01010 11111 10000 11110 10000 10000 11111', 'Ж': '10101 10101 10101 01110 10101 10101 10101',
    'З': '01110 10001 00001 00110 00001 10001 01110', 'И': '10001 10001 10011 10101 11001 10001 10001',
    'Й': '10101 10001 10011 10101 11001 10001 10001', 'К': '10001 10010 10100 11000 10100 10010 10001',
    'Л': '00111 01001 01001 01001 01001 01001 10001', 'М': '10001 11011 10101 10101 10001 10001 10001',
    'Н': '10001 10001 10001 11111 10001 10001 10001', 'О': '01110 10001 10001 10001 10001 10001 01110',
    'П': '11111 10001 10001 10001 10001 10001 10001', 'Р': '11110 10001 10001 11110 10000 10000 10000',
    'С': '01110 10001 10000 10000 10000 10001 01110', 'Т': '11111 00100 00100 00100 00100 00100 00100',
    'У': '10001 10001 10001 01111 00001 10001 01110', 'Ф': '00100 01110 10101 10101 10101 01110 00100',
    'Х': '10001 10001 01010 00100 01010 10001 10001', 'Ц': '10010 10010 10010 10010 10010 11111 00001',
    'Ч': '10001 10001 10001 01111 00001 00001 00001', 'Ш': '10001 10001 10101 10101 10101 10101 11111',
    'Щ': '10101 10101 10101 10101 10101 11111 00001', 'Ъ': '11000 01000 01000 01110 01001 01001 01110',
    'Ы': '10001 10001 10001 11101 10011 10011 11101', 'Ь': '10000 10000 10000 11110 10001 10001 11110',
    'Э': '01110 10001 00001 00111 00001 10001 01110', 'Ю': '10010 10101 10101 11101 10101 10101 10010',
    'Я': '01111 10001 10001 01111 00101 01001 10001',
    'D': '11110 10001 10001 10001 10001 10001 11110', 'F': '11111 10000 10000 11110 10000 10000 10000',
    'G': '01110 10001 10000 10111 10001 10001 01111', 'I': '01110 00100 00100 00100 00100 00100 01110',
    'J': '00111 00010 00010 00010 00010 10010 01100', 'L': '10000 10000 10000 10000 10000 10000 11111',
    'N': '10001 11001 10101 10101 10011 10001 10001', 'Q': '01110 10001 10001 10001 10101 10010 01101',
    'R': '11110 10001 10001 11110 10100 10010 10001', 'S': '01111 10000 10000 01110 00001 00001 11110',
    'U': '10001 10001 10001 10001 10001 10001 01110', 'V': '10001 10001 10001 10001 10001 01010 00100',
    'W': '10001 10001 10001 10101 10101 11011 10001', 'Z': '11111 00001 00010 00100 01000 10000 11111',
    '0': '01110 10001 10011 10101 11001 10001 01110', '1': '00100 01100 00100 00100 00100 00100 01110',
    '2': '01110 10001 00001 00110 01000 10000 11111', '3': '11110 00001 00001 01110 00001 00001 11110',
    '4': '00010 00110 01010 10010 11111 00010 00010', '5': '11111 10000 11110 00001 00001 10001 01110',
    '6': '00110 01000 10000 11110 10001 10001 01110', '7': '11111 00001 00010 00100 01000 01000 01000',
    '8': '01110 10001 10001 01110 10001 10001 01110', '9': '01110 10001 10001 01111 00001 00010 01100',
    '.': '00000 00000 00000 00000 00000 01100 01100', ',': '00000 00000 00000 00000 01100 00100 01000',
    ':': '00000 01100 01100 00000 01100 01100 00000', '!': '00100 00100 00100 00100 00100 00000 00100',
    '?': '01110 10001 00001 00110 00100 00000 00100', '-': '00000 00000 00000 11111 00000 00000 00000',
    '+': '00000 00100 00100 11111 00100 00100 00000', '/': '00001 00010 00010 00100 01000 01000 10000',
    '%': '11001 11010 00010 00100 01000 01011 10011', '(': '00010 00100 01000 01000 01000 00100 00010',
    ')': '01000 00100 00010 00010 00010 00100 01000', '>': '01000 00100 00010 00001 00010 00100 01000',
    'x': '00000 10001 01010 00100 01010 10001 00000',
}
# Latin letters that look like a Cyrillic twin reuse its glyph at runtime (see PixelFont.cs).
FONT_CHARS = 'АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯDFGIJLNQRSUVWZ0123456789.,:!?-+/%()>x'


def make_font():
    per_row = 16
    rows = (len(FONT_CHARS) + per_row - 1) // per_row
    img = Img(per_row * 6, rows * 8)
    for i, ch in enumerate(FONT_CHARS):
        cx, cy = (i % per_row) * 6, (i // per_row) * 8
        for y, line in enumerate(GLYPHS[ch].split()):
            for x, bit in enumerate(line):
                if bit == '1':
                    img.set(cx + x, cy + y, (255, 255, 255, 255))
    img.save('font')


def write_wav(name, samples, rate=22050):
    with wave.open(os.path.join(AUDIO, name + '.wav'), 'wb') as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(rate)
        f.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, s)) * 30000)) for s in samples))


def make_sounds():
    rate = 22050
    r = random.Random(3)

    def tone(freqs, dur, vol=.5, wave_kind='square'):
        out, phase = [], 0.0
        n = int(rate * dur)
        for i in range(n):
            f = freqs[min(len(freqs) - 1, i * len(freqs) // n)]
            phase += f / rate
            s = 1 if phase % 1 < .5 else -1
            if wave_kind == 'tri':
                s = 4 * abs((phase % 1) - .5) - 1
            out.append(s * vol * (1 - i / n))
        return out

    def noise(dur, vol=.6, hold=4):
        out, v = [], 0
        n = int(rate * dur)
        for i in range(n):
            if i % hold == 0:
                v = r.uniform(-1, 1)
            out.append(v * vol * (1 - i / n) ** 2)
        return out

    write_wav('hit', [a + b for a, b in zip(noise(.08, .5, 6), tone([190, 110], .08, .25))])
    write_wav('break', noise(.2, .7, 9))
    write_wav('ore', tone([660, 880, 1320], .16, .28))
    write_wav('coin', tone([988, 1319], .22, .28))
    write_wav('upgrade', tone([523, 659, 784, 1047], .36, .3))
    write_wav('lift', tone([220, 294, 370, 440, 554], .45, .28, 'tri'))
    write_wav('faint', tone([440, 330, 247, 165], .55, .3, 'tri'))
    write_wav('denied', tone([140, 110], .14, .3))


def main():
    os.makedirs(ART, exist_ok=True)
    os.makedirs(AUDIO, exist_ok=True)
    dwarf_frames()
    for i, (name, base, speck, style) in enumerate(LAYERS):
        for v in range(2):
            make_tile(base, speck, style, i * 10 + v).save(f'tile_{name}_{v}')
    make_grass(1).save('tile_grass')
    for i, (name, dark, mid, light, kind) in enumerate(ORES):
        make_ore(dark, mid, light, kind, i).save('ore_' + name)
        make_ore_icon(dark, mid, light, kind).save('icon_' + name)
    make_cracks()
    make_ladder()
    make_pick()
    make_dark()
    make_ui()
    make_icons()
    make_shop()
    make_cloud()
    Img(3, 3, (255, 255, 255, 255)).save('chip')
    make_font()
    make_sounds()
    print('art written to', ART)


if __name__ == '__main__':
    main()
