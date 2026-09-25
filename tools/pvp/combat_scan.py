#!/usr/bin/env python3
"""Scan JX3 netcode string dumps + symbol dumps for combat-related names.

Read-only helper. Emits a TSV of source_file, offset, encoding, text for every
matching line, and a deduplicated symbol-name list per source.

Usage:
  python combat_scan.py -o out.tsv [--symbols out.txt] [--terms terms.txt]
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(r"C:\Users\Zhibin Ren\Desktop\reborn-pvp\proof\netcode")
EXTRA = Path(r"C:\Users\Zhibin Ren\Desktop\reborn-netcode\proof\netcode\mode_juejing")

NET_DUMPS = [
    ROOT / "JX3ClientX64_exe_net_strings.txt",
    ROOT / "JX3LogicEditOperation_net_strings.txt",
    ROOT / "JX3RepresentX64_net_strings.txt",
    ROOT / "KBaseX64_net_strings.txt",
    ROOT / "JX3ClientX64Base_net_strings.txt",
    ROOT / "JX3UIX64_net_strings.txt",
    ROOT / "SIMWorldX64_net_strings.txt",
    ROOT / "DLBT_net_strings.txt",
    ROOT / "Engine_Lua5X64_net_strings.txt",
]

SYMBOL_DUMPS = [
    ROOT / "symbols_KPlayerClient.txt",
    ROOT / "symbols_KBaseX64_net.txt",
    ROOT / "symbols_JX3RepresentX64_net.txt",
    ROOT / "symbols_JX3LogicEditOperation_net.txt",
    EXTRA / "battlefield_api_symbols.txt",
    EXTRA / "loot_symbols.txt",
    EXTRA / "mode_load_symbols.txt",
]

# Combat / PvP keyword terms. Method-shaped terms are matched as substrings;
# struct/field terms too.
TERMS = [
    # skill lifecycle / cast
    "DoSkill", "OnSkill", "DoCast", "OnCast", "CastSkill", "PrepareCast", "BreakPrepare",
    "SkillPrepare", "SkillCast", "SkillChannel", "SkillEffectResult", "SkillBeatBack",
    "SkillRayEffect", "SkillChainEffect", "OnPointChainSkillEffect",
    "OnStartHoardSkill", "DoStartHoardSkill", "DoCastHoardSkill", "DoCastProfessionSkill",
    "DoCharacterSkill", "DoSkillHoard", "OnSkillMove", "OnSpecialSkillMove",
    "SkillHoardProgress", "SkillPrepareProgress", "SkillChannelProgress", "SkillCastLog",
    "OnBeginProxySkillList", "OnEndProxySkillList", "OnClearProxySkillList", "OnSyncProxySkillInfo",
    "OnChangeImmunityCastIDNotify", "OnChangeMultiImmunityCastIDNotify",
    # cooldown
    "Cooldown", "CoolDown", "CDTimer", "OnResetCooldown", "OnPauseCDTimer",
    "OnAccelerateCDTimer", "OnCoolDownOverDraftNotify", "OnPauseBuffTimer",
    "CoolDownList", "CoolDownAdd", "GetMaxCoolDown", "GetCoolDownInfo", "GetMinCoolDownValue",
    "GetMaxCoolDownValue",
    # buffs / state
    "OnSyncBuffList", "OnSyncBuffSingle", "OnSyncPlayerStateInfo", "OnSyncMoveState",
    "OnSyncMoveCtrl", "OnSyncMoveParam", "OnSyncPlayerOperationMask", "OnSyncStealthCharacter",
    "OnSyncPKState", "OnSyncFightflagList", "OnSyncBattleStatFlag", "OnSyncCampInfo",
    "OnSyncSceneCampTypeToPlayer", "OnSetCamp", "OnSyncSceneKillersSet",
    "OnSyncTargetOutputDamage", "OnPVEDamageStatNotify", "OnSyncBehitRepresent",
    "OnSyncSelfCurrentSprintPower", "OnSyncSelfCurrentLMRS", "OnSyncSelfCurrentST",
    # damage / result
    "HitResult", "DamageResult", "SkillResult", "OnPVPDamage", "DamageStat", "OnSyncTargetHP",
    # death / revive
    "DoPlayerReviveRequest", "OnPlayerRevive", "OnPlayerDeath", "OnCharacterDeath",
    "OnRevive", "ReviveRequest", "OnBroadcastCharacterLife", "PlayerDeath",
    # target / camp / relation
    "DoSelectTarget", "OnSelectTarget", "OnCamp", "OnSetForce", "SetBattleFieldSide",
    "OnSetBattleFieldSide", "CampType", "OnSyncForceId", "OnBroadcastKillInfo",
    "OnSyncRelationAllEnemyExceptTeam",
    # arena / battlefield
    "DoEnterArena", "OnArena", "DoBattlefield", "OnBattlefield", "BF_", "Battlefield",
    "OnSyncArenaCompetitor", "OnSyncArenaPlayerList", "OnSyncArenaLevel", "OnSyncArenaStatistics",
    "OnSyncBattlefieldCompetitor", "OnSyncBattlefieldStatistics", "OnSyncBaseInfoFromBattlefield",
    "OnSyncVariableInfoFromBattlefield", "DoSyncBattlefieldCompetitor",
    "DoCancelSyncBattlefieldCompetitor", "DoSyncOBCompetitorSkillList", "OnSyncBFRoleData",
    "KBattlefieldCache", "m_BattlefieldCompetitorInfoMap", "GetBattlefieldPlayersPosInfo",
    "OnCompetitorSkillOTActionState", "RecordCompetitorSkillOTActionState",
    "DoQueryMapQueueInfo", "DoLeaveMapQueue", "DoComfirmEnterQueueMap", "JoinBattleFieldQueue",
    "LeaveBattleField", "OnCreateBattlefieldRoomRespond", "OnForceStartBattleFieldChaosFightRespond",
    "DoApplyBFRoleData", "DoApplyBFRoleWeekData", "DoGetBFRankRequest", "OnGetBFRankRespond",
    "DoApplyBFPlayerTeamGroupIDInfo", "GetAllBFPlayerTeamGroupID", "OnSyncMapQueueInfo",
    "DoCreateArenaRoom", "OnCreateArenaRoomRespond", "DoJoinArenaVisitorQueueRequest",
    "DoLeaveArenaVisitorQueueRequest", "OnSyncArenaVisitorCount", "OnSyncArenaCompetitorCDState",
    "OnSyncArenaCompetitorBuffList", "OnSyncArenaNewCompetitor",
    # prediction / rollback
    "predict", "Predict", "rollback", "Rollback", "replay", "Replay", "resim", "Resim",
    "interpolat", "Interpolat",
]

# names that should always be reported if present
METHOD_PREFIX = ("Do", "On", "KPlayerClient::", "KCharacter::", "KRLLocal", "KRLRemote")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("-o", "--out", required=True)
    ap.add_argument("--symbols", default=None)
    ap.add_argument("--terms", default=None)
    args = ap.parse_args()

    terms = TERMS
    if args.terms:
        terms = [t.strip() for t in Path(args.terms).read_text(encoding="utf-8").splitlines()
                 if t.strip() and not t.startswith("#")]

    rows = ["source\toffset\tencoding\ttext"]
    sym_rows = ["source\tname"]
    n_hits = 0
    for dump in NET_DUMPS:
        if not dump.is_file():
            print(f"skip missing {dump}")
            continue
        for line in dump.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.startswith("#") or not line.strip():
                continue
            parts = line.split("\t", 2)
            if len(parts) < 3:
                continue
            off, enc, text = parts
            # drop offsets for keyword terms when line matches
            if any(t in text for t in terms):
                rows.append(f"{dump.name}\t{off}\t{enc}\t{text}")
                n_hits += 1

    for sym in SYMBOL_DUMPS:
        if not sym.is_file():
            print(f"skip missing {sym}")
            continue
        for line in sym.read_text(encoding="utf-8", errors="replace").splitlines():
            s = line.strip()
            if not s or s.startswith("#") or s.startswith("=="):
                continue
            name = s[3:] if s.startswith("  ") else s
            if any(t in name for t in terms):
                sym_rows.append(f"{sym.name}\t{name}")

    Path(args.out).write_text("\n".join(rows) + "\n", encoding="utf-8")
    print(f"{n_hits} string hits -> {args.out}")
    if args.symbols:
        Path(args.symbols).write_text("\n".join(sym_rows) + "\n", encoding="utf-8")
        print(f"{len(sym_rows)-1} symbol rows -> {args.symbols}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
