#!/usr/bin/env python3
"""Collect combat-related assert/protocol/struct strings from the net dumps."""
from __future__ import annotations

import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(r"C:\Users\Zhibin Ren\Desktop\reborn-pvp\proof\netcode")
FILES = [
    ("JX3ClientX64_exe_net_strings.txt", "JX3ClientX64.exe"),
    ("JX3LogicEditOperation_net_strings.txt", "JX3LogicEditOperationX64.dll"),
    ("JX3RepresentX64_net_strings.txt", "JX3RepresentX64.dll"),
]
OUT = Path(r"C:\Users\Zhibin Ren\Desktop\reborn-pvp\proof\pvp\netcode\combat_asserts_raw.txt")

NEEDLES = [
    "sizeof(S2C_SKILL_EFFECT_RESULT", "sizeof(S2C_SYNC_BUFF_LIST",
    "sizeof(S2C_SYNC_BATTLEFIELD_COMPETITOR_BUFF_LIST", "sizeof(S2C_POINT_CHAIN_SKILL_EFFECT",
    "sizeof(S2C_RESET_COOLDOWN", "sizeof(S2C_PAUSE_CD_TIMER", "sizeof(S2C_ACCELERATE_CD_TIMER",
    "sizeof(S2C_COOLDOWN_OVER_DRAFT_NOTIFY", "sizeof(S2C_SYNC_ARENA_COMPETITIOR_CD_STATE",
    "sizeof(S2C_SYNC_CAMP_INFO", "MAX_UI_SKILL_RESULT_TYPE_COUNT",
    "s2c_sync_arena_competitior_cd_state", "s2c_pause_cd_timer", "s2c_accelerate_cd_timer",
    "s2c_reset_cooldown", "s2c_cooldown_over_draft_notify",
    "KARENA_RECOMMEND_COMPETITION_NODE", "KARENA_PLAYER_RECORD_INFO",
    "pSkillResult->cResultCount", "pSyncBuff->wDataSize", "pSync->wDataSize",
    "pPak->nCount", "S2C_SYNC_BATTLEFIELD_COMPETITOR_BUFF_LIST",
    "S2C_SKILL_EFFECT_RESULT::KSKILL_RESULT",
    "OnSkillPrepare", "OnSkillCast", "OnSkillChannel", "OnSkillEffectResult",
    "OnSkillBeatBack", "OnSkillRayEffect", "OnSkillChainEffect", "OnPointChainSkillEffect",
    "OnStartHoardSkill", "OnResetCooldown", "OnPauseCDTimer", "OnAccelerateCDTimer",
    "OnCoolDownOverDraftNotify", "OnSyncBuffList", "OnSyncBuffSingle", "OnCharacterDeath",
    "OnSyncBattlefieldCompetitorCDState", "OnSyncBattlefieldCompetitorBuffList",
    "OnSyncArenaCompetitorCDState", "OnSyncArenaCompetitorList", "OnSyncBattlefieldStatistics",
    "OnSyncBaseInfoFromBattlefieldCompetitorList", "OnSyncVariableInfoFromBattlefieldCompetitorList",
    "DoSyncBattlefieldCompetitorSkillCDStateRequest", "DoCancelSyncBattlefieldCompetitorSkillCDStateRequest",
    "DoSyncBattlefieldCompetitorsListRequest", "DoSyncOBCompetitorSkillList",
    "DoSyncSubSkillPosition", "DoCastProfessionSkill", "DoCharacterSkill",
    "DoCastHoardSkill", "DoStartHoardSkill", "DoPlayerReviveRequest",
    "DoApplyCharacterBuffList", "KBattlefieldCache", "GetBattlefieldPlayersPosInfo",
    "m_BattlefieldCompetitorInfoMap", "m_ArenaPlayerInfoVector", "m_DungeonCompetitorInfoMap",
    "KPlayerBattleStat", "MAX_EXTERNAL", "m_nProtocolSize",
    "MOVE_STATE_ERROR", "MOVE_STATE_INVALID", "YOU_MOVE_STATE_WRONG",
    "TARGET_MOVE_STATE_WRONG", "DST_MOVE_STATE_ERROR", "ADD_DAMAGE_BY_DST_MOVE_STATE",
    "CAST_SKILL_NO_TARGET", "IMMUNE_SKILL_MOVED", "IN_COOLDOWN", "QUEUE_COOLDOWN",
    "LuaCheckSilence", "dwSkillImmunity", "IgnoreImmunityCast",
    "OnChangeImmunityCastIDNotify", "OnChangeMultiImmunityCastIDNotify",
    "OnSyncStealthCharacter", "OnSyncRelationAllEnemyExceptTeam", "OnSetCamp", "OnSetForce",
    "OnSetBattleFieldSide", "OnSyncSceneCampTypeToPlayer",
]


def main() -> int:
    lines = ["# combat asserts / protocol / struct strings (auto-extracted)",
             "# source dumps: " + ", ".join(f for f, _ in FILES), ""]
    for fname, binname in FILES:
        f = ROOT / fname
        if not f.is_file():
            continue
        lines.append(f"## {binname}")
        for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.startswith("#"):
                continue
            parts = line.split("\t", 2)
            if len(parts) < 3:
                continue
            off, _enc, text = parts
            if any(n in text for n in NEEDLES):
                lines.append(f"{binname}\t{off}\t{text}")
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(lines)} lines -> {OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
