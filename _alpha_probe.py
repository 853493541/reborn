from PIL import Image
from pathlib import Path
tex = Path(r"C:\Users\Zhibin Ren\jx3-ani-player\samples\actor_presets\f1_hualuo\tex")
for f in [
  "f1_1004_face_hd_s0_c0_Diffuse.png",
  "f1_1004_face_hd_s1_c0_Diffuse.png",
  "f1_1004_face_hd_s2_c0_Diffuse.png",
  "f1_1004_face_hd_s3_c0_Diffuse.png",
  "f1_1004_head_hd_s0_c0_Diffuse.png",
  "f1_2227_body_hd_s0_c0_Diffuse.png",
  "f1_2227_body_hd_s1_c0_Diffuse.png",
  "f1_2227_body_hd_s2_c0_Diffuse.png",
]:
  im = Image.open(tex/f).convert("RGBA")
  a = im.split()[-1].histogram()
  n=sr=sg=sb=0
  for r,g,b,aa in im.getdata():
    if aa>200:
      n+=1; sr+=r; sg+=g; sb+=b
  mean = (round(sr/n), round(sg/n), round(sb/n)) if n else None
  print(f"{f}: a0={a[0]} a255={a[255]} mid={sum(a[1:255])} meanOpaque={mean}")
