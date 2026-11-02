"""Erzeugt kachelbare Platzhalter-Texturen (eigene Assets) für die Raumgeometrie des Clients.

Aufruf (im Repo-Root):  python -I game/tools/make_textures.py game/client/textures

Je Material: <name>_albedo.png und <name>_normal.png (1024×1024, nahtlos kachelbar).
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

N = 1024
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


def speckle(density, size=1):
    """Kleine Punkte (Kies, Moos, Schmutz) als 0/1-Maske, kachelbar."""
    m = (rng.random((N, N)) < density).astype(np.float32)
    for _ in range(size - 1):
        m = np.maximum(m, np.roll(m, 1, 0))
        m = np.maximum(m, np.roll(m, 1, 1))
    return m


def strokes(count, length, angle_spread, width=1):
    """Kurze Striche (Grashalme, Maserung) als Höhenfeld, kachelbar."""
    h = np.zeros((N, N), np.float32)
    xs = rng.integers(0, N, count); ys = rng.integers(0, N, count)
    angles = -np.pi / 2 + rng.uniform(-angle_spread, angle_spread, count)
    lengths = rng.integers(length // 2, length + 1, count)
    for x, y, a, l in zip(xs, ys, angles, lengths):
        t = np.arange(l)
        px = (x + np.cos(a) * t).astype(int) % N
        py = (y + np.sin(a) * t).astype(int) % N
        val = 1 - t / max(l, 1) * 0.6
        for w in range(width):
            h[py, (px + w) % N] = np.maximum(h[py, (px + w) % N], val)
    return h


def cobblestone():
    """Kopfsteinpflaster: unregelmäßige Steine mit eigener Tönung, abgerundeten Kanten, Körnung und Schmutz in den
    Fugen, vereinzelt Moos."""
    d1, d2, cell = voronoi(14, 0.95)
    edge = (d2 - d1)
    stone = np.clip(edge / 9.0, 0, 1)
    dome = np.sqrt(stone) * (0.75 + 0.25 * np.clip(1 - d1 / 40.0, 0, 1))      # gewölbte Steinoberfläche
    var = rng.random(cell.max() + 1)[cell]
    hue = rng.random(cell.max() + 1)[cell]
    grain = periodic_noise(6, 32)
    coarse = periodic_noise(4, 6)
    height = dome * (0.8 + 0.2 * grain)
    warm = np.array((0.52, 0.48, 0.42)); cool = np.array((0.44, 0.45, 0.47))
    base = warm * hue[..., None] + cool * (1 - hue[..., None])
    base = base * (0.65 + 0.45 * var[..., None]) * (0.85 + 0.25 * grain[..., None]) * (0.9 + 0.2 * coarse[..., None])
    grout = colorize(periodic_noise(5, 16), (0.12, 0.11, 0.09), (0.26, 0.24, 0.20))
    moss = (speckle(0.002, 3) * (1 - stone) * (coarse > 0.55))[..., None]
    albedo = base * stone[..., None] + grout * (1 - stone[..., None])
    albedo = albedo * (1 - moss) + np.array((0.22, 0.32, 0.12)) * moss
    return albedo, height


def plaster():
    """Putz: feine Körnung, Flecken, Wasserläufe von oben und feine Risse."""
    fine = periodic_noise(7, 64)
    noise = periodic_noise(6, 4)
    stains = periodic_noise(3, 2)
    d1, d2, _ = voronoi(6, 1.0)
    cracks = np.clip(1 - (d2 - d1) / 1.5, 0, 1) * (periodic_noise(4, 8) > 0.6)
    streak = np.tile(periodic_noise(5, 48)[0:1, :], (N, 1))   # senkrechte Wasserläufe
    tone = 0.55 + 0.25 * noise + 0.15 * (fine - 0.5) - 0.3 * np.clip(stains - 0.55, 0, 1) * 3 - 0.12 * np.clip(streak - 0.6, 0, 1) * 3
    albedo = colorize(tone, (0.55, 0.49, 0.38), (0.88, 0.82, 0.70))
    albedo *= (1 - 0.2 * cracks)[..., None]
    return albedo, noise * 0.3 + fine * 0.25 - cracks * 0.25


def planks():
    """Dielen: sechs Bretter mit eigener Tönung, feiner Maserung, Astlöchern, Nagelköpfen und dunklen Fugen."""
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    boards = 6
    row = (yy / N * boards).astype(int)
    fy = yy / N * boards - row
    gap = np.clip(np.minimum(fy, 1 - fy) * N / boards / 3.0, 0, 1)
    var = rng.random(boards)[row]
    # Maserung läuft längs der Bretter: Rauschen in X stark gestreckt, Linien entlang Y verschoben.
    stretched = periodic_noise(5, 16)[:, (np.arange(N) // 16)]
    grain = np.sin((fy * 9 + stretched * 2.5 + row * 0.37) * 2 * np.pi) * 0.5 + 0.5
    grain = grain ** 3
    fine = periodic_noise(7, 64)
    knots = np.zeros((N, N), np.float32)
    for _ in range(10):
        kx, ky = rng.uniform(0, N), (rng.integers(0, boards) + rng.uniform(0.3, 0.7)) * N / boards
        dx = np.minimum(np.abs(xx - kx), N - np.abs(xx - kx)); dy = np.abs(yy - ky)
        knots = np.maximum(knots, np.clip(1 - np.sqrt((dx / 14) ** 2 + (dy / 9) ** 2), 0, 1))
    nails = np.zeros((N, N), np.float32)
    for b in range(boards):
        for nx in (N * 0.08, N * 0.58):
            ny = (b + 0.5) * N / boards
            nails = np.maximum(nails, np.clip(1 - np.hypot(xx - nx, yy - ny) / 4, 0, 1))
    tone = 0.30 + 0.35 * var + 0.22 * grain + 0.1 * (fine - 0.5) - 0.35 * knots
    albedo = colorize(tone, (0.24, 0.15, 0.08), (0.62, 0.43, 0.25))
    albedo *= (gap[..., None] * 0.7 + 0.3)
    albedo = albedo * (1 - nails[..., None]) + np.array((0.18, 0.18, 0.2)) * nails[..., None]
    return albedo, gap * (0.75 + 0.15 * grain + 0.1 * fine) - knots * 0.2 + nails * 0.3


def roof():
    """Dachziegel: überlappende Reihen, je Ziegel eigene Tönung, Abnutzung und etwas Moos."""
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    rows, cols = 14, 10
    row = (yy / N * rows).astype(int)
    u = (xx / N * cols + (row % 2) * 0.5) % cols
    fx = u - u.astype(int)
    fy = yy / N * rows - row
    shape = np.clip(fy * 1.25, 0, 1) * np.clip(np.sin(fx * np.pi) * 3, 0, 1)
    tile = row * cols + u.astype(int)
    var = rng.random(rows * cols + cols)[tile]
    noise = periodic_noise(6, 16)
    coarse = periodic_noise(3, 4)
    albedo = colorize(0.25 + 0.45 * var + 0.3 * noise, (0.30, 0.11, 0.07), (0.66, 0.30, 0.18))
    albedo *= (0.5 + 0.5 * shape)[..., None]
    moss = (np.clip(coarse - 0.6, 0, 1) * 2.5 * (1 - shape * 0.5))[..., None]
    albedo = albedo * (1 - moss) + np.array((0.25, 0.30, 0.14)) * moss
    return albedo, shape * (0.85 + 0.15 * noise)


def stonewall():
    """Bruchsteinmauer: unregelmäßige Steine (Voronoi, gestaucht) mit Körnung und tiefen Fugen."""
    d1, d2, cell = voronoi(10, 0.9)
    stone = np.clip((d2 - d1) / 7.0, 0, 1)
    var = rng.random(cell.max() + 1)[cell]
    grain = periodic_noise(6, 32)
    noise = periodic_noise(5, 8)
    tone = 0.3 + 0.4 * var + 0.2 * noise + 0.1 * (grain - 0.5)
    albedo = colorize(tone, (0.30, 0.30, 0.29), (0.62, 0.61, 0.57))
    albedo *= (0.35 + 0.65 * np.sqrt(stone))[..., None]
    return albedo, np.sqrt(stone) * (0.8 + 0.2 * grain)


def grass():
    """Wiese: viele Halme in verschiedenen Grüntönen, dunklere Büschel, vereinzelt Klee und trockene Halme."""
    noise = periodic_noise(6, 4)
    patches = periodic_noise(3, 3)
    blades = strokes(60000, 14, 0.5)
    dry = strokes(4000, 12, 0.6)
    clover = speckle(0.0008, 4) * (patches > 0.5)
    tone = 0.25 + 0.3 * noise + 0.25 * patches + 0.35 * blades
    albedo = colorize(np.clip(tone, 0, 1), (0.10, 0.20, 0.05), (0.42, 0.62, 0.20))
    albedo = albedo * (1 - 0.7 * dry[..., None]) + np.array((0.62, 0.58, 0.32)) * 0.7 * dry[..., None]
    albedo = albedo * (1 - clover[..., None]) + np.array((0.30, 0.52, 0.18)) * clover[..., None]
    return albedo, noise * 0.3 + blades * 0.6


MATERIALS = {
    "cobblestone": (cobblestone, 6.0),
    "plaster": (plaster, 2.0),
    "planks": (planks, 3.5),
    "roof": (roof, 3.5),
    "stonewall": (stonewall, 5.0),
    "grass": (grass, 2.5),
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
