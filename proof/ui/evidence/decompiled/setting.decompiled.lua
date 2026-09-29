local L0_0, L1_1
L0_0 = "1.0"
local L1_1, L3_3, L4_4, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17 = "0.2", L3_3, L4_4, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17
L3_3 = GetVersion
L3_3, L4_4, L8_8 = L3_3()
L9_9 = Table_GetSoundSetting
L10_10 = L8_8
L9_9 = L9_9(L10_10)
g_tDefaultSoundSetting = L9_9
L9_9 = g_tDefaultSoundSetting
L9_9.szVersion = L0_0
L9_9 = clone
L10_10 = g_tDefaultSoundSetting
L9_9 = L9_9(L10_10)
g_SoundSetting = L9_9
L9_9 = {}
L9_9.nRSize = 1
L10_10 = MOVIE
L10_10 = L10_10.FILTER_LINEAR
L9_9.nFilter = L10_10
L10_10 = MOVIE
L10_10 = L10_10.QUALITY_CINEMATIC1
L9_9.nQuality = L10_10
L10_10 = MOVIE
L10_10 = L10_10.MPEG4
L9_9.nCode = L10_10
L10_10 = MOVIE
L10_10 = L10_10.FPS_25
L9_9.nFps = L10_10
L9_9.bRecordWhenStart = false
g_MovieRecordSetting = L9_9
L9_9 = {}
L9_9.NPC = 0
L9_9.PLAYER = 1
L9_9.OTHER_PLAYER = 2
L9_9.CLOAK = 3
L10_10 = SM_IsEnable
L10_10 = L10_10()
if L10_10 then
	L10_10 = RegisterCustomData
	L11_11 = "Account\\g_SoundSetting"
	L10_10(L11_11)
else
	L10_10 = RegisterCustomData
	L11_11 = "Global\\g_SoundSetting"
	L10_10(L11_11)
end
L10_10 = RegisterCustomData
L11_11 = "Global\\g_MovieRecordSetting"
L10_10(L11_11)
L10_10 = ""
L11_11 = nil
L12_12 = PlayBgMusic
L13_13 = nil
function L14_14(A0_22, A1_23, A2_24, A3_25, A4_26)
	if not A3_25 then
		_ENV = A0_22
	end
	L12_12(A0_22, A1_23)
	local L7_29 = L7_29
	if not A4_26 then
		L7_29 = SetPlayBGMAsyncFlag
		L7_29(false)
		local L6_28 = L6_28
	end
end
PlayBgMusic = L14_14
function L14_14(A0_30)
	local L1_31
	_ENV = A0_30
end
SetPlayBGMAsyncFlag = L14_14
function L14_14()
	local L1_32
	L1_32 = _ENV
	return L1_32
end
GetPlayBGMAsyncFlag = L14_14
L14_14 = nil
L15_15 = false
function L16_16(A0_33)
	if _ENV and IsPlaying2DSound(_ENV) and L15_15 then
		Stop2DSound(_ENV)
		_ENV = nil
		L15_15 = false
	end
	AddToHelpSoundQueue(A0_33)
	local L2_34 = L2_34
end
PlayHelpSound = L16_16
function L16_16(A0_35)
	if _ENV and IsPlaying2DSound(_ENV) then
		Stop2DSound(_ENV)
		_ENV = nil
		L15_15 = false
	end
	local L1_36 = L1_36
	L1_36 = L1_36 .. A0_35 .. ".wav"
	local L2_37 = L2_37
	L2_37 = L2_37(L1_36)
	if L2_37 and IsFileExist(L2_37) then
		L1_36 = L2_37
	end
	local L3_38 = L3_38
	local L4_39 = L4_39
	local L5_40 = L5_40
	local L6_41 = L6_41
	local L7_42 = L7_42
	local L3_38, L8_43 = L3_38(L4_39, L5_40, L6_41, L7_42, false), L8_43
	if L3_38 then
		_ENV = L3_38
		L4_39 = false
		L15_15 = L4_39
	end
	return L3_38
end
PlayTheHelpSound = L16_16
function L16_16()
	local L1_44
	L1_44 = _ENV
	return L1_44
end
GetLastBgSound = L16_16
function L16_16(A0_45)
	if _ENV then
		local L1_46 = L1_46
		L1_46 = L1_46(_ENV)
		if L1_46 then
			return
		end
	end
	L1_46 = ""
	if g_SoundSetting.bFemale then
		L1_46 = "ui\\sound\\female\\" .. A0_45 .. ".wav"
	else
		L1_46 = "ui\\sound\\male\\" .. A0_45 .. ".wav"
	end
	local L2_47 = L2_47
	L2_47 = L2_47(L1_46)
	if L2_47 and IsFileExist(L2_47) then
		L1_46 = L2_47
	end
	local L3_48 = L3_48
	local L4_49 = L4_49
	local L5_50 = L5_50
	local L6_51 = L6_51
	local L7_52 = L7_52
	local L3_48, L8_53 = L3_48(L4_49, L5_50, L6_51, L7_52, false), L8_53
	if L3_48 then
		_ENV = L3_48
		L4_49 = true
		L15_15 = L4_49
	end
end
PlayTipSound = L16_16
function L16_16()
	local L1_54
	L1_54 = g_tDefaultSoundSetting
	return L1_54
end
GetDefaultSoundSetting = L16_16
function L16_16()
	local L1_55
	L1_55 = g_SoundSetting
	return L1_55
end
GetSoundSetting = L16_16
L16_16 = nil
function L17_17()
	local L1_56 = L1_56
	if not L16_16 then
	end
	L1_56(_ENV.OTHER_PLAYER, g_SoundSetting.fOtherPlayerVolume)
	local L2_57 = L2_57
end
function SetSoundSetting(A0_58, A1_59)
	L6_64 = A0_58
	L5_63, L6_64, _FOR_ = L5_63(L6_64)
	for _FORV_5_, _FORV_6_ in L5_63, L6_64, _FOR_ do
		g_SoundSetting[_FORV_5_] = _FORV_6_
	end
	L5_63 = ApplySoundSetting
	L6_64 = A1_59
	L5_63(L6_64)
end
function ApplySoundSetting(A0_66)
	if not A0_66 then
		EnableAllSound(g_SoundSetting.bEnable)
		EnableSound(SOUND.UI_SOUND, g_SoundSetting.bEnableUISound)
		EnableSound(SOUND.UI_ERROR_SOUND, g_SoundSetting.bEnableErrorSound)
		EnableSound(SOUND.SCENE_SOUND, g_SoundSetting.bEnableSceneSound)
		EnableSound(SOUND.CHARACTER_SOUND, g_SoundSetting.bEnableCharacterSound)
		EnableSound(SOUND.BG_MUSIC, g_SoundSetting.bEnableBgMusic)
		EnableSound(SOUND.FRESHER_TIP, g_SoundSetting.bEnableHelpSound)
		EnableSound(SOUND.SYSTEM_TIP, g_SoundSetting.bEnableTipSound)
		EnableSound(SOUND.CHARACTER_SPEAK, g_SoundSetting.bEnableCharacterSpeak)
		Enable3DSound(true)
		SetBgMusicLoop(g_SoundSetting.bBgMusicLoop)
		EnableSoundWhenLoseFocus(g_SoundSetting.bEnableLoseFocusPlay)
	end
	SetTotalVolume(g_SoundSetting.fTotalVolume)
	SetVolume(SOUND.UI_SOUND, g_SoundSetting.fUIVolume)
	SetVolume(SOUND.UI_ERROR_SOUND, g_SoundSetting.fErrorVolume)
	SetVolume(SOUND.SCENE_SOUND, g_SoundSetting.fSceneVolume)
	SetVolume(SOUND.CHARACTER_SOUND, g_SoundSetting.fChVolume)
	SetVolume(SOUND.BG_MUSIC, g_SoundSetting.fBgVolume)
	SetVolume(SOUND.FRESHER_TIP, g_SoundSetting.fHelpVolume)
	SetVolume(SOUND.SYSTEM_TIP, g_SoundSetting.fTipVolume)
	SetVolume(SOUND.CHARACTER_SPEAK, g_SoundSetting.fSpeakVolume)
	SetVolume(SOUND.WARNING_SOUND, g_SoundSetting.fWarningSound)
	SetActorTypeVolume(_ENV.PLAYER, g_SoundSetting.fActorPlayerVolume)
	L17_17()
	SetActorTypeVolume(_ENV.NPC, g_SoundSetting.fActorNpcVolume)
	if g_SoundSetting.bEnableCloakSound then
		SetActorTypeVolume(_ENV.CLOAK, g_SoundSetting.fActorPlayerVolume)
	else
		SetActorTypeVolume(_ENV.CLOAK, 0)
		local L3_69 = L3_69
	end
	L3_69 = type
	L3_69 = L3_69(GVoiceBase_SetMicVolume)
	if "function" == L3_69 then
		L3_69 = GVoiceBase_SetMicVolume
		L3_69(g_SoundSetting.fGVMicVolume)
		L3_69 = GVoiceBase_SetSpeakerVolume
		L3_69(g_SoundSetting.fGVSpeakerVolume)
		L3_69 = GVoiceBase_SetVoiceType
		L3_69(g_SoundSetting.nVoiceType)
	end
	L3_69 = BgMusic_TryPlayLast
	L3_69()
end
function BgMusic_TryPlayLast()
	if g_SoundSetting.bEnable and g_SoundSetting.bEnableBgMusic and _ENV ~= "" then
		PlayBgMusic(_ENV)
		local L1_70 = L1_70
	end
end
function Sound_GuardModify(A0_71, A1_72)
	if not _ENV then
	end
	_ENV = {}
	local L2_73 = GetSoundSetting()
	if not _ENV[A0_71] then
		_ENV[A0_71] = L2_73[A0_71]
		L2_73[A0_71] = A1_72
	end
	local L3_74 = L3_74
	L3_74(L2_73)
	local L4_75 = L4_75
end
function Sound_Restore(A0_76)
	if not _ENV or not _ENV[A0_76] then
		return
	end
	local L1_77 = GetSoundSetting()
	L1_77[A0_76] = _ENV[A0_76]
	_ENV[A0_76] = nil
	local L2_78 = L2_78
	L2_78(L1_77)
	local L3_79 = L3_79
end
function GetMovieRecordSetting()
	local L1_80
	L1_80 = g_MovieRecordSetting
	return L1_80
end
function SetMovieRecordSetting(A0_81)
	local L1_82, L2_83
	L1_82 = g_MovieRecordSetting
	L2_83 = A0_81.nRSize
	L1_82.nRSize = L2_83
	L1_82 = g_MovieRecordSetting
	L2_83 = A0_81.nFilter
	L1_82.nFilter = L2_83
	L1_82 = g_MovieRecordSetting
	L2_83 = A0_81.nQuality
	L1_82.nQuality = L2_83
	L1_82 = g_MovieRecordSetting
	L2_83 = A0_81.nCode
	L1_82.nCode = L2_83
	L1_82 = g_MovieRecordSetting
	L2_83 = A0_81.nFps
	L1_82.nFps = L2_83
end
function GetKillSoundBody()
	if not g_SoundSetting.nKillSoundBody then
		local L0_84 = GetClientPlayer()
		L0_84 = L0_84.nRoleType
	end
	return L0_84
end
function SetKillSoundBody(A0_85)
	local L1_86
	L1_86 = g_SoundSetting
	L1_86.nKillSoundBody = A0_85
end
function SetSoundSettingVoiceType(A0_87)
	g_SoundSetting.nVoiceType = A0_87
	local L1_88 = L1_88
	L1_88(g_SoundSetting)
	local L2_89 = L2_89
end
function GetSoundSettingVoiceType()
	local L0_90 = L0_90
	local L0_90, L1_91 = L0_90(g_SoundSetting.nVoiceType), L1_91
	if "number" == L0_90 then
		L0_90 = g_SoundSetting
		L0_90 = L0_90.nVoiceType
		if not (11 < L0_90) then
			goto lbl_13
		end
	end
	L0_90 = g_SoundSetting
	L0_90.nVoiceType = 0
	::lbl_13::
	L0_90 = g_SoundSetting
	L0_90 = L0_90.nVoiceType
	return L0_90
end
function Sound_SetSience(A0_92)
	if A0_92 then
		SetTotalVolume(0)
	else
		local L1_93 = L1_93
		L1_93(g_SoundSetting.fTotalVolume)
		local L2_94 = L2_94
	end
end
local L18_18 = L18_18
RegisterEvent("CUSTOM_DATA_LOADED", function(A0_95)
	if A0_95 == "CUSTOM_DATA_LOADED" and arg0 == "Global" then
		_ENV()
		if not SM_IsEnable() then
			_ENV()
		end
		_UPVALUE1_()
	elseif SM_IsEnable() and A0_95 == "CUSTOM_DATA_LOADED" and arg0 == "Account" then
		_ENV()
		local L1_96 = L1_96
	end
end)
function Sound_GetOtherPlayerVolumeTemp()
	local L1_97
	L1_97 = _ENV
	return L1_97
end
function Sound_ClearOtherPlayerVolumeTemp()
	local L0_98, L1_99
	_ENV = L0_98
end
local L19_19 = L19_19
local L20_20 = L20_20
RegisterEvent("LOADING_END", function()
	_ENV = nil
	local L0_100 = GetClientPlayer()
	if not L0_100 then
		L17_17()
		return
	end
	local L1_101 = L0_100.GetMapID()
	local L2_102 = L2_102
	local L2_102, L3_103 = L2_102(L1_101)
	if IsInArena() then
		_ENV = g_SoundSetting.fActorPlayerVolume
	else
		local L5_105 = L5_105
		if VideoBase.GetSceneSettingSwitch("bDungeonSceneSetting") and L3_103 == MAP_TYPE.DUNGEON and g_SoundSetting.fOtherPlayerVolume > g_SoundSetting.fActorPlayerVolume * 0.25 then
			L5_105 = g_SoundSetting
			L5_105 = L5_105.fActorPlayerVolume
			L5_105 = L5_105 * 0.25
			_ENV = L5_105
		end
	end
	L5_105 = L17_17
	L5_105()
end)
local L21_21 = L21_21
