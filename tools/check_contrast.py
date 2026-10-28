#!/usr/bin/env python3
"""配色对比度检查：解析 Theme.cs 中的调色板，按 WCAG 2.1 计算对比度。

设计约束（不通过则构建失败）：
  - 主文字 / 卡片       >= 7.0   (AAA)
  - 正文 / 卡片         >= 4.5   (AA)
  - 说明文字 / 卡片     >= 4.5   (AA)
  - 主文字 / 窗口背景   >= 4.5   (AA)
  - 强调色上的文字      >= 4.5   (AA)
  - 菜单文字 / 菜单背景 >= 4.5   (AA)
  - 卡片边框 vs 卡片    >= 1.20  (可见分区)
  - 卡片 vs 窗口背景    >= 1.10  (层级可辨)
  - 输入框 vs 卡片      >= 1.05  (控件可辨)
"""
import os
import re
import sys

# CI 环境（Windows runner）默认 cp1252，输出中文会抛 UnicodeEncodeError
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

THEME_FILE = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Theme.cs")

PAIRS = [
    ("主文字/卡片", "Text", "Card", 7.0),
    ("正文/卡片", "TextSecondary", "Card", 4.5),
    ("说明/卡片", "TextMuted", "Card", 4.5),
    ("主文字/窗口", "Text", "Window", 4.5),
    ("强调色上的文字", "AccentText", "Accent", 4.5),
    ("菜单文字/菜单底", "Text", "MenuBackground", 4.5),
    ("卡片边框/卡片", "CardBorder", "Card", 1.20),
    ("卡片/窗口", "Card", "Window", 1.10),
    ("输入框/卡片", "Input", "Card", 1.05),
]


def parse_palettes(path):
    """返回 {palette_id: {slot: (r,g,b)}}"""
    with open(path, "r", encoding="utf-8") as f:
        source = f.read()

    palettes = {}
    for pid in ("dark", "light"):
        # 定位该调色板的定义块
        m = re.search(r"Id\s*=\s*\"%s\"" % pid, source)
        if not m:
            raise SystemExit("找不到调色板定义: %s" % pid)
        start = source.rfind("new ThemePalette", 0, m.start())
        end = source.find("};", m.end())
        block = source[start:end if end > 0 else len(source)]

        slots = {}
        for sm in re.finditer(r"(\w+)\s*=\s*Color\.FromArgb\((\d+),\s*(\d+),\s*(\d+)\)", block):
            slots[sm.group(1)] = (int(sm.group(2)), int(sm.group(3)), int(sm.group(4)))
        palettes[pid] = slots
    return palettes


def luminance(rgb):
    def channel(v):
        c = v / 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (channel(x) for x in rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a, b):
    la, lb = luminance(a), luminance(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def main():
    palettes = parse_palettes(THEME_FILE)
    failures = []

    for pid in ("light", "dark"):
        slots = palettes[pid]
        print("\n=== %s ===" % pid)
        for label, fg, bg, threshold in PAIRS:
            if fg not in slots or bg not in slots:
                failures.append("%s: 缺少颜色槽位 %s / %s" % (pid, fg, bg))
                print("  %-16s 缺少槽位 %s/%s" % (label, fg, bg))
                continue
            ratio = contrast(slots[fg], slots[bg])
            ok = ratio >= threshold
            print("  %-16s %5.2f : 1  (要求 %.2f)  %s" % (label, ratio, threshold, "OK" if ok else "FAIL"))
            if not ok:
                failures.append("%s %s: %.2f < %.2f" % (pid, label, ratio, threshold))

    if failures:
        print("\n对比度检查未通过：")
        for f in failures:
            print("  - " + f)
        return 1

    print("\n全部通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
