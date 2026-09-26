local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7
function L0_0()
	if not Queue.IsOpened() then
		Queue.Open()
	end
end
L1_1 = {}
L1_1.m_bLoadMainUI = false
function L2_2()
	local L0_11
	L0_11 = "config.ini"
	local L1_12 = L1_12
	L1_12 = L1_12(L0_11, true)
	if not L1_12 then
		return
	end
	local L2_13 = L2_13
	local L5_16 = L5_16
	local L2_13, L6_17 = L2_13(L5_16, "Debug", "ShowLuaErrMsg", 0), L6_17
	if L2_13 and L2_13 ~= 0 then
		L5_16 = true
		_g_ShowLuaErrMsg = L5_16
	end
	L6_17 = L1_12
	L5_16 = L1_12.Close
	L5_16(L6_17)
end
L1_1.CheckConfig = L2_2
function L2_2(A0_18, A1_19)
	LockBgMusic(false)
	PlayBgMusic(Login_GetBGMusic())
	LoadingPanel.CloseSound()
	RegisterEvent("LOGIN_QUEUE_STATE", _ENV)
	Wnd.OpenWindow("LoadingPanel"):Hide()
	Wnd.OpenWindow("LoginWaitServerList"):Hide()
	Wnd.OpenWindow("LoginServerList"):Hide()
	Wnd.OpenWindow("LoginServerPanel"):Hide()
	Wnd.OpenWindow("LoginWaiting"):Hide()
	Wnd.OpenWindow("LoginSwordLogo"):Hide()
	Wnd.OpenWindow("LoginMessage"):Hide()
	Wnd.OpenWindow("SecurityCard"):Hide()
	Wnd.OpenWindow("LoginRename"):Hide()
	Wnd.OpenWindow("LoginTokenPanel"):Hide()
	PakV4Info.OpenWindow()
	LoginScene.Enter()
	if A0_18 then
		Login.Enter("rolelist")
		if TwoDimLogin.IsLogined() then
			if TwoDimensionalLogin.RqstLoginToken() then
				goto lbl_117
			end
			Login.EndWait()
			Login.Enter("password")
			local L4_22 = L4_22
			break -- pseudo-goto
		end
		L4_22 = Login
		L4_22 = L4_22.RequestRelogin
		L4_22()
		break -- pseudo-goto
	end
	L4_22 = LOAD_LOGIN_REASON
	L4_22 = L4_22.START_GAME_LOGIN
	if A1_19 ~= L4_22 then
		L4_22 = Login
		L4_22 = L4_22.Enter
		L4_22("password")
	end
	::lbl_117::
	repeat
	until true
	L4_22 = LoadingPanel
	L4_22 = L4_22.OpenSound
	L4_22()
end
L1_1.LoadLoginUI = L2_2
function L2_2()
	FireEvent("CLOSE_LOGIN_FRAME")
	StopBgMusic()
	UnRegisterEvent("LOGIN_QUEUE_STATE", _ENV)
	local L2_24 = L2_24
	L2_24 = IsModuleLoaded
	L2_24 = L2_24("Queue")
	if L2_24 then
		L2_24 = Queue
		L2_24 = L2_24.Close
		L2_24()
	end
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginServerList")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginServerPanel")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginWaitServerList")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginWaiting")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginSwordLogo")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginRoleName")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginHomeplace")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginMessage")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("SecurityCard")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginRename")
	L2_24 = Wnd
	L2_24 = L2_24.CloseWindow
	L2_24("LoginTokenPanel")
end
L1_1.CloseAllLoginWindow = L2_2
function L2_2()
	UIShell.CloseAllLoginWindow()
end
L1_1.UnloadLoginUI = L2_2
function L2_2()
	if UIShell.m_bLoadMainUI then
		AdjustFrameListPosition()
		CorrectAutoPosFrameAfterClientResize()
	end
end
L1_1.ResizeUI = L2_2
function L2_2()
	local L0_25, L1_26
	L0_25 = UIShell
	L0_25 = L0_25.m_bLoadMainUI
	return L0_25
end
L1_1.IsAreadyInGame = L2_2
function L2_2(A0_27)
	local L1_28
	function L1_28(A0_37)
		local L1_38, L2_39
		L1_38 = A0_37
		while L1_38 do
			L2_39 = L1_38
			L1_38 = L1_38:GetNext()
			if _ENV then
				local L3_40 = L2_39:IsAddOn()
				if not L3_40 then
					goto lbl_20
				end
			end
			L3_40 = Wnd
			L3_40 = L3_40.CloseWindow
			local L4_41, L5_42 = L2_39:GetName()
			L3_40(L4_41, L5_42)
			::lbl_20::
		end
	end
	L4_31 = pairs_c
	L5_32 = UILayer
	L4_31, L5_32, _FOR_ = L4_31(L5_32)
	for _FORV_5_ in L4_31, L5_32, _FOR_ do
		if Station.Lookup(_FORV_5_):GetFirstChild() then
			local L8_35 = L8_35
			L1_28((Station.Lookup(_FORV_5_):GetFirstChild()))
			local L9_36 = L9_36
		end
	end
end
L1_1.CloseAllWindow = L2_2
function L2_2()
	if USE_COCOS then
		ApplyUIScale(1)
		return
	end
	local L1_43 = L1_43
	L1_43(1, true)
	local L2_44 = L2_44
end
L1_1.InitUIScale = L2_2
UIShell = L1_1
function L1_1()
	local L0_45, L1_46 = GetLoadLoginReason(), L1_46
	L1_46 = false
	UIShell.InitUIScale()
	if L0_45 == LOAD_LOGIN_REASON.START_GAME_LOGIN then
		UIShell.CheckConfig()
	elseif L0_45 == LOAD_LOGIN_REASON.RETURN_GAME_LOGIN then
		if g_tLoginData then
			g_tLoginData.szAccount = Login_GetAccount()
			Login.m_szAccount = g_tLoginData.szAccount
		end
		UIShell.CheckConfig()
		ResetGameworld()
	elseif L0_45 == LOAD_LOGIN_REASON.RETURN_ROLE_LIST then
		if g_tLoginData then
			g_tLoginData.szAccount = Login_GetAccount()
			Login.m_szAccount = g_tLoginData.szAccount
		end
		if GetVersion() == "zhkr" then
			local Login.m_szAccount, L6_51 = KOR_GetAccountName(), L6_51
		end
		UIShell.CheckConfig()
		ResetGameworld()
		repeat
			L1_46 = true
			do break end -- pseudo-goto
			L6_51 = LOAD_LOGIN_REASON
			L6_51 = L6_51.RETURN_CLEAR_UI
			if L0_45 == L6_51 then
				L6_51 = ResetGameworld
				L6_51()
				return
			else
				L6_51 = LOAD_LOGIN_REASON
				L6_51 = L6_51.KICK_OUT_BY_GM
				if L0_45 == L6_51 then
					L6_51 = g_tLoginData
					if L6_51 then
						L6_51 = g_tLoginData
						L6_51.szAccount = Login_GetAccount()
						L6_51 = Login
						L6_51.m_szAccount = g_tLoginData.szAccount
					end
				else
					L6_51 = LOAD_LOGIN_REASON
					L6_51 = L6_51.KICK_OUT_BY_OTHERS
					if L0_45 == L6_51 then
						L6_51 = g_tLoginData
						if L6_51 then
							L6_51 = g_tLoginData
							L6_51.szAccount = Login_GetAccount()
							L6_51 = Login
							L6_51.m_szAccount = g_tLoginData.szAccount
						end
						L6_51 = UIShell
						L6_51 = L6_51.CheckConfig
						L6_51()
						L6_51 = ResetGameworld
						L6_51()
					else
						L6_51 = LOAD_LOGIN_REASON
						L6_51 = L6_51.AUTO_EXIT
						if L0_45 == L6_51 then
							L6_51 = g_tLoginData
							if L6_51 then
								L6_51 = g_tLoginData
								L6_51.szAccount = Login_GetAccount()
								L6_51 = Login
								L6_51.m_szAccount = g_tLoginData.szAccount
							end
							L6_51 = UIShell
							L6_51 = L6_51.CheckConfig
							L6_51()
							L6_51 = ResetGameworld
							L6_51()
						else
							L6_51 = LOAD_LOGIN_REASON
							L6_51 = L6_51.KICK_OUT_FOR_UNDERAGE_LIMIT
							if L0_45 == L6_51 then
								L6_51 = g_tLoginData
								if L6_51 then
									L6_51 = g_tLoginData
									L6_51.szAccount = Login_GetAccount()
									L6_51 = Login
									L6_51.m_szAccount = g_tLoginData.szAccount
								end
								L6_51 = UIShell
								L6_51 = L6_51.CheckConfig
								L6_51()
								L6_51 = ResetGameworld
								L6_51()
							else
								L6_51 = LOAD_LOGIN_REASON
								L6_51 = L6_51.RESET_UI
								if L0_45 == L6_51 then
									L6_51 = UIShell
									L6_51 = L6_51.CheckConfig
									L6_51()
								end
							end
						end
					end
				end
			end
		until true
	end
	L6_51 = UIShell
	L6_51 = L6_51.LoadLoginUI
	L6_51(L1_46, L0_45)
	L6_51 = LOAD_LOGIN_REASON
	L6_51 = L6_51.KICK_OUT_BY_GM
	if L0_45 ~= L6_51 then
		L6_51 = LOAD_LOGIN_REASON
		L6_51 = L6_51.KICK_OUT_BY_OTHERS
		if L0_45 ~= L6_51 then
			L6_51 = LOAD_LOGIN_REASON
			L6_51 = L6_51.KICK_OUT_FOR_UNDERAGE_LIMIT
			if L0_45 ~= L6_51 then
				goto lbl_190
			end
		end
	end
	L6_51 = ShowKickOutMessage
	L6_51(L0_45)
	L6_51 = SM_IsEnable
	L6_51 = L6_51()
	if L6_51 then
		L6_51 = SM_PostEvent
		L6_51(STREAMING_POST_EVENT_TYPE.GAME_USER_EXIT, 1)
		local L4_49 = L4_49
		goto lbl_196
		::lbl_190::
		L6_51 = LOAD_LOGIN_REASON
		L6_51 = L6_51.AUTO_EXIT
		if L0_45 == L6_51 then
			L6_51 = ShowAutoExitMessage
			L6_51()
		end
	end
	::lbl_196::
end
LoadLoginUI = L1_1
function L1_1(A0_52)
	local L1_53, L2_54, L3_55, L4_56
	L1_53 = false
	L2_54 = nil
	L3_55 = LOAD_LOGIN_REASON
	L3_55 = L3_55.KICK_OUT_BY_GM
	if A0_52 == L3_55 then
		L3_55 = g_tGlue
		L3_55 = L3_55.tLoginString
		L2_54 = L3_55.BE_KICK_ACCOUNT_GM
		L1_53 = true
	else
		L3_55 = LOAD_LOGIN_REASON
		L3_55 = L3_55.KICK_OUT_BY_OTHERS
		if A0_52 == L3_55 then
			L3_55 = g_tGlue
			L3_55 = L3_55.tLoginString
			L2_54 = L3_55.BE_KICK_ACCOUNT
			L1_53 = true
		end
	end
	if L2_54 then
		L3_55 = nil
		L4_56 = nil
		local L5_57 = L5_57
		L5_57 = L5_57(Station.Lookup("Normal/LoginPassword"), "WndPassword")
		if L5_57 then
			local L6_58 = L5_57:GetRelPos()
			L4_56 = L5_57:GetRelPos()
			L3_55 = L6_58
		end
		L6_58 = {}
		L6_58.bModal = true
		L6_58.szMessage = L2_54
		L6_58.szName = "BeKickAccount"
		;({}).szOption = g_tStrings.STR_HOTKEY_SURE
		local L6_58[1], L7_59 = {}, L7_59
		function L7_59()
			OpenExplorer(tUrl.CustomerServiceWeb)
			local L1_64 = L1_64
		end
		if L1_53 then
			local ({}).szOption, L11_63 = g_tStrings.STR_GO_TO, L11_63
			;({}).fnAction = L7_59
			L11_63(L6_58, {})
		end
		L11_63 = MessageBox
		L11_63(L6_58)
		local L9_61 = L9_61
	end
end
ShowKickOutMessage = L1_1
function L1_1()
	local L1_66, L2_67 = Station.GetClientSize, L2_67
	L1_66, L2_67 = L1_66()
	;({}).x = L1_66 / 2
	;({}).y = L2_67 / 2
	;({}).szMessage = g_tStrings.MSG_BE_AUTO_EXIT
	;({}).szName = "AutoExitMessage"
	;({}).szOption = g_tStrings.STR_HOTKEY_SURE
	;({})[1] = {}
	local L3_68 = L3_68
	MessageBox({})
	local L4_69 = L4_69
end
ShowAutoExitMessage = L1_1
function L1_1()
	UIShell.UnloadLoginUI()
end
UnloadLoginUI = L1_1
function L1_1()
	UIShell.ResizeUI()
end
ResizeUI = L1_1
function L1_1()
	local L0_70
	L0_70 = {}
	local L25_95 = {}
	L25_95.name = "GlobalEventHandler"
	L25_95.hide = true
	;({}).name = "HomelandEventHandler"
	;({}).hide = true
	local L2_72 = L2_72
	;({}).name = "Hand"
	;({}).action = function()
		Hand_Init()
	end
	;({}).hide = true
	local L3_73 = L3_73
	;({}).name = "Scene"
	;({}).s = "CENTER"
	;({}).x = 0
	;({}).y = 0
	;({}).anchor, ({}).r = {}, "CENTER"
	;({}).hide = true
	local L4_74 = L4_74
	;({}).name = "Announce"
	local L5_75 = L5_75
	;({}).name = "EnterAreaTip"
	local L6_76 = L6_76
	;({}).name = "MainBarPanel"
	local L7_77 = L7_77
	;({}).name = "TargetBuff"
	;({}).hide = true
	local L8_78 = L8_78
	;({}).name = "TargetDeBuff"
	;({}).hide = true
	local L9_79 = L9_79
	;({}).name = "EditBox"
	local L10_80 = L10_80
	;({}).name = "Matrix"
	;({}).hide = true
	local L11_81 = L11_81
	;({}).name = "BreatheBar"
	;({}).hide = true
	local L12_82 = L12_82
	;({}).name = "CampPanel"
	local L13_83 = L13_83
	;({}).name = "SceneCampTip"
	local L14_84 = L14_84
	;({}).name = "GMAnnouncePanel"
	local L15_85 = L15_85
	;({}).name = "BattleTipPanel"
	local L16_86 = L16_86
	;({}).name = "ExpLine"
	local L17_87 = L17_87
	;({}).name = "TopMenu"
	local L18_88 = L18_88
	;({}).name = "TopMenuOther"
	;({}).action = function()
		TopMenuOther.Open()
	end
	local L19_89 = L19_89
	;({}).name = "SystemMenu_Right"
	;({}).bPCOnly = true
	local L20_90 = L20_90
	;({}).name = "SystemMenu_Left"
	;({}).bPCOnly = true
	local L21_91 = L21_91
	;({}).name = "TraceButton"
	;({}).bPCOnly = true
	local L22_92 = L22_92
	;({}).name = "MainBar_BottomLeft"
	;({}).action = function()
		MainBar_BottomLeft.Open()
	end
	;({}).bSMOnly = true
	local L23_93 = L23_93
	;({}).name = "MainBar_PageSign"
	;({}).action = function()
		MainBar_PageSign.Open()
	end
	;({}).bSMOnly = true
	L0_70[1] = L25_95
	L0_70[2] = L2_72
	L0_70[3] = L3_73
	L0_70[4] = L4_74
	L0_70[5] = L5_75
	L0_70[6] = L6_76
	L0_70[7] = L7_77
	L0_70[8] = L8_78
	L0_70[9] = L9_79
	L0_70[10] = L10_80
	L0_70[11] = L11_81
	L0_70[12] = L12_82
	L0_70[13] = L13_83
	L0_70[14] = L14_84
	L0_70[15] = L15_85
	L0_70[16] = L16_86
	L0_70[17] = L17_87
	L0_70[18] = L18_88
	L0_70[19] = L19_89
	L0_70[20] = L20_90
	L0_70[21] = L21_91
	L0_70[22] = L22_92
	L0_70[23] = L23_93
	local L0_70[24], L24_94 = {}, L24_94
	return L0_70
end
GetFrameList = L1_1
function L1_1()
	local L0_96 = GetFrameList()
	L3_99 = ipairs
	L4_100 = L0_96
	L3_99, L4_100, L5_101 = L3_99(L4_100)
	for _FORV_4_, _FORV_5_ in L3_99, L4_100, L5_101 do
		if _FORV_5_.anchor then
			if Station.SearchFrame(_FORV_5_.name) then
				local L8_104, L9_105 = L8_104, L9_105
				local L10_106 = L10_106
				local L11_107 = L11_107
				local L12_108 = L12_108
				local L13_109 = L13_109
				local L14_110 = L14_110
				L10_106(L11_107, L12_108, L13_109, L14_110, L9_105.r, 0, 0)
				local L15_111 = L15_111
			end
		end
	end
end
AdjustFrameListPosition = L1_1
function L1_1(A0_112)
	local L1_113 = IsMobileStreamingEnable()
	if A0_112.bPCOnly and L1_113 then
		return
	end
	if A0_112.bSMOnly and not L1_113 then
		return
	end
	if A0_112.action then
		return A0_112.action()
	end
	if A0_112.callOpenFunc then
		if A0_112.hide and _G[A0_112.name].Open() then
			_G[A0_112.name].Open():SetVisible(false)
		end
		return
	end
	local L2_114 = L2_114
	L2_114 = L2_114(A0_112.name)
	if L2_114 then
		local L3_115 = L3_115
		L3_115(L2_114, not A0_112.hide)
		L3_115 = A0_112.anchor
		if L3_115 then
			L3_115 = A0_112.anchor
			local L4_116, L5_117 = L4_116, L5_117
			L4_116(L5_117, L3_115.s, L3_115.x, L3_115.y, L3_115.r, 0, 0)
		end
	end
end
function L2_2()
	local L0_118 = GetFrameList()
	L3_121 = ipairs
	L4_122 = L0_118
	L3_121, L4_122, L5_123 = L3_121(L4_122)
	for _FORV_4_, _FORV_5_ in L3_121, L4_122, L5_123 do
		Wnd.CloseWindow(_FORV_5_.name)
	end
end
UnloadFrameList = L2_2
L2_2 = nil
L3_3 = 0
L4_4 = 0
L5_5 = nil
L6_6 = 0
function L7_7(A0_126)
	local L2_128, L3_129 = L2_128, L3_129
	if not _ENV then
		return
	end
	L2_128 = GetTime
	L2_128 = L2_128()
	if A0_126 then
		L3_129 = L6_6
		L3_129 = L2_128 - L3_129
		if L3_129 < 100 then
			return
		end
	else
		L6_6 = L2_128
	end
	L3_129 = L3_3
	if L2_128 < L3_129 then
		return
	end
	L3_129 = L4_4
	L3_129 = L2_128 + L3_129
	L3_3 = L3_129
	L3_129 = _ENV
	L3_129 = #L3_129
	local L4_130 = L4_130
	local L5_131 = L5_131
	L5_131 = L5_131(L5_5, L4_130)
	if not L5_131 then
		Log(L5_131(L5_5, L4_130))
	end
	table.remove(_ENV, L3_129)
	if L3_129 - 1 == 0 then
		_ENV = nil
		RenderCall("OnUIShellLoading", false)
		local L8_134 = L8_134
		FireUIEvent("ON_UI_SHELL_LOAD_END")
		local L7_133 = L7_133
	end
end
OnUIShellLoading = L7_7
function L7_7()
	local L0_135
	L0_135 = {}
	;({}).name = "DurabilityPanel"
	;({}).callOpenFunc = true
	local L1_136 = L1_136
	;({}).name = "SprintPower"
	;({}).callOpenFunc = true
	local L2_137 = L2_137
	;({}).name = "BuffMonitor"
	;({}).callOpenFunc = true
	local L3_138 = L3_138
	;({}).action = function()
		if not StorageServer.GetData("CloseQuestTrace") then
			OpenQuestTraceList(true)
		else
			CloseQuestTraceList(true)
			local L1_146 = L1_146
		end
	end
	local L4_139 = L4_139
	;({}).name = "FightingNum"
	;({}).hide = true
	local L5_140 = L5_140
	;({}).name = "OTGCDBar"
	;({}).hide = true
	local L6_141 = L6_141
	;({}).name = "OTPetActionBar"
	;({}).hide = true
	local L7_142 = L7_142
	;({}).name = "OTActionBar"
	;({}).hide = true
	local L8_143 = L8_143
	;({}).name = "Player"
	local L9_144 = L9_144
	;({}).name = "Minimap"
	L0_135[1] = L1_136
	L0_135[2] = L2_137
	L0_135[3] = L3_138
	L0_135[4] = L4_139
	L0_135[5] = L5_140
	L0_135[6] = L6_141
	L0_135[7] = L7_142
	L0_135[8] = L8_143
	L0_135[9] = L9_144
	local L0_135[10], L10_145 = {}, L10_145
	return L0_135
end
GetDelayOpenFrames = L7_7
function L7_7()
	local L1_147 = L1_147
	L1_147("ON_ENTER_CUSTOM_UI_MODE", _ENV)
	local L2_148 = L2_148
	L1_147 = Playerbar
	L1_147 = L1_147.Open
	L1_147()
	L1_147 = BuffMonitor
	L1_147 = L1_147.Open
	L1_147()
	L1_147 = DurabilityPanel
	L1_147 = L1_147.Open
	L1_147()
	L1_147 = SprintPower
	L1_147 = L1_147.Open
	L1_147()
end
local L8_8 = L8_8
local L9_9 = L9_9
L8_8(L9_9, L7_7)
local L10_10 = L10_10
function L8_8(A0_149, A1_150, A2_151)
	_ENV = GetTime() + A1_150
	if A0_149 == "FramesOpenInLoading" then
		L2_2 = GetFrameList()
	else
		local L3_152 = GetDelayOpenFrames()
		L2_2 = L3_152
	end
	L3_152 = L2_2
	L3_152 = #L3_152
	if 0 < L3_152 then
		L6_6 = 0
		L5_5 = L1_1
		L4_4 = math.floor(A2_151 / #L2_2)
		L4_4 = math.min(L4_4, 200)
		local L4_153 = L4_153
		L4_153("OnUIShellLoading", OnUIShellLoading)
		L4_153 = Log
		local L5_154 = L5_154
		local L6_155 = L6_155
		local L7_156 = L7_156
		local L8_157 = L8_157
		local L5_154, L6_155, L7_156, L8_157, L9_158 = L5_154(L6_155, L7_156, L8_157, L3_152)
		L4_153(L5_154, L6_155, L7_156, L8_157, L9_158)
	end
end
LoadFrameList = L8_8
function L8_8()
	local L0_159, L1_160
	L0_159 = _ENV
	if L0_159 then
		L0_159 = _ENV
		L0_159 = #L0_159
		return L0_159
	end
	L0_159 = 0
	return L0_159
end
GetFrameLoadingQueueCount = L8_8
L8_8 = GetFrameLoadingQueueCount
GetModuleLoadingQueueCount = L8_8
function L8_8(A0_161, A1_162, A2_163)
	_ENV = GetTime() + A1_162
	local L3_164 = L3_164
	L3_164 = L3_164(A0_161)
	L4_165 = {}
	L2_2 = L4_165
	L4_165 = #L3_164
	_FOR_ = -1
	for _FORV_7_ = L4_165, _FOR_, _FOR_ do
		table.insert(L2_2, L3_164[_FORV_7_])
	end
	L4_165 = L2_2
	L4_165 = #L4_165
	if 0 < L4_165 then
		L10_171 = 0
		L6_6 = L10_171
		L10_171 = LoadModule
		L5_5 = L10_171
		L10_171 = math
		L10_171 = L10_171.floor
		L10_171 = L10_171(A2_163 / #L2_2)
		L4_4 = L10_171
		L10_171 = math
		L10_171 = L10_171.min
		L10_171 = L10_171(L4_4, 200)
		L4_4 = L10_171
		L10_171 = RenderCall
		L10_171("OnUIShellLoading", OnUIShellLoading)
		L10_171 = Log
		local L6_167 = L6_167
		local L7_168 = L7_168
		local L8_169 = L8_169
		local L6_167, L7_168, L8_169, L9_170 = L6_167(L7_168, L8_169, L4_165)
		L10_171(L6_167, L7_168, L8_169, L9_170, L6_167(L7_168, L8_169, L4_165))
	end
end
LoadModulesSmooth = L8_8
