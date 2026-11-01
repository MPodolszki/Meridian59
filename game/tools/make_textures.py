"""Erzeugt kachelbare Platzhalter-Texturen (eigene Assets) für die Raumgeometrie des Clients.

Aufruf (im Repo-Root):  python -I game/tools/make_textures.py game/client/textures

Je Material: <name>_albedo.png und <name>_normal.png (512×512, nahtlos kachelbar).
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

N = 512
rng = np.random.default_rng(1234)


def periodic_noise(octaves=5, base=4):
    """Kachelbares Rauschen: Summe von Zufallsgittern, periodisch interpoliert."""
    out = np.zeros((N, N), np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cells = base * 2 ** o
        grid = rng.random((cells, cells)).astype(np.float32)
        idx = np.arange(N) * cells / N
        i0 = np.floor(idx).astype(int)
        t = idx - i0
        t = t * t * (3 - 2 * t)
        i1 = (i0 + 1) % cells
        rows = grid[i0] * (1 - t)[:, None] + grid[i1] * t[:, None]
        layer = rows[:, i0] * (1 - t)[None, :] + rows[:, i1] * t[None, :]
        out += amp * layer
        total += amp
        amp *= 0.5
    return out / total


def voronoi(points_per_side=7, jitter=0.8):
    """Periodisches Voronoi: Abstand zum nächsten und zweitnächsten Punkt, Zell-ID."""
    k = points_per_side
    pts = []
    for gy in range(k):
        for gx in range(k):
            pts.append(((gx + 0.5 + rng.uniform(-jitter, jitter) / 2) / k * N,
                        (gy + 0.5 + rng.uniform(-jitter, jitter) / 2) / k * N))
    pts = np.array(pts, np.float32)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    d1 = np.full((N, N), 1e9, np.float32)
    d2 = np.full((N, N), 1e9, np.float32)
    cell = np.zeros((N, N), np.int32)
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xx - px); dx = np.minimum(dx, N - dx)
        dy = np.abs(yy - py); dy = np.minimum(dy, N - dy)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < d1
        d2 = np.where(closer, d1, np.minimum(d2, d))
        cell = np.where(closer, i, cell)
        d1 = np.where(closer, d, d1)
    return d1, d2, cell


def colorize(value, dark, light):
    v = np.clip(value, 0, 1)[..., None]
    return (np.array(dark) * (1 - v) + np.array(light) * v)


def normal_map(height, strength=4.0):
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * strength
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * strength
    n = np.stack([-dx, -dy, np.ones_like(height)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5)


def bricks(rows, cols, mortar, offset=True):
    """Ziegel-/Steinmuster: Höhenfeld (Fugen tief) und Stein-ID."""
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    row = (yy / N * rows).astype(int)
    shift = (row % 2) * 0.5 if offset else 0
    u = (xx / N * cols + shift) % cols
    col = u.astype(int)
    fy = yy / N * rows - row
    fx = u - col
    edge = np.minimum(np.minimum(fx, 1 - fx) * N / cols, np.minimum(fy, 1 - fy) * N / rows)
    height = np.clip(edge / mortar, 0, 1)
    return height, row * 97 + col


def cobblestone():
    d1, d2, cell = voronoi(9, 0.9)
    gap = np.clip((d2 - d1) / 6.0, 0, 1)
    stone = np.sqrt(gap)
    var = rng.random(cell.max() + 1)[cell]
    noise = periodic_noise(5, 8)
    height = stone * (0.7 + 0.3 * noise)
    base = colorize(0.35 + 0.35 * var + 0.3 * noise, (0.30, 0.29, 0.27), (0.62, 0.60, 0.55))
    mortar = np.array((0.22, 0.20, 0.17))
    albedo = base * stone[..., None] + mortar * (1 - stone[..., None])
    return albedo, height


def plaster():
    noise = periodic_noise(6, 4)
    stains = periodic_noise(3, 2)
    albedo = colorize(0.55 + 0.35 * noise - 0.25 * np.clip(stains - 0.55, 0, 1) * 3,
                      (0.62, 0.55, 0.42), (0.86, 0.80, 0.68))
    return albedo, noise * 0.4


def planks():
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    boards = 8
    row = (yy / N * boards).astype(int)
    fy = yy / N * boards - row
    gap = np.clip(np.minimum(fy, 1 - fy) * N / boards / 2.0, 0, 1)
    var = rng.random(boards)[row]
    grain = np.sin((xx / N * 6 + periodic_noise(4, 4) * 3 + row * 0.37) * 2 * np.pi * 3) * 0.5 + 0.5
    albedo = colorize(0.3 + 0.4 * var + 0.25 * grain, (0.26, 0.17, 0.10), (0.55, 0.38, 0.22))
    albedo *= gap[..., None] * 0.6 + 0.4
    return albedo, gap * (0.8 + 0.2 * grain)


def roof():
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    rows, cols = 10, 8
    row = (yy / N * rows).astype(int)
    u = (xx / N * cols + (row % 2) * 0.5) % cols
    fx = u - u.astype(int)
    fy = yy / N * rows - row
    # Jede Ziegelreihe wird nach unten dicker (überlappend) und ist an den Seiten gerundet.
    shape = np.clip(fy * 1.2, 0, 1) * np.clip(np.sin(fx * np.pi) * 3, 0, 1)
    var = rng.random(rows * cols + cols)[row * cols + u.astype(int)]
    noise = periodic_noise(5, 8)
    albedo = colorize(0.3 + 0.4 * var + 0.3 * noise, (0.32, 0.12, 0.08), (0.62, 0.28, 0.18))
    albedo *= (0.55 + 0.45 * shape)[..., None]
    return albedo, shape


def stonewall():
    height, sid = bricks(8, 4, 4.0)
    var = rng.random(sid.max() + 1)[sid]
    noise = periodic_noise(5, 8)
    albedo = colorize(0.3 + 0.4 * var + 0.3 * noise, (0.30, 0.30, 0.29), (0.58, 0.57, 0.53))
    albedo *= (0.45 + 0.55 * height)[..., None]
    return albedo, height * (0.8 + 0.2 * noise)


MATERIALS = {
    "cobblestone": (cobblestone, 5.0),
    "plaster": (plaster, 1.5),
    "planks": (planks, 3.0),
    "roof": (roof, 3.0),
    "stonewall": (stonewall, 4.0),
}


def main(out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for name, (make, strength) in MATERIALS.items():
        albedo, height = make()
        Image.fromarray((np.clip(albedo, 0, 1) * 255).astype(np.uint8)).save(out / f"{name}_albedo.png")
        Image.fromarray((normal_map(height, strength) * 255).astype(np.uint8)).save(out / f"{name}_normal.png")
        print(f"{name}: ok")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "textures")
