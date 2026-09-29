local L0_0, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15 = L0_0, "UIModule", L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15
L4_4 = ExportExternalLib
L0_0(L3_3, L4_4)
L0_0 = {}
function L3_3()
	local L1_24 = L1_24
	local L2_25 = L2_25
	local L3_26 = L3_26
	local L4_27 = L4_27
	L1_24(L2_25, L3_26, L4_27, arg3, arg4)
	local L5_28 = L5_28
end
function L4_4()
	local L1_29 = L1_29
	local L2_30 = L2_30
	local L3_31 = L3_31
	local L4_32 = L4_32
	L1_29(L2_30, L3_31, L4_32, 0, arg3)
	local L5_33 = L5_33
end
function L5_5(A0_34, A1_35)
	if not A0_34 then
		return ""
	end
	local L2_36 = L2_36
	local L2_36, L3_37 = L2_36(A0_34), L3_37
	if L2_36 then
		L3_37 = L2_36.nMaxCopy
		if L3_37 then
			goto lbl_14
		end
	end
	L3_37 = 0
	::lbl_14::
	local L4_38 = L4_38
	L4_38 = L4_38(A0_34)
	if not L4_38 then
		L4_38 = ""
	end
	local L5_39 = L5_39
	L5_39 = L5_39(A0_34)
	if 0 < L3_37 or L5_39 then
		local L6_40 = L6_40
		local L7_41 = L7_41
		local L8_42 = L8_42
		local L4_38, L9_43 = L6_40 .. L7_41 .. L8_42 .. "]", L9_43
	end
	return L4_38
end
function L6_6()
	if not MapQueue.bShowSureNotice then
		local L0_44 = L0_44
		L0_44(arg0, arg1)
		return
	end
	L0_44 = 30
	local L1_45 = GetTickCount()
	L1_45 = L1_45 + L0_44 * 1000
	local L2_46 = L2_46
	local L3_47 = L3_47
	L2_46 = L2_46(L3_47, arg1)
	L3_47 = {}
	L3_47.szMessage = FormatString(g_tStrings.STR_SWITCHMAP_GFZ_TIP, L2_46)
	if not arg0 then
	end
	L3_47.szName = "entermap" .. ""
	function L3_47.fnAutoClose()
		local L0_51
		L0_51 = _ENV
		local L1_52 = GetTickCount()
		if L0_51 <= L1_52 then
			L0_51 = true
			return L0_51
		end
	end
	L3_47.args, ({})[1] = {}, arg0
	L3_47.args, ({})[2] = {}, arg1
	;({}).szOption = g_tStrings.STR_HOTKEY_SURE
	;({}).fnAction = ComfirmEnterQueuedMap
	;({}).nCountDownTime = L0_44
	local ({}).szOption, L6_50 = g_tStrings.STR_HOTKEY_CANCEL, L6_50
	L3_47[1] = L6_50
	L3_47[2] = {}
	L6_50 = MessageBox
	L6_50(L3_47)
	local L5_49 = L5_49
end
function L7_7()
	if not SwitchServerQueue.GetShowSureNotice() then
		local L0_53 = GetPVPFieldClient()
		if not L0_53 then
			return
		end
		local L2_55 = L2_55
		local L3_56 = L3_56
		L2_55(L3_56, arg3, true)
		return
	end
	L0_53 = 30
	L2_55 = GetTickCount
	L2_55 = L2_55()
	L3_56 = L0_53 * 1000
	L2_55 = L2_55 + L3_56
	L3_56 = arg2
	local L4_57 = L4_57
	if not Table_GetMapName(L3_56) then
	end
	local L5_58 = L5_58
	local L6_59 = L6_59
	local L7_60 = L7_60
	L7_60 = L7_60(L3_56, L4_57)
	local L11_64 = L11_64
	local L12_65 = L12_65
	L12_65 = L12_65(g_tStrings.STR_SWITCHMAP_GFZ_TIP, L7_60 .. "-" .. L5_58)
	L11_64.szMessage = L12_65
	L12_65 = "enterservermap"
	if not L3_56 then
	end
	L12_65 = L12_65 .. ""
	L11_64.szName = L12_65
	function L12_65()
		local L0_66
		L0_66 = _ENV
		local L1_67 = GetTickCount()
		if L0_66 <= L1_67 then
			L0_66 = true
			return L0_66
		end
	end
	L11_64.fnAutoClose = L12_65
	L12_65 = {}
	L12_65.szOption = g_tStrings.STR_HOTKEY_SURE
	function L12_65.fnAction()
		local L1_68 = L1_68
		local L2_69 = L2_69
		local L3_70 = L3_70
		L2_69(L3_70, L4_57, true)
		local L4_71 = L4_71
	end
	L12_65.nCountDownTime = L0_53
	;({}).szOption = g_tStrings.STR_HOTKEY_CANCEL
	;({}).fnAction = function()
		local L1_72 = L1_72
		local L2_73 = L2_73
		local L3_74 = L3_74
		L2_73(L3_74, L4_57, false)
		local L4_75 = L4_75
	end
	L11_64[1] = L12_65
	L11_64[2] = {}
	L12_65 = MessageBox
	L12_65(L11_64)
end
function L8_8()
	repeat
		if arg0 == PVP_FIELD_QUEUE_INFO_CODE.QUEUEING then
			SwitchServerQueue.Open(arg1, arg2, arg3)
			local L3_78 = L3_78
			break -- pseudo-goto
		end
		L3_78 = arg0
		if L3_78 == PVP_FIELD_QUEUE_INFO_CODE.JOIN_PVPFIELD_QUEUE then
			L3_78 = _ENV
			L3_78()
		else
			L3_78 = arg0
			if L3_78 == PVP_FIELD_QUEUE_INFO_CODE.LEAVE_PVPFIELD_QUEUE then
				L3_78 = SwitchServerQueue
				L3_78 = L3_78.QueueEnd
				L3_78(arg2, arg3)
				local L2_77 = L2_77
			end
		end
	until true
end
function L9_9()
	CampMaps.ClearData()
	EndOfBattle.Open(arg0)
	local L1_79 = L1_79
end
function L10_10()
	local L1_80 = L1_80
	L1_80(arg0, arg1)
	local L2_81 = L2_81
end
function L11_11()
	local L1_82 = L1_82
	local L2_83 = L2_83
	L1_82(L2_83, arg1, arg2)
	local L3_84 = L3_84
end
function L12_12()
	local L1_85 = L1_85
	if arg0 == UI_GetClientPlayerID() then
		L1_85 = UpGradeEffect
		L1_85 = L1_85.Open
		L1_85()
	end
end
function L13_13()
	TeamBuilding.OnTeamPushMessageNotify(arg0)
	local L1_86 = L1_86
end
function L14_14()
	local L1_87 = L1_87
	L1_87(arg0, true)
	local L2_88 = L2_88
end
function L15_15()
	local L1_89 = L1_89
	local L2_90 = L2_90
	local L3_91 = L3_91
	L1_89(L2_90, L3_91, arg2, arg3)
	local L4_92 = L4_92
end
RegisterEvent("FIRST_LOADING_END", function()
	UnRegisterEvent("FIRST_LOADING_END", _ENV)
	RemoteCallToServer("On_QiYu_GetCurrentTaskID")
	if IsLaptopEdition() then
		BugReport.Open()
	end
	local L0_93 = L0_93
	L0_93 = L0_93("Hotkey_NewMode")
	local L1_94 = L1_94
	L1_94(L0_93)
	local L2_95 = L2_95
end)
RegisterEvent("ON_START_MAP_QUEUING", L3_3)
RegisterEvent("ON_MAP_QUEUE_POS_UPDATE", L4_4)
RegisterEvent("ON_CAN_ENTER_MAP_NOTIFY", L6_6)
RegisterEvent("ON_CASTLE_END_ACTIVITY", L9_9)
RegisterEvent("ON_CASTLE_CHANGE_OWNER", L10_10)
RegisterEvent("ON_FELLOW_PET_DATEUPDATAE", L11_11)
RegisterEvent("PLAYER_LEVEL_UP", L12_12)
RegisterEvent("ON_TEAM_PUSH_MESSAGE_NOTIFY", L13_13)
RegisterEvent("ON_ROOM_PUSH_MESSAGE_NOTIFY", L14_14)
RegisterEvent("EXCHANGE_ITEM", L15_15)
RegisterEvent("OPEN_SWITCH_MAP_WINDOW", function()
	local L1_96 = L1_96
	local L2_97 = L2_97
	L1_96(L2_97, arg1, arg2)
	local L3_98 = L3_98
end)
RegisterEvent("ON_PVP_FIELD_QUEUE_INFO_NOTIFY", L8_8)
function _Exit()
	UnRegisterEvent("ON_START_MAP_QUEUING", _ENV)
	UnRegisterEvent("ON_MAP_QUEUE_POS_UPDATE", L2_2)
	UnRegisterEvent("ON_CAN_ENTER_MAP_NOTIFY", L6_6)
	UnRegisterEvent("ON_CASTLE_END_ACTIVITY", L9_9)
	UnRegisterEvent("ON_CASTLE_CHANGE_OWNER", L10_10)
	UnRegisterEvent("ON_FELLOW_PET_DATEUPDATAE", L11_11)
	UnRegisterEvent("PLAYER_LEVEL_UP", L12_12)
	UnRegisterEvent("ON_TEAM_PUSH_MESSAGE_NOTIFY", L13_13)
	UnRegisterEvent("ON_ROOM_PUSH_MESSAGE_NOTIFY", L14_14)
	UnRegisterEvent("EXCHANGE_ITEM", L15_15)
	UnRegisterEvent("OPEN_SWITCH_MAP_WINDOW", _UPVALUE10_)
	local L1_99 = L1_99
	L1_99("ON_PVP_FIELD_QUEUE_INFO_NOTIFY", L8_8)
	local L2_100 = L2_100
end
RegisterCustomData("ArenaOpponent.Anchor")
RegisterCustomData("Dismantle.nGroupNum")
RegisterCustomData("Dismantle.nGroups")
RegisterCustomData("Dismantle.bShowWndSetting")
RegisterCustomData("Dismantle.bSelectReverse")
RegisterCustomData("Dismantle.bSelectForword")
RegisterCustomData("FBTimeRank.bMinimize")
RegisterCustomData("FBTimeRank.Anchor")
local L16_16 = L16_16
RegisterEvent("ON_FORCE_SYNC_PLAYER_MAP_PROGRESS", function()
	local L1_101 = L1_101
	local L2_102 = L2_102
	local L3_103 = L3_103
	L1_101(L2_102, L3_103, arg2, arg3)
	local L4_104 = L4_104
end)
local L17_17 = L17_17
RegisterEvent("ON_ADVENTURE_DATA_CHANGED", function()
	if arg2 == 1 then
		return
	end
	if IsMobileStreamingEnable() then
		if IsModuleLoaded("MainBar_BottomCenter") then
			MainBar_BottomCenter.IsCloseTrace(arg0)
		end
	else
		if arg1 then
			LuckyMeetingTrace.UpdateInfo(arg0)
		end
		if IsModuleLoaded("LuckyMeetingTrace") then
			LuckyMeetingTrace.IsCloseTrace(arg0)
			local L1_105 = L1_105
		end
	end
end)
local L18_18 = L18_18
local L19_19 = L19_19
RegisterEvent("QUEST_ACCEPTED", function()
	if not IsMobileStreamingEnable() and arg1 then
		local L0_106 = L0_106
		L0_106 = L0_106(arg1)
		if L0_106 and L0_106.IsAdventure == 1 then
			local L1_107 = L1_107
			L1_107(arg1)
			local L2_108 = L2_108
		end
	end
end)
RegisterEvent("QUEST_FINISHED", function()
	if IsMobileStreamingEnable() then
		if arg0 and IsModuleLoaded("MainBar_BottomCenter") then
			MainBar_BottomCenter.IsCloseTrace(arg0)
		end
	elseif arg0 and IsModuleLoaded("LuckyMeetingTrace") then
		LuckyMeetingTrace.IsCloseTrace(arg0)
		local L1_109 = L1_109
	end
end)
RegisterEvent("SET_QUEST_STATE", function()
	if arg0 and arg1 == 1 then
		_ENV()
	end
end)
if IsDebugClient() then
	local L20_20 = L20_20
	if IsRecordingEnabled() then
		local L21_21 = L21_21
		local L22_22 = L22_22
		RegisterEvent("BEGIN_LOAD_FAME_LIST", function()
			local L0_110 = L0_110
			L0_110 = L0_110(recording, "login_data")
			if L0_110 and L0_110.name then
				local L1_111 = L1_111
				local L2_112 = L2_112
				local L3_113 = L3_113
				L1_111(L2_112, L3_113, L0_110.wnds)
				local L4_114 = L4_114
			end
		end)
		local L23_23 = L23_23
	end
end
