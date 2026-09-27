#!/usr/bin/env python3
"""Build ability_picker/data/ability_candidates.json.

Sources:
  1. map-viewer ability-matcher cache (ability -> tani candidates + sound events)
  2. hand-curated dig mappings (exact/strong findings from the animation research)

Usage:
  python build_candidates.py [--cache PATH] [--out PATH]
"""
from __future__ import annotations

import argparse
import json
import os
import re
import time

DEFAULT_CACHE = (
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer"
    r"\cache-extraction\pakv4-probe\ability-matcher\ability-tani-sound-cache.json"
)
DEFAULT_OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data", "ability_candidates.json")

F1 = r"data\source\player\f1\动作"
BEST = {"悟": 3, "HD": 2, "hd": 2}

PREFIXES = ("绝境_", "绝境·", "绝境", "伪传_", "伪传·", "伪传", "道具_", "道具·")


def norm_name(s: str) -> str:
    s = (s or "").strip()
    for p in PREFIXES:
        if s.startswith(p):
            s = s[len(p):]
    return s.strip("_· ")


def flatten_names(items) -> list[str]:
    out = []
    for it in items or []:
        if isinstance(it, dict):
            v = it.get("name") or it.get("event") or it.get("id")
            if v:
                out.append(str(v))
        else:
            out.append(str(it))
    return out


def tani_paths(entry) -> list[str]:
    out = []
    for t in entry.get("taniResults") or []:
        if isinstance(t, dict):
            p = t.get("path") or t.get("file") or t.get("logicalPath")
            if p:
                out.append(str(p).replace("/", "\\"))
        elif isinstance(t, str):
            out.append(t.replace("/", "\\"))
    seen, uniq = set(), []
    for p in out:
        k = p.lower()
        if k not in seen:
            seen.add(k)
            uniq.append(p)
    # prefer _悟-free first, then HD, then the rest
    def rank(p: str) -> tuple:
        wu = 1 if "_悟" in p else 0
        hd = 0 if ("HD" in p or "hd" in p) else 1
        return (wu, hd, len(p))
    return sorted(uniq, key=rank)


MANUAL = [
    ("风来吴山", [F1 + r"\f1s07cj重剑技能15_风来吴山红色hd.tani"], "dig: FLWS red HD (proven chain)"),
    ("九霄风雷", [
        F1 + r"\f1h唐门弩车001b_技能02a_HD.tani",
        F1 + r"\F1H唐门弩车001b_上车01_HD.tani",
        F1 + r"\F1h唐门弩车001b_下车01_悟.tani",
    ], "dig: 唐门弩车 vehicle family"),
    ("洞烛机微", [F1 + r"\f1h唐门弩车001b_技能02a_HD.tani"], "dig: 伪九霄风雷加速 -> 弩车"),
    ("魂压怒涛", [F1 + r"\f1h唐门弩车001b_技能02a_HD.tani"], "dig: 伪九霄风雷冲撞 -> 弩车"),
    ("韦陀献杵", [F1 + r"\F1s04sl棍技能12_hd02.tani"], "dig: M1/M2 named 棍技能12 韦陀献杵"),
    ("大狮子吼", [F1 + r"\F1ssl04技能06_hd01.tani"], "dig: M1/M2 named ssl04技能06 大狮子吼"),
    ("心诤", [
        F1 + r"\F1s04sl棍技能18a_扫击蓄力01.tani",
        F1 + r"\F1s04sl棍技能18b_扫击释放01.tani",
    ], "dig: 扫击 events + named clips"),
    ("摩诃无量", [F1 + r"\F1s04sl棍技能14_hd03.tani"], "dig: M1/M2 named 棍技能14"),
    ("捕风式", [F1 + r"\F1s04sl爪技能11_hd01.tani"], "dig: M1 named 爪技能11 捕风式"),
    ("抱残式", [F1 + r"\F1s04sl爪技能12_hd01.tani"], "dig: only unnamed claw slot left"),
    ("拿云式", [F1 + r"\F1s04sl爪技能13_hd01.tani"], "dig: M1 named 爪技能13 拿云式"),
    ("捉影式", [F1 + r"\F1s04sl爪技能14a_hd01.tani"], "dig: M1 named 爪技能14a 捉影式"),
    ("守缺式", [F1 + r"\F1s04sl爪技能15a_hd01.tani"], "dig: M1 named 爪技能15a 守缺式"),
    ("斗转星移", [F1 + r"\F1s17ytz星相技01_斗转星_悟.tani"], "dig: named 星相技01 斗转星"),
    ("穹隆化生", [
        F1 + r"\F1s03cy气技能15a_穹窿化生01_悟.tani",
        F1 + r"\F1s03cy气技能15b_穹窿化生01_悟.tani",
    ], "dig: named 气技能15 穹窿化生"),
    ("撼如雷", [F1 + r"\F1s04tc技能22_撼HD.tani"], "dig: event hanhd02 + 技能22 撼"),
    ("花语酥心", [F1 + r"\F1swh01辅助技能03.tani"], "dig: skill_tag row 134"),
    ("天地无极", [F1 + r"\F1s03cy剑技能11c.ani"], "dig: skill_tag row 309"),
    ("九转归一", [F1 + r"\F1scy03气攻击01_九转HD.tani"], "dig: skill_tag row 305"),
    ("天绝地灭", [F1 + r"\F1stm09机关攻击03七煞毒.tani"], "dig: skill_tag row 3108"),
    ("破风", [F1 + r"\F1s04tc技能11b_穿云hd.tani"], "dig: shared thrust (verify)"),
    ("怖畏暗刑", [F1 + r"\F1smj10双刀攻击07a.tani"], "dig: skill_tag row 3975"),
    ("驱夜断愁", [F1 + r"\F1smj10双刀攻击10连杀.tani"], "dig: skill_tag row 3979"),
    ("极乐引", [F1 + r"\F1smj10圣火诀buff02.tani"], "dig: skill_tag row 3971"),
]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", default=os.environ.get("AP_CACHE", DEFAULT_CACHE))
    ap.add_argument("--out", default=os.environ.get("AP_CANDIDATES_OUT", DEFAULT_OUT))
    args = ap.parse_args()

    with open(args.cache, encoding="utf-8") as fh:
        cache = json.load(fh)
    results = cache.get("results") or []

    abilities: dict[str, dict] = {}
    for e in results:
        raw = e.get("abilityName") or e.get("name") or ""
        if not raw:
            t = e.get("term") or {}
            raw = t.get("label") or ""
        if not raw:
            for t in e.get("terms") or []:
                if t.get("label"):
                    raw = t["label"]
                    break
        name = norm_name(raw)
        if not name:
            continue
        key = e.get("abilityKey") or f"{e.get('prefix','?')}:{e.get('id','?')}:{e.get('kind','?')}"
        abilities[key] = {
            "key": key,
            "id": str(e.get("id") or ""),
            "prefix": e.get("prefix") or "",
            "kind": e.get("kind") or "",
            "name": name,
            "rawName": raw,
            "status": e.get("status") or "",
            "tanis": tani_paths(e),
            "sounds": flatten_names(e.get("events")) + flatten_names(e.get("wems")),
            "notes": "cache",
        }

    # merge manual dig mappings into matching cache entries (by normalized name)
    manual_group: dict[str, dict] = {}
    for name, paths, note in MANUAL:
        matched = [a for a in abilities.values() if a["name"] == name]
        if matched:
            for a in matched:
                have = {p.lower() for p in a["tanis"]}
                for p in paths:
                    if p.lower() not in have:
                        a["tanis"].append(p)
                a["notes"] = (a["notes"] + "; " + note).strip("; ")
        else:
            g = manual_group.setdefault(name, {
                "key": "dig:" + name, "id": "", "prefix": "手工映射", "kind": "skill",
                "name": name, "rawName": name, "status": "dig", "tanis": [], "sounds": [],
                "notes": note,
            })
            have = {p.lower() for p in g["tanis"]}
            for p in paths:
                if p.lower() not in have:
                    g["tanis"].append(p)

    all_entries = list(abilities.values()) + list(manual_group.values())
    all_entries.sort(key=lambda a: (a["prefix"], a["name"]))

    out = {
        "generatedAt": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "cache": args.cache,
        "count": len(all_entries),
        "withTanis": sum(1 for a in all_entries if a["tanis"]),
        "abilities": all_entries,
    }
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)
    print(f"wrote {args.out}: {out['count']} abilities, {out['withTanis']} with tani candidates")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
