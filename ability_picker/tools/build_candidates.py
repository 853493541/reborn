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


def flatten_wems(items) -> list[str]:
    out = []
    for it in items or []:
        if isinstance(it, dict):
            v = it.get("id") or it.get("wem") or it.get("wwid")
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


# zhenchuan ability IDs (client skill ids the user supplied), id -> ability name
ZC_IDS = [
    # batch 2 - 绝境
    ("65161", "鹊踏枝"), ("65165", "雷震子"), ("65097", "韦陀献杵"), ("65048", "夺命蛊"),
    ("65159", "帝骖龙翔"), ("65154", "坐忘无我"), ("65107", "星楼月影"), ("65162", "蛊虫献祭"),
    ("64992", "长针"), ("65060", "无我无剑"), ("65105", "春泥护花"), ("65092", "圣明佑"),
    ("65061", "万剑归宗"), ("65242", "太阴指"), ("65156", "雷霆震怒"), ("65146", "七星拱瑞"),
    ("65160", "天地低昂"), ("65074", "穿心弩"), ("65149", "五方行尽"), ("65145", "三才化生"),
    ("65046", "蛇影"), ("64993", "商阳指"), ("65250", "啸如虎"), ("65078", "鹤归孤山"),
    ("65667", "亢龙有悔"), ("65090", "银月斩"), ("64898", "横扫六合"), ("65671", "棒打狗头"),
    ("65022", "穿"), ("65109", "芙蓉并蒂"), ("65100", "捉影式"), ("65153", "大道无术"),
    ("65101", "五蕴皆空"), ("65104", "无相诀"), ("65113", "傍花随柳"), ("65058", "玉石俱焚"),
    ("65065", "剑主天地"), ("65089", "烈日斩"), ("65024", "太极无极"), ("64994", "钟林毓秀"),
    ("64995", "兰摧玉折"), ("65087", "净世破魔击"), ("65059", "两仪化形"), ("65038", "灭"),
    ("65096", "驱夜断愁"), ("64899", "捕风式"), ("65116", "沧月"), ("65049", "迷心蛊"),
    ("65050", "枯残蛊"), ("64900", "抱残式"), ("64996", "破风"), ("65075", "听雷"),
    ("65083", "幽月轮"), ("65669", "龙战于野"), ("65108", "少明指"), ("65155", "剑转流云"),
    ("64991", "拿云式"), ("65157", "绛唇珠袖"), ("65127", "冲阴阳"), ("65140", "吞日月"),
    ("65138", "凌太虚"), ("65142", "生太极"), ("65028", "剑飞惊天"), ("64901", "守缺式"),
    ("65670", "潜龙勿用"), ("39494", "风流云散"), ("39493", "引窍"), ("65133", "碎星辰"),
    ("65136", "破苍穹"), ("65119", "撼如雷"), ("65036", "龙吟"), ("65150", "人剑合一"),
    ("65120", "渊"), ("39483", "雾暗迷云"), ("65053", "化血镖"), ("65054", "孔雀翎"),
    ("65076", "云飞玉皇"), ("65668", "狂龙乱舞"), ("65071", "追命箭"), ("65672", "笑醉狂"),
    ("65244", "浮光掠影"), ("65062", "天地无极"), ("65241", "鸟翔碧空"), ("65240", "疾"),
    ("65029", "龙牙"), ("65118", "疾如风"), ("65026", "三环套月"), ("65098", "摩诃无量"),
    ("65095", "生死劫"), ("65047", "蟾啸"), ("65243", "暗尘弥散"), ("65103", "大狮子吼"),
    ("65251", "风袖低昂"), ("65254", "千蝶吐瑞"), ("65068", "百足"), ("39492", "截阳"),
    ("65163", "女娲补天"), ("65144", "任驰骋"), ("65167", "镇山河"),
    # batch 1 - 道具/伪传
    ("27967", "风来吴山"), ("30261", "无间狱"), ("30226", "心诤"), ("30257", "剑破虚空"),
    ("27903", "散流霞"), ("30082", "踏星行"), ("32245", "花语酥心"), ("30025", "转乾坤"),
    ("30023", "锻骨诀"), ("27846", "云栖松"), ("27878", "孤风飒踏"), ("27905", "跃潮斩波"),
    ("30024", "守如山"), ("27869", "烟雨行"), ("30243", "龙啸九天"), ("27926", "九转归一"),
    ("27872", "撼地"), ("27969", "孤影化双"), ("30081", "极乐引"), ("30234", "鸿蒙天禁"),
    ("30276", "逐云寒蕊"), ("30231", "疾电叱羽"), ("36501", "应天授命"), ("27906", "斩无常"),
    ("27874", "十方玄机"), ("30029", "惊鸿游龙"), ("30235", "天绝地灭"), ("32300", "连环弩"),
    ("27902", "楚河汉界"), ("30262", "振翅图南"), ("30233", "飞刃回转"), ("27904", "翔极碧落"),
    ("28046", "魂压怒涛"), ("28045", "洞烛机微"), ("27863", "怖畏暗刑"), ("27888", "舍身诀"),
    ("30123", "凌然天风"), ("27850", "紫气东来"), ("27875", "斗转星移"), ("34594", "洗兵雨"),
    ("27847", "霞流宝石"), ("27890", "抢珠式"), ("27892", "九霄风雷"), ("28031", "临时飞爪"),
    ("27857", "琴音共鸣"), ("34591", "游风飘踪"), ("32287", "徐如林"), ("27862", "梯云纵"),
    ("36500", "乘黄之威"), ("27871", "盾立"), ("30301", "绿野蔓生"), ("32247", "如意法"),
    ("30247", "驭羽骋风"), ("27924", "蚀心蛊"), ("27855", "蝶弄足"), ("30239", "穹隆化生"),
    ("27844", "听风吹雪"), ("30238", "玄水蛊"), ("27895", "化蝶"),
]


# identified tani per ability (verified during the animation research).
# value = list of filename substrings, tried in order against the candidates.
RESOLVE = {
    # IP research pass replacements (new primary picks, user to verify)
    "疾": ["s04tc技能16_疾HD"],
    "蝶弄足": ["s05qx扇技能18_蝶弄足HD"],
    "净世破魔击": ["smj10双刀攻击06b_净世破魔击_月_悟"],
    "渊": ["s04tc技能18a_渊HD"],
    "听雷": ["s07cj内功轻剑12_听雷HD"],
    "穿": ["s04tc技能11a_穿云hd"],
    "孔雀翎": ["stm09针攻击02_孔雀翎_悟"],
    "风来吴山": ["s07cj重剑技能15_风来吴山HD"],
    "剑破虚空": ["s05qx剑技能13_剑破虚空HD"],
    "龙牙": ["s04tc技能13_龙牙hd"],
    "龙吟": ["s04tc技能12_龙吟HD"],
    "灭": ["s04tc技能17_灭HD"],
    "银月斩": ["smj10双刀攻击04_月斩"],
    "烈日斩": ["smj10组合刀攻击04_日斩"],
    "春泥护花": ["s01wh技能15_春泥HD"],
    "长针": ["s01wh针灸13b长针hd"],
    "星楼月影": ["s01wh技能12_星楼月影hd"],
    "帝骖龙翔": ["s05qx剑技能23_帝骖龙翔HD"],
    "雷霆震怒": ["s05qx剑技能25_雷霆震怒hd"],
    "百足": ["swd08毒攻击03_百足"],
    "蛇影": ["swd08毒攻击02_蛇影"],
    "三环套月": ["s03cy剑技能11b_三环HD"],
    "万剑归宗": ["s03cy剑技能13_万剑HD"],
    "人剑合一": ["s03cy剑技能15_人剑合一hd"],
    "五方行尽": ["s03cy气技能13_五方HD"],
    "九转归一": ["scy03气攻击01_九转HD"],
    "听雷_legacy": ["s07cj内功重剑12_听雷HD"],
    "天地低昂": ["s05qx剑技能20_天地低昂hd"],
    "大狮子吼": ["ssl04技能06_hd01"],
    "韦陀献杵": ["s04sl棍技能12_hd02"],
    "心诤": ["s04sl棍技能18a_扫击蓄力01"],
    "摩诃无量": ["s04sl棍技能14_hd03"],
    "捕风式": ["s04sl爪技能11_hd01"],
    "抢珠式": ["F1s04sl爪技能12"],
    "拿云式": ["s04sl爪技能13_hd01"],
    "捉影式": ["s04sl爪技能14a_hd01"],
    "守缺式": ["s04sl爪技能15a_hd01"],
    "撼如雷": ["s04tc技能22_撼HD"],
    "穹隆化生": ["s03cy气技能15a_穹窿化生01_悟", "s03cy气技能15b_穹窿化生01_悟"],
    "斗转星移": ["s17ytz星相技01_斗转星"],
    "九霄风雷": ["h唐门弩车001b_技能02a_HD"],
    "洞烛机微": ["h唐门弩车001b_技能02a_HD"],
    "魂压怒涛": ["h唐门弩车001b_技能02a_HD"],
    "临时飞爪": ["s16lxg链技能03_释放HD", "s16lxg链技能03_HD"],
    "花语酥心": ["swh01辅助技能03"],
    "天地无极": ["s03cy剑技能11c"],
    "破风": ["s04tc技能11b_穿云hd"],
    "怖畏暗刑": ["smj10双刀攻击07a"],
    "驱夜断愁": ["smj10双刀攻击10连杀"],
    "极乐引": ["smj10圣火诀buff02"],
    "云飞玉皇": ["s07cj重剑技能11a_云飞HD"],
    "天绝地灭": ["stm09机关攻击03七煞毒"],
    "云栖松": ["s07cj内功重剑18_云栖松hd"],
    "追命箭": ["stm09弩攻击03夺命01"],
    "凌然天风": ["s18yz凌然天风01.tani"],
    "无间狱": ["s16lxg链技能08_hd"],
    "剑主天地": ["F1sqx05双剑攻击04"],
    "龙战于野": ["F1sgb11掌法攻击05b"],
    "抱残式": ["F1ssl04爪攻击02"],
    "五蕴皆空": ["F1ssl04袈裟攻击05"],
    "雾暗迷云": ["F1s21ds技能04b"],
    "踏星行": ["F1s17ytz乘灯01"],
    "圣明佑": ["F1smj10乾坤大法buff01"],
    "翔极碧落": ["F1s15pl伞技能空04c"],
    "徐如林": ["F1stc04技能01-02"],
    "凌太虚": ["F1scy03技能05-05"],
    "傍花随柳": ["F1s01wh点穴13a_兰摧玉折hd"],
    "如意法": ["F1smj10双刀buff04_清净心01"],
    "散流霞": ["F1s14bd双刀技能08_01"],
    "舍身诀": ["F1s04sl技能11"],
    "无相诀": ["F1s04sl技能12"],
    "锻骨诀": ["F1s04sl技能16"],
    "横扫六合": ["F1s04sl棍技能13"],
    "十方玄机": ["F1s16lxg技能02a"],
}

# no in-data animation binding found; best shared/sibling-slot suggestion
# (shown orange in the picker, needs engine-scrub confirmation)
DEDUCE = {}

# user-flagged "in progress" abilities (IP checkbox in the picker filters to these)
IP = {
    "乘黄之威": "断魂刺 match wrong; player beat = generic leap, then pet A393 (summon/roar) + sounds needed",
    "九霄风雷": "needs more improvement (parked by user)",
    "徐如林": "it is a phase-active animation, not the active animation",
    "心诤": "missing 扫击 phase (18b 扫击释放 added as extra match)",
    "散流霞": "use normal version, not 悟 (now F1s14bd双刀技能08_01)",
    "无间狱": "missing the big ghost appearing behind the back when active (animation itself OK)",
    "琴音共鸣": "wrong; marked no-animation",
    "盾立": "no player animation in data; wrong sound removed; boss P081 sk02b is the only 盾立 anim",
    "穹隆化生": "using wrong one (15a vs 15b)",
    "蝶弄足": "wrong one",
    "跃潮斩波": "wrong (03a vs 03b)",
    "踏星行": "maybe wrong; look for more options",
    "驭羽骋风": "wrong but related",
    "龙啸九天": "animation correct; sound wrong",
    "两仪化形": "animation maybe wrong; sound 100% wrong",
    "云飞玉皇": "missing cast-success (sound)",
    "冲阴阳": "wrong; 剑冲阴阳 is a different ability",
    "净世破魔击": "wrong; it is the moon (月) version, current one is the sun (日) version",
    "化血镖": "good, but relist all 化血镖 variants (the many version)",
    "千蝶吐瑞": "missing; it is only an active animation",
    "听雷": "seems wrong",
    "夺命蛊": "two different entries, actually the same",
    "孔雀翎": "wrong",
    "截阳": "using wrong one",
    "春泥护花": "candidate list contained 太阴指 (removed, not needed)",
    "暗尘弥散": "wrong",
    "浮光掠影": "maybe wrong version (HD vs 悟); animation correct",
    "渊": "wrong",
    "生死劫": "maybe wrong",
    "疾": "completely wrong",
    "穿": "wrong",
    "追命箭": "missing channel phase",
    "风流云散": "wrong",
}

# abilities with no animation binding at all (shown as "(no animation)")
NOANIM = {"琴音共鸣"}

# wrong binding must not be kept as the green match (candidates only)
NO_MATCH = {"冲阴阳"}

# extra phase clips that belong to an already matched ability; all shown green
MATCHED_EXTRA = {
    "心诤": ["F1s04sl棍技能18b_扫击释放01"],
    "追命箭": ["F1stm09弩蓄力03夺命01_追命箭_悟"],
}

# replacement candidates found during the IP research pass (appended for scrub)
ADD = {
    "乘黄之威": ["s20wl弓技能01a_骑乘", "s20wl弓技能01b_骑乘", "s20wl升空01", "s20wl风尽浮沉01a_b", "F1HA393_start01"],
    "九霄风雷": ["唐门弩车001b_上车01_HD", "唐门弩车001b_下车01_hd", "唐门弩车001b_技能02a_悟", "唐门弩车001b_下车01_悟"],
    "徐如林": ["stc04技能01-01", "stc04技能01-03", "stc04技能01-04", "stc04技能01-05", "stc04技能02-01", "stc04技能02-02"],
    "盾立": ["scy12刀盾buff01_01", "scy12刀盾buff01盾壁01", "scy12刀盾buff01_苍云盾护01", "sqg12cy刀盾buff01_盾壁HD", "scy12刀盾buff01_盾壁_悟", "scy12盾墙01a_盾墙释放01", "scy12盾墙01a_盾墙_悟"],
    "穹隆化生": ["s03cy气技能15b_穹窿化生01_悟"],
    "蝶弄足": ["sqg05qx扇技能18_蝶弄足hd"],
    "跃潮斩波": ["s15pl掌技能空03b_跃潮斩波_悟"],
    "踏星行": ["s17ytz星相技01_HD", "s17ytz星相技02_HD", "s17ytz星相技02_解_HD", "s17ytz星相技04皆字_HD", "s17ytz星相技05_HD", "s17ytz星相技06_HD", "s17ytz占卜技04", "s17ytz占卜技05"],
    "驭羽骋风": ["s15pl姿态切换01_驭羽骋风HD", "s15pl姿态切换01_奇穴_驭羽骋风hd", "s15pl姿态切换01_驭羽骋风_悟", "s15pl姿态切换04_驭羽骋风hd", "s15pl雕技能空02_驭羽骋风HD"],
    "两仪化形": ["s03cy气技能11a_两仪HD", "s03cy气技能11_两仪HD", "s03cy气技能11b_悟_两仪化形_悟", "scy03纯阳技能18_两仪化形_贰式_悟"],
    "云飞玉皇": ["s07cj重剑技能11_奇穴_云飞HD", "s07cj重剑技能11b_云飞HD", "s07cj重剑技能11a_云飞_悟", "s07cj重剑技能11_奇穴_云飞_悟"],
    "冲阴阳": ["s03cy技能16b_生太极HD", "s03cy技能13a_生太极hd"],
    "化血镖": ["stm09针攻击01_化血镖_悟", "stm09针攻击01毒芒针"],
    "千蝶吐瑞": ["swd08治疗03_金蝉吐瑞", "swd08治疗03_金蝉吐瑞空战", "swd08治疗03_千蝶皮肤", "swd08治疗03_千蝶皮肤空战"],
    "听雷": ["s07cj内功轻剑12_听雷_悟", "s07cj内功轻剑21_悟_听雷_悟", "s07cj内功重剑12_听雷_悟"],
    "夺命蛊": ["swd08蛊攻击01_枯残蛊", "swd08蛊攻击01_迷心蛊"],
    "孔雀翎": ["sqg09tm针攻击02_孔雀翎HD", "stm09针攻击02孔雀"],
    "截阳": ["s21ds指法15_悟_破穴贰式01_悟", "s21ds指法14_悟_破穴01_悟", "s21ds指法13_悟_断脉01_悟", "s21ds指法12_悟_锁神01_悟"],
    "春泥护花": ["s01wh技能15_春泥护花_悟"],
    "暗尘弥散": ["smj10双刀buff03_暗尘弥散_悟", "smj10双刀buff03_暗尘弥散hd", "smj10双刀buff03隐匿"],
    "浮光掠影": ["sqg09tm伪装01_浮光HD"],
    "渊": ["s04tc技能18b_渊HD", "sqg04tc技能18a_渊hd", "sqg04tc技能18b_渊hd"],
    "生死劫": ["smj10乾坤大法buff04_生死劫_悟", "smj10圣火诀buff02a_生死劫_悟"],
    "疾": ["s04tc技能16_疾01_悟", "sqg04tc技能16_疾hd"],
    "穿": ["s04tc技能11b_穿云hd"],
    "追命箭": ["stm09弩蓄力03夺命01_追命箭_悟"],
    "风流云散": ["s21ds步法01_b", "s21ds步法01_f"],
}

# replacement wem ids (prepended so they are tried/played first)
WEM_ADD = {
    "龙啸九天": ["206378928"],
    "两仪化形": ["28700397", "252763356", "101880340", "533720938"],
    "云飞玉皇": ["315695614", "93452115", "90745220", "578379755"],
}

# playable process steps per ability (visual timeline + staged playback in the picker)
# step = {t: ms, kind: anim|sound|dummy|remove, v: value, n: note, x/y/z: world offset}
PROCESS = {
    "临时飞爪": [
        {"t": 0, "kind": "cursor", "v": r"data\source\other\特效\系统\SFX\其他\鼠标移动.Sfx",
         "n": "瞄准指示 = cursor_effect.txt 的鼠标特效; 范围 40尺 = 2560u"},
        {"t": 0, "kind": "action", "v": "DoAction(0,140022)", "n": "施法者动作 140022/140023"},
        {"t": 0, "kind": "anim", "v": "s16lxg链技能03_释放HD", "n": "投掷/释放 (孤风飒踏)"},
        {"t": 0, "kind": "sound", "v": "62588785", "n": "链音效 (s16lxglianjineng01_HD)"},
        {"t": 0, "kind": "chain", "v": "device 67816 / model 70025=a021b",
         "n": "装置NPC在落点: 引擎隐藏 (buff 12363 气场隐藏且无敌 + 12343 悬停), 仅提供 S_fxmid 锚点, 不显示"},
        {"t": 83, "kind": "anim", "v": "s16lxg链技能03b_hd", "n": "牵引冲刺动画 (skill_dash: 28033 -> AnimationID 91076 -> 03b_hd.tani)"},
        {"t": 83, "kind": "chain", "v": "s_锁链01.pss", "n": "链状表现 28032: S_rh->S_fxmid (Millisecond=1000, EnableRotation=1)"},
        {"t": 83, "kind": "move", "v": "DASH_TO_POINT(120)", "n": "牵引: 120 u/帧 @15fps = 1800 u/s"},
        {"t": 450, "kind": "sound", "v": "697798714", "n": "命中/固定音"},
        {"t": 800, "kind": "anim", "v": "s16lxg链技能03_缓冲HD", "n": "落地缓冲 (AnimationID 91074)"},
        {"t": 2700, "kind": "chain", "v": "remove device", "n": "装置消失 (160帧寿命, 隐藏物)"},
    ],
    "如意法": [
        {"t": 0, "kind": "anim", "v": r"data\source\player\f1\动作\f1smj10双刀buff04.ani",
         "durMs": 939,
         "n": "免控姿态基础动画 31f@33fps=939ms, 播放一次 (tani 内嵌 .Sfx 标签会让宿主 AV; 播放其基础 .ani)"},
        {"t": 0, "kind": "sound", "v": "75054615", "n": "riyuejiaohui.wav"},
        {"t": 0, "kind": "dummy", "v": r"data\source\other\hd特效\技能\pss\发招\m_明教清净心01.pss",
         "k": "ruyi_pss", "durMs": 12480,
         "n": "清净心发招 PSS: 作者寿命最长 12480ms (RepeatTimes=1), 随施法者"},
    ],
    "五蕴皆空": [
        {"t": 0, "kind": "anim", "v": r"data\source\player\f1\动作\f1ssl04袈裟攻击05.ani",
         "durMs": 576,
         "n": "基础动画 576ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "157383905", "n": "157383905.wav"},
    ],
    "凌太虚": [
        {"t": 0, "kind": "anim", "v": r"data\source\player\f1\动作\f1scy03技能05.ani",
         "durMs": 620,
         "n": "基础动画 620ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
    ],
    "撼如雷": [
        {"t": 0, "kind": "anim", "v": r"data\source\player\f1\动作\f1s04tc技能22.ani",
         "durMs": 939,
         "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "22651465", "n": "22651465.wav"},
        {"t": 0, "kind": "dummy", "v": r"data\source\other\hd特效\技能\pss\发招\t_天策撼如雷02_重制.pss",
         "k": "cast_pss", "durMs": 8400,
         "n": "施法者 PSS 作者寿命 8400ms"},
    ],
    "傍花随柳": [
        {"t": 0, "kind": "anim", "v": r"data\source\player\f1\动作\f1s01wh点穴13a.ani",
         "durMs": 1697,
         "n": "基础动画 1697ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "7390771", "n": "7390771.wav"},
        {"t": 0, "kind": "dummy", "v": r"data\source\other\hd特效\技能\pss\发招\w_万花蓄力脚下.pss",
         "k": "cast_pss", "durMs": 14880,
         "n": "施法者 PSS 作者寿命 14880ms"},
    ],
    "天地无极": [
        {"t": 0, "kind": "anim", "v": r"data\source\player\f1\动作\F1s03cy剑技能11c.ani",
         "durMs": 1394,
         "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "252612285", "n": "252612285.wav"},
    ],
    "坐忘无我": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能11.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "209352714", "n": "209352714.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳_坐忘无我_起跳_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "夺命蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "121381088", "n": "121381088.wav"},
    ],
    "帝骖龙翔": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能23.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "795435370", "n": "795435370.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀帝骖龙翔002.pss", "durMs": 24000, "n": "施法者 PSS 作者寿命 24000ms", "k": "cast_pss"},
    ],
    "雷震子": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09控制03.ani", "durMs": 394, "n": "基础动画 394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "641180650", "n": "641180650.wav"},
    ],
    "韦陀献杵": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl棍技能12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "227396769", "n": "227396769.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\h_灰层.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "鹊踏枝": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能22.ani", "durMs": 1303, "n": "基础动画 1303ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "199467777", "n": "199467777.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀鹊踏枝01.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "九转归一": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1scy03气攻击01.ani", "durMs": 816, "n": "基础动画 816ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "235665690", "n": "235665690.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳气攻击02.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "九霄风雷": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1h唐门弩车001b_技能02a.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "217082779", "n": "217082779.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门弩车大弩蓄力_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "云栖松": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s07cj内功重剑18.ani", "durMs": 1606, "n": "基础动画 1606ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "138653210", "n": "138653210.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_藏剑buff_a01.pss", "durMs": 25200, "n": "施法者 PSS 作者寿命 25200ms", "k": "cast_pss"},
    ],
    "人剑合一": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能15.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "31836471", "n": "31836471.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳剑蓄力02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "任驰骋": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能28.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "86402225", "n": "86402225.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策手拖01_重制.pss", "durMs": 6000, "n": "施法者 PSS 作者寿命 6000ms", "k": "cast_pss"},
    ],
    "凌然天风": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s18yz凌然天风01.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "198699810", "n": "198699810.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\y_yz药宗卷轴_起跳_01.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "剑破虚空": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能13.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "173126276", "n": "173126276.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀剑破虚空_蓄力.pss", "durMs": 24000, "n": "施法者 PSS 作者寿命 24000ms", "k": "cast_pss"},
    ],
    "剑转流云": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能14.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "80988625", "n": "80988625.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀剑转流云01.pss", "durMs": 12960, "n": "施法者 PSS 作者寿命 12960ms", "k": "cast_pss"},
    ],
    "十方玄机": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s16lxg技能02a.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "157918947", "n": "157918947.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\lxg_玄机蓄力01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "听风吹雪": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh针灸16.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "77386871", "n": "77386871.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花听风01.pss", "durMs": 12960, "n": "施法者 PSS 作者寿命 12960ms", "k": "cast_pss"},
    ],
    "啸如虎": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能25.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "117804806", "n": "117804806.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策啸如虎01.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "天绝地灭": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09机关攻击03.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "59615434", "n": "59615434.wav"},
    ],
    "孤影化双": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s13cg琴技能11a.ani", "durMs": 930, "n": "基础动画 930ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "1058902080", "n": "1058902080.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_长歌_云生结海_琴弦01_悟.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "孤风飒踏": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s16lxg链技能03_悟.ani", "durMs": 1091, "n": "基础动画 1091ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "62588785", "n": "62588785.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\l_凌雪阁_孤风飒踏_起跳01_悟.pss", "durMs": 10320, "n": "施法者 PSS 作者寿命 10320ms", "k": "cast_pss"},
    ],
    "守如山": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能23.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "283074756", "n": "283074756.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策守如山01_重制.pss", "durMs": 19200, "n": "施法者 PSS 作者寿命 19200ms", "k": "cast_pss"},
    ],
    "应天授命": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s20wlbuff03.ani", "durMs": 1303, "n": "基础动画 1303ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "93959721", "n": "93959721.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万灵_应天授命_蓄力01_悟.pss", "durMs": 11040, "n": "施法者 PSS 作者寿命 11040ms", "k": "cast_pss"},
    ],
    "徐如林": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stc04技能01.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "307459863", "n": "307459863.wav"},
    ],
    "心诤": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl棍技能18a.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "337850833", "n": "337850833.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\s_少林_扫击蓄力01.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "怖畏暗刑": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击07a.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "695637316", "n": "695637316.wav"},
    ],
    "惊鸿游龙": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09buff01.ani", "durMs": 273, "n": "基础动画 273ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "398995430", "n": "398995430.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_惊鸿游龙_扩散01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "抢珠式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能12.ani", "durMs": 1091, "n": "基础动画 1091ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "878465710", "n": "878465710.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\f_佛珠.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "振翅图南": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s15pl雕技能03.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "498850934", "n": "498850934.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\p_蓬莱_振翅图南_释放01_悟.pss", "durMs": 4560, "n": "施法者 PSS 作者寿命 4560ms", "k": "cast_pss"},
    ],
    "捉影式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能14a.ani", "durMs": 636, "n": "基础动画 636ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "389392912", "n": "389392912.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\j_聚气少林02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "撼地": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sqg12cy刀盾攻击12.ani", "durMs": 879, "n": "基础动画 879ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "367701865", "n": "367701865.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\新特效\\技能\\pss\\状态\\c_苍云拖尾03.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "散流霞": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s14bd双刀技能08.ani", "durMs": 697, "n": "基础动画 697ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "820258212", "n": "820258212.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\新特效\\技能\\pss\\发招\\b_霸刀翻周身01.pss", "durMs": 5280, "n": "施法者 PSS 作者寿命 5280ms", "k": "cast_pss"},
    ],
    "斗转星移": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s17ytz星相技01.ani", "durMs": 1364, "n": "基础动画 1364ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "735859637", "n": "735859637.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\y_衍天宗_斗转星_释放01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "斩无常": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s16lxg鞭技能04_悟.ani", "durMs": 15062, "n": "基础动画 15062ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "746207134", "n": "746207134.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\l_凌雪阁_斩无常_盾01_悟.pss", "durMs": 11280, "n": "施法者 PSS 作者寿命 11280ms", "k": "cast_pss"},
    ],
    "无间狱": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s16lxg链技能08.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "340506192", "n": "340506192.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\lxg_修罗释放02.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "星楼月影": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh技能12.ani", "durMs": 1303, "n": "基础动画 1303ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "315572417", "n": "315572417.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花水墨晕开_重置_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "春泥护花": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh技能15.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "70432965", "n": "70432965.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花春泥01_重置.pss", "durMs": 9120, "n": "施法者 PSS 作者寿命 9120ms", "k": "cast_pss"},
    ],
    "暗尘弥散": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击13.ani", "durMs": 2909, "n": "基础动画 2909ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "447482419", "n": "447482419.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\m_明教暗尘弥散_起跳02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "极乐引": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10圣火诀buff02.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "164321672", "n": "164321672.wav"},
    ],
    "枯残蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "573915315", "n": "573915315.wav"},
    ],
    "梯云纵": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能12a.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "196781273", "n": "196781273.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳聚气z_01.pss", "durMs": 12000, "n": "施法者 PSS 作者寿命 12000ms", "k": "cast_pss"},
    ],
    "楚河汉界": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sqg14bd拔刀技能07.ani", "durMs": 1485, "n": "基础动画 1485ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "817911493", "n": "817911493.wav"},
    ],
    "洗兵雨": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s18dz擒拿02.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "836145145", "n": "836145145.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\d_刀宗_洗兵雨_释放01_悟.pss", "durMs": 3840, "n": "施法者 PSS 作者寿命 3840ms", "k": "cast_pss"},
    ],
    "浮光掠影": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09伪装01.ani", "durMs": 318, "n": "基础动画 318ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "54653536", "n": "54653536.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_浮光掠影_蓄力_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "游风飘踪": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s18dz身法04_单.ani", "durMs": 1030, "n": "基础动画 1030ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "60595593", "n": "60595593.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\d_刀宗_游风飘踪_蓄力02_悟.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "烟雨行": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11前冲刺01_悟.ani", "durMs": 1000, "n": "基础动画 1000ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "241742548", "n": "241742548.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮_烟雨行_爆发01_悟.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "玄水蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sqg08wd蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "211858359", "n": "211858359.wav"},
    ],
    "生太极": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能13a.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄力03.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "疾如风": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能24.ani", "durMs": 758, "n": "基础动画 758ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "43019181", "n": "43019181.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策疾如风02_重制.pss", "durMs": 10896, "n": "施法者 PSS 作者寿命 10896ms", "k": "cast_pss"},
    ],
    "疾电叱羽": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s15pl雕技能04a.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "377449004", "n": "377449004.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\p_蓬莱吸收蓄力_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "穹隆化生": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能15a.ani", "durMs": 1545, "n": "基础动画 1545ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "16601009", "n": "16601009.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳_七星_聚气01_悟.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "笑醉狂": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11buff01.ani", "durMs": 8970, "n": "基础动画 8970ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "128409036", "n": "128409036.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮群体笑醉狂01.pss", "durMs": 14400, "n": "施法者 PSS 作者寿命 14400ms", "k": "cast_pss"},
    ],
    "紫气东来": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能19.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "294240191", "n": "294240191.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳八卦01.pss", "durMs": 28800, "n": "施法者 PSS 作者寿命 28800ms", "k": "cast_pss"},
    ],
    "绛唇珠袖": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能26.ani", "durMs": 1545, "n": "基础动画 1545ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "258176149", "n": "258176149.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀绛唇珠袖手.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "绿野蔓生": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s18yz植物系03.ani", "durMs": 1848, "n": "基础动画 1848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "831605528", "n": "831605528.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\y_药宗_施法_荆棘盾.pss", "durMs": 14400, "n": "施法者 PSS 作者寿命 14400ms", "k": "cast_pss"},
    ],
    "翔极碧落": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s15pl伞技能空04c.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "390961829", "n": "390961829.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\p_蓬莱打飞02.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "舍身诀": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl技能11.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "43553148", "n": "43553148.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\f_佛像_02.pss", "durMs": 12240, "n": "施法者 PSS 作者寿命 12240ms", "k": "cast_pss"},
    ],
    "花语酥心": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swh01辅助技能03.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "306672272", "n": "306672272.wav"},
    ],
    "蚀心蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sqg08wd蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "121381088", "n": "121381088.wav"},
    ],
    "蝶弄足": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx扇技能18.ani", "durMs": 1000, "n": "基础动画 1000ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "712132253", "n": "712132253.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀手部拖尾_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "跃潮斩波": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s15pl掌技能空03a.ani", "durMs": 636, "n": "基础动画 636ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "897349988", "n": "897349988.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\p_蓬莱_跃潮斩波_下坠手拖尾_悟.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "踏星行": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s17ytz乘灯01.ani", "durMs": 1848, "n": "基础动画 1848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "901987144", "n": "901987144.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\武器\\y_衍天宗踏星行_魂灯附魔01.pss", "durMs": 24000, "n": "施法者 PSS 作者寿命 24000ms", "k": "cast_pss"},
    ],
    "转乾坤": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能16.ani", "durMs": 1061, "n": "基础动画 1061ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "796924560", "n": "796924560.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳_转乾坤_施法_悟.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "连环弩": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09通道02.ani", "durMs": 2758, "n": "基础动画 2758ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "459546289", "n": "459546289.wav"},
    ],
    "逐云寒蕊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s18yz植物系03.ani", "durMs": 1848, "n": "基础动画 1848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "631681103", "n": "631681103.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\y_药宗逐云吟唱01.pss", "durMs": 7200, "n": "施法者 PSS 作者寿命 7200ms", "k": "cast_pss"},
    ],
    "锻骨诀": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl技能16.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "541556101", "n": "541556101.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\s_少林解印.pss", "durMs": 10800, "n": "施法者 PSS 作者寿命 10800ms", "k": "cast_pss"},
    ],
    "镇山河": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能13b.ani", "durMs": 1303, "n": "基础动画 1303ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "48443349", "n": "48443349.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳镇山河01.pss", "durMs": 12000, "n": "施法者 PSS 作者寿命 12000ms", "k": "cast_pss"},
    ],
    "霞流宝石": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s07cj重剑技能17.ani", "durMs": 1515, "n": "基础动画 1515ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "389359519", "n": "389359519.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_藏剑霞流宝石06.pss", "durMs": 9552, "n": "施法者 PSS 作者寿命 9552ms", "k": "cast_pss"},
    ],
    "风来吴山": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s07cj重剑技能15.ani", "durMs": 576, "n": "基础动画 576ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "161340541", "n": "161340541.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_藏剑刀光01b.pss", "durMs": 24000, "n": "施法者 PSS 作者寿命 24000ms", "k": "cast_pss"},
    ],
    "飞刃回转": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s16lxg链技能10.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "169644474", "n": "169644474.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\l_凌雪阁_飞刃回转_互换01_悟.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "驭羽骋风": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s15pl雕技能02.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "226536459", "n": "226536459.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\p_蓬莱爆闪_02.pss", "durMs": 11040, "n": "施法者 PSS 作者寿命 11040ms", "k": "cast_pss"},
    ],
    "鸿蒙天禁": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s17ytz星相技03.ani", "durMs": 2485, "n": "基础动画 2485ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "72605924", "n": "72605924.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\x_星相技画符_03.pss", "durMs": 17280, "n": "施法者 PSS 作者寿命 17280ms", "k": "cast_pss"},
    ],
    "龙啸九天": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11掌法攻击06_悟.ani", "durMs": 1242, "n": "基础动画 1242ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "21569854", "n": "21569854.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮_龙啸九天_聚气01_悟.pss", "durMs": 5760, "n": "施法者 PSS 作者寿命 5760ms", "k": "cast_pss"},
    ],
    "七星拱瑞": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能15a.ani", "durMs": 1545, "n": "基础动画 1545ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "453831787", "n": "453831787.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳_七星_聚气01_悟.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "七星拱瑞": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能15a.ani", "durMs": 1545, "n": "基础动画 1545ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "453831787", "n": "453831787.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳_七星_聚气01_悟.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "万剑归宗": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能13.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "450639193", "n": "450639193.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄01.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "万剑归宗": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能13.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "450639193", "n": "450639193.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄01.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "三才化生": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能18.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "793524874", "n": "793524874.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳攻击范围圈z_01.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "三才化生": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能18.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "793524874", "n": "793524874.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳攻击范围圈z_01.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "三环套月": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能11b.ani", "durMs": 1606, "n": "基础动画 1606ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "398301227", "n": "398301227.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳三环套月.pss", "durMs": 10176, "n": "施法者 PSS 作者寿命 10176ms", "k": "cast_pss"},
    ],
    "三环套月": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能11b.ani", "durMs": 1606, "n": "基础动画 1606ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "398301227", "n": "398301227.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳三环套月.pss", "durMs": 10176, "n": "施法者 PSS 作者寿命 10176ms", "k": "cast_pss"},
    ],
    "两仪化形": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能11a_悟.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "252763356", "n": "252763356.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳_两仪化形_蓄力02_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "两仪化形": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能11a_悟.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "252763356", "n": "252763356.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳_两仪化形_蓄力02_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "云飞玉皇": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s07cj重剑技能11a.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "93452115", "n": "93452115.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_藏剑蓄力01.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "云飞玉皇": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s07cj重剑技能11a.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "93452115", "n": "93452115.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_藏剑蓄力01.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "五方行尽": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能13.ani", "durMs": 1242, "n": "基础动画 1242ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "86733635", "n": "86733635.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄06.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "五方行尽": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能13.ani", "durMs": 1242, "n": "基础动画 1242ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "86733635", "n": "86733635.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄06.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "亢龙有悔": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11掌法攻击18a01.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "87945997", "n": "87945997.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮掌法04.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "亢龙有悔": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11掌法攻击18a01.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "87945997", "n": "87945997.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮掌法04.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "人剑合一": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能15.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "31836471", "n": "31836471.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳剑蓄力02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "人剑合一": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能15.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "31836471", "n": "31836471.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳剑蓄力02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "任驰骋": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能28.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "86402225", "n": "86402225.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策手拖01_重制.pss", "durMs": 6000, "n": "施法者 PSS 作者寿命 6000ms", "k": "cast_pss"},
    ],
    "任驰骋": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能28.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "86402225", "n": "86402225.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策手拖01_重制.pss", "durMs": 6000, "n": "施法者 PSS 作者寿命 6000ms", "k": "cast_pss"},
    ],
    "兰摧玉折": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴13a.ani", "durMs": 1697, "n": "基础动画 1697ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "7390771", "n": "7390771.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花蓄力脚下.pss", "durMs": 14880, "n": "施法者 PSS 作者寿命 14880ms", "k": "cast_pss"},
    ],
    "兰摧玉折": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴13a.ani", "durMs": 1697, "n": "基础动画 1697ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "7390771", "n": "7390771.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花蓄力脚下.pss", "durMs": 14880, "n": "施法者 PSS 作者寿命 14880ms", "k": "cast_pss"},
    ],
    "兰摧玉折": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴13a.ani", "durMs": 1697, "n": "基础动画 1697ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "7390771", "n": "7390771.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花蓄力脚下.pss", "durMs": 14880, "n": "施法者 PSS 作者寿命 14880ms", "k": "cast_pss"},
    ],
    "净世破魔击": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击06b.ani", "durMs": 1091, "n": "基础动画 1091ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "916670015", "n": "916670015.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\m_明教_净世破魔击_月_刀光01_悟.pss", "durMs": 6000, "n": "施法者 PSS 作者寿命 6000ms", "k": "cast_pss"},
    ],
    "净世破魔击": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击06b.ani", "durMs": 1091, "n": "基础动画 1091ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "916670015", "n": "916670015.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\m_明教_净世破魔击_月_刀光01_悟.pss", "durMs": 6000, "n": "施法者 PSS 作者寿命 6000ms", "k": "cast_pss"},
    ],
    "剑主天地": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sqx05双剑攻击04.ani", "durMs": 3588, "n": "基础动画 3588ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "370997661", "n": "370997661.wav"},
    ],
    "剑主天地": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sqx05双剑攻击04.ani", "durMs": 3588, "n": "基础动画 3588ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "370997661", "n": "370997661.wav"},
    ],
    "剑转流云": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能14.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "80988625", "n": "80988625.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀剑转流云01.pss", "durMs": 12960, "n": "施法者 PSS 作者寿命 12960ms", "k": "cast_pss"},
    ],
    "剑转流云": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能14.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "80988625", "n": "80988625.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀剑转流云01.pss", "durMs": 12960, "n": "施法者 PSS 作者寿命 12960ms", "k": "cast_pss"},
    ],
    "剑飞惊天": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能12.ani", "durMs": 1000, "n": "基础动画 1000ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "294586550", "n": "294586550.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳剑飞01.pss", "durMs": 10800, "n": "施法者 PSS 作者寿命 10800ms", "k": "cast_pss"},
    ],
    "剑飞惊天": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能12.ani", "durMs": 1000, "n": "基础动画 1000ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "294586550", "n": "294586550.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳剑飞01.pss", "durMs": 10800, "n": "施法者 PSS 作者寿命 10800ms", "k": "cast_pss"},
    ],
    "化血镖": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09针攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "559920750", "n": "559920750.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_化血镖_旋转01_悟.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "化血镖": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09针攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "559920750", "n": "559920750.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_化血镖_旋转01_悟.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "化血镖": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09针攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "559920750", "n": "559920750.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_化血镖_旋转01_悟.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "千蝶吐瑞": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08治疗03.ani", "durMs": 900, "n": "基础动画 900ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "829175179", "n": "829175179.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_五毒_千蝶吐瑞_气场02_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "千蝶吐瑞": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08治疗03.ani", "durMs": 900, "n": "基础动画 900ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "829175179", "n": "829175179.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_五毒_千蝶吐瑞_气场02_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "吞日月": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能18b.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳释放_吞日月.pss", "durMs": 9840, "n": "施法者 PSS 作者寿命 9840ms", "k": "cast_pss"},
    ],
    "吞日月": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能18b.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳释放_吞日月.pss", "durMs": 9840, "n": "施法者 PSS 作者寿命 9840ms", "k": "cast_pss"},
    ],
    "听雷": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s07cj内功轻剑12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "223855778", "n": "223855778.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_藏剑听雷轻剑.pss", "durMs": 10176, "n": "施法者 PSS 作者寿命 10176ms", "k": "cast_pss"},
    ],
    "听雷": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s07cj内功轻剑12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "223855778", "n": "223855778.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_藏剑听雷轻剑.pss", "durMs": 10176, "n": "施法者 PSS 作者寿命 10176ms", "k": "cast_pss"},
    ],
    "商阳指": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "657996750", "n": "657996750.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花笔迹03_新.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "商阳指": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "657996750", "n": "657996750.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花笔迹03_新.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "商阳指": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "657996750", "n": "657996750.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花笔迹03_新.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "啸如虎": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能25.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "117804806", "n": "117804806.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策啸如虎01.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "啸如虎": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能25.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "117804806", "n": "117804806.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策啸如虎01.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "啸如虎": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能25.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "117804806", "n": "117804806.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策啸如虎01.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "圣明佑": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10乾坤大法buff01.ani", "durMs": 1061, "n": "基础动画 1061ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "75054615", "n": "75054615.wav"},
    ],
    "圣明佑": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10乾坤大法buff01.ani", "durMs": 1061, "n": "基础动画 1061ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "75054615", "n": "75054615.wav"},
    ],
    "圣明佑": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10乾坤大法buff01.ani", "durMs": 1061, "n": "基础动画 1061ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "75054615", "n": "75054615.wav"},
    ],
    "大狮子吼": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1ssl04技能06.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "749751865", "n": "749751865.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\s_少林聚气z02.pss", "durMs": 12960, "n": "施法者 PSS 作者寿命 12960ms", "k": "cast_pss"},
    ],
    "大狮子吼": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1ssl04技能06.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "749751865", "n": "749751865.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\s_少林聚气z02.pss", "durMs": 12960, "n": "施法者 PSS 作者寿命 12960ms", "k": "cast_pss"},
    ],
    "大道无术": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1scy03纯阳技能11.ani", "durMs": 788, "n": "基础动画 788ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "169319130", "n": "169319130.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄力07.pss", "durMs": 12480, "n": "施法者 PSS 作者寿命 12480ms", "k": "cast_pss"},
    ],
    "大道无术": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1scy03纯阳技能11.ani", "durMs": 788, "n": "基础动画 788ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "169319130", "n": "169319130.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄力07.pss", "durMs": 12480, "n": "施法者 PSS 作者寿命 12480ms", "k": "cast_pss"},
    ],
    "天地低昂": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能20.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "676715470", "n": "676715470.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀手部拖尾_02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "天地低昂": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能20.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "676715470", "n": "676715470.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀手部拖尾_02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "太极无极": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能17.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "415238227", "n": "415238227.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳_太极无极_烟01_悟.pss", "durMs": 12720, "n": "施法者 PSS 作者寿命 12720ms", "k": "cast_pss"},
    ],
    "太极无极": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy气技能17.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "415238227", "n": "415238227.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳_太极无极_烟01_悟.pss", "durMs": 12720, "n": "施法者 PSS 作者寿命 12720ms", "k": "cast_pss"},
    ],
    "太阴指": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴19.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "293470965", "n": "293470965.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花_太阴指_施法01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "太阴指": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴19.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "293470965", "n": "293470965.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花_太阴指_施法01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "女娲补天": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08变身02.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "69800972", "n": "69800972.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_五毒_女娲补天结束_飘带_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "女娲补天": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08变身02.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "69800972", "n": "69800972.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_五毒_女娲补天结束_飘带_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "孔雀翎": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09针攻击02.ani", "durMs": 394, "n": "基础动画 394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "1041928453", "n": "1041928453.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_孔雀翎_聚气_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "孔雀翎": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09针攻击02.ani", "durMs": 394, "n": "基础动画 394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "1041928453", "n": "1041928453.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_孔雀翎_聚气_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "孔雀翎": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09针攻击02.ani", "durMs": 394, "n": "基础动画 394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "1041928453", "n": "1041928453.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_孔雀翎_聚气_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "守缺式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能15a.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "390920500", "n": "390920500.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\z_爪击少林02.pss", "durMs": 5760, "n": "施法者 PSS 作者寿命 5760ms", "k": "cast_pss"},
    ],
    "守缺式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能15a.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "390920500", "n": "390920500.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\z_爪击少林02.pss", "durMs": 5760, "n": "施法者 PSS 作者寿命 5760ms", "k": "cast_pss"},
    ],
    "少明指": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴21a.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "118979417", "n": "118979417.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花蓄力_01.pss", "durMs": 15840, "n": "施法者 PSS 作者寿命 15840ms", "k": "cast_pss"},
    ],
    "少明指": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴21a.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "118979417", "n": "118979417.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花蓄力_01.pss", "durMs": 15840, "n": "施法者 PSS 作者寿命 15840ms", "k": "cast_pss"},
    ],
    "幽月轮": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击01.ani", "durMs": 1242, "n": "基础动画 1242ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "293597586", "n": "293597586.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\m_明教_幽月轮1段_刀光01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "幽月轮": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击01.ani", "durMs": 1242, "n": "基础动画 1242ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "293597586", "n": "293597586.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\m_明教_幽月轮1段_刀光01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "引窍": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s21ds指法11a_悟.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "866782677", "n": "866782677.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\ds_段氏_引窍_释放02_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "引窍": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s21ds指法11a_悟.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "866782677", "n": "866782677.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\ds_段氏_引窍_释放02_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "截阳": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s21ds指法16_悟.ani", "durMs": 1212, "n": "基础动画 1212ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "668699640", "n": "668699640.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\ds_段氏_截阳_扇子01_悟.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "截阳": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s21ds指法16_悟.ani", "durMs": 1212, "n": "基础动画 1212ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "668699640", "n": "668699640.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\ds_段氏_截阳_扇子01_悟.pss", "durMs": 11520, "n": "施法者 PSS 作者寿命 11520ms", "k": "cast_pss"},
    ],
    "抱残式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1ssl04爪攻击02.ani", "durMs": 848, "n": "基础动画 848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "718779610", "n": "718779610.wav"},
    ],
    "抱残式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1ssl04爪攻击02.ani", "durMs": 848, "n": "基础动画 848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "718779610", "n": "718779610.wav"},
    ],
    "抱残式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1ssl04爪攻击02.ani", "durMs": 848, "n": "基础动画 848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "718779610", "n": "718779610.wav"},
    ],
    "拿云式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能13.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "270289801", "n": "270289801.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\s_手动画少林01.pss", "durMs": 7920, "n": "施法者 PSS 作者寿命 7920ms", "k": "cast_pss"},
    ],
    "拿云式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能13.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "270289801", "n": "270289801.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\s_手动画少林01.pss", "durMs": 7920, "n": "施法者 PSS 作者寿命 7920ms", "k": "cast_pss"},
    ],
    "捉影式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能14a.ani", "durMs": 636, "n": "基础动画 636ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "389392912", "n": "389392912.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\j_聚气少林02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "捉影式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能14a.ani", "durMs": 636, "n": "基础动画 636ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "389392912", "n": "389392912.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\j_聚气少林02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "捉影式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能14a.ani", "durMs": 636, "n": "基础动画 636ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "389392912", "n": "389392912.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\j_聚气少林02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "捕风式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能11.ani", "durMs": 1758, "n": "基础动画 1758ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "418339926", "n": "418339926.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\j_聚气少林01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "捕风式": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl爪技能11.ani", "durMs": 1758, "n": "基础动画 1758ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "418339926", "n": "418339926.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\j_聚气少林01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "摩诃无量": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl棍技能14.ani", "durMs": 1061, "n": "基础动画 1061ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "28830163", "n": "28830163.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\h_灰层.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "摩诃无量": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl棍技能14.ani", "durMs": 1061, "n": "基础动画 1061ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "28830163", "n": "28830163.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\h_灰层.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "无我无剑": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能14.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "431950557", "n": "431950557.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄力04.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "无我无剑": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy剑技能14.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "431950557", "n": "431950557.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄力04.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "无相诀": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl技能12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "130631848", "n": "130631848.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\s_少林拖尾.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "无相诀": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl技能12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "130631848", "n": "130631848.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\s_少林拖尾.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "星楼月影": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh技能12.ani", "durMs": 1303, "n": "基础动画 1303ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "315572417", "n": "315572417.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花水墨晕开_重置_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "星楼月影": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh技能12.ani", "durMs": 1303, "n": "基础动画 1303ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "315572417", "n": "315572417.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花水墨晕开_重置_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "星楼月影": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh技能12.ani", "durMs": 1303, "n": "基础动画 1303ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "315572417", "n": "315572417.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花水墨晕开_重置_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "春泥护花": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh技能15.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "70432965", "n": "70432965.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花春泥01_重置.pss", "durMs": 9120, "n": "施法者 PSS 作者寿命 9120ms", "k": "cast_pss"},
    ],
    "春泥护花": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh技能15.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "70432965", "n": "70432965.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花春泥01_重置.pss", "durMs": 9120, "n": "施法者 PSS 作者寿命 9120ms", "k": "cast_pss"},
    ],
    "春泥护花": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh技能15.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "70432965", "n": "70432965.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花春泥01_重置.pss", "durMs": 9120, "n": "施法者 PSS 作者寿命 9120ms", "k": "cast_pss"},
    ],
    "暗尘弥散": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击13.ani", "durMs": 2909, "n": "基础动画 2909ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "447482419", "n": "447482419.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\m_明教暗尘弥散_起跳02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "暗尘弥散": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击13.ani", "durMs": 2909, "n": "基础动画 2909ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "447482419", "n": "447482419.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\m_明教暗尘弥散_起跳02.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "枯残蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "573915315", "n": "573915315.wav"},
    ],
    "枯残蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "573915315", "n": "573915315.wav"},
    ],
    "枯残蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "573915315", "n": "573915315.wav"},
    ],
    "棒打狗头": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11棒法攻击01b_悟.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "369499237", "n": "369499237.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮_棒打狗头_棍刺01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "棒打狗头": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11棒法攻击01b_悟.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "369499237", "n": "369499237.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮_棒打狗头_棍刺01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "横扫六合": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl棍技能13.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "811042022", "n": "811042022.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\h_灰层.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "横扫六合": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04sl棍技能13.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "811042022", "n": "811042022.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\h_灰层.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "沧月": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能20.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "380559345", "n": "380559345.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策火01_重制.pss", "durMs": 2880, "n": "施法者 PSS 作者寿命 2880ms", "k": "cast_pss"},
    ],
    "沧月": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能20.ani", "durMs": 818, "n": "基础动画 818ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "380559345", "n": "380559345.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策火01_重制.pss", "durMs": 2880, "n": "施法者 PSS 作者寿命 2880ms", "k": "cast_pss"},
    ],
    "浮光掠影": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09伪装01.ani", "durMs": 318, "n": "基础动画 318ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "54653536", "n": "54653536.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_浮光掠影_蓄力_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "浮光掠影": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09伪装01.ani", "durMs": 318, "n": "基础动画 318ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "54653536", "n": "54653536.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_浮光掠影_蓄力_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "渊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能18a.ani", "durMs": 485, "n": "基础动画 485ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "234975435", "n": "234975435.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_冲刺_渊飘带_重制.pss", "durMs": 3360, "n": "施法者 PSS 作者寿命 3360ms", "k": "cast_pss"},
    ],
    "渊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能18a.ani", "durMs": 485, "n": "基础动画 485ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "234975435", "n": "234975435.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_冲刺_渊飘带_重制.pss", "durMs": 3360, "n": "施法者 PSS 作者寿命 3360ms", "k": "cast_pss"},
    ],
    "潜龙勿用": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11掌法攻击08.ani", "durMs": 2606, "n": "基础动画 2606ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "468067016", "n": "468067016.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮手掌01.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "潜龙勿用": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11掌法攻击08.ani", "durMs": 2606, "n": "基础动画 2606ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "468067016", "n": "468067016.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮手掌01.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "灭": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能17.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "896595858", "n": "896595858.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策灭02.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "灭": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能17.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "896595858", "n": "896595858.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策灭02.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "烈日斩": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10组合刀攻击04.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "173210721", "n": "173210721.wav"},
    ],
    "烈日斩": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10组合刀攻击04.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "173210721", "n": "173210721.wav"},
    ],
    "玉石俱焚": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴15.ani", "durMs": 1121, "n": "基础动画 1121ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "149060746", "n": "149060746.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花_玉石俱焚_起跳.pss", "durMs": 11280, "n": "施法者 PSS 作者寿命 11280ms", "k": "cast_pss"},
    ],
    "玉石俱焚": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴15.ani", "durMs": 1121, "n": "基础动画 1121ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "149060746", "n": "149060746.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花_玉石俱焚_起跳.pss", "durMs": 11280, "n": "施法者 PSS 作者寿命 11280ms", "k": "cast_pss"},
    ],
    "生太极": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能13a.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄力03.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "生太极": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能13a.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳蓄力03.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "生死劫": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10圣火诀buff02.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "927639305", "n": "927639305.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\m_明教_生死劫_蓄力01.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "生死劫": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10圣火诀buff02.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "927639305", "n": "927639305.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\m_明教_生死劫_蓄力01.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "疾": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能16.ani", "durMs": 1121, "n": "基础动画 1121ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "678228934", "n": "678228934.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策轻功转身_疾_重制.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "疾": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能16.ani", "durMs": 1121, "n": "基础动画 1121ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "678228934", "n": "678228934.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策轻功转身_疾_重制.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "疾如风": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能24.ani", "durMs": 758, "n": "基础动画 758ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "43019181", "n": "43019181.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策疾如风02_重制.pss", "durMs": 10896, "n": "施法者 PSS 作者寿命 10896ms", "k": "cast_pss"},
    ],
    "疾如风": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能24.ani", "durMs": 758, "n": "基础动画 758ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "43019181", "n": "43019181.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策疾如风02_重制.pss", "durMs": 10896, "n": "施法者 PSS 作者寿命 10896ms", "k": "cast_pss"},
    ],
    "百足": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08毒攻击03.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "482646056", "n": "482646056.wav"},
    ],
    "百足": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08毒攻击03.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "482646056", "n": "482646056.wav"},
    ],
    "百足": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08毒攻击03.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "482646056", "n": "482646056.wav"},
    ],
    "破苍穹": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能15b.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳曝气_破苍穹.pss", "durMs": 11040, "n": "施法者 PSS 作者寿命 11040ms", "k": "cast_pss"},
    ],
    "破苍穹": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能15b.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳曝气_破苍穹.pss", "durMs": 11040, "n": "施法者 PSS 作者寿命 11040ms", "k": "cast_pss"},
    ],
    "破风": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能11b.ani", "durMs": 1545, "n": "基础动画 1545ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "780764483", "n": "780764483.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策圆形刀光01.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "破风": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能11b.ani", "durMs": 1545, "n": "基础动画 1545ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "780764483", "n": "780764483.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策圆形刀光01.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "碎星辰": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能17b.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳释放碎星辰.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "碎星辰": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能17b.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "45472664", "n": "45472664.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳释放碎星辰.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "穿": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能11a.ani", "durMs": 1515, "n": "基础动画 1515ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "39771509", "n": "39771509.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策尖刺04.pss", "durMs": 5520, "n": "施法者 PSS 作者寿命 5520ms", "k": "cast_pss"},
    ],
    "穿": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能11a.ani", "durMs": 1515, "n": "基础动画 1515ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "39771509", "n": "39771509.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策尖刺04.pss", "durMs": 5520, "n": "施法者 PSS 作者寿命 5520ms", "k": "cast_pss"},
    ],
    "穿心弩": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09飞天攻击03a.ani", "durMs": 1848, "n": "基础动画 1848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "934807344", "n": "934807344.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门穿心弩聚01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "穿心弩": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09飞天攻击03a.ani", "durMs": 1848, "n": "基础动画 1848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "934807344", "n": "934807344.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门穿心弩聚01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "笑醉狂": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11buff01.ani", "durMs": 8970, "n": "基础动画 8970ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "128409036", "n": "128409036.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮群体笑醉狂01.pss", "durMs": 14400, "n": "施法者 PSS 作者寿命 14400ms", "k": "cast_pss"},
    ],
    "笑醉狂": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11buff01.ani", "durMs": 8970, "n": "基础动画 8970ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "128409036", "n": "128409036.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮群体笑醉狂01.pss", "durMs": 14400, "n": "施法者 PSS 作者寿命 14400ms", "k": "cast_pss"},
    ],
    "绛唇珠袖": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能26.ani", "durMs": 1545, "n": "基础动画 1545ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "258176149", "n": "258176149.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀绛唇珠袖手.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "绛唇珠袖": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能26.ani", "durMs": 1545, "n": "基础动画 1545ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "258176149", "n": "258176149.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀绛唇珠袖手.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "绛唇珠袖": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能26.ani", "durMs": 1545, "n": "基础动画 1545ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "258176149", "n": "258176149.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀绛唇珠袖手.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "芙蓉并蒂": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴20.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "907403389", "n": "907403389.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花攻击_02_重制.pss", "durMs": 10272, "n": "施法者 PSS 作者寿命 10272ms", "k": "cast_pss"},
    ],
    "芙蓉并蒂": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴20.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "907403389", "n": "907403389.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花攻击_02_重制.pss", "durMs": 10272, "n": "施法者 PSS 作者寿命 10272ms", "k": "cast_pss"},
    ],
    "蚀心蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sqg08wd蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "121381088", "n": "121381088.wav"},
    ],
    "蛇影": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08毒攻击02.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "384712708", "n": "384712708.wav"},
    ],
    "蛇影": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08毒攻击02.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "384712708", "n": "384712708.wav"},
    ],
    "蛊虫献祭": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sqg08wd牺牲01.ani", "durMs": 364, "n": "基础动画 364ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "410215945", "n": "410215945.wav"},
    ],
    "蛊虫献祭": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sqg08wd牺牲01.ani", "durMs": 364, "n": "基础动画 364ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "410215945", "n": "410215945.wav"},
    ],
    "蟾啸": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08毒攻击02.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "467733878", "n": "467733878.wav"},
    ],
    "蟾啸": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08毒攻击02.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "467733878", "n": "467733878.wav"},
    ],
    "迷心蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "429679959", "n": "429679959.wav"},
    ],
    "迷心蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "429679959", "n": "429679959.wav"},
    ],
    "迷心蛊": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1swd08蛊攻击01.ani", "durMs": 348, "n": "基础动画 348ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "429679959", "n": "429679959.wav"},
    ],
    "追命箭": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09弩攻击03.ani", "durMs": 848, "n": "基础动画 848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "469981245", "n": "469981245.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_追命箭_释放01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "追命箭": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1stm09弩攻击03.ani", "durMs": 848, "n": "基础动画 848ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "469981245", "n": "469981245.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_唐门_追命箭_释放01_悟.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "钟林毓秀": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴14a.ani", "durMs": 2000, "n": "基础动画 2000ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "68381228", "n": "68381228.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花旋转线条_02.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "钟林毓秀": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴14a.ani", "durMs": 2000, "n": "基础动画 2000ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "68381228", "n": "68381228.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花旋转线条_02.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "钟林毓秀": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh点穴14a.ani", "durMs": 2000, "n": "基础动画 2000ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "68381228", "n": "68381228.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花旋转线条_02.pss", "durMs": 10080, "n": "施法者 PSS 作者寿命 10080ms", "k": "cast_pss"},
    ],
    "银月斩": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击04.ani", "durMs": 1000, "n": "基础动画 1000ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "339922410", "n": "339922410.wav"},
    ],
    "银月斩": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击04.ani", "durMs": 1000, "n": "基础动画 1000ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "339922410", "n": "339922410.wav"},
    ],
    "镇山河": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能13b.ani", "durMs": 1303, "n": "基础动画 1303ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "48443349", "n": "48443349.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳镇山河01.pss", "durMs": 12000, "n": "施法者 PSS 作者寿命 12000ms", "k": "cast_pss"},
    ],
    "镇山河": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s03cy技能13b.ani", "durMs": 1303, "n": "基础动画 1303ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "48443349", "n": "48443349.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_纯阳镇山河01.pss", "durMs": 12000, "n": "施法者 PSS 作者寿命 12000ms", "k": "cast_pss"},
    ],
    "长针": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh针灸13b.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "342270609", "n": "342270609.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花长针吟唱01.pss", "durMs": 5760, "n": "施法者 PSS 作者寿命 5760ms", "k": "cast_pss"},
    ],
    "长针": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s01wh针灸13b.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "342270609", "n": "342270609.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\w_万花长针吟唱01.pss", "durMs": 5760, "n": "施法者 PSS 作者寿命 5760ms", "k": "cast_pss"},
    ],
    "雷霆震怒": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能25.ani", "durMs": 1242, "n": "基础动画 1242ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "531511511", "n": "531511511.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀江海_蓄力01.pss", "durMs": 24000, "n": "施法者 PSS 作者寿命 24000ms", "k": "cast_pss"},
    ],
    "雷霆震怒": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx剑技能25.ani", "durMs": 1242, "n": "基础动画 1242ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "531511511", "n": "531511511.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀江海_蓄力01.pss", "durMs": 24000, "n": "施法者 PSS 作者寿命 24000ms", "k": "cast_pss"},
    ],
    "雾暗迷云": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s21ds技能04b.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "170046962", "n": "170046962.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\ds_段氏_雾海蓄力_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "雾暗迷云": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s21ds技能04b.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "170046962", "n": "170046962.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\ds_段氏_雾海蓄力_01.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "风流云散": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s21ds步法03a_悟.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "455851133", "n": "455851133.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\ds_段氏_风流云散_残影01_f1_悟.pss", "durMs": 14400, "n": "施法者 PSS 作者寿命 14400ms", "k": "cast_pss"},
    ],
    "风流云散": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s21ds步法03a_悟.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "455851133", "n": "455851133.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\ds_段氏_风流云散_残影01_f1_悟.pss", "durMs": 14400, "n": "施法者 PSS 作者寿命 14400ms", "k": "cast_pss"},
    ],
    "风袖低昂": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx扇技能12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "186804602", "n": "186804602.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀_风袖低昂_飘带01_悟.pss", "durMs": 9840, "n": "施法者 PSS 作者寿命 9840ms", "k": "cast_pss"},
    ],
    "风袖低昂": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s05qx扇技能12.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "186804602", "n": "186804602.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\q_七秀_风袖低昂_飘带01_悟.pss", "durMs": 9840, "n": "施法者 PSS 作者寿命 9840ms", "k": "cast_pss"},
    ],
    "驱夜断愁": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击10.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "303951196", "n": "303951196.wav"},
    ],
    "驱夜断愁": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1smj10双刀攻击10.ani", "durMs": 939, "n": "基础动画 939ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "303951196", "n": "303951196.wav"},
    ],
    "鹤归孤山": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s07cj重剑技能14.ani", "durMs": 1727, "n": "基础动画 1727ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "426736700", "n": "426736700.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_藏剑手部拖尾.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "鹤归孤山": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s07cj重剑技能14.ani", "durMs": 1727, "n": "基础动画 1727ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "426736700", "n": "426736700.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\c_藏剑手部拖尾.pss", "durMs": 9600, "n": "施法者 PSS 作者寿命 9600ms", "k": "cast_pss"},
    ],
    "龙吟": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能12.ani", "durMs": 1485, "n": "基础动画 1485ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "491484005", "n": "491484005.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策爆发.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "龙吟": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能12.ani", "durMs": 1485, "n": "基础动画 1485ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "491484005", "n": "491484005.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策爆发.pss", "durMs": 10560, "n": "施法者 PSS 作者寿命 10560ms", "k": "cast_pss"},
    ],
    "龙战于野": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11掌法攻击05b.ani", "durMs": 1273, "n": "基础动画 1273ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "704432442", "n": "704432442.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮_亢龙有悔_挥拳01_悟.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "龙战于野": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1sgb11掌法攻击05b.ani", "durMs": 1273, "n": "基础动画 1273ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "704432442", "n": "704432442.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\g_丐帮_亢龙有悔_挥拳01_悟.pss", "durMs": 4800, "n": "施法者 PSS 作者寿命 4800ms", "k": "cast_pss"},
    ],
    "龙牙": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能13.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "380889589", "n": "380889589.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策龙牙_狼头版.pss", "durMs": 6240, "n": "施法者 PSS 作者寿命 6240ms", "k": "cast_pss"},
    ],
    "龙牙": [
        {"t": 0, "kind": "anim", "v": "data\\source\\player\\f1\\动作\\f1s04tc技能13.ani", "durMs": 1394, "n": "基础动画 1394ms (播放一次; tani 的 Sfx 标签受引擎版本限制)"},
        {"t": 0, "kind": "sound", "v": "380889589", "n": "380889589.wav"},
        {"t": 0, "kind": "dummy", "v": "data\\source\\other\\hd特效\\技能\\pss\\发招\\t_天策龙牙_狼头版.pss", "durMs": 6240, "n": "施法者 PSS 作者寿命 6240ms", "k": "cast_pss"},
    ],
}

# full ability mechanism write-ups (shown in the Mechanism tab of the picker)
MECH = {
    "临时飞爪": (
        "临时飞爪 (道具技能 28031, 地面点, 最大 40 尺, CD 30s, 非战斗返还部分CD)\n"
        "1) 在落点生成隐形装置 NPC (模板 67816, 寿命 160 帧), SetModelID(70025)=爪锚; buff 12363 隐藏+无敌, 12343 悬停\n"
        "2) 施法者 DoAction(0, 140022/140023)\n"
        "3) 5 帧后 OnTimer: CastSkill(28032 链状表现) + CastSkillXYZ(28033 道具_孤风飒踏冲刺)\n"
        "4) 链条: skill_chain 28032 = 起点 S_rh(右手) -> 终点 S_fxmid, 特效 s_锁链01.pss\n"
        "   贴图: t_铁链02(链环) + s_链状_闪电01(光效) + l_流光01(流光); 已提取 bin64\\ability_picker\\chains\\\n"
        "5) 牵引: 28033 = DASH_TO_POINT(120,0) 把玩家拉向落点\n"
        "6) 动画: F1s16lxg链技能03_释放HD (孤风飒踏系); 落地 F1s16lxg链技能03_缓冲_HD\n"
        "7) 声音: wem 62588785 = s16lxglianjineng01_HD.wav (凌雪阁链音效)\n"
        "已解(引擎代码): 装置 70025 = data\\source\\npc_source\\a021\\模型\\a021b.mdl (GetRepresentModelPath), "
        "但引擎用 buff 12363(气场隐藏且无敌)+12343(悬停) 将其隐藏 -> 落点不显示任何模型, 只提供 S_fxmid 链锚点\n"
        "动画链 (引擎表): skill_dash.txt 28033 -> AnimationID 91076 -> F1s16lxg链技能03b_hd.tani (牵引); "
        "释放=链技能03_释放HD; 落地=链技能03_缓冲HD (91074)\n"
        "瞄准指示: cursor_effect.txt -> data\\source\\other\\特效\\系统\\SFX\\其他\\鼠标移动.Sfx (鼠标特效)\n"
        "链条表: 技能链=skill_chain.txt (s_锁链01.pss); 公共链=public_chain.txt (独立系统)\n"
        "技能层链条 = KRLSfx 实例 + 两点绑定 (从二进制解析):\n"
        "  - ApplyBindChainSfx -> KRLSfx::Init(this, pcszFile=FilePath(s_锁链01.pss)) 加载资源 -> handle@sfx+0x98, path@+0x20\n"
        "  - 绑定子对象 @sfx+0x48: 起点=施法者 S_rh, 终点=装置NPC S_fxmid; 偏移vec3@+0x1b4..1bc, scale@+0x1c8, Millisecond(float)@+0x1e0\n"
        "  - _CreatePublicChain: 按 ID 解析 pSrcCharacter/pDstCharacter, nLinkType 1..4 (rpclt_Total=5), 创建后每帧 UpdateSkillChain 由插槽重绑\n"
        "  - 触发: 服务器包 S2C_POINT_CHAIN_SKILL_EFFECT -> OnCharacterSkillChainDisplay\n"
        "  => 实现配方: 用 FilePath 建 SFX; 双点绑定到 S_rh 与装置 S_fxmid; 设 Millisecond=1000/旋转/偏移; 每帧从插槽更新\n"
        "动画层链条 (已忠实): tani 自带 l_凌雪阁出链带01/出链02/链技能03_3, 播放 tanis 时引擎自动触发\n"
        "未解: DoAction(0,140022/140023) 不在客户端动画表内 (服务器侧动作触发); DASH_TO_POINT 牵引移动 (引擎无 actor 位移 API)\n"
        "宿主(Skill, 2026-09-29): 落点=visibleTop (碰撞烘焙自上向下首击, 可站立) 且无攀爬上限; "
        "瞄准 FOV=应用投影 48°x因子; 指示=正确资源 释放_范围选择01.mesh 但 VFS 缺材质 -> 引擎回退错误材质 (亮地面上几乎不可见); "
        "忠实显示需要引擎特效通道 KG3DScene::GetSceneSFXEditor / KG3DSFX (未接通); 28032 .Sfx/KRLSfx 同类阻塞 (AddStateMachineModel E_FAIL)"
    ),
    "乘黄之威": (
        "乘黄之威 (道具 36500)\n"
        "1) 道具脚本 = DASH_FORWARD(8,110) 向前跳跃; 玩家自身无专属动画(通用冲刺)\n"
        "2) buff 27453 冲刺维持 + 26952 游雾乘云检测 (脚本头来自 万灵山庄_游雾乘云跳跃)\n"
        "3) 乘黄宠物 A393: 召唤 A393_start01_召唤_乘黄01_悟 (召唤传送门+召唤跳 PSS, a393_birth01b 音); 咆哮 a393_sk03 (c_乘黄吼_01.pss + a393_sk03.wav); 冲刺 a393_rush01_冲刺_悟\n"
        "4) 音效: 382642492 骑乘断魂刺(手动) + 457495140 a393_birth01b; A393 全套 15 音已抓\n"
        "5) 玩家配对动作: F1HA393_start01.tani"
    ),
    "盾立": (
        "盾立 (道具 27871 / 本体 13067)\n"
        "1) 脚本仅 EXECUTE_SCRIPT (SKILL_MOVE 104 注释掉) -> 玩家无骨骼动画\n"
        "2) 表现都在 buff: 8303 盾立效果 -> C_苍云盾挡02.pss; 20230 -> C_苍云盾墙01.pss; 20231 释放 Sfx -> 事件 CangYun/tex/dunlibuff -> wem 916951452 dunlibuff01\n"
        "3) 被击音 wem 697798714 zhandou_jineng_beiji_behit_dunli\n"
        "4) 唯一同名动画在 BOSS 长孙忘情 P081: sk02a 吟唱 / sk02b 释放(c_苍云盾立01) / sk02c,d 状态\n"
        "   已存 bin64\\ability_picker\\npc_clips\\P081-dunli\\"
    ),
    "琴音共鸣": (
        "琴音共鸣 (道具 27857)\n"
        "1) 脚本仅 STEAL_BUFF x2 (偷取目标 2 个增益); 无动画 无音效 无特效 (评审: 无需声音)\n"
        "2) 需求 buff 21117 相知切剑持续\n"
        "3) 全部 9248 个 f1 tani 无任何 共鸣/琴音共鸣 tag -> 判定无动画"
    ),
    "如意法": (
        "如意法 (道具 32247)\n"
        "1) 脚本: EXECUTE_SCRIPT + DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE x4 (清除移动限制组) + BindBuff 4421 (明教_夜叉心_免控)\n"
        "2) 动画 = F1smj10双刀buff04_清净心01 (免控姿态, m_明教清净心01.pss)\n"
        "3) 音效 wem 75054615 = riyuejiaohui.wav\n"
        "宿主(Skill 2026-09-29): tani F1smj10双刀buff04_清净心01 内嵌 .Sfx 标签 (m明教元素18/19.sfx, g光晕02.sfx, 释放_气场聚集03.sfx) "
        "-> 播放该 tani 让宿主 AV (Render); 改播其基础 .ani f1smj10双刀buff04.ani (无标签) + 作者 PSS m_明教清净心01.pss 正常渲染 (火柱)"
    ),
}

# candidate-list hygiene (substring filters on tani filenames)
EXCLUDE = {
    "春泥护花": ["太阴指"],
    "盾立": ["丐帮笑醉狂"],
}

# wrong wem ids merged from cache/review (never play these)
WEM_EXCLUDE = {
    "盾立": ["128409036"],
}


def apply_overrides(all_entries: list) -> None:
    """IP marks, no-anim flags, extra phase matches, candidate/wem hygiene."""
    catalog = load_catalog_f1()
    for e in all_entries:
        name = e["name"]
        for frag in EXCLUDE.get(name, []):
            e["tanis"] = [t for t in e["tanis"] if frag not in t]
        for wid in WEM_EXCLUDE.get(name, []):
            e["wems"] = [w for w in e["wems"] if str(w) != wid]
            e["confirmedWems"] = [w for w in (e.get("confirmedWems") or []) if str(w) != wid]
        for pref in ADD.get(name, []):
            fn = catalog_find(pref, catalog)
            if not fn:
                continue
            p = F1_DIR + "\\" + fn
            if p.lower() not in {t.lower() for t in e["tanis"]}:
                e["tanis"].append(p)
        for wid in WEM_ADD.get(name, []):
            if str(wid) not in {str(w) for w in e["wems"]}:
                e["wems"].insert(0, str(wid))
        extra = []
        for pref in MATCHED_EXTRA.get(name, []):
            fn = catalog_find(pref, catalog)
            p = F1_DIR + "\\" + fn if fn else F1_DIR + "\\" + pref
            if p.lower() not in {t.lower() for t in e["tanis"]}:
                e["tanis"].append(p)
            extra.append(p)
        e["matchedExtra"] = extra
        e["noAnim"] = name in NOANIM
        e["ip"] = name in IP
        e["ipNote"] = IP.get(name, "")
        e["mech"] = MECH.get(name, "")
        # process steps attach to the resolved row only: the dataset carries
        # duplicate rows per name (resolved + empty) and the host picks the
        # first row with a resolved tani
        e["process"] = PROCESS.get(name, []) if e.get("matched") else []

F1_DIR = r"data\source\player\f1\动作"
TANI_RT = r"C:\SeasunGame\MovieEditor\ResourcePack\Tani.rt"
BAD_TOKENS = {"技能", "攻击", "武器", "普通", "待机", "行走", "奔跑", "死亡"}


def load_catalog_f1() -> list:
    """all f1 tani filenames from the movie editor catalog (real casing)"""
    names = []
    try:
        for ln in open(TANI_RT, encoding="gb18030", errors="replace").read().splitlines():
            c = ln.split("\t")
            if len(c) >= 3 and c[1].lower().endswith(".tani") and "\\player\\f1\\" in c[2].lower():
                names.append(c[1])
    except Exception:
        pass
    return names


def catalog_find(pref: str, catalog: list) -> str:
    hits = [fn for fn in catalog if pref.lower() in fn.lower()]
    if not hits:
        return ""
    hits.sort(key=lambda fn: (1 if "_悟" in fn else 0, len(fn)))
    return hits[0]


WEMS_INDEX = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4\jx3-web-map-viewer\log\wwise-soundbank-index.json"


def load_wems_index() -> dict:
    """wwise wem id -> source wav name (the name mirrors the tani clip stem)"""
    if not os.path.exists(WEMS_INDEX):
        return {}
    try:
        return json.load(open(WEMS_INDEX, encoding="utf-8")).get("wems") or {}
    except Exception:
        return {}


def load_tables(base: str):
    rows = [r.split("\t") for r in
            open(os.path.join(base, "ad-desc-probe-out", "settings", "skill", "skills.tab"),
                 encoding="gb18030", errors="replace").read().splitlines()[1:]
            if len(r.split("\t")) > 99]
    tag: dict[str, list] = {}
    for ln in open(os.path.join(base, "skill-tables-out", "Represent", "skill", "skill_tag.txt"),
                   encoding="gb18030", errors="replace").read().splitlines()[1:]:
        c = ln.split("\t")
        if len(c) >= 3:
            tag.setdefault(c[0], []).append(c[1])
    anim: dict[str, list] = {}
    for ln in open(os.path.join(base, "player-animation-out", "Represent", "player", "player_animation_f1.txt"),
                   encoding="gb18030", errors="replace").read().splitlines()[1:]:
        c = ln.split("\t")
        if len(c) >= 7 and c[0] and c[6]:
            anim.setdefault(c[0], []).append(c[6].replace("/", "\\").split("\\")[-1])
    return rows, tag, anim


def tag_match(name: str, rows, tag, anim) -> str:
    """skill_tag exact layer: a skills.tab row related to the ability whose
    tagged animation filename shares a distinctive token with the ability."""
    ids = set()
    for r in rows:
        n = r[0]
        if n.startswith(("道具_", "道具·", "绝境_", "绝境·", "伪传_", "伪传·")):
            continue
        if n == name or (name in n and len(n) <= len(name) + 12):
            ids.add(r[1])
    toks = {name[i:i + 2] for i in range(len(name) - 1)} - BAD_TOKENS
    best = ""
    for i in ids:
        for a in tag.get(i, []):
            for f in anim.get(a, []):
                if any(t in f for t in toks) and ("\\f1\\" in f.lower() or f.lower().lstrip("_").startswith("f1")):
                    if not best or len(f) < len(best):
                        best = f
    return best


def name_match(name: str, tanis: list) -> str:
    """conservative fallback: only when the ability name itself is in an f1
    filename; prefers plain over 皮肤/悟 and the lowest phase number."""
    cands = [t for t in tanis if "\\f1\\" in t.lower()]
    named = [t for t in cands if name in t.replace("/", "\\").split("\\")[-1]]
    if not named:
        return ""
    def score(t: str):
        fn = t.replace("/", "\\").split("\\")[-1]
        s = 0
        if "_悟" in fn:
            s += 100
        if "皮肤" in fn:
            s += 50
        m = re.search(r"(\d{2})", fn)
        s += int(m.group(1)) if m else 20
        return (s, len(fn))
    named.sort(key=score)
    return named[0]


def body_match(name: str, tanis: list) -> str:
    """named clip on another body (borrow to f1, per the locked rule).
    conservative: name must be in the filename; skips demo/皮肤, prefers non-悟
    and the smallest body/phase."""
    cands = [t for t in tanis if name in t.replace("/", "\\").split("\\")[-1] and "演武" not in t]
    if not cands:
        return ""
    def score(t: str):
        low = t.lower()
        fn = t.replace("/", "\\").split("\\")[-1]
        s = 0
        if "_悟" in fn:
            s += 100
        if "皮肤" in fn:
            s += 50
        if "\\m1\\" in low:
            s += 1
        elif "\\m2\\" in low:
            s += 2
        elif "\\f2\\" in low:
            s += 3
        m = re.search(r"(\d{2})", fn)
        s += int(m.group(1)) if m else 20
        return (s, len(fn))
    cands.sort(key=score)
    return cands[0]


def wem_stem_match(name: str, confirmed: list, wems_idx: dict, catalog: list) -> str:
    """a human-confirmed wem whose source name is a tani clip stem proves the
    animation (e.g. F1s16lxglianjineng08_hd.wav -> F1s16lxg链技能08_hd.tani)"""
    if not wems_idx or not confirmed:
        return ""
    for wem in confirmed:
        info = wems_idx.get(str(wem)) or {}
        src = (info.get("name") or "").replace("/", "\\").split("\\")[-1]
        if not src.lower().endswith(".wav"):
            continue
        stem = src[:-4]
        if not stem:
            continue
        fn = catalog_find(stem, catalog)
        if fn:
            return F1_DIR + "\\" + fn
    return ""


def resolve_matched(name: str, tanis: list, rows, tag, anim, catalog: list, wems_idx: dict, confirmed: list) -> tuple[str, str]:
    if name in NO_MATCH:
        return "", ""
    for pref in RESOLVE.get(name, []):
        for t in tanis:
            if pref.lower() in t.lower():
                return t, "dig"
        fn = catalog_find(pref, catalog)
        if fn:
            return F1_DIR + "\\" + fn, "dig-cat"
    w = wem_stem_match(name, confirmed, wems_idx, catalog)
    if w:
        for t in tanis:
            if t.lower().endswith(w.lower().split("\\")[-1]):
                return t, "wem"
        return w, "wem"
    f = tag_match(name, rows, tag, anim)
    if f:
        for t in tanis:
            if t.lower().endswith(f.lower()):
                return t, "tag"
        fn = catalog_find(f[:-5] if f.lower().endswith(".tani") else f, catalog) or f
        return F1_DIR + "\\" + fn, "tag"
    t = name_match(name, tanis)
    if t:
        return t, "name"
    t = body_match(name, tanis)
    if t:
        return t, "body"
    return "", ""


def attach_matched(all_entries: list, cache_path: str) -> int:
    base = os.path.dirname(os.path.dirname(cache_path))
    try:
        rows, tag, anim = load_tables(base)
    except Exception as exc:
        print("resolve: tables unavailable (" + str(exc) + ")")
        rows, tag, anim = [], {}, {}
    catalog = load_catalog_f1()
    wems_idx = load_wems_index()
    resolved = 0
    for e in all_entries:
        e.setdefault("matched", "")
        e.setdefault("matchSource", "")
        e.setdefault("deduced", "")
        e.setdefault("deduceNote", "")
        if not e["ids"]:
            continue
        m, src = resolve_matched(e["name"], e["tanis"], rows, tag, anim, catalog, wems_idx, e.get("confirmedWems") or [])
        if m:
            have = {t.lower() for t in e["tanis"]}
            if m.lower() not in have:
                e["tanis"].insert(0, m)
            e["matched"] = m
            e["matchSource"] = src
            resolved += 1
            continue
        spec = DEDUCE.get(e["name"])
        if spec:
            fn = catalog_find(spec[0], catalog)
            path = F1_DIR + "\\" + fn if fn else F1_DIR + "\\" + spec[0]
            have = {t.lower() for t in e["tanis"]}
            if path.lower() not in have:
                e["tanis"].insert(0, path)
            e["deduced"] = path
            e["deduceNote"] = spec[1]
    return resolved


def merge_review_wems(all_entries: list, cache_path: str) -> int:
    """merge human-confirmed Wwise wem ids from ability-tani-sound-review.json"""
    rp = os.path.join(os.path.dirname(cache_path), "ability-tani-sound-review.json")
    if not os.path.exists(rp):
        return 0
    try:
        review = json.load(open(rp, encoding="utf-8"))
    except Exception:
        return 0
    ents = review.get("entries") or {}
    bykey = {}
    for e in all_entries:
        bykey[e.get("key", "")] = e
        bykey[str(e.get("prefix", "")) + ":" + str(e.get("id", "")) + ":" + str(e.get("kind", ""))] = e
    merged = 0
    for k, v in ents.items():
        e = bykey.get(k)
        if e is None:
            continue
        have = {w.lower() for w in e["wems"]}
        for w in v.get("confirmedWems") or []:
            wid = str(w.get("id") or "")
            if wid and wid.lower() not in have:
                e["wems"].append(wid)
                have.add(wid.lower())
                e.setdefault("confirmedWems", []).append(wid)
                merged += 1
    return merged


def attach_zc_ids(all_entries: list) -> tuple[int, int]:
    """Point zhenchuan ability ids at their entries (by id, else by name)."""
    for e in all_entries:
        e.setdefault("ids", [])
    by_id = {e["id"]: e for e in all_entries if e.get("id")}
    by_name: dict[str, list] = {}
    for e in all_entries:
        by_name.setdefault(e["name"], []).append(e)
    attached = 0
    for sid, nm in ZC_IDS:
        nm = norm_name(nm)
        e = by_id.get(sid)
        if e is None:
            cands = by_name.get(nm, [])
            e = max(cands, key=lambda x: len(x["tanis"])) if cands else None
        if e is None:
            e = {
                "key": "zc:" + sid, "id": sid, "prefix": "阵船", "kind": "skill",
                "name": nm, "rawName": nm, "status": "zc", "tanis": [], "events": [],
                "wems": [], "notes": "zc id (no cache entry)", "ids": [], "confirmedWems": [],
            }
            all_entries.append(e)
            by_name.setdefault(nm, []).append(e)
        if sid not in e["ids"]:
            e["ids"].append(sid)
            attached += 1
    return attached, len(ZC_IDS)


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
            "events": flatten_names(e.get("events")),
            "wems": flatten_wems(e.get("wems")),
            "notes": "cache",
            "ids": [],
            "confirmedWems": [],
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
                "name": name, "rawName": name, "status": "dig", "tanis": [], "events": [],
                "wems": [], "notes": note, "ids": [], "confirmedWems": [],
            })
            have = {p.lower() for p in g["tanis"]}
            for p in paths:
                if p.lower() not in have:
                    g["tanis"].append(p)

    all_entries = list(abilities.values()) + list(manual_group.values())
    attached, total_ids = attach_zc_ids(all_entries)
    merged_wems = merge_review_wems(all_entries, args.cache)
    resolved = attach_matched(all_entries, args.cache)
    apply_overrides(all_entries)
    all_entries.sort(key=lambda a: (a["prefix"], a["name"]))

    out = {
        "generatedAt": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "cache": args.cache,
        "count": len(all_entries),
        "withTanis": sum(1 for a in all_entries if a["tanis"]),
        "withIds": sum(1 for a in all_entries if a["ids"]),
        "attachedIds": attached,
        "totalIds": total_ids,
        "resolved": resolved,
        "abilities": all_entries,
    }
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)
    print(f"wrote {args.out}: {out['count']} abilities, {out['withTanis']} with tani candidates, "
          f"{out['withIds']} with ids ({attached}/{total_ids} ids attached), "
          f"{resolved} with an identified matched tani, "
          f"{merged_wems} confirmed wems merged")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
