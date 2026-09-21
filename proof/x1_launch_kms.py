import re, os, subprocess, time
kms = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4_probe\seasun\editortool\movieeditor\source\plot\actor\经首道源岛\沈眠风海上偷袭\f1_沈眠风海上偷袭.kms"
data = open(kms, "rb").read()
text = data.decode("utf-8", "ignore")
paths = set(re.findall(r'data\\source\\[^"\\s>]+\.pss', text, re.I))
paths |= set(re.findall(r"data/source/[^\"\\s>]+\.pss", text, re.I))
paths = sorted(paths)
open(r"C:\Users\Zhibin Ren\jx3-ani-player\proof\f1_shenmianfeng_pss.txt", "w", encoding="utf-8").write("\n".join(paths))
print("pss", len(paths))
for p in paths[:40]:
    print(p)

me = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\MovieEditor"
subprocess.run(["taskkill", "/IM", "MovieEditorHD.exe", "/F"], capture_output=True)
time.sleep(2)
exe = os.path.join(me, "bin64", "MovieEditorHD.exe")
print("launch", exe, "NOTLAUCNER", kms)
subprocess.Popen([exe, "NOTLAUCNER", kms], cwd=me)
print("spawned")
