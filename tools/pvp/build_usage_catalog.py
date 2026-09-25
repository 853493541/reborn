#!/usr/bin/env python3
"""Build cooldown_usage_catalog.tsv: Usage -> inferred meaning + representative rows.

Usage is inferred from the note text + cooldown group clustering; label confidence
in the final column.
"""
from __future__ import annotations

import sys
from collections import Counter, defaultdict
from pathlib import Path

CDLIST = Path(
    r"C:\Users\Zhibin Ren\Desktop\reborn-netcode\proof\netcode\mode_juejing\pak_out2\CoolDownList.tab"
)
OUT = Path("proof/pvp/cooldown_usage_catalog.tsv")

# School ids observed: 1=天策 2=万花 3=纯阳 4=七秀 5=少林 6=藏剑 7=丐帮 8=明教
# 9=五毒 10=唐门 11=江湖/通用 12=NPC 13=系统/道具 (inferred from note prefixes)
MEANING = {
    "0": "系统/其他(含加速、配方、活动)",
    "1": "天策",
    "2": "万花",
    "3": "纯阳",
    "4": "七秀",
    "5": "少林",
    "6": "藏剑",
    "7": "丐帮",
    "8": "明教",
    "9": "五毒",
    "10": "唐门",
    "11": "江湖/通用(含公共CD、轻功、道具)",
    "12": "NPC技能",
    "13": "系统/道具/交互",
    "14": "技艺/烹饪",
    "15": "绷带/消耗品",
    "20": "战场/NPC阵营公共CD",
    "21": "任务机关",
    "24": "药品共用CD",
    "25": "技艺-阅读/抄录",
    "29": "任务",
    "30": "活动/场景交互",
    "31": "奇穴辅助系(百战?)",
    "38": "毒(活动)",
    "42": "装备/活动",
    "44": "活动道具",
    "46": "活动/节日",
    "48": "道具",
    "49": "称号/外观",
    "59": "战场技能(锻打/射箭/暗杀)",
    "61": "长CD通用",
    "62": "御城(活动)",
    "64": "墨家密殿(活动)",
    "65": "神机车/载具",
    "66": "驯养",
    "68": "苍云(盾/刀姿态)",
    "69": "长歌(曲/音域)",
    "70": "百战/宠物?",
    "71": "霸刀(姿态/透支)",
    "72": "方士/绿林(身份)",
    "73": "蓬莱(雕系)",
    "74": "刀宗(套路)",
    "75": "家园/交互状态",
    "76": "衍天(占术)",
    "78": "北天药宗",
    "79": "康宴别/ACT NPC",
    "80": "段氏(擒拿)",
    "82": "万灵山庄",
    "83": "万灵山庄(抟风令等)",
    "84": "绝境战场/奇境寻宝",
    "85": "百战异闻录(傀儡/空蝉)",
    "86": "活动(五行/超链接)",
    "锻造": "锻造技艺",
    "成都": "活动(蹴鞠)",
}


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    text = CDLIST.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    hdr = rows[0]

    def c(r, i):
        return r[i] if i < len(r) else ""

    groups: dict[str, list] = defaultdict(list)
    for r in rows[1:]:
        groups[c(r, 4)].append(r)

    lines = ["Usage\tcount\tinferred_meaning\tconfidence\tsample_rows(ID;Duration;Min;Max;Count;OverDraft;Accel;SyncOB;note)"]
    for usage, rs in sorted(groups.items(), key=lambda x: -len(x[1])):
        samples = []
        for r in rs[:3]:
            samples.append("|".join([c(r, 0), c(r, 1), c(r, 2), c(r, 6), c(r, 5),
                                     c(r, 8), c(r, 9), c(r, 10), c(r, 3)]))
        conf = "MED" if usage in MEANING else "LOW"
        lines.append(f"{usage}\t{len(rs)}\t{MEANING.get(usage,'unknown')}\t{conf}\t" + " ;; ".join(samples))
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {OUT} ({len(groups)} usage values)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
