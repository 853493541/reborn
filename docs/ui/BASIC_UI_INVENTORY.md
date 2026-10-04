# Basic UI inventory — the client's `ui/Config/Default` window register

**Status: extraction DONE (2026-10-03); classification is a working heuristic.** This is the scope register for the BASIC UI deep-research pass (full default client UI, HUD + panels + menus + world/interaction; login, mode-specific (BR/arena/minigames), housing and debug windows excluded).

## Provenance / reproduce

- Window names: `proof/netcode/ui_lua_probe/out/ui/module_info.xml` (tracked manifest) — every `ui\Config\Default\**.lua` entry was converted to its candidate INI path (`ui/Config/Default/<same rel>.ini`).
- Candidate list (1,365 paths): `proof/ui/basic_ui/candidate_inis.txt`.
- Extraction (2026-10-03, official PakV4 extractor via `tools/netcode/extract_pak_paths.py --batch 250`): **1,210 HIT / 155 MISS** — log `proof/ui/basic_ui/extract.log`. Extracted INIs live in the ignored `ui-process-app/assets/ui/Config/Default/` (viewer asset root).
- 155 candidates have no same-path INI (script-only/helper entries) — `proof/ui/basic_ui/missing_inis.txt`.
- Path table reference: `ui/filepath.txt` (extracted) declares `ConfigDefault=\UI\Config\Default`; fonts/strings at `\UI\Scheme\Elem\*`, `\UI\Scheme\Case\String.txt`.

## Coverage summary

| | count |
|---|---|
| candidate INI paths probed | 1,365 |
| INIs in the client (`HIT`) | **1210** |
| script-only entries (no same-path INI, `MISS`) | 155 |
| previously local (before this pass) | 131 |
| newly extracted this pass | **1079** |

`was-local` marks rows whose INI already existed locally before this pass (assets/ui + the map-era extraction).

## Scope classes

### HUD (tier 1 - always-on elements) — 53 windows

| window (INI) | was-local |
|---|---|
| `AccelerateBall` | yes |
| `ActionBar` | yes |
| `ActionBarBind` | **new** |
| `ActionSmallBar` | **new** |
| `Announce` | **new** |
| `Balloon` | yes |
| `BuffFold` | yes |
| `BuffList` | yes |
| `BuffMonitor` | **new** |
| `BuffMonitorDaoZong` | **new** |
| `BuffMonitorGeneral` | **new** |
| `BuffMonitorYaoZong` | yes |
| `Bullet` | yes |
| `CDProcess` | **new** |
| `ChatButton` | **new** |
| `ChatPanel_Bg` | **new** |
| `ChatPanel_Game` | **new** |
| `ChatPanel_Normal` | **new** |
| `ChatPanel_Recent` | **new** |
| `ChatSettingPanel` | **new** |
| `ComboPanel` | yes |
| `CompassPanel` | yes |
| `DeBuffList` | yes |
| `DynamicActionBar` | **new** |
| `DynamicMutualBar` | yes |
| `EnterAreaTip` | yes |
| `ExpLine` | yes |
| `FullScreenWarning` | **new** |
| `GeneralProgressBar` | **new** |
| `MainBarPanel` | yes |
| `MainMessageLine` | yes |
| `MiddleMap` | yes |
| `MindControlActionBar` | **new** |
| `Minimap` | yes |
| `PetActionBar` | yes |
| `PLActionBar` | yes |
| `Player` | yes |
| `PQprogressbar` | yes |
| `ProgressBar` | yes |
| `PuppetActionBar` | yes |
| `QuestTraceList` | yes |
| `TargetBuff` | yes |
| `TargetDeBuff` | yes |
| `TargetFaceSet` | **new** |
| `TargetResourcebar` | **new** |
| `TargetSkill` | **new** |
| `TargetTarget` | yes |
| `TeamBuff` | yes |
| `Teammate` | yes |
| `TeamSwitchBtn` | yes |
| `TopBuff` | **new** |
| `WeaponSkillBar` | **new** |
| `WorldMark` | **new** |

### Panels: character / bag / skill (tier 2) — 38 windows

| window (INI) | was-local |
|---|---|
| `Album` | **new** |
| `BatchUse` | **new** |
| `BigBagPanel` | yes |
| `CharacterPanel` | yes |
| `CharacterPanel_Homeland` | **new** |
| `CharacterPanelAwardTip` | **new** |
| `CharacterPanelExplainTip` | **new** |
| `CharInfo` | **new** |
| `CharInfoMore` | **new** |
| `CraftManagePanelnew` | **new** |
| `CraftPanel` | yes |
| `CraftReadComparePanel` | **new** |
| `CraftReaderPanel` | **new** |
| `CraftReadManagePanel` | **new** |
| `Dismantle` | yes |
| `EquipmentShare` | yes |
| `ExaminationPanel` | yes |
| `ExteriorAction` | yes |
| `HorsePanel` | yes |
| `KungFuPanel` | yes |
| `Matrix` | yes |
| `MingJiaoSkill` | yes |
| `NewPet` | **new** |
| `NewPetInfo` | **new** |
| `NewPetSkill` | yes |
| `NewRecipeTip` | **new** |
| `NewSkillGuidePanel` | yes |
| `NewSkillPanel` | yes |
| `PetPanel` | yes |
| `RecipeOpenSure` | **new** |
| `ShareBagPanel` | **new** |
| `Skill_SkinVideo` | **new** |
| `Skill_TalentComment` | yes |
| `SkillFormulaPanel` | yes |
| `SkillGuideSettingPanel` | yes |
| `SkillIntroduce` | yes |
| `ToyBox` | yes |
| `ViewEquip` | yes |

### Panels: social / team / guild (tier 2) — 59 windows

| window (INI) | was-local |
|---|---|
| `AccountException` | **new** |
| `AccountFriendTip` | **new** |
| `AddAccountFriend` | **new** |
| `AddFriendPanel` | **new** |
| `AddPartnerExterior` | **new** |
| `AddTBMegBox` | **new** |
| `AllKBAccounts` | **new** |
| `AnniversaryWishPop` | **new** |
| `AudienceListPanel` | **new** |
| `BarMitzvah` | **new** |
| `BirthdayCelebrateCardPop` | **new** |
| `CallFriendPannel` | **new** |
| `CallGuildMemberPannel` | **new** |
| `FormationPanel` | **new** |
| `friendrank` | **new** |
| `GoldTeam` | yes |
| `GoldTeamAddMoney` | **new** |
| `GoldTeamDistribution` | **new** |
| `GoldTeamLootList` | **new** |
| `GoldTeamPartialPayment` | **new** |
| `GoldTeamPrice` | **new** |
| `GoldTeamSetSubsidy` | **new** |
| `GuildAddMember` | yes |
| `GuildBankPanel` | **new** |
| `GuildCampReverse` | **new** |
| `GuildCastleWarPoints` | **new** |
| `GuildCastleWarRule` | **new** |
| `GuilderPanel` | **new** |
| `GuildLeagueMatches` | **new** |
| `GuildLeagueMatches_BattleInfo` | **new** |
| `GuildLeagueMatches_EnterTip` | **new** |
| `GuildLeagueMatchesSheet` | **new** |
| `GuildLeagueShowPanel` | **new** |
| `GuildLeagueShowPanelTips` | **new** |
| `GuildLeagueSignHint` | **new** |
| `GuildListPanel` | **new** |
| `GuildMainPanel` | yes |
| `GuildMemberDragPanel` | **new** |
| `GuildRename` | **new** |
| `GuildRenameEX` | **new** |
| `MentorPanel` | **new** |
| `MentorPanelTip` | **new** |
| `PersonalCard_BirthdaySetPop` | **new** |
| `PersonalCard_CheckOut` | **new** |
| `PersonalCard_Cut` | **new** |
| `PersonalCard_DataEdit` | **new** |
| `PersonalCard_Decorate` | **new** |
| `PersonalCard_Mine` | **new** |
| `PersonalCard_ShowData` | yes |
| `PersonalCard_Tip` | **new** |
| `RaidPanel` | yes |
| `RankingPanel` | yes |
| `ReputationPanelNew` | yes |
| `SocialPanel` | yes |
| `TeamBuilding` | yes |
| `TeamBuildingPlayerSet` | **new** |
| `TeamBuildMessage` | yes |
| `TeamBuildTip` | **new** |
| `WhoSeeMe` | yes |

### Panels: mail / auction / bank (tier 2) — 10 windows

| window (INI) | was-local |
|---|---|
| `AuctionMsgBox` | **new** |
| `AuctionPanel` | yes |
| `BanishPanel` | **new** |
| `BanksInterface` | yes |
| `BigBankPanel` | yes |
| `BlackMarketOperate` | **new** |
| `BuyRule` | **new** |
| `BuyTime` | **new** |
| `MailPanel` | yes |
| `WantedPanel` | yes |

### Menus / settings (tier 3) — 24 windows

| window (INI) | was-local |
|---|---|
| `AutoExitPanel` | **new** |
| `BuyNumberPanel` | **new** |
| `CustomEffects` | yes |
| `EditBox` | yes |
| `EmotionPanel` | yes |
| `ExitPanel` | yes |
| `FilterPanel` | yes |
| `GetNumberPanel` | **new** |
| `HotkeyPanel` | yes |
| `KeyPanel` | **new** |
| `OptionPanel` | yes |
| `SafeReminder` | **new** |
| `SoundSettingPanel` | yes |
| `SystemMenu_Left` | yes |
| `SystemMenu_List` | **new** |
| `SystemMenu_Right` | yes |
| `TopMenu` | yes |
| `TradePanel` | **new** |
| `TradingPanels` | yes |
| `TradingSellers` | **new** |
| `TradingSure` | yes |
| `UICustomModePanel` | yes |
| `UISetting` | yes |
| `VideoSettingPanel` | yes |

### World / quest / interaction (tier 4) — 25 windows

| window (INI) | was-local |
|---|---|
| `AchievementPanel` | **new** |
| `AchievementTip` | **new** |
| `ActivityDetail` | **new** |
| `ActivityGift` | **new** |
| `ActivityList` | yes |
| `ActivityPanel` | **new** |
| `ActivityPlotPanel` | **new** |
| `ActivityRewardCollection` | **new** |
| `ActivitySignIn` | **new** |
| `ActivityTipPanel` | **new** |
| `AutoSearch` | **new** |
| `CampPanel` | yes |
| `DialoguePanel` | yes |
| `MilitaryRanking` | **new** |
| `Navigator` | **new** |
| `NPCGuidelines` | **new** |
| `QuestAcceptPanel` | **new** |
| `QuestBar` | **new** |
| `QuestContrastPanel` | **new** |
| `QuestGuide` | **new** |
| `Questionnaire_FresherExitGame` | **new** |
| `QuestItem` | **new** |
| `QuestRewardTip` | **new** |
| `TrackingTip` | **new** |
| `WorldMap` | yes |

### Other panels / dialogs (tier 5, unclassified) — 790 windows

In scope for the full default UI; classification refined per session when a group is researched. Compact list:

- `ActiveMessage0` · `AIChat_Statement` · `AIChatPanel` · `AnimationMgr` · `ArtistReward` · `ArtistRewardAmount`
- `ArtistRewardSure` · `AssistNewbieDungeon` · `AssistNewbieInvite` · `AssistNewbieRelease` · `BalanceBar` · `BlueprintsChoice`
- `BookCopyPanel` · `BookExchangePanel` · `BookInfoPanel` · `BubblePanel` · `BugReport` · `ButlerNpcInfo`
- `CampActiveTime` · `CampBossPanel` · `CampFireworks` · `CampMapsTips` · `CampMapsWeatherTip` · `CampMapsYinShanPanel`
- `CampOB` · `CampRewardTip` · `CampTipPanel` · `CardBuy` · `CardSell` · `CastingPanel`
- `CastleFightCleanup` · `Challenge` · `ChallengeCountDown` · `ChangeVoice` · `ChangGeShadow` · `ChannelPanel`
- `Chapters` · `Charge` · `CheatWarningPanel` · `CheckBaiZhanInfo` · `ChooseGift` · `ChooseGiftMessage`
- `ChooseGiftSFX` · `ChooseReward` · `ChooseVoiceRoom` · `CleanDxPanel` · `CloakColorChange` · `ClueShowList`
- `CMD_Add` · `CMDOB` · `Cohabitation` · `CoinShop` · `CoinShop_AiFace` · `CoinShop_BodyShop`
- `CoinShop_BoxMod` · `Coinshop_CantBuy` · `CoinShop_Center` · `CoinShop_ChangeHairColor` · `CoinShop_CheckOut` · `CoinShop_CustomEffects`
- `CoinShop_Cutscene` · `CoinShop_DyeingCheckOut` · `CoinShop_DyeingHair` · `CoinShop_Entrance` · `CoinShop_Exterior` · `CoinShop_FaceSave`
- `CoinShop_GeneralSetList` · `CoinShop_GoodsIntroduce` · `CoinShop_Groupon` · `CoinShop_HairdyeSave` · `CoinShop_HairShop` · `CoinShop_Home`
- `CoinShop_MyBody` · `CoinShop_MyExterior` · `CoinShop_MyHair` · `CoinShop_MyNewFace` · `CoinShop_MyPosture` · `CoinShop_MySpecialEffects`
- `CoinShop_NewFaceShop` · `CoinShop_NewHairShop` · `CoinShop_News` · `CoinShop_Outfit` · `CoinShop_PackTip` · `CoinShop_PointsAward`
- `CoinShop_PreviewBox` · `CoinShop_RemoveSure` · `CoinShop_SchoolExterior` · `CoinShop_Search` · `CoinShop_SetList` · `CoinShop_SetTip`
- `CoinShop_ShareStation` · `CoinShop_ShowHide` · `CoinShop_SortSetting` · `Coinshop_SpecialEffectPart` · `CoinShop_TradeCenter` · `CoinShop_TuoyinRule`
- `CoinShop_UnPayRel` · `CoinShop_Video` · `CoinShop_View` · `CoinShop_Weapons` · `CoinShop_Welfare` · `CoinShop_ZhouBianRule`
- `Collection` · `Collection_Bag` · `Collection_DeleteSure` · `Collection_Message` · `Collection_MiniBag` · `Collection_OpenBox`
- `Collection_Orange` · `Collection_View` · `ColorPalette` · `ColorTablePanel` · `CombatText` · `CommandAddGang`
- `CommandAddMoney` · `CommandAuction` · `CommandBuySure` · `CommandChangeCommander` · `CommandDataPanel` · `CommandDistribute`
- `CommandDistributeSure` · `CommandElection` · `CommandKickPlayer` · `CommandPlayerList` · `CommandSetting` · `CommandSignup`
- `CommandVoteOnline` · `CommonBlankPanel` · `ConfirmTime` · `ConflatePanel` · `ContactsList` · `CraftIntroduce`
- `CraftStuffPanel` · `CreateVoiceRoom` · `Crosshair` · `CrossingFinishPanel` · `CrossingProcessPanel` · `CrossMap`
- `CurrencyBagPanel` · `CustomMessage` · `CustomTrackList` · `Cyclopaedia` · `Cyclopaedia_Active` · `Cyclopaedia_Career`
- `Cyclopaedia_FAQ` · `Cyclopaedia_JX3Library` · `Cyclopaedia_Log` · `DailySignIn` · `Danmaku` · `DanmakuSetting`
- `DashBoard` · `DaTangJiaYuan` · `DBMPanel` · `DdzIconPanel` · `DdzPanel` · `DdzSettlementPanel`
- `DebugNpcPortrait` · `DelphisGift` · `DesertEquipmentChoose` · `DesertItemBuySure` · `DesertItemNumSure` · `DesertPreset`
- `DesertQuickPack` · `DesertSell` · `DesertStormInfoPanel` · `DesertStormOB` · `DesertStormOBList` · `DesertStormSkillPanel`
- `DesertSuit` · `DesertWarehouse` · `DesertWeaponChoose` · `DesertWeaponSkill` · `DesignationPanelNew` · `Direction`
- `DisableCompositionTip` · `DivinationPanel` · `DLCPanel` · `DomesticatePanel` · `DramaAnnouncement` · `DramaBuyingTip`
- `DramaCluePanel` · `DramaDetailPanel` · `DramaHall` · `DramaHallFilterMenu` · `DramaJieSan` · `DramaLabelFilter`
- `DramaLinkSwitch` · `DramaMoreMessageCard` · `DramaNewClueCard` · `DramaOperation` · `DramaPassWord` · `DramaPlayerList`
- `DramaPlayerTip` · `DramaScore` · `DramaSelectRole` · `DramaStoryPanel` · `DramaTeamBuilding` · `DramaTitle`
- `DramaVotePanel` · `DramaVotingResults` · `DropDownWnd` · `DropLinePanel` · `DurabilityPanel` · `DyeingQualityTips`
- `DynamicBeastBar` · `DynamicNpcMorphPhoto` · `DynamicPetBar` · `DynamicSkillBar` · `DynamicWeatherSetting` · `EmergencyChoose`
- `EmotionManagePanel` · `Equip_Distance` · `EquipCopy` · `EquipInquire` · `EquipmentDIY` · `EquipmentDIYChoose`
- `EquipmentDIYExport` · `EquipmentDIYImport` · `EquipmentDIYMgr` · `EquipmentDIYModify` · `EquipmentDIYPic` · `EquipmentDIYPlan`
- `EquipRecommend` · `Exterior_Operator` · `ExteriorBoxError` · `ExteriorSellBag` · `ExteriorView` · `EYaShaEmergency`
- `EYaShaEventPlay` · `EYaShaGameTeach` · `EYaShaHelper` · `EYaShaInterlude` · `EYaShaLeaveButton` · `EYaShaMeetingOperate`
- `EYaShaMeetingRoom` · `EYaShaMeetingStage` · `EYaShaOpenMeetingConfirm` · `EYaShaPlayer` · `EYaShaPlayerCard` · `EYaShaQuestWatchList`
- `EYaShaQuickMsgPanel` · `EYaShaReport` · `EYaShaSetting` · `EYaShaShowFinal` · `EYaShaTopMenu` · `FamePanel`
- `FameTeach` · `FameUpgrade` · `FancySkating` · `FBBossPanel` · `FBCountDown` · `FBCountNum`
- `FBlist` · `FBlistBossKillTip` · `FBShowPanel` · `FBShowTeam` · `FBTimeRank` · `FEActivationPanel`
- `FEEquipExtractPanel` · `Fellowship` · `FellowshipChoose` · `FellowshipQuest` · `FieldPQPanel` · `FightProgress`
- `FilterInviteMsg` · `FilterMask` · `FindingYouShang` · `FindTeamPQObjective` · `FireCardSFX` · `FishPanel`
- `FiveAttributeDetailsPop` · `FiveAttributePop` · `FixRoomNum` · `FlowerDayPs` · `FlowerInfoPanel` · `FlowerPanel`
- `FPS` · `FriendBack` · `FriendPraise` · `FriendPraiseTip` · `FriendRecruit` · `FriendTip`
- `FullScreenSFX` · `FullShop` · `FullShop_BG` · `FullShop_Detail` · `FullShop_ItemImage` · `FullShop_Left`
- `FullShop_List` · `FullShop_Money` · `FullShop_View` · `FurnitureSetCollect` · `GameGuideCPLevelAwards` · `GameTeach`
- `GasMonitorCY` · `GeneralAttributePop` · `GeneralCounterSFX` · `GeneralInvitation` · `GetNamePanel` · `GetNew`
- `GetNewHomelandSkin` · `GetNewPartner` · `GetPercentagePanel` · `GetPricePanel` · `GlobalEventHandler` · `GMAnnouncePanel`
- `GMPanel` · `GoldPresetPanel` · `GongZhan` · `GoodFanWorks` · `GrouponConfirm` · `GrouponRemind`
- `GrouponRule` · `GuardInfo` · `GuardList` · `GuardPanelSure` · `GuardPanelSureInfo` · `GuidePerson_MengXin`
- `HatredPanel` · `HelpPanel` · `HelpSound` · `HideOtherHomeTip` · `HLBOp_Main` · `HLBView_AreaManagement`
- `HLBView_Blueprint` · `HLBView_Blueprint_SerialNum` · `HLBView_BlueprintExport` · `HLBView_BlueprintImport` · `HLBView_BlueprintLoadBar` · `HLBView_BuildingStats`
- `HLBView_CamSpeed` · `HLBView_CellarLayers` · `HLBView_CustomBrush` · `HLBView_DeleteSure` · `HLBView_DigitalBlueprintExport` · `HLBView_ErrorItemList`
- `HLBView_ExtractFurniture` · `HLBView_ExtractPanel` · `HLBView_Filters` · `HLBView_GenuineIcons` · `HLBView_Help` · `HLBView_ItemList`
- `HLBView_ItemOpColor` · `HLBView_ItemOpHeight` · `HLBView_ItemOpMain` · `HLBView_ItemOpRotate` · `HLBView_ItemOpScale` · `HLBView_ItemRotateDirection`
- `HLBView_Main` · `HLBView_MatchItemList` · `HLBView_Message` · `HLBView_NormalItemList` · `HLBView_PendantBuy` · `HLBView_RClick`
- `HLBView_Recycle` · `HLBView_ReplaceList` · `HLBView_Saveusage` · `HLBView_SomeInfo` · `HLBView_WeatherSetting` · `HLBView_Welfare`
- `HonorChallengePanel` · `HonorChallengeReward` · `HorseEquip` · `HorseExterior` · `HorseExterior_CheckOut` · `HorseStable`
- `HotSpot` · `HouseFastPanel` · `HouseFastPanel_Vistor` · `HouseFastPanel_Wander` · `HouseFrameLicense` · `HouseKeeper`
- `HouseLinkTip` · `HouseMovingGuide` · `HousePlayPanel` · `HouseUpgrade` · `HuaZhaoPhoto` · `Identity`
- `IdentityDynActBar` · `IdentityDynamicBar` · `IdentityPanel` · `IdentityUpGrade` · `IllusionPanel` · `InstanceInfo`
- `Instrument_File` · `Instrument_Main` · `Instrument_Op` · `Instrument_Play` · `InstrumentStatement` · `InterludeHSLHPanel`
- `InterludePanel` · `InviteFriends` · `IrrigatePanel` · `ItemBox` · `ItemBuy_MoneyTrace` · `ItemBuySure`
- `JiangHuMatrix` · `JigsawDragPiece` · `JoinCamp` · `JoinHousePVP` · `Keyboard` · `KFActionBarPanel`
- `KillInformation` · `KillMessage` · `LeftMessage` · `LevelUpAward` · `LevelUpMax` · `LightingCityPanel`
- `LimitedSalePanel` · `LoadingPanel` · `LoadingPlay` · `LockPanel` · `LootRoll` · `LootRollMini`
- `LootShowList` · `LuckyPerson` · `LXGMonitor` · `MacroSettingPanel` · `MahjongIconPanel` · `MahjongPanel`
- `MahjongSettlementPanel` · `MainPlotPanel` · `MainStoryPanel` · `MapCopyList` · `MasterEquipRecommend` · `MasterNote`
- `Match3Game` · `MaterialInfoMore` · `MentorFindMessage` · `MentorMessage` · `MentorTask` · `MentorTransform`
- `MessageBoard` · `MiddleMapFlagEditor` · `MiddleMapSimple` · `MinimizeEffect` · `MobileBuffList` · `MobileSkillGrandPanelTip`
- `MobileSkillIntroduce` · `MobileSkillTip` · `MoGaoKuPanel` · `MoneyBuy` · `MoneySell` · `MonsterBuffChoose`
- `MonsterBuffPanel` · `MonsterDistribute` · `MonsterEntrance` · `MonsterLocker` · `MonsterPanel` · `MonsterSettlement`
- `MonsterSkillPreset` · `MonsterSpiritEnduranceData` · `MoviePanel` · `MultiItemSelect` · `MvpShowPanel` · `MyAward`
- `MyCloudInstrument` · `NetbarAd` · `NetworkVideo` · `NetworkVideoWaiting` · `NewAchievement` · `NewAddFriendPanel`
- `NewBag` · `NewBattleFieldRull` · `NewChargeGiftMonthly` · `NewEmotionAction` · `NewEquip` · `NewEquipRecommend`
- `NewHomelandChangeSkin` · `NewHorse` · `NewJYPlayReward` · `NewOperationActivity` · `NewPartnerEquipment` · `NewPlayerBF`
- `NewQuestPanel` · `NewSafePanel` · `NewSkillBar` · `NewTrialValley` · `NewYearPanel` · `NoticeBoard`
- `NpcExteriorView` · `NPCFeeling` · `NpcMorphBar` · `NPCRoster` · `NPCSpeechSounds` · `NumericalPanel`
- `OldQuestAcceptPanel` · `OperationCenter` · `OperationMode` · `OperationRules` · `OperatPreOrder` · `OrangeWeaponUpg`
- `OTGCDBar` · `OTPetActionBar` · `OutfitModList` · `OutMap` · `PakV4DownloadInfo` · `PakV4Info`
- `PakV4Loading` · `PanelChargeHintPop` · `PanzhazhaiPanel` · `PanzhazhaiTime` · `Partner` · `PartnerAttribute`
- `PartnerBag` · `PartnerBreak` · `PartnerBuyConfirm` · `PartnerDefaultPlan` · `PartnerEquipmentUpgrade` · `PartnerExterior`
- `PartnerLockLink` · `PartnerMeetbyChance` · `PartnerMessage` · `PartnerScence` · `PartnerSelect` · `PartnerTaskAwards`
- `PartnerTaskCheck` · `PartnerTaskList` · `PartnerTaskSetting` · `PartnerTeam` · `PartnerTeamSetting` · `PartnerTip`
- `PartnerUpGrade` · `PartnerVoiceAndStory` · `PartyRecruitPanel` · `PayPathPanel` · `PendantBase` · `PendantChoiceColor`
- `PendantUpgrade` · `PendantUpgradeSelect` · `PerformanceCollect` · `PhotoShop` · `Playerbar` · `PlayerKillMessage`
- `PlayerMode` · `PlayerReturn` · `PlayerReturnTip` · `PlayerView` · `PlayerViewJJC` · `PlayerVisitCard`
- `PlotDialoguePanel` · `PlotExplain` · `PlotSound` · `PluginSingle` · `PluginTotal` · `PopupBuffList`
- `PopupRemind` · `PQNextStage` · `PQTeach` · `PQTimePanel` · `PQwarning` · `PreSetComment`
- `PresetMusicList` · `PrestigePanel` · `ProgressSaveData` · `PVPInput` · `PVPMessageBoard` · `PVPQiXueList`
- `PVPRandomForce` · `PVPReplayAnnouncement` · `PVPReplayBar` · `PVPSelectMap` · `PVPSetting` · `PVPSetting_Last`
- `PZZ_Buildings` · `PZZ_ChoosePage` · `PZZ_SpecialBuildings` · `PZZ_VillageManage` · `PZZ_Villagers` · `QGJump`
- `QixiAlbumPop` · `QixiPicturePop` · `QixueLookup` · `QixueTeachBy` · `QMSoulPanel` · `QTEPanel`
- `QuickConsumePanel` · `QuickConsumeShare` · `QuitCohabitMessage` · `RaidDragPanel` · `RandomReward` · `ReadMailPanel`
- `RealBP` · `RealFirstCharge` · `RealNameCertify` · `RechargeRemind` · `RecordClientData` · `RecoverEquipment`
- `RedEnvelope` · `RedEnvelopeInfo` · `RefinePanel` · `regionPQPanel` · `regionPQreward` · `RegressionPanel`
- `RemainingTimeNotify` · `RemoteCDProcess` · `RenewRule` · `Resourcebar` · `RevivePanel` · `RoadChivalrous`
- `RoadChivalrousDetail` · `RoadChivalrousPopup` · `RoadChivalrousTips` · `RoleRename` · `RoomLinkTip` · `RoommateDragPanel`
- `RoommateTeam` · `RoomPanel` · `RoomRaidReset` · `SafeModifyPwd` · `SafePanel` · `SafeTip`
- `SanFangShaQueue` · `SavePreset` · `SaveSharePreset` · `Scene` · `SceneBlackMask` · `SceneCampTip`
- `SceneMini` · `ScreenLock` · `ScrollDisplay` · `SearchWnd` · `SeasonDistance` · `SeasonFurniture`
- `SeasonFurnitureFragment` · `SeasonFurnitureInfo` · `SeasonLetter` · `SeasonRankPanel` · `SeasonRewardPanel` · `SecurityCard`
- `SelectCampPlanes` · `SelectEnchantment` · `SelectGround` · `SelectMacroIconPanel` · `Selfie` · `SelfieEmotion`
- `SelfieExportDetails` · `SelfieMovieRecordLogo` · `SelfieNav` · `SelfieOneClick` · `SelfieOneClickPreview` · `SelfieOneClickRecording`
- `SelfiePosture` · `SelfieRecord` · `SelfieSaveAI` · `SelfieSaveMusic` · `SelfieSizeBox` · `SelfieStatement`
- `SelfieStudio` · `SelfieTemplateExport` · `SelfieTemplateImport` · `SelfieTemplatePop` · `SellBag` · `SellSure`
- `ServantDismiss` · `ServerReconnect` · `SetMusic` · `SetPersonalVolume` · `SetTeamLootMode` · `SFXPanel`
- `ShareBagBindingPanel` · `ShareStation` · `ShareStation_DyeDetails` · `ShareStation_EditInfo` · `ShareStation_Expression` · `ShareStation_ExteriorChoose`
- `ShareStation_ExteriorFilter` · `ShareStation_Filter` · `ShareStation_ImportExterior` · `ShareStation_Report` · `ShareStation_Rules` · `ShareStation_Shoot`
- `ShareStation_Statement` · `ShareStation_UploadCut` · `ShareStation_UploadInfo` · `ShareStation_WorkLinkTip` · `ShenJianXin` · `ShortcutInturn`
- `SidePanel` · `SignIn` · `SimpleDLCPanel` · `SituationMap` · `SkillBanEdit` · `SkillCDJingYuJue`
- `SkillGlossaryPanel` · `SkillGuidePanel` · `SkillGuidePanelTips` · `SkillPanelTeaching` · `SkillRemind` · `SkillTipPanel`
- `SmallBagPanel` · `SmallCalender` · `SMPopupMenu` · `SpeedEffect` · `SpeedRankPanel` · `SpiritEndurancePanel`
- `SprintPanel` · `StampPlay` · `StartTask` · `StoryDisplay` · `StoryMode` · `StrangeBox`
- `SummonBar` · `SuperRoom` · `SurpriseFreeAd` · `SwitchCenter` · `SwitchServerDLC` · `SwitchServerInfoTip`
- `SwitchServerMemorabilia` · `SwitchServerQueue` · `SwitchServerStandHoldTip` · `SwitchSword` · `TapTapAdvice` · `TapTapFeedback`
- `Teaching` · `TeachingAim` · `TeachingMove` · `TeachingPanel` · `TeamCountdown` · `TeamEditPlayerTags`
- `TeamNumList` · `TeamNumListLong` · `TeamPlayerTagList` · `TeamStatePop` · `TeamTagPlayers` · `TheFlopPanel`
- `ThermometerPanel` · `TimeBuff` · `TitleRankReward` · `TongArena` · `TongBaoGift` · `TongBaoPanel`
- `TongBattledragonTips` · `TongBattleTips` · `TongFarmPanel` · `TopBuffSet` · `TopMenuOther` · `TopMenuSaleList`
- `TraceButton` · `TrafficSure` · `TrafficSurePanel` · `Treasure_GetRewardNew` · `Treasure_PreviewNew` · `TreasureHuntBP`
- `TrialValleyReward` · `TurnCard` · `UIComfirm` · `UICursor` · `UILock` · `UIMovie`
- `UpGradeEffect` · `UserActionChoose` · `VagabondCraftManage` · `VagabondCrossMap` · `VagabondPanel` · `VagabondReward`
- `VampireCountPanel` · `VampireInfoPanel` · `VideoCustomPanel` · `VideoSettingDetails` · `VkActionBar` · `VoiceHall`
- `VoiceHallAgreement` · `VoiceMessage` · `VoiceRoomMessage` · `VoiceRoomNotice` · `VoiceRoomPassword` · `VoiceRoomUpGrade`
- `Wanted_Publish` · `WarningTipPanel` · `WeaponBag` · `WeaponsDisplay` · `WeaponSwitcher` · `WelcomeSignIn`
- `WelfareReturn` · `WhoIsUndercover` · `WinterFestivalNpcInfo` · `WinterFestivalSkillMsg` · `WishingBar` · `WishPanel`
- `WithdrawGold` · `WndLock` · `WoodCardSFX` · `WulinShenghuiDuizhen` · `XiangQiPanel` · `XuanSuQingMaiMonitor`
- `YangDaoCardItem` · `YanTianZongSoullamp` · `YaoZongSkillHint` · `ZombieFightFinal`

## Excluded classes

**login/entry (excluded) — 31:**

- `AddonChangeLog` · `AddonPanel` · `CreditsPanel` · `EULAPanel` · `InternetExplorer` · `KoreaLogo` · `LoadingWaiting` · `LoginCustomRole`
- `LoginCustomRoleMode` · `LoginCustomRoleName` · `LoginCustomRoleNext` · `LoginDeleteRole` · `LoginLogo` · `LoginMessage` · `LoginPassword` · `LoginPayFor`
- `LoginQuestPanel` · `LoginRename` · `LoginRoleList` · `LoginScene` · `LoginServerList` · `LoginServerPanel` · `LoginSwordLogo` · `LoginTokenPanel`
- `LoginWaiting` · `LoginWaitServerList` · `MobileSkillGrandPanel` · `PluginsWarning` · `Queue` · `ScreenShotPanel` · `TwoDimensionalLogin`

**BR mode (excluded - already reproduced) — 57:**

- `ACC_BFInfo` · `ACC_BFShowFinal` · `ACC_DesertStormInfo` · `ACC_Excellent` · `ACC_JJCInfo` · `ACC_JJCRougeInfo` · `ACC_JJCRougeShowFinal` · `ACC_JJCRougeShowPassData`
- `ACC_JJCShowFinal` · `ACC_MobaAffordableEquipment` · `ACC_MobaBattleGeneralMsg` · `ACC_MobaBattleGeneralMsgEx` · `ACC_MobaBattleOneSidedMsg` · `ACC_MobaBattleTwoSidedMsg` · `ACC_MobaLocalData` · `ACC_MobaShowFinal`
- `ACC_MyRecord` · `ACC_PleasantGoatFinal` · `ACC_PleasantGoatTeamInfo` · `ACC_PleasantGoatWinOrDefect` · `ACC_Praise` · `ACC_TreasureFinal` · `ACC_TreasureHuntFinal` · `ACC_TreasureHuntInfo`
- `ACC_WinOrDefect` · `Aim` · `AssassinationTaskScroll` · `BalanceShip` · `BattleFieldHSLHNotice` · `BattleFieldMap` · `BattleFieldObjective` · `BattleIntegral`
- `BattleMapPay` · `BattlePass` · `BattleTipPanel` · `BZBossList` · `ComboWinEffect` · `DaoZongInjuryRecord` · `DynamicBattleRoyale` · `EndOfBattle`
- `FightingNum` · `FightingStatistic` · `FightingWarning` · `GoldTeamTotalLootList` · `LootList` · `MapQueue` · `NewBattleFieldQueue` · `PVPShowFinal`
- `PVPShowHarm` · `PVPShowPanel` · `PVPShowSetting` · `PVPShowSFX` · `QCSword` · `SingleFStatistic` · `SniperPanel` · `SprintPower`
- `YaoZCultivateBar`

**arena/JJC mode (excluded) — 23:**

- `ArenaBonusPool` · `ArenaCHList` · `ArenaCorpsPanel` · `ArenaCorpsTipl` · `ArenaGuessPool` · `ArenaInheritLevels` · `ArenaLivePanel` · `ArenaOpponent`
- `ArenaQueue` · `ArenaVotingPanel` · `BadaoPosture` · `ChangGePosture` · `CJPosture` · `JJCEquipmentDIY` · `JJCQiXuePanel` · `JJCRougeChallengePop`
- `JJCRougeChooseCards` · `JJCRougeInsideEnhanced` · `JJCRougeInsideShop` · `JJCRougeObtainCards` · `JJCRougePanel` · `PKLeavePanel` · `QiXiuPosture`

**other modes / minigames (excluded) — 77:**

- `AsuraAssemble` · `AsuraBattle` · `AsuraPanel` · `AsuraSettlement` · `BlueSeaPanel` · `BreatheBar` · `CampBigThings` · `CampMaps`
- `CGSelectPanel` · `CityBelong` · `CrossingChoosePanel` · `DynamicCarrierBar` · `DynamicRougeActionBar` · `DynamicRougeActionBarSetting` · `FindBugGame` · `LuckyMeeting`
- `LuckyMeetingDialogue` · `LuckyMeetingGet` · `LuckyMeetingInfo` · `LuckyMeetingTrace` · `MiniGameDescription` · `MiniGameGuide` · `MiniGameJigsaw` · `MiniGamePoetry`
- `MiniGameResult` · `MiniGameSelectLevel` · `MiniGameStart` · `MiniGameStatusBar` · `MobaControlPanel` · `MobaEnemyPanel` · `MobaInformationPanel` · `MobaPVPInput`
- `MobaPVPList` · `MobaShop` · `MobaShowPanel` · `MonopolyAuctionInfoPanel` · `MonopolyBg` · `MonopolyCardCast` · `MonopolyCardList` · `MonopolyCardShop`
- `MonopolyCardUseConfirm` · `MonopolyChangeBuffRequest` · `MonopolyDaLeTouPanel` · `MonopolyDice` · `MonopolyFateEvent` · `MonopolyGameNotice` · `MonopolyGameOverFinal` · `MonopolyGamePanel`
- `MonopolyGodNotify` · `MonopolyInfo` · `MonopolyLandExchangeRequest` · `MonopolyLandPurchaseDlg` · `MonopolyLogList` · `MonopolyMenuBar` · `MonopolyMoneyNotify` · `MonopolyNotify`
- `MonopolyOutShow` · `MonopolyPlayerList` · `MonopolyQueue` · `MonopolyReadyArea` · `MonopolyRemoveObstacles` · `MonopolyRoundCountdown` · `MonopolySelectDirection` · `MonopolyShowFinal`
- `MonopolyStep` · `MonopolyTargetSelectPanel` · `MonopolyTip` · `MonopolyWarning` · `OTActionBar` · `RougeLikeDataPanel` · `RougeLikeFinal` · `RougeLikeIntro`
- `RougeLikeKillCount` · `RougeLikeQueue` · `RougeLikeTimeEvent` · `SnsPanel` · `WishingTemplePanel`

**housing (excluded) — 20:**

- `HomelandAddFriends` · `HomelandAddOthers` · `HomelandArchBuyConfirm` · `HomelandCoinBuyConfirm` · `HomelandEasyBuy` · `HomelandEasyBuySearch` · `HomelandEventHandler` · `HomelandGetHouse`
- `HomelandGroupBuy` · `HomelandInvitation` · `HomelandLocker` · `HomelandOverview` · `HomelandOverviewMenu` · `HomelandOverviewTips` · `HomelandPVP` · `HomelandSeasonDistance`
- `HomelandSpecialBuyConfirm` · `HomelandStorageArea` · `HomelandTeamSure` · `HomelandUnlockArea`

**debug/gm (excluded) — 3:**

- `Debug` · `GMCheck` · `TestGuild`

## Script-only entries (no same-path INI) — 155

These manifest scripts have no INI at `ui/Config/Default/<rel>.ini`; they are helpers/data/logic files (e.g. `CharInfoData`, `QuestData`) or windows whose INI lives elsewhere (probe variants when a session needs one).

- `AccountFriend` · `ActivityList_BattlePass` · `ActivityProgress` · `AddonDownload` · `Asura_Base` · `AutoSelectDiamond` · `BankLock` · `BlackMarket`
- `CampOBBase` · `ChargeGiftMonthly` · `ChatPanelTab` · `ChatPanel_Base` · `ChatPanel_Center` · `ChatPanel_Data` · `ChatPanel_Main` · `ChatPanel_Other`
- `ChatPanel_SpecialMode` · `CoinShopAct` · `CoinShop_Main` · `CoinShop_PendantPetPos` · `CoinShop_Rewards` · `CoinShop_Shop` · `CommandBase` · `Comment`
- `CountDownPanel` · `CounterOperatAct` · `CouresPanel` · `CueWords` · `Cyclopaedia_Home` · `Desert_Base` · `DynamicActionBar_Base` · `DynamicOriginalActionBar`
- `EYaShaMinimapMark` · `EYaShaMinimapUI` · `EYaShaQuestList` · `EYaShaWatchList` · `EquipInquireCommon` · `ExteriorLottery` · `FaceLift` · `FameAndPunishEvil`
- `Filter_Base` · `FriendsRecruit` · `FullLevelHelpPanel` · `GMMessagePanel` · `GuildDiplomacy` · `GuildFightQueue` · `GuildTree` · `HLBOp_Amount`
- `HLBOp_Blueprint` · `HLBOp_Bottom` · `HLBOp_Brush` · `HLBOp_Camera` · `HLBOp_Check` · `HLBOp_CustomBrush` · `HLBOp_Enter` · `HLBOp_Exit`
- `HLBOp_Group` · `HLBOp_MultiItemOp` · `HLBOp_Other` · `HLBOp_Place` · `HLBOp_Rotate` · `HLBOp_Save` · `HLBOp_Select` · `HLBOp_SingleItemOp`
- `HLBOp_Step` · `HLBView_Blueprint_Local` · `HLBView_Blueprint_Web` · `HLBView_ExportTip` · `HLBView_FurnitureList` · `HLBView_ImportTip` · `HLBView_Operations` · `HorseExterior_Equip`
- `HorseExterior_Horse` · `IdleActionSet` · `InstrumentData` · `InstrumentPreset` · `LeagueNote` · `LeyouJi` · `LoginCustomRoleView` · `LoginPreview`
- `MOBA_BattleMsgManager` · `MainBar_MacroPattern` · `MainBar_ModeSwitch` · `MainBar_SkillSetting` · `ManualDropList` · `MapCircle` · `MessageBox` · `MiddleMapCommand`
- `MiniGame` · `MiniGame_Base` · `Monopoly_GridHighlight` · `MonsterBook_Base` · `MonsterBook_Skill` · `MovieManager` · `NewFirstCharge` · `NormalShop`
- `OnePhoto` · `OperatPreOrderMsg` · `OperationActivity` · `OperationAdventureDisplay` · `OperationChargeGift` · `OperationFlowerBP` · `OperationRewardPreview` · `OperationShop`
- `OperationSignIn` · `OrangeWeapon` · `PakV4Common` · `PakV4LimitSpeed` · `PakV4SimpleInfo` · `PartnerView` · `PendantPanelNew` · `PopupMenuPanel`
- `QuestionnairePanel` · `RemotePanel` · `ReputationBase` · `RoadChivalrousBase` · `RoleChangeNew` · `SeasonGongZhan` · `SeasonPrize` · `SeasonReturnGift`
- `SeasonUpdateOverview` · `SellBag_Blueprint` · `SellBag_Exterior` · `ShareStation_Confirm` · `ShareStation_WorkDetails` · `ShopPanel_Main` · `Similar2Double11Day` · `Similar2Double11Lottery`
- `Similar2FirstCharge` · `Simple5Host` · `SimpleOperation` · `SimpleReward` · `SimpleTip` · `SimpleWebPage` · `SimulationClientData` · `SimulationEnv`
- `Target` · `TaskList` · `TestGuildBase` · `TestGuildOverview` · `TestGuildProgress` · `UINotifyMessage` · `UIPlugin` · `UISetting_ActionBar`
- `UISetting_Addon` · `UISetting_BuffList` · `UISetting_Combat` · `UISetting_Comprehensive` · `UISetting_Display` · `UISetting_Efficiency` · `UISetting_HeadTop` · `UISetting_Interface_Switch`
- `UISetting_Operation_Switch` · `UISetting_Special_Sect` · `UISetting_StatusBar`

## Bulk bring-in (2026-10-03, user request: "way more UI")

**991 authored-state entries** now live in the viewer's 基础界面 stage: the classified groups
(HUD + character/bag/skill + social/team/guild + mail/auction/bank + menus/settings +
world/quest/interaction = 204 entries incl. the hand-built trio) plus the full tier-5
"other panels/dialogs" set (787). Each entry renders the INI's authored state (`PARTIAL`,
backdrop `#33393E`, summary with section count + root geometry + group; no Lua replay, no GT
capture yet). Catalog total: 1,008 windows (13 BR + 991 basic + 4 spare); `--selftest`
1008/0/0.

Two authored-state edge cases fixed during the pass: `AccelerateBall` and `HLBOp_Main` author
`LockShowAndHide=1` roots (runtime-shown / invisible anchor host) — their entries carry
`show` lists so the authored state renders.

## Next sessions

1. HUD core deep research (`BASIC_UI_HUD.md`): MainBarPanel/ActionBar, Player, Target family, BuffList/DebuffList/TargetBuff/DeBuff, ExpLine, CompassPanel, QuestTraceList, ChatPanel — then viewer entries under the new 基础界面 stage.
2. Panels (character/bag/skill → social/team → mail/auction/bank), menus/settings, world/interaction — one group per session, doc + entries + EXPERIENCES.
3. Native-driven elements (cast bar `ProgressBar`, generic progress bars, nameplates/damage numbers) get INI-art layout only, flagged; engine-host behavior is a later phase.
4. Per-window refinement pass over the bulk entries: runtime state replay (texts/tabs/lists/show-hide) where the authored state is empty or misleading, guided by GT captures when available.
