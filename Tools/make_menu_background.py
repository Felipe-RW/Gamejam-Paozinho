"""Gera o fundo do menu (1920x1080) a partir dos sprites do jogo.

Composicao: clareira da batalha a noite, o Rei de Espadas (o traidor) apagado no alto com aura
vermelha, os quatro herois nas laterais virados para o centro, cartas, brasas, nevoa e vinheta.
Tudo em escala inteira (pixels de 4 px) para combinar com a arte dos herois.

Uso:  python Tools/make_menu_background.py  [saida.png]
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SPRITES = ROOT / "Assets" / "Sprites"
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else SPRITES / "menu_background.png"
W, H = 1920, 1080
random.seed(7)  # resultado sempre igual


def sprite(name):
    return Image.open(SPRITES / f"{name}.png").convert("RGBA")


def scale(im, k):
    return im.resize((im.width * k, im.height * k), Image.NEAREST)


def tint(im, rgb, amount):
    """Mistura a cor do sprite com rgb (0..1), mantendo o alfa."""
    r, g, b, a = im.split()
    solid = Image.new("RGB", im.size, rgb)
    mixed = Image.blend(Image.merge("RGB", (r, g, b)), solid, amount)
    return Image.merge("RGBA", (*mixed.split(), a))


def multiply(im, rgb):
    r, g, b, a = im.split()
    r = r.point(lambda v: int(v * rgb[0]))
    g = g.point(lambda v: int(v * rgb[1]))
    b = b.point(lambda v: int(v * rgb[2]))
    return Image.merge("RGBA", (r, g, b, a))


def with_alpha(im, factor):
    r, g, b, a = im.split()
    return Image.merge("RGBA", (r, g, b, a.point(lambda v: int(v * factor))))


def radial(size, center, radius, color, alpha):
    """Brilho radial suave (camada RGBA do tamanho da tela)."""
    layer = Image.new("RGBA", size, color + (0,))
    mask = Image.new("L", size, 0)
    d = ImageDraw.Draw(mask)
    cx, cy = center
    steps = 40
    for i in range(steps, 0, -1):
        rr = radius * i / steps
        v = int(255 * alpha * (1 - i / steps) ** 1.6)
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=v)
    layer.putalpha(mask.filter(ImageFilter.GaussianBlur(radius * 0.08)))
    return layer


def vertical_band(size, center_x, half_width, alpha):
    """Faixa escura atras da coluna de botoes, suave nas bordas."""
    mask = Image.new("L", size, 0)
    px = mask.load()
    for x in range(size[0]):
        t = abs(x - center_x) / half_width
        v = int(255 * alpha * max(0.0, 1 - t) ** 1.5)
        for y in range(size[1]):
            px[x, y] = v
    layer = Image.new("RGBA", size, (5, 8, 14, 0))
    layer.putalpha(mask)
    return layer


def vignette(size, strength):
    mask = Image.new("L", size, 0)
    px = mask.load()
    cx, cy = size[0] / 2, size[1] / 2
    for y in range(size[1]):
        for x in range(size[0]):
            dx, dy = (x - cx) / cx, (y - cy) / cy
            d = min(1.0, math.sqrt(dx * dx * 0.8 + dy * dy))
            px[x, y] = int(255 * strength * d ** 2.4)
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    layer.putalpha(mask)
    return layer


def bottom_fog(size, height, alpha):
    mask = Image.new("L", size, 0)
    d = ImageDraw.Draw(mask)
    for i in range(height):
        v = int(255 * alpha * (i / height) ** 1.8)
        y = size[1] - height + i
        d.line([(0, y), (size[0], y)], fill=v)
    layer = Image.new("RGBA", size, (40, 70, 80, 0))
    layer.putalpha(mask)
    return layer


def figure_only(card):
    """Recorta o retrato do meio da carta e torna transparente o papel creme."""
    w, h = card.size
    im = card.crop((int(w * 0.17), int(h * 0.12), int(w * 0.83), int(h * 0.88)))
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if r > 225 and g > 215 and b > 190:  # papel
                px[x, y] = (0, 0, 0, 0)
    return im


def paste(base, im, xy):
    base.alpha_composite(im, (int(xy[0]), int(xy[1])))


def main():
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 255))

    # 1) Clareira da batalha (2 px -> 4 px), recortada e escurecida para noite.
    arena = scale(sprite("background"), 2)  # 1920x1280
    arena = arena.crop((0, 100, W, 100 + H))
    arena = multiply(arena, (0.64, 0.72, 0.82))
    canvas.alpha_composite(arena)

    # 2) Faixa escura atras dos botoes (coluna central de 700 px).
    canvas.alpha_composite(vertical_band((W, H), W / 2, 560, 0.45))

    # 3) O Rei traidor: so a figura (sem o papel da carta), grande, como um vulto vermelho no ceu.
    canvas.alpha_composite(radial((W, H), (W / 2, 230), 460, (170, 20, 25), 0.6))
    king = figure_only(sprite("king_spade"))
    king = scale(king, 5)
    king = with_alpha(tint(king, (150, 25, 30), 0.55), 0.55)
    paste(canvas, king, (W / 2 - king.width / 2, -40))

    # 4) Cartas espalhadas atras dos herois (2x, girando).
    faces = ["ace_spade", "7_heart", "10_club", "4_diamond", "king_heart", "queen_club", "jack_diamond", "card_back"]
    spots = [  # (x, y, rotacao)
        (40, 30, -16), (250, -70, 11), (470, 120, -6),
        (1440, 110, 8), (1650, -60, -12), (1760, 260, 17),
        (-40, 520, 9), (1840, 600, -9),
    ]
    for (x, y, rot), name in zip(spots, faces):
        path = SPRITES / f"{name}.png"
        if not path.exists():
            name = "card_back"
        card = scale(sprite(name), 2).rotate(rot, resample=Image.NEAREST, expand=True)
        card = with_alpha(multiply(card, (0.75, 0.75, 0.85)), 0.55)
        paste(canvas, card, (x, y))

    # 5) Luz quente dos lampioes atras de cada par de herois.
    canvas.alpha_composite(radial((W, H), (330, 760), 520, (255, 150, 60), 0.30))
    canvas.alpha_composite(radial((W, H), (W - 330, 760), 520, (255, 150, 60), 0.30))

    # 6) Herois (4 px). Os de tras ficam mais escuros para dar profundidade.
    back = (0.62, 0.66, 0.78)
    healer = multiply(sprite("healer_full"), back)
    bard = multiply(sprite("bard_full"), back)
    paste(canvas, healer, (-30, 150))
    paste(canvas, bard, (W - 400 + 30, 150))
    paste(canvas, sprite("paladin_full"), (190, 120))
    paste(canvas, sprite("barbarian_full"), (W - 400 - 190, 120))

    # 7) Nevoa no chao, brasas subindo e vinheta.
    canvas.alpha_composite(bottom_fog((W, H), 300, 0.55))

    embers = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(embers)
    for _ in range(140):
        side = random.random() < 0.5
        x = random.gauss(330 if side else W - 330, 260)
        y = H - abs(random.gauss(0, 420))
        x, y = int(x) // 4 * 4, int(y) // 4 * 4  # na grade de 4 px
        c = random.choice([(255, 200, 90), (255, 150, 60), (255, 230, 150)])
        a = random.randint(120, 230)
        s = 4 if random.random() < 0.8 else 8
        d.rectangle([x, y, x + s - 1, y + s - 1], fill=c + (a,))
    glow = embers.filter(ImageFilter.GaussianBlur(8))
    canvas.alpha_composite(glow)
    canvas.alpha_composite(glow)
    canvas.alpha_composite(embers)

    canvas.alpha_composite(vignette((W, H), 0.65))

    canvas.convert("RGB").save(OUT)
    print(f"salvo em {OUT}")


if __name__ == "__main__":
    main()
