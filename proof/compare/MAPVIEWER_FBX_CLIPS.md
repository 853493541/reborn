# Map-viewer 花萝 FBX clips (AnimationMixer path)

Staged 2026-09-20 for Banner/Tony. GT driver = **actor-viewer** AnimationMixer on FBX-embedded clips (not Animation Player PSS, not MIN2 locals).

## How map-viewer loads them

| API / UI | Root | Role |
|----------|------|------|
| /api/actor-exports | C:\SeasunGame\MovieEditor\source\fbx\* | Character mesh FBX + optional 	ex/ |
| /api/repo-clips | jx3-web-map-viewer/public/repo-clips/* (mirrors same folders) | **Clip source** picker — load another export’s 
oot.animations onto current character |
| Client | public/js/actor-viewer.js | FBXLoader → prepareClipLibraryClips(root.animations) → mixer.clipAction(clip) |

Workflow: load **花萝** (skin) → switch clip source to **走路** / **跳跃N** → Mixer plays embedded clip on 花萝 skeleton.

Code refs: ensureClipSourceLoaded, storeClipLibrary, playClip, onFbxLoaded in ctor-viewer.js.

## Export files (MovieEditor source of truth)

| Folder | FBX | Bytes | Embedded clips (Three FBXLoader) | Duration | Tracks |
|--------|-----|-------|----------------------------------|----------|--------|
| 花萝 | 花萝无动作.fbx | 2,771,440 | **0** (bind / no_anim) | — | — |
| 走路 | 花萝走路.fbx | 5,320,816 | seasun animation ×1 | 1.400 s | 546 |
| 跳跃1 | 跳跃1.fbx | 4,927,680 | seasun animation ×1 | 0.733 s | 546 |
| 跳跃2 | 跳跃2.fbx | 4,888,368 | seasun animation ×1 | 0.667 s | 546 |
| 跳跃3 | 跳跃3.fbx | 4,966,992 | seasun animation ×1 | 0.800 s | 546 |

Absolute ME paths:
- C:\SeasunGame\MovieEditor\source\fbx\花萝\花萝无动作.fbx
- C:\SeasunGame\MovieEditor\source\fbx\走路\花萝走路.fbx
- C:\SeasunGame\MovieEditor\source\fbx\跳跃1\跳跃1.fbx
- C:\SeasunGame\MovieEditor\source\fbx\跳跃2\跳跃2.fbx
- C:\SeasunGame\MovieEditor\source\fbx\跳跃3\跳跃3.fbx

Map-viewer mirrors under ...\jx3-web-map-viewer\public\repo-clips\{folder}\.

## Staged on Andy (jx3-ani-player)

Under samples/actor_presets/f1_hualuo/mapviewer_clips/:

| Staged file | Use |
|-------------|-----|
| 花萝__花萝无动作.fbx | Same no-anim skin we already use |
| 走路__花萝走路.fbx | **Embedded 走路** — prefer this as Mixer clip source |
| 跳跃1__跳跃1.fbx | Embedded jump variant 1 |
| 跳跃2__跳跃2.fbx | Embedded jump variant 2 |
| 跳跃3__跳跃3.fbx | Embedded jump variant 3 |

Also copied to proof/compare/mapviewer_fbx_clips/.
Clip name dump: proof/compare/mapviewer_fbx_clip_names.json.

Textures: keep using existing samples/actor_presets/f1_hualuo/tex/ (same as 花萝 export). Walk/jump folders have no separate 	ex/.

## Banner note

- Our previous default hualuo_no_anim / 花萝无动作 correctly has **zero** clips — Mixer needs the **走路** / **跳跃*** FBXs as clip sources (or convert MIN2→AnimationClip as you’re doing).
- Mid-frame MIN2 local TRS twin **not** required for Mixer GT (Tony cut). Existing hualuo_walk_mid_local_trs.json / ours_walk_mid_local_trs.json remain MIN2-side only.
- Clip display name inside FBX is literally seasun animation for walk and all three jumps (distinct durations).

## ASCII aliases (same bytes)

Also under `samples/actor_presets/f1_hualuo/mapviewer_clips/`:

| Alias | Same as | `root.animations` |
|-------|---------|---------------------|
| `hualuo_walk.fbx` | 走路__花萝走路.fbx | 1 × `seasun animation` (1.400s, 546 tracks) |
| `hualuo_jump1.fbx` | 跳跃1__跳跃1.fbx | 1 × `seasun animation` (0.733s, 546 tracks) |
| `hualuo_jump2.fbx` | 跳跃2__跳跃2.fbx | 1 × `seasun animation` (0.667s, 546 tracks) |
| `hualuo_jump3.fbx` | 跳跃3__跳跃3.fbx | 1 × `seasun animation` (0.800s, 546 tracks) |
| `hualuo_no_anim.fbx` | 花萝__花萝无动作.fbx | **0** clips |

**No single FBX embeds both 走路 and 跳跃.** Map-viewer GT loads skin from 花萝, then switches clip source to 走路 / 跳跃N (actor-viewer clip library). Prefer `hualuo_walk.fbx` + `hualuo_jump1.fbx` as Mixer sources.

Verified 2026-09-20: Three `FBXLoader.parse` on staged `hualuo_walk.fbx` / `hualuo_jump1.fbx` → non-empty `root.animations`.
