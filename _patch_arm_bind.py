from pathlib import Path
ROOT = Path(r"C:\Users\Zhibin Ren\jx3-ani-player")

# Insert preferHandArmBinding() and wire it before mixer; extend attachments with reverse arm sync.

PREFER = r'''
/** Body mesh only has upperarm stubs; hand mesh owns the full arm chain.
 *  Rename body duplicates so AnimationMixer binds bip01_* arm tracks to the hand mesh,
 *  then bone-link body stubs from the hand bones each frame. */
function preferHandArmBinding(root) {
  const ARM_NAMES = new Set([
    'bip01_l_upperarm', 'bip01_r_upperarm',
    'bip01_l_foretwist', 'bip01_r_foretwist',
    'bip01_l_foretwist1', 'bip01_r_foretwist1',
    'bip01_l_hand', 'bip01_r_hand',
    'bone_l_armtwist', 'bone_r_armtwist',
  ]);
  // also fingers on body if any
  const isArmish = (name) => {
    const n = String(name || '').toLowerCase();
    if (ARM_NAMES.has(n)) return true;
    if (/^bip01_[lr]_finger/.test(n)) return true;
    if (/^bip01_[lr]_fore/.test(n)) return true;
    if (/^bip01_[lr]_hand/.test(n)) return true;
    if (/armtwist/.test(n)) return true;
    return false;
  };

  let handMesh = null;
  let bodyMesh = null;
  root.traverse((o) => {
    if (!o.isSkinnedMesh) return;
    if (/hand_hdmesh/i.test(o.name) && !/glove/i.test(o.name)) handMesh = o;
    if (/body_hdmesh/i.test(o.name)) bodyMesh = o;
  });
  if (!handMesh || !bodyMesh) {
    return { renamed: 0, note: 'missing hand/body' };
  }
  const handNames = new Set(
    handMesh.skeleton.bones.map((b) => String(b.name || '').toLowerCase())
  );
  let renamed = 0;
  for (const bone of bodyMesh.skeleton.bones) {
    const key = String(bone.name || '').toLowerCase();
    if (!isArmish(key)) continue;
    if (!handNames.has(key)) continue;
    bone.name = `__body__${bone.name}`;
    renamed += 1;
  }
  return { renamed, hand: handMesh.name, body: bodyMesh.name };
}

function createArmBodySyncLinks(root) {
  // After rename, body arm bones are __body__*; hand keeps original names (mixer targets).
  let handMesh = null;
  let bodyMesh = null;
  root.traverse((o) => {
    if (!o.isSkinnedMesh) return;
    if (/hand_hdmesh/i.test(o.name) && !/glove/i.test(o.name)) handMesh = o;
    if (/body_hdmesh/i.test(o.name)) bodyMesh = o;
  });
  if (!handMesh || !bodyMesh) return [];

  const handMap = new Map(
    handMesh.skeleton.bones.map((b) => [String(b.name || '').toLowerCase(), b])
  );
  const links = [];
  for (const bone of bodyMesh.skeleton.bones) {
    const n = String(bone.name || '');
    if (!n.startsWith('__body__')) continue;
    const orig = n.slice('__body__'.length).toLowerCase();
    const sourceBone = handMap.get(orig);
    if (!sourceBone) continue;
    links.push({
      meshName: bodyMesh.name,
      mesh: bodyMesh,
      sourceBone,
      targetBone: bone,
      parentInverse: new THREE.Matrix4(),
      targetWorld: new THREE.Matrix4(),
      localMatrix: new THREE.Matrix4(),
      position: new THREE.Vector3(),
      quaternion: new THREE.Quaternion(),
      scale: new THREE.Vector3(),
    });
  }
  return links;
}
'''

for rel in ("web/fbx_viewport.js", "viewport_fbx/fbx_viewport.js"):
    p = ROOT / rel
    js = p.read_text(encoding="utf-8")

    MARKER = "/* === HEAD_ATTACHMENTS_BEGIN === */"
    if "function preferHandArmBinding" not in js:
        # insert prefer helpers right after MARKER line
        a = js.find(MARKER)
        if a < 0:
            raise SystemExit(f"no marker {rel}")
        insert_at = a + len(MARKER)
        js = js[:insert_at] + "\n" + PREFER + js[insert_at:]

    # Wire: before createHeadAttachments, call preferHandArmBinding
    old = (
        "      partVisibility = applyAnimatedHeadVisibility(root);\n"
        "      window.__actor.partVisibility = partVisibility;\n"
        "      headAttachments = createHeadAttachments(root);\n"
        "      window.__actor.attachments = headAttachments;\n"
        "      updateHeadAttachments(headAttachments, root);\n"
    )
    new = (
        "      partVisibility = applyAnimatedHeadVisibility(root);\n"
        "      window.__actor.partVisibility = partVisibility;\n"
        "      const armBind = preferHandArmBinding(root);\n"
        "      window.__actor.armBind = armBind;\n"
        "      headAttachments = createHeadAttachments(root);\n"
        "      const armSync = createArmBodySyncLinks(root);\n"
        "      headAttachments.boneLinks = [...(headAttachments.boneLinks || []), ...armSync];\n"
        "      window.__actor.attachments = headAttachments;\n"
        "      updateHeadAttachments(headAttachments, root);\n"
    )
    if old not in js:
        raise SystemExit(f"wire missing {rel}")
    js = js.replace(old, new, 1)

    # meta
    if "armBind" not in js[js.find("__FBX_READY__"):js.find("__FBX_READY__")+800]:
        meta = "      if (partVisibility) window.__FBX_READY__.partVisibility = partVisibility;"
        meta2 = (
            "      if (partVisibility) window.__FBX_READY__.partVisibility = partVisibility;\n"
            "      if (window.__actor?.armBind) window.__FBX_READY__.armBind = window.__actor.armBind;"
        )
        if meta not in js:
            raise SystemExit(f"meta missing {rel}")
        js = js.replace(meta, meta2, 1)

    p.write_text(js, encoding="utf-8")
    print(rel, "arm bind patched", "preferHandArmBinding" in js)
