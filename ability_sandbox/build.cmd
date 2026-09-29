@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set BIN=C:\SeasunGame\MovieEditor\bin64
"%CSC%" /nologo /unsafe /platform:x64 /target:winexe /codepage:65001 /out:"%BIN%\ability_sandbox.exe" ^
  /r:"%BIN%\MovieEngineCLR.dll" ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ^
  ability_sandbox\rb\RebornClient.cs ability_sandbox\rb\TerrainSampler.cs ability_sandbox\rb\FoliageCollision.cs ^
  ability_sandbox\rb\CameraSystem.cs ability_sandbox\rb\CameraSettings.cs ability_sandbox\rb\EngineRay.cs ^
  ability_sandbox\rb\CameraShim.cs ability_sandbox\rb\VideoSettings.cs
if errorlevel 1 goto :fail
copy /y ability_sandbox\rb\camera.json "%BIN%\camera.json" >nul
copy /y ability_sandbox\rb\scene_init_param.txt "%BIN%\scene_init_param.txt" >nul
echo build OK
goto :eof
:fail
echo BUILD FAILED
exit /b 1
