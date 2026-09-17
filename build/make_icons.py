"""Generate distinctive ScreenAudioRouter icons (tray + app)."""
from PIL import Image, ImageDraw, ImageFont
import os

OUT = r"C:\Users\winte\Desktop\mimo\ScreenAudioRouter\src\ScreenAudioRouter\Assets"
os.makedirs(OUT, exist_ok=True)

def rounded_rect(draw, xy, r, fill):
    draw.rounded_rectangle(xy, radius=r, fill=fill)

def draw_icon(size: int) -> Image.Image:
    # High-res supersample then downscale for crisp edges
    s = max(size * 4, 256)
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Background: charcoal squircle
    pad = int(s * 0.04)
    bg = (28, 28, 30, 255)
    rounded_rect(d, (pad, pad, s - pad, s - pad), int(s * 0.22), bg)

    # Soft inner top highlight
    highlight = (64, 64, 68, 90)
    rounded_rect(d, (pad + int(s*0.06), pad + int(s*0.05), s - pad - int(s*0.06), s//2),
                 int(s * 0.16), highlight)

    # Dual monitors
    # Left monitor (primary blue)
    lx0, ly0 = s * 0.14, s * 0.28
    lx1, ly1 = s * 0.46, s * 0.58
    rounded_rect(d, (lx0, ly0, lx1, ly1), s * 0.045, (10, 132, 255, 255))
    # stand
    d.rectangle((s*0.26, ly1, s*0.34, s*0.62), fill=(10, 132, 255, 220))
    d.rectangle((s*0.20, s*0.62, s*0.40, s*0.655), fill=(10, 132, 255, 220))

    # Right monitor (cyan accent, slightly higher — multi-screen)
    rx0, ry0 = s * 0.52, s * 0.22
    rx1, ry1 = s * 0.86, s * 0.52
    rounded_rect(d, (rx0, ry0, rx1, ry1), s * 0.045, (100, 210, 255, 255))
    d.rectangle((s*0.64, ry1, s*0.72, s*0.56), fill=(100, 210, 255, 220))
    d.rectangle((s*0.58, s*0.56, s*0.78, s*0.595), fill=(100, 210, 255, 220))

    # Audio wave across bottom — bold, eye-catching
    wave_y = s * 0.78
    # bars
    bars = [
        (0.22, 0.10), (0.30, 0.16), (0.38, 0.22), (0.46, 0.14),
        (0.54, 0.20), (0.62, 0.26), (0.70, 0.18), (0.78, 0.12),
    ]
    for bx, bh in bars:
        x = s * bx
        h = s * bh
        w = s * 0.035
        color = (255, 255, 255, 235) if bh < 0.18 else (48, 200, 120, 245)
        # green accent for taller EQ bars
        if bh >= 0.20:
            color = (48, 209, 88, 255)  # system green
        elif bh >= 0.16:
            color = (255, 214, 10, 255)  # accent yellow mid
        rounded_rect(d, (x, wave_y - h, x + w, wave_y + s*0.02), w/2, color)

    # Small routing arrow between monitors (white chevron)
    ax, ay = s * 0.485, s * 0.40
    d.polygon(
        [(ax, ay - s*0.035), (ax + s*0.06, ay), (ax, ay + s*0.035)],
        fill=(255, 255, 255, 230)
    )

    out = img.resize((size, size), Image.Resampling.LANCZOS)
    return out


def main():
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    images = [draw_icon(sz) for sz in sizes]
    ico_path = os.path.join(OUT, "ScreenAudioRouter.ico")
    images[-1].save(ico_path, format="ICO", sizes=[(sz, sz) for sz in sizes],
                    append_images=images[:-1])
    # Also save PNGs for tray / preview
    for sz, im in zip(sizes, images):
        im.save(os.path.join(OUT, f"icon_{sz}.png"))
    # Tray uses 32
    draw_icon(32).save(os.path.join(OUT, "tray_32.png"))
    draw_icon(256).save(os.path.join(OUT, "app_256.png"))
    print("WROTE", ico_path)
    print("WROTE", os.path.join(OUT, "tray_32.png"))


if __name__ == "__main__":
    main()
