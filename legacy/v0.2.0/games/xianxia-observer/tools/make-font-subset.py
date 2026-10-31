#!/usr/bin/env python3
"""선협 관찰 UI 글꼴(Noto Serif KR) 부분집합을 만든다.

원본 글꼴은 한 굵기당 약 14MB라 APK에 그대로 넣기 어렵다. 다음 문자만 남긴다.
- KS X 1001 한글 2,350자(현대 한국어 문장 대부분)와 ASCII·자주 쓰는 기호
- src/ 아래 C#·AXAML 소스에 실제로 등장하는 모든 문자(한자 아이콘·드문 음절 포함)

UI 문자열을 바꾼 뒤에는 이 도구를 다시 실행한다. 누락 문자는 App 테스트가 찾아낸다.

원본: npm @expo-google-fonts/noto-serif-kr 0.4.4 (Google Fonts, SIL OFL 1.1)
사용법: python3 tools/make-font-subset.py <NotoSerifKR_400Regular.ttf> <NotoSerifKR_700Bold.ttf>
"""
import pathlib
import sys

from fontTools import subset

PROJECT_DIR = pathlib.Path(__file__).resolve().parents[1]
SOURCE_DIR = PROJECT_DIR / 'src'
OUTPUT_DIR = SOURCE_DIR / 'Xianxia.App' / 'Assets' / 'Fonts'
SOURCE_SUFFIXES = {'.cs', '.axaml'}

# 표시에 쓰는 기본 기호 범위(라틴 문장부호·화살표·도형·CJK 괄호).
BASE_RANGES = [
    (0x0020, 0x007E),  # ASCII
    (0x00A0, 0x00BF),  # 라틴-1 기호(·, ×, ° 등)
    (0x00D7, 0x00D7),
    (0x2010, 0x2027),  # 대시·따옴표·말줄임표
    (0x2030, 0x203B),  # ‰, ※
    (0x2190, 0x2199),  # 화살표
    (0x2460, 0x2473),  # 원문자 숫자
    (0x25A0, 0x25CF),  # 도형
    (0x2605, 0x2606),  # 별
    (0x3000, 0x3011),  # CJK 문장부호·괄호
    (0x3131, 0x318E),  # 한글 호환 자모
]


def ks_x_1001_hangul():
    """EUC-KR 한글 영역(0xB0A1~0xC8FE)을 해석해 KS X 1001 완성형 2,350자를 얻는다."""
    characters = set()
    for lead in range(0xB0, 0xC9):
        for trail in range(0xA1, 0xFF):
            try:
                characters.add(bytes([lead, trail]).decode('euc_kr'))
            except UnicodeDecodeError:
                continue
    return characters


def source_characters():
    """소스 파일에 쓰인 비 ASCII 문자를 모은다."""
    characters = set()
    for path in sorted(SOURCE_DIR.rglob('*')):
        if path.suffix in SOURCE_SUFFIXES and 'obj' not in path.parts and 'bin' not in path.parts:
            characters.update(ch for ch in path.read_text(encoding='utf-8') if ord(ch) > 0x7E)
    return characters


def wanted_codepoints():
    """부분집합에 남길 코드 포인트 목록을 만든다."""
    codepoints = set()
    for first, last in BASE_RANGES:
        codepoints.update(range(first, last + 1))
    codepoints.update(ord(ch) for ch in ks_x_1001_hangul())
    codepoints.update(ord(ch) for ch in source_characters() if ch not in '﻿\r\n\t')
    return sorted(codepoints)


def make_subset(source_font, output_font, codepoints):
    """fontTools로 글꼴 부분집합을 만든다. 힌팅과 레이아웃 기능은 보존한다."""
    options = subset.Options()
    options.layout_features = ['*']
    options.name_IDs = ['*']
    options.name_languages = ['*']
    options.notdef_outline = True
    options.glyph_names = False
    font = subset.load_font(str(source_font), options)
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=codepoints)
    subsetter.subset(font)
    subset.save_font(font, str(output_font), options)


def main(arguments):
    if len(arguments) != 2:
        print(__doc__)
        return 2
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    codepoints = wanted_codepoints()
    pairs = [(arguments[0], 'NotoSerifKR-Regular.ttf'), (arguments[1], 'NotoSerifKR-Bold.ttf')]
    for source, name in pairs:
        output = OUTPUT_DIR / name
        make_subset(pathlib.Path(source), output, codepoints)
        print(f'{output.relative_to(PROJECT_DIR)}: {output.stat().st_size:,} bytes, {len(codepoints):,} code points')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
