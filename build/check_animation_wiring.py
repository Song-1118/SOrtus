#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
动画接线断言（VD-062 建议的 CI 检查）。

背景：Animations.xaml 曾定义 3 组 Storyboard + 3 个 OnLoad 样式，全仓 0 引用——
「做了没接」比「没做」更危险，因为维护者会以为动画已经生效。
本脚本要求：Animations.xaml 里每个 x:Key 至少被 XAML（StaticResource / DynamicResource）
或 C#（字符串字面量）引用一次；断言失败即报出未接线的 key。

用法：python SOruts/build/check_animation_wiring.py   （退出码 0 = 全部接线）
"""
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
ANIM = REPO / "SOruts/src/SOrtus/Resources/Styles/Animations.xaml"

# 缓动函数（EasingFunction）与时间语义令牌：它们是被 Storyboard 内部 StaticResource 引用的，
# 已在 Animations.xaml 自身内命中，无需全仓再找。
EASING_KEYS = {"CubicEaseOut", "StdEase", "EnterEase", "ExitEase", "BackEaseOut"}


def main():
    text = ANIM.read_text(encoding="utf-8")
    keys = set(re.findall(r'x:Key="([A-Za-z0-9_]+)"', text))
    check_keys = keys - EASING_KEYS

    # 全仓 XAML 引用：StaticResource/DynamicResource + Motion.* 附加属性里的资源名字符串
    # （如 local:Motion.PlayOnLoad="StartupFadeInStoryboard"、StaggerStoryboard="TimelineInStoryboard"）
    xaml_hits = set()
    for path in REPO.glob("SOruts/src/SOrtus/**/*.xaml"):
        content = path.read_text(encoding="utf-8", errors="ignore")
        for m in re.finditer(r'\{(?:Static|Dynamic)Resource\s+([A-Za-z0-9_]+)\}', content):
            xaml_hits.add(m.group(1))
        for m in re.finditer(r'\.(?:PlayOnLoad|StaggerStoryboard|PulseStoryboard|PulseStoryboardOut|PageEnter)="([A-Za-z0-9_]+)"', content):
            xaml_hits.add(m.group(1))

    # C# 里的字符串引用（Motion.Begin(fe, "XxxStoryboard") 等）
    cs_hits = set()
    for path in REPO.glob("SOruts/src/**/*.cs"):
        if {"bin", "obj"} & set(path.parts):
            continue
        content = path.read_text(encoding="utf-8", errors="ignore")
        for m in re.finditer(r'"([A-Za-z0-9_]+Storyboard|[A-Za-z0-9_]+Style)"', content):
            cs_hits.add(m.group(1))

    unused = sorted(k for k in check_keys if k not in xaml_hits and k not in cs_hits)
    if unused:
        for key in unused:
            print("FAIL 未接线:", key)
        print(f"\n共 {len(unused)} 个资源 0 引用（做了没接 = VD-062 回归）")
        return 1

    print(f"OK {len(check_keys)} 个动画资源全部至少被引用一次")
    return 0


if __name__ == "__main__":
    sys.exit(main())
