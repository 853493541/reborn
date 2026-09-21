
from pathlib import Path
frag = Path(r"C:\Users\Zhibin Ren\jx3-ani-player\_ready_block.jsfrag").read_text(encoding="utf-8")
for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
    p = Path(r"C:\Users\Zhibin Ren\jx3-ani-player") / rel
    js = p.read_text(encoding="utf-8")
    a = js.find("      let bc = 0;")
    b = js.find("window.dispatchEvent(new Event('fbx-ready'));", a)
    if a < 0 or b < 0:
        raise SystemExit(f"range missing in {rel}")
    b = b + len("window.dispatchEvent(new Event('fbx-ready'));")
    js = js[:a] + frag + js[b:]
    p.write_text(js, encoding="utf-8")
    # sanity
    if "`花萝 FBX ready" not in js and "花萝 FBX ready · ${bc}" not in js:
        # check template ok
        if "${bc}" not in js[a:a+200]:
            print(rel, "WARN still broken?", js[a:a+120])
    print(rel, "fixed", "mixer:" in js[a:a+len(frag)+50])
