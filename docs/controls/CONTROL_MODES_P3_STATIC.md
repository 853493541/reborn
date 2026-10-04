# Control modes P3 — exe/engine consumer chain (static)

**Client build:** 1.5.0.9975 (`JX3RepresentX64.dll`,
`...\zhcn_hd\bin64`, 2026-09-27). All addresses are game-client RVAs.

## 1. Thunk table (game-client `JX3RepresentX64.dll`)

Every control/input function is reached through a single 5-byte `jmp` thunk in
the low stub region (`0x1800xxxxx`); direct `E8` xrefs give exactly one thunk
per function:

| Function | RVA | Thunk |
|---|---|---|
| `CommitInput` | `0x1805E3270` | `0x18000E9B2` |
| `GetMoveInfo` | `0x1805DFE90` | `0x18001AC8F` |
| queued-input applier | `0x1805DF7E0` | `0x18001622F` |
| `Move` setter | `0x1805E0010` | `0x18000569B` |
| `Jump` setter | `0x1805DFFF0` | `0x1800099E9` |
| clear-input | `0x1805E0030` | `0x18000759A` |
| `AjustCtrlInput` | `0x1805E1ED0` | `0x1800164D7` |
| controller apply | `0x1805DF350` | `0x1800214DB` |
| `EnableControlCamera` | `0x1805E36F0` | `0x18001BB99` |
| `BeginDragCamera` | `0x1805E2640` | `0x180005344` |
| `EndDragCamera` | `0x1805E3B20` | `0x18001F82A` |
| `SetCameraDragParams` | `0x1805F9B30` | `0x18000D8F0` |
| `ForceResetCamera` | `0x1805E4680` | `0x1800217F1` |

Evidence: `proof/controls/p3/refs_all.txt`, `thunks.txt`.

## 2. Script API wrappers (`0x180AD35A0` … `0x180AD3A40`)

A cluster of small wrappers (assert strings resolved from the `KGLOG_PROCESS_ERROR`
prologue) that validate the controller via `0x180014E2F` and forward to the
engine thunk:

| Wrapper | Site | Assert string | Forwards to |
|---|---|---|---|
| `GetMoveInfo` | `0x180AD35A0` (call `0x180AD3609`) | `GetMoveInfo` (L194/0xC2, first check L191/0xBF) | `0x1805DFE90` |
| `Move` | `0x180AD3730` (call `0x180AD3796`) | `Move` (L130/0x82) | `0x1805E0010` |
| `Jump` | `0x180AD3690` (call `0x180AD36F6`) | `Jump` | `0x1805DFFF0` |
| (variant with `cmp edi,8 / 10` switch) | `0x180AD39F5` | — | `0x1805E0010` |

These are the **C-side script bindings** for the character controller API
(`Move/GetMoveInfo/Jump/…`). The Lua global names that reach them are registered
through the binding registry (gap **G11** — names are not plaintext).

Evidence: `proof/controls/p3/binding_strings.txt`, `binding_cluster.txt`.

## 3. Input apply chain (HIGH, matches the earlier behavioral model)

```
exe KEventCommonMgr  → world vtable slots (+0x3D0…+0x450, +0x7D0)
        → KGameWorldHandler::AjustCtrlInput 0x1805E1ED0
            → controller apply 0x1805DF350
                → queued-input applier 0x1805DF7E0  (0x18001622F thunk)
                    callers at 0x1805DF459 / 0x1805DF6F9:
                    iterate container [obj+8] (node tick [node+0x10] vs edi),
                    call applier(obj, node)
        → CommitInput 0x1805E3270 (thunk exists; callers are runtime/vtable —
          not statically visible: this is the runtime-wired boundary)
```

The applier callers implement a **tick-ordered queue drain** (compare
`[node+0x10]` against a tick register), i.e., the per-frame commit consumes the
queued input commands in order — the static half of the G1 boundary.

`ClearInput` (`0x1805E0030`) has a caller at `0x1805EBA41` (controller class
region), consistent with state-reset paths.

## 4. Remaining (dynamic, probe host)

1. Watchpoint writers/readers of the camera-manager flags `+0x1AC`/`+0x1B0`
   and state `+0x5C` (G2/G3) in our own host process.
2. Identify the runtime vtable dispatch that calls `CommitInput` per frame
   (G1 completion).
3. G11: locate the binding registry (hashed names) to name the Lua → wrapper
   mappings exactly.

Probe plan: a `#iso` feature build (`reborn_client_control_probe.exe`) with an
`RC_PROBE` mode that logs, per frame: controller queue (`+0x7C..+0x98`), intent
fields (`+0x3C/+0x4C/+0x50`), camera manager fields (`+0x90..`/`+0x1A8`/
`+0x1AC`/`+0x1B0`/`+0x5C`), and the property store `[SO3+0x25F08]`; plus
debugger watchpoints on the runtime-built tables for the field writers.

## Reproduce

```
.venv\Scripts\python.exe <inline scanner>   # see proof/controls/p3/*.txt headers
tools/netcode/xref_va.py JX3RepresentX64.dll 0x...   # data refs
```

Last verified: 2026-10-02.
