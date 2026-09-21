# Natasha LIVE chrome fix (R4b) — Andy desktop Tk

**Scope:** Tk chrome / labels / layout only on live `player.py`.  
**Playback / catalog / mesh / pose logic:** unchanged.  
**Webview:** not promoted (native matplotlib / Mesh RQ remains default).  
**Date:** 2026-09-20 PT

**Source of chrome:** mirrored from polished `/workspace/jx3-ani-player-andy/player.py` + `NATASHA_CHROME_PASS.md` — applied surgically to live (no blind overwrite).

## Diffs applied to LIVE

### 1. Header
- **Before:** `JX3 · Ani Player ANIMATION` + pills `Animation Player` / `Movie Player` / `PSS`
- **After:**
  - Title: `JX3 Ani Player · free play companion`
  - Subtitle: `动作 = clip · Body → Character → 动作 → Play`
  - Quiet mode label only: `Animation Player` (no fake Movie Player / PSS nav)

### 2. Transport
- **Before:** `▶` / `↺` / `⟳` (no Pause)
- **After:** `▶ Play` / `⏹ Stop` / `⏸ Pause` / `☑ Loop`
- Pause wired to existing `pause()` (already present on live)

### 3. Left rail
- Added section labels: **Body 体型**, **Character 角色**, **动作 Clip**
- Character rail now visible (label + listbox); filled via new `_refresh_character_rail()` using clip-path inference / `CHAR_BY_BODY` defaults (e.g. F1 → 花萝)
- Catalog tabs: `动作` / `脸部(Tani)` / `Serial` via `TAB_TITLES` (never “Anim Table” / “Tani Catalog”)

### 4. Status / detail copy
- Count wording: `animations` → `clips`
- Meta: `Choose a 动作 (clip) to play.` / empty → `No 动作 (clip) for this body / tab / search.`
- Detail row: `Anim ID` → `Clip ID`

### 5. Helpers added (chrome only)
- `CHAR_BY_BODY`, `TAB_TITLES`
- `_section_label()`
- `_refresh_character_rail()` (+ calls after catalog refresh)

## Unchanged (by design)
- All live playback / catalog / mesh / pose / FBX resolve paths
- Default character mode (native / Mesh RQ; no webview promotion)
- Existing `play()` / `pause()` / `stop()` / clock / scrub behavior

## Check
- `python3 -m py_compile /workspace/jx3-ani-player-andy-live/player.py` → **OK**
- Remaining diff vs polished mirror: one unrelated comment wording on FBX local_matrices (live kept its original comment)

## Paths
- Edited: `/workspace/jx3-ani-player-andy-live/player.py`
- This note: `/workspace/jx3-ani-player-andy-live/NATASHA_LIVE_CHROME_FIX.md`
- Reference: `/workspace/jx3-ani-player-andy/player.py`, `/workspace/jx3-ani-player-andy/NATASHA_CHROME_PASS.md`

## Ready
Live `player.py` matches R4b chrome requirements; ready to CopyFromBox to Andy.
