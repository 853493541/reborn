# native — agent notes

C++ camera shim (`camera_shim.cpp`) injected/loaded for camera work.

- Build: `native\build_shim.cmd` (MSVC VS2022 BuildTools, `vcvars64`). Output goes to
  `C:\SeasunGame\MovieEditor\bin64\camera_shim.dll`; `native/obj/` is ignored.
- The script verifies the `RC_Shim` exports with `dumpbin` — a build without those
  exports is a failure.
- Kill running engine hosts before rebuilding; the DLL can be locked.
- The shim must stay minimal: engine interfaces/behavior are authoritative, adopt from
  IL/binaries rather than reimplementing (root `AGENTS.md` §6).
- Root `AGENTS.md` rules apply.
