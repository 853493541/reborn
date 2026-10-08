# Basic UI — panel group research register (recommended shortlist)

**Scope:** the 推荐 shortlist's panel windows (stage 1 of the viewer catalog). **Verification policy:**
INI + authored state (user decision) — each entry renders the shipped INI state, marked `PARTIAL`.
This register records the INI facts, the page/tab structure, the authored-vs-runtime split, and the
review state of each panel; per-panel Lua deep-dives follow in later passes.

2026-10-04: the shortlist grew to 61 windows (36 promoted from the classified stages), then split —
the reviewer's 38 confirmed items (1.1-1.38) moved to stage 2 `liked` (2.x) and 20 more were
promoted into 推荐 (1.x). The new panels (CharInfo/Matrix/KungFuPanel/CraftPanel/TeamBuilding/
TeamBuildingPlayerSet/RankingPanel/ReputationPanelNew/GuildBankPanel/PetPanel/PersonalCard_ShowData/
FormationPanel/GoldTeam/...) get register rows in the next panel pass.

**Sources:** `ui-process-app/assets/ui/Config/Default/<Panel>.ini` (extracted 2026-10-03),
`docs/ui/BASIC_UI_INVENTORY.md` (scope register), the render audit (2026-10-03, all 25 recommended
windows rendered; content ratios in `docs/EXPERIENCES.md`).

## Panel register

| window (cn) | sections | root | pages | LSH | authored vs runtime |
|---|---|---|---|---|---|
| CharacterPanel (角色) | 882 | 508x515 @0,170 | 9: Equipment/Strengthen/EquipmentPage/Exterior/Camp/Zhanjie/Zhenying/Homeland/PersonalCard | 263 | frame + page sets authored; equipment slots, attribute rows and the live 3D role (`Wnd_SceneRole_1`) are runtime |
| BigBagPanel (背包) | 229 | 410x410 @500,273 (runtime 440x410) | – | 32 | normal list view (bCompact=false, all six bags open); six category rows are engine-laid (FormatAllItemPos) and clipped to the Handle_Bag_Normal viewport; item cells runtime |
| NewSkillPanel (武学) | 1005 | 960x624 @10,150 | 5: Kungfu/Qixue/BZSkillList/PickedSkills/Qixue_22 | 245 | page sets + skill frames authored; `Handle_SkillList` rows runtime |
| SocialPanel (社交) | 248 | 280x624 @500,225 | 5: Friend/Enemy/Partner/AccountFriend/Recently | 66 | frame + avatar block authored; friend rows runtime |
| MailPanel (邮件) | 171 | 836x512 @100,100 | 2: Receive/Send | 17 | list/content frames authored; mail rows + body runtime |
| AuctionPanel (交易行) | 396 | 960x624 @20,200 | 3: Business/State/Contraband | 90 | search/sell/state frames authored; item lists runtime |
| BigBankPanel (储物箱) | 126 | 750x884 @200,56 | – | 35 | bank frame + grids authored; slots runtime |
| RaidPanel (团队) | 77 | 550x273 @40,270 | – | 32 | toolbar/tabs authored; member rows runtime. Entry shows the tabs + member slots (LSH-shown) |
| GuildMainPanel (帮会) | 1625 | 960x624 @300,200 | 15: OverView/Notice/ShowTip/Inform/SysBig/PersonnelBig/Salary/Manage/Member_Manage/Member_Permission/... | 261 | the largest panel; page sets authored; member/treasury/activity lists runtime |
| TradePanel (交易) | 116 | 380x500 @100,200 | – | 30 | two-sided trade frame authored; trade slots runtime |
| HorsePanel (坐骑槽位) | 680 | 836x510 @10,200 | 4: Horse/HorseDetail/HorseExterior/Qiqu | 163 | frame + detail sets authored; mount slots/scene runtime |
| NewPet (宠物秘鉴) | 335 | 1141x641 @376,247 | 2: MyPet/MedalCollected | 57 | frame + info blocks authored; pet grid runtime |
| WorldMap (地图) | 1061 | 1410x890 @370,96 | – | 469 | map art + zone frames authored; markers/routes runtime (heaviest LSH surface) |
| OptionPanel (系统设置) | 61 | 770x500 @0,0 | – | 10 | settings hub frame authored; the sub-panels are separate windows (VideoSettingPanel/SoundSettingPanel/HotkeyPanel/UICustomModePanel/...) |
| HotkeyPanel (快捷键设置) | 72 | 770x500 @150,150 | – | 14 | frame + group/key columns authored; binding rows runtime (`Handle_List`) |
| UISetting (界面设置) | 1749 | 770x500 @595,280 | – | 132 | the UI-customization settings pages (`WndContainer_AllPage/Comprehensive/UI_zoom/UIScale/TextScale/...`) — layout authored, values runtime |
| UICustomModePanel (主界面自定义模式) | 14 | 350x144 @400,320 | – | 0 | custom-mode confirm bar (Sure/Default) fully authored |
| SystemMenu_Left (系统菜单) | 51 | 320x56 @100,500 | – | 21 | Esc-menu entry row (Char/Guild/ShiTu/TeamBuild/Mail/VoiceHall/HaiLuo/Listen/Speak/Npc) |
| CompassPanel | 57 | 236x259 @500,100 | – | 31 | compass ring + team info; no authored title string (kept English) |
| ExpLine | 28 | 1280x22 @100,400 | – | 16 | experience bar frame; value runtime; no authored title string (kept English) |

## Review notes (2026-10-03 render audit)

All 25 recommended windows render with authored content. Two notable cases:

- **RaidPanel** rendered nearly empty (content 0.04) because its tabs/member slots are
  `LockShowAndHide=1` (the script shows them); the entry now carries a `show` list for
  `CheckBox_Team1..5` + `Image_Member1..5` + title/minimize so the panel bar is visible.
- **MainBarPanel** (主技能栏) renders the frame + kungfu box + page controls; the slot icons are
  runtime skill data (the atlas's middle/right slot frames are transparent — see
  `BASIC_UI_HUD.md`).

Runtime-filled lists (rows/slots/values) are the main authored-state gap across the panel group;
the rows are created by each panel's Lua (`AppendItemFromIni`/`addItem`-style builders). Replaying a
sample row per panel is the next fidelity step once the row templates are identified per panel.

## 2026-10-04 pass — page defaults + BigBagPanel normal-mode state

- **Default pages set** for the multi-page shortlist panels (viewer `page` field; the page chain
  filter drops the other `Page_*` subtrees): characterpanel→`Page_Equipment`,
  newskillpanel→`Page_Kungfu`, socialpanel→`Page_Friend`, mailpanel→`Page_Receive`,
  auctionpanel→`Page_Business`, horsepanel→`Page_Horse`, newpet→`Page_MyPet`,
  questtracelist→`Page_QuestTraceList`, guildmainpanel→`Page_OverView`. GuildMainPanel render drops
  783→201 sections, QuestTraceList 51→50.
- **BigBagPanel** now renders its runtime normal-mode state, from `BigBagPanel.lua`:
  window 440x410 (`nFrameW/nFrameH` L33-34, init `UpdateSize` call L6788-6795);
  `Handle_Bag_Compact` hidden (`UpdateNormal` L2275-2286, module default `bCompact=false` L26);
  `Wnd_Dismantling` hidden (shown only while the cursor is in BREAK mode, `ShowBreakEquipProduct`
  L5604-5612 / `BigBagPanel_EndBreakEquip` L10343-10347); `Text_Bag2/3/4` shown (the script shows
  all six rows; the authored LSH=1 flags hid three in the static render); filter checkboxes at the
  container origin (runtime `FormatAllItemPos`); `Handle_Total` pinned to (0,0) (its background
  children are resized to the full frame by `UpdateSize`, so it cannot be offset); and
  `Handle_Bag_Normal` clipped to its 330x111 viewport (scroll content handle, `RegisterScrollControl`
  L6742-6752, rows laid out by `FormatAllItemPos` L1442-1455).
- New viewer `adjust` fields: **`posType`** (override the viewer's PosType inference for a section)
  and **`clip`** (clip a container's children to its authored rect, `$Clip`).
- Post-art render audit (25 recommended): bigbagpanel 145 sections content=0.477 (was 166/0.277),
  guildmainpanel 201/0.402 (was 783/0.764), questtracelist 50/0.815. Remaining thin panels
  (raidpanel 0.034, chatpanel-normal 0.036) are runtime-filled lists/messages — the authored state
  is genuinely empty.

## Reproduce

```powershell
ui-process-app\bin\Release\net5.0-windows\UiProcessApp.exe --render characterpanel --out cp.png --dump cp.txt
# render every recommended window + content audit: see docs/EXPERIENCES.md 2026-10-03 render audit
```

**Confidence:** INI facts HIGH (parsed from the shipped INIs); authored/runtime split MED (from the
scripts seen so far + render audit); visual fidelity LOW until GT captures exist.

Last verified: 2026-10-04 (`--selftest` 1240/0/0; bigbagpanel 145 sections / 445x411; guildmainpanel
page=Page_OverView 201 sections).
