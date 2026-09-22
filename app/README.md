# JX3 Reborn — start app

Branch: `app`

A small WinForms launcher with two options:

| Option | Host | What it plays |
|---|---|---|
| 角色动作 / Character Action | `bin64\spike_host.exe` | FLWS tani on 花萝 + PSS SFX + skill sound |
| 地图显示 / Map Display | `bin64\map_spike_host.exe` | MovieEditor map render (龙门寻宝, editor camera) |

## Build

```cmd
app\build_launcher.cmd
```

Produces `app\JX3Reborn.exe` (C# net48, x64, winexe).

## Run

- Desktop shortcut: **JX3 Reborn** (icon from MovieEditorHD.exe)
- Or run `app\JX3Reborn.exe` directly.

Both hosts are expected in `C:\SeasunGame\MovieEditor\bin64` and are launched with
working dir `C:\SeasunGame\MovieEditor` (required for engine config/VFS).

## Rebuild the hosts

Character action host (from repo root):

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /target:exe `
  /out:"C:\SeasunGame\MovieEditor\bin64\spike_host.exe" `
  /r:"C:\SeasunGame\MovieEditor\bin64\MovieEngineCLR.dll" `
  /r:"C:\SeasunGame\MovieEditor\bin64\MovieEditorHD.exe" `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll engine_host_spike\SpikeHost.cs
```

Map host: see `engine_host_spike\run_map.cmd` and `render_map.ps1`.
