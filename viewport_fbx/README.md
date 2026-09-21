# F1 FBX actor viewport (map-viewer 花萝 path)

Wenjing: catalog UI stays; viewport must host map-viewer’s F1 FBX actor/anchor, not only blue mesh LBS.

## Preset (same as `ensurePlayerAnchorRig`)
- `samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx`
- `samples/actor_presets/f1_hualuo/tex/`
- Source: MovieEditor `source/fbx/花萝` / `/api/actor-exports` name `花萝`

## Host
```bash
cd viewport_fbx && python3 -m http.server 8765
# open http://127.0.0.1:8765/index.html
```

Pose hook for Dev4 / PlaybackClock:
```js
window.__applyPose({ BoneName: Float32Array(16) /* column-major */ })
```

Dev3 feeds `clock.sample(with_pose=True)` matrices into `__applyPose` once Tk embeds this surface (or Dev4 paints into the lookalike viewport directly).
