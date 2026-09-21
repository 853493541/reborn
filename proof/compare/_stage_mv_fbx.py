from pathlib import Path
import re, hashlib, shutil, json

fbx_root = Path(r"C:\SeasunGame\MovieEditor\source\fbx")
files = [
  fbx_root / "花萝" / "花萝无动作.fbx",
  fbx_root / "走路" / "花萝走路.fbx",
  fbx_root / "跳跃1" / "跳跃1.fbx",
  fbx_root / "跳跃2" / "跳跃2.fbx",
  fbx_root / "跳跃3" / "跳跃3.fbx",
]

def extract_clip_names(data: bytes):
    names = set()
    for m in re.finditer(rb"AnimStack::([^\x00]{1,80})", data):
        names.add(m.group(1).decode("ascii", errors="ignore"))
    text = data.decode("latin1", errors="ignore")
    for m in re.finditer(r"AnimationStack::([^\",\n\r]{1,80})", text):
        names.add(m.group(1).strip())
    for m in re.finditer(r'Take\s+\d+\s*:\s*"([^"]+)"', text):
        names.add(m.group(1).strip())
    gbk = data.decode("gb18030", errors="ignore")
    for m in re.finditer(r"AnimationStack::([^\x00\"]{1,80})", gbk):
        names.add(m.group(1).strip())
    for needle in ("走路", "跳跃", "待机", "奔跑", "Walk", "Jump", "Take"):
        idx = 0
        while True:
            i = gbk.find(needle, idx)
            if i < 0:
                break
            s = gbk[max(0, i - 12) : i + len(needle) + 12]
            s = "".join(ch for ch in s if ch.isprintable() and ch not in "\r\n\t")
            if 2 <= len(s) <= 50:
                names.add(s.strip())
            idx = i + 1
    cleaned = []
    for n in names:
        n = n.strip().strip("\x00")
        if not n or len(n) > 80:
            continue
        if n.startswith(("Model::", "Material::", "Geometry::", "Texture::")):
            continue
        cleaned.append(n)
    return sorted(set(cleaned))

results = []
for p in files:
    data = p.read_bytes()
    is_bin = data.startswith(b"Kaydara FBX Binary")
    names = extract_clip_names(data)
    stack_count = data.count(b"AnimStack") + data.count(b"AnimationStack")
    # binary FBX: count AnimationStack nodes more carefully via null-terminated
    anim_node = len(re.findall(rb"AnimationStack\x00", data))
    results.append({
        "path": str(p),
        "folder": p.parent.name,
        "name": p.name,
        "bytes": len(data),
        "sha256": hashlib.sha256(data).hexdigest(),
        "binary": is_bin,
        "AnimStack_hits": stack_count,
        "AnimationStack_nul": anim_node,
        "clip_name_candidates": names[:50],
    })
    print(f"{p.parent.name}/{p.name} bytes={len(data)} stacks={stack_count} nul={anim_node} names={names[:20]}")

dest = Path(r"C:\Users\Zhibin Ren\jx3-ani-player\samples\actor_presets\f1_hualuo\mapviewer_clips")
dest.mkdir(parents=True, exist_ok=True)
proof = Path(r"C:\Users\Zhibin Ren\jx3-ani-player\proof\compare\mapviewer_fbx_clips")
proof.mkdir(parents=True, exist_ok=True)
root = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")
staged = []
for p in files:
    target = dest / f"{p.parent.name}__{p.name}"
    shutil.copy2(p, target)
    shutil.copy2(p, proof / target.name)
    rel = str(target.relative_to(root)).replace("\\", "/")
    staged.append(rel)
    print("STAGED", rel, target.stat().st_size)

(Path(r"C:\Users\Zhibin Ren\jx3-ani-player\proof\compare") / "mapviewer_fbx_clip_probe.json").write_text(
    json.dumps({"results": results, "staged": staged}, ensure_ascii=False, indent=2),
    encoding="utf-8",
)
print("probe ok")
