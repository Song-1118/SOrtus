#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
本地化键一致性校验（防「界面空白」回归）。

存在原因：2026-10-25 第三批修复里，7 个新加的界面文本引用了 resx 里不存在的键
（Detail_DangerZoneTitle / Settings_CheckNowLabel / Git_TimelineCount ...）。
WPF 的 {DynamicResource 缺失键} 不报错，直接渲染成空白；C# 侧 GetString 缺键则更糟，
会把键名本身显示给用户（"Git_TimelineCount"）。两种都只在肉眼看截图时才会发现。

检查三件事：
  1. XAML 里 {DynamicResource Xxx_*} 的文本键是否都在 Strings.resx；
  2. C# 里 GetString("Xxx") 的键是否都在 Strings.resx；
  3. 中英两本 resx 的键集合是否完全相同（缺一边 = 切语言后缺文案）。

用法：python SOruts/build/check_locale_keys.py   （退出码 0 = 全通过）
"""
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
RESX = REPO / "SOruts/src/SOrtus/Resources/Strings/Strings.resx"
RESX_EN = REPO / "SOruts/src/SOrtus/Resources/Strings/Strings.en-US.resx"

# 只有文本键用这些前缀；样式 / 画刷 / 尺寸键不在 resx 里，必须排除，否则全是误报
TEXT_PREFIXES = (
    "Settings_", "Detail_", "Wizard_", "Doc_", "Git_", "Common_", "App_", "Update_",
    "List_", "Nav_", "Window_", "Error_", "Dialog_", "Confirm_", "Sidebar_", "Empty_",
    "Type_", "Status_", "Project_",
)


def keys_of(path):
    return {d.get("name") for d in ET.parse(path).getroot().findall("data")}


def main():
    zh, en = keys_of(RESX), keys_of(RESX_EN)
    problems = []

    for key in sorted(zh - en):
        problems.append(f"[resx] 仅中文有：{key}")
    for key in sorted(en - zh):
        problems.append(f"[resx] 仅英文有：{key}")

    for path in REPO.glob("SOruts/src/SOrtus/**/*.xaml"):
        text = path.read_text(encoding="utf-8", errors="ignore")
        for match in re.finditer(r"\{DynamicResource\s+([A-Za-z0-9_]+)\}", text):
            key = match.group(1)
            if key.startswith(TEXT_PREFIXES) and key not in zh:
                problems.append(f"[xaml] {path.name}: DynamicResource {key} 不存在")

    for path in REPO.glob("SOruts/src/**/*.cs"):
        if {"bin", "obj"} & set(path.parts):
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        for match in re.finditer(r'GetString\(\s*"([A-Za-z0-9_]+)"', text):
            if match.group(1) not in zh:
                problems.append(f"[cs] {path.name}: GetString(\"{match.group(1)}\") 不存在")

    if problems:
        for line in dict.fromkeys(problems):
            print("FAIL", line)
        print(f"\n共 {len(set(problems))} 处问题（界面会显示空白或键名）")
        return 1

    print(f"OK 中文 {len(zh)} 键 / 英文 {len(en)} 键，XAML 与 C# 引用全部命中")
    return 0


if __name__ == "__main__":
    sys.exit(main())
