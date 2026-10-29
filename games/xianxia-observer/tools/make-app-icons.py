#!/usr/bin/env python3
"""선협 관찰 Android 실행 아이콘을 만든다.

양피지 바탕에 인장(印章) 모양 원과 '仙' 자를 그린다. 결과 PNG는 저장소에 커밋하므로
아이콘을 바꿀 때만 다시 실행한다.

사용법: python3 tools/make-app-icons.py <NotoSerifKR_700Bold.ttf>
"""
import pathlib
import sys

from PIL import Image, ImageDraw, ImageFont

PROJECT_DIR = pathlib.Path(__file__).resolve().parents[1]
RESOURCE_DIR = PROJECT_DIR / 'src' / 'Xianxia.Android' / 'Resources'
PARCHMENT = (245, 237, 224, 255)
PARCHMENT_DEEP = (232, 216, 184, 255)
SEAL = (150, 52, 32, 255)
SEAL_RIM = (122, 72, 0, 255)
GLYPH = '仙'

# 밀도별 크기(dp 48 기준 실행 아이콘, dp 108 기준 적응형 전경).
DENSITIES = {'mdpi': 1.0, 'hdpi': 1.5, 'xhdpi': 2.0, 'xxhdpi': 3.0, 'xxxhdpi': 4.0}


def draw_seal(draw, center, radius, font_path):
    """인장 원과 글자를 그린다."""
    cx, cy = center
    rim = max(2, radius // 14)
    draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=SEAL_RIM)
    inner = radius - rim
    draw.ellipse((cx - inner, cy - inner, cx + inner, cy + inner), fill=SEAL)
    ring = inner - rim * 2
    draw.ellipse((cx - ring, cy - ring, cx + ring, cy + ring), outline=PARCHMENT, width=max(1, rim // 2))
    font = ImageFont.truetype(font_path, int(radius * 1.15))
    box = draw.textbbox((0, 0), GLYPH, font=font)
    width, height = box[2] - box[0], box[3] - box[1]
    draw.text((cx - width / 2 - box[0], cy - height / 2 - box[1]), GLYPH, font=font, fill=PARCHMENT)


def legacy_icon(size, font_path, round_shape):
    """API 25 이하용 실행 아이콘(정사각형 모서리 둥글림 또는 원형)."""
    scale = 4
    big = size * scale
    image = Image.new('RGBA', (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    margin = big // 24
    if round_shape:
        draw.ellipse((margin, margin, big - margin, big - margin), fill=PARCHMENT_DEEP)
        draw.ellipse((margin * 2, margin * 2, big - margin * 2, big - margin * 2), fill=PARCHMENT)
    else:
        corner = big // 5
        draw.rounded_rectangle((margin, margin, big - margin, big - margin), corner, fill=PARCHMENT_DEEP)
        draw.rounded_rectangle((margin * 2, margin * 2, big - margin * 2, big - margin * 2), corner, fill=PARCHMENT)
    draw_seal(draw, (big // 2, big // 2), int(big * 0.32), font_path)
    return image.resize((size, size), Image.LANCZOS)


def adaptive_foreground(size, font_path):
    """API 26 이상 적응형 아이콘 전경. 108dp 중 가운데 66dp 안에 인장을 둔다."""
    scale = 4
    big = size * scale
    image = Image.new('RGBA', (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw_seal(draw, (big // 2, big // 2), int(big * 0.25), font_path)
    return image.resize((size, size), Image.LANCZOS)


def main(arguments):
    if len(arguments) != 1:
        print(__doc__)
        return 2
    font_path = arguments[0]
    for density, factor in DENSITIES.items():
        folder = RESOURCE_DIR / f'mipmap-{density}'
        folder.mkdir(parents=True, exist_ok=True)
        launcher = int(48 * factor)
        legacy_icon(launcher, font_path, False).save(folder / 'ic_launcher.png', optimize=True)
        legacy_icon(launcher, font_path, True).save(folder / 'ic_launcher_round.png', optimize=True)
        adaptive_foreground(int(108 * factor), font_path).save(folder / 'ic_launcher_foreground.png', optimize=True)
    preview = PROJECT_DIR / 'docs' / 'images'
    preview.mkdir(parents=True, exist_ok=True)
    legacy_icon(512, font_path, False).save(preview / 'icon-512.png', optimize=True)
    print('아이콘을 만들었습니다:', RESOURCE_DIR.relative_to(PROJECT_DIR))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
