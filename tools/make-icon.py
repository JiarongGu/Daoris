#!/usr/bin/env python3
"""Draw the application icon and write the multi-size `.ico` the shell ships with.

    python tools/make-icon.py

Run rarely — when the mark changes — and **commit the result**, because the `.ico` is a source asset
the build embeds and no consumer should need this script to build the app. It is here rather than in
somebody's head so the next change to the mark is a one-line edit and a re-run.

🔴 **The geometry is duplicated from `src/Daoris.Web/src/Mark.tsx`, deliberately and visibly.** They
are twins: one is SVG in a page, one is rasterised into a Windows resource, and they share no code
because nothing sensible spans a React component and an ICO encoder. The FILE is the contract — the
same construction in both, and they move together. It is the same twin rule the remotes map carries.

Pillow is the only dependency and it is not in any gate: this writes a committed artefact, so a
machine without it can still build, test and ship the application.
"""
from pathlib import Path

from PIL import Image, ImageDraw

# The accent, light theme — `tokens.css` `--accent`. A taskbar icon has one fixed colour: it cannot
# follow a theme, so it takes the one the brand is, and the knocked-out figure takes `--accent-ink`.
FIELD = (0x7A, 0x5C, 0x2E, 0xFF)
FIGURE = (0xFA, 0xF9, 0xF6, 0xFF)

# Every size Windows actually asks for. 16 is the one that matters most and is the hardest — it is
# what a taskbar and a title bar show — which is why each is drawn at 8x and downsampled rather than
# scaled from one master: a 256 master resampled to 16 loses the arms entirely.
SIZES = [16, 20, 24, 32, 48, 64, 128, 256]
SUPERSAMPLE = 8


def draw(size: int) -> Image.Image:
    """The seal, at one size. Same construction as `Mark.tsx`: field, three arms, one centre."""
    s = size * SUPERSAMPLE
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    pen = ImageDraw.Draw(image)

    # The field. rx 5.5 of 24 in the SVG, kept as a ratio so every size has the same silhouette.
    pen.rounded_rectangle([0, 0, s - 1, s - 1], radius=s * 5.5 / 24, fill=FIELD)

    # The figure, knocked out by drawing in the field's own ink — a raster has no mask, so the twin
    # of the SVG's knockout is simply painting the negative shape.
    centre = s / 2
    arm = s * 6.4 / 24          # 12 -> 5.6 in the viewBox
    width = max(1, round(s * 2.4 / 24))
    import math
    for degrees in (0, 120, 240):
        angle = math.radians(degrees - 90)
        pen.line(
            [centre, centre, centre + arm * math.cos(angle), centre + arm * math.sin(angle)],
            fill=FIGURE, width=width,
        )
        # Round caps: PIL's `line` has none, so the terminal is a disc of the same diameter.
        pen.ellipse(
            [centre + arm * math.cos(angle) - width / 2, centre + arm * math.sin(angle) - width / 2,
             centre + arm * math.cos(angle) + width / 2, centre + arm * math.sin(angle) + width / 2],
            fill=FIGURE,
        )

    radius = s * 2.6 / 24
    pen.ellipse([centre - radius, centre - radius, centre + radius, centre + radius], fill=FIGURE)

    return image.resize((size, size), Image.LANCZOS)


def main() -> None:
    out = Path(__file__).resolve().parent.parent / "src" / "Daoris.Desktop" / "Daoris.Desktop.App" / "daoris.ico"
    frames = [draw(size) for size in SIZES]
    # Pillow writes every frame when the largest carries the `sizes` list.
    frames[-1].save(out, format="ICO", sizes=[(s, s) for s in SIZES], append_images=frames[:-1])
    print(f"make-icon: {out} — {len(SIZES)} sizes, {out.stat().st_size} bytes")


if __name__ == "__main__":
    main()
