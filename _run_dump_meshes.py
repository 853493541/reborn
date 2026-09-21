from pathlib import Path
from urllib.parse import urlencode
import json, subprocess, shutil
import fbx_actor as fa

actor = fa.load_fbx_actor()
fa.ensure_server(actor)
port = actor._port
q = urlencode({
    'fbx': '/samples/actor_presets/f1_hualuo/hualuo_no_anim.fbx',
    'tex': '/samples/actor_presets/f1_hualuo/tex/',
    'closeup': '1',
    'clipFbx': '/samples/actor_presets/f1_hualuo/mapviewer_clips_ascii/walk.fbx',
    't': '0.63',
})
url = f'http://127.0.0.1:{port}/web/fbx_viewport.html?{q}'
chrome = (
    shutil.which('chrome')
    or r'C:\Program Files\Google\Chrome\Application\chrome.exe'
)
print('url', url)
r = subprocess.run(
    ['node', '_dump_meshes.mjs', url, chrome],
    cwd=str(Path('.').resolve()),
    capture_output=True,
    text=True,
    timeout=180,
)
print(r.stdout)
print(r.stderr)
print('rc', r.returncode)
