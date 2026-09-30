# tools/proof — agent notes

| Tool | Purpose |
|---|---|
| `image_stats.py` | image numeric fingerprint: size, SHA256, per-region mean RGB — inspect screenshots/proof images **without attaching them** (the model API caps images per request; see root `AGENTS.md` §13) |

Run with `.venv\Scripts\python.exe` (Pillow is present via matplotlib).

```powershell
.venv\Scripts\python.exe tools\proof\image_stats.py <image> [<image>...] [--grid 8x8]
```
