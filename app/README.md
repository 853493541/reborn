# JX3 Reborn — start app

A small WinForms launcher for the canonical game client:

| Option | Launches | Notes |
|---|---|---|
| 进入游戏 / Play | `bin64\reborn_client.exe` | default map (龙门寻宝) |
| 选择地图 / Play with map | `bin64\reborn_client.exe` with `RC_MAP=<jsonmap>` | picker: 龙门寻宝 / 夜晚 / 海岛绝境 / 白龙绝境 / 天原绝境 |

The client is started with working dir `C:\SeasunGame\MovieEditor` (required for
engine config/VFS). Map selection is the client's own `RC_MAP` env var.

## Build

```cmd
app\build_launcher.cmd
```

Produces `app\JX3Reborn.exe` (C# net48, x64, winexe).

## Run

- Desktop shortcut: **JX3 Reborn** (icon from MovieEditorHD.exe)
- Or run `app\JX3Reborn.exe` directly.
