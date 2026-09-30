# native — agent notes

C++ camera shim (`camera_shim.cpp`) injected/loaded for camera work.

- Build: `native\build_shim.cmd` (MSVC VS2022 BuildTools, `vcvars64`). Output goes to
  `C:\SeasunGame\MovieEditor\bin64\camera_shim.dll`; `native/obj/` is ignored.
- The script verifies the `RC_Shim` exports with `dumpbin` — a build without those
  exports is a failure.
- Kill running engine hosts before rebuilding; the DLL can be locked.
- The shim must stay minimal: engine interfaces/behavior are authoritative, adopt from
  IL/binaries rather than reimplementing (root `AGENTS.md` §6).
- Never patch the installed client/engine binaries (on disk or in memory) as a research
  technique (root `AGENTS.md` §4). Any patch is a locked decision needing explicit
  sign-off — the broken `RC_PatchD6` trampoline is the cautionary example.
- Root `AGENTS.md` rules apply.
