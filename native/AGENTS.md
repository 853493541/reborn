# native — agent notes

C++ camera shim (`camera_shim.cpp`) injected/loaded for camera work.

- Build: `native\build_shim.cmd` (MSVC VS2022 BuildTools, `vcvars64`). Output goes to
  `C:\SeasunGame\MovieEditor\bin64\camera_shim.dll`; `native/obj/` is ignored.
- The script verifies the `RC_Shim` exports with `dumpbin` — a build without those
  exports is a failure.
- The bin64 shim is **shared by every client**: the script refuses to build from a source
  without `RC_D6Seed` (stale branch) and verifies that export after linking. A 2026-09-30
  stale-branch build silently reintroduced the D6 AV for all clients.
- Kill running engine hosts before rebuilding; the DLL can be locked.
- The shim must stay minimal: engine interfaces/behavior are authoritative, adopt from
  IL/binaries rather than reimplementing (root `AGENTS.md` §6).
- Never patch the installed client/engine binaries (on disk or in memory) as a research
  technique (root `AGENTS.md` §4). Any patch is a locked decision needing explicit
  sign-off — the broken `RC_PatchD6` trampoline is the cautionary example.
- `client_host/` is the Phase 3 client-engine host core (boot the client stack the
  game way, drive actor/animation/.Sfx; see `docs/engine_host/SFX_WIRING_PLAN.md`).
  Build: `native\client_host\build_client_host.cmd` -> `native\client_host\out\`.
  It patches the loaded engine **in memory** for two host adaptations (window
  substitution via `CreateTargetWindow`; hiding `heightmap_bc` so the engine picks
  its own source-format landscape loader for loose maps). These are host wiring,
  documented, not research patches of the install.
- Root `AGENTS.md` rules apply.
