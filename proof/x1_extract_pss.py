import re
kms = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4_probe\seasun\editortool\movieeditor\source\plot\actor\经首道源岛\沈眠风海上偷袭\f1_沈眠风海上偷袭.kms"
text = open(kms, "rb").read().decode("utf-8", "ignore")
paths = sorted(set(re.findall(r'data\\source\\(?:[^"]+?)\\.pss', text, re.I)))
# looser
paths2 = sorted(set(m.group(0) for m in re.finditer(r'data\\source\\[^"]{5,200}?\.pss', text, re.I)))
open(r"C:\Users\Zhibin Ren\jx3-ani-player\proof\f1_shenmianfeng_pss.txt","w",encoding="utf-8").write("\n".join(paths2))
print("paths", len(paths), "paths2", len(paths2))
for p in paths2[:40]:
    print(p)
