local L0_0
L0_0 = {}
function GlobalMgr_Register(A0_6, A1_7, A2_8)
	if not _ENV[A0_6] then
		_ENV[A0_6] = {}
	end
	local L3_9 = L3_9
	local L4_10 = L4_10
	local L3_9, L5_11 = L3_9(L4_10, A2_8), L5_11
	L4_10 = _ENV
	L4_10 = L4_10[A0_6]
	L4_10[A1_7] = L3_9
end
function GlobalMgr_UnRegister(A0_12, A1_13)
	L5_17 = _ENV[A0_12]
	if not L5_17 then
		return
	end
	if not A1_13 then
		L5_17 = pairs
		L6_18 = _ENV
		L6_18 = L6_18[A0_12]
		L5_17, L6_18, _FOR_ = L5_17(L6_18)
		for _FORV_5_, _FORV_6_ in L5_17, L6_18, _FOR_ do
			UnRegisterEvent(_FORV_5_, _FORV_6_)
		end
		L5_17 = _ENV
		L5_17[A0_12] = nil
		return
	end
	L5_17 = _ENV
	L5_17 = L5_17[A0_12]
	L5_17 = L5_17[A1_13]
	if L5_17 then
		L5_17 = UnRegisterEvent
		L6_18 = A1_13
		L7_19 = _ENV
		L7_19 = L7_19[A0_12]
		L7_19 = L7_19[A1_13]
		L5_17(L6_18, L7_19)
		L5_17 = _ENV
		L5_17 = L5_17[A0_12]
		L5_17[A1_13] = nil
	end
end
function OpenCharacterPanel(A0_22, A1_23)
	local L3_24 = L3_24
	L3_24(A0_22, A1_23)
	local L4_25 = L4_25
end
function CloseCharacterPanel(A0_26)
	if IsModuleLoaded("CharacterPanel") then
		local L2_27 = L2_27
		L2_27(A0_26, true)
		local L3_28 = L3_28
	end
end
function IsCharacterPanelOpened()
	local L1_30 = IsModuleLoaded("CharacterPanel")
	if not L1_30 then
		L1_30 = false
		return L1_30
	end
	L1_30 = CharacterPanel
	L1_30 = L1_30.IsOpened
	return L1_30()
end
function CharacterPanel_OnHandPickObj()
	if CharacterPanel then
		CharacterPanel.HighlightHandItemEquipPos()
	end
end
function CharacterPanel_OnHandDropObj()
	if CharacterPanel then
		CharacterPanel.UnHighlightHandItemEquipPos()
	end
end
function CharacterPanel_GetItemBox(A0_31, A1_32, A2_33)
	local L3_34
	L3_34 = INVENTORY_INDEX
	L3_34 = L3_34.EQUIP
	if A0_31 == L3_34 then
		L3_34 = Station
		L3_34 = L3_34.Lookup
		L3_34 = L3_34(GetCharacterPanelPath())
		if L3_34 and (L3_34:IsVisible() or A2_33) then
			local L4_35 = L4_35
			local L5_36 = L5_36
			local L4_35, L6_37 = L4_35(L5_36, A1_32), L6_37
			return L4_35
		end
	end
	L3_34 = nil
	return L3_34
end
function GetCharacterPanelPath()
	local L1_38
	L1_38 = "Normal/CharacterPanel"
	return L1_38
end
function OnSuitChangeHotkey(A0_39)
	if A0_39 < 1 or A0_39 > CharacterPanel.m_nEquipSuitCount then
		return
	end
	CharacterPanel.m_nSelectSuitIndex = A0_39
	local L1_40 = L1_40
	L1_40(A0_39)
	local L2_41 = L2_41
end
function CharacterPanel_IsCharacterOpen()
	if not IsCharacterPanelOpened() then
		return false
	end
	local L0_42 = L0_42
	L0_42 = L0_42("Normal/CharacterPanel")
	local L1_43 = L1_43
	L1_43 = L1_43(L0_42:Lookup("PageSet_Main"), "Page_Battle")
	local L4_46 = L0_42:Lookup("PageSet_Main")
	L4_46 = L4_46.GetActivePage
	local L4_46, L3_45 = L4_46(L4_46), L3_45
	if L1_43 == L4_46 then
		L4_46 = true
		return L4_46
	end
	L4_46 = false
	return L4_46
end
function CharacterPanel_IsShowWeaponBag()
	local L0_47, L1_48
	L0_47 = CharacterPanel
	L0_47 = L0_47.m_bShowWeaponBag
	return L0_47
end
function CharacterPanel_SetPendantType(A0_49)
	local L1_50
	L1_50 = CharacterPanel
	L1_50.szPendantType = A0_49
end
function CharacterPanel_SetClothingPendantType(A0_51)
	local L1_52
	L1_52 = CharacterPanel
	L1_52.szClothingPendantType = A0_51
end
function CharacterPanel_OpenZhenyingPage()
	if not IsCharacterPanelOpened() then
		OpenCharacterPanel(nil, "CAMP")
	else
		local L0_53 = L0_53
		L0_53 = L0_53("Normal/CharacterPanel")
		L0_53:Show()
		L0_53:Lookup("PageSet_Main"):ActivePage("Page_Camp")
		local L1_54, L2_55 = L1_54, L2_55
		L1_54(L2_55, "Page_Zhenying")
		local L3_56 = L3_56
	end
end
RegisterCustomData("CharacterPanel.m_bShowWeaponBag")
RegisterCustomData("CharacterPanel.nReputationSelForceID")
RegisterCustomData("CharacterPanel.tReputationCollapse")
RegisterCustomData("SprintPower.m_nSprintTeachSuccessCount")
RegisterCustomData("SprintPower.m_nMoveStateTeachCount")
function CloseDialoguePanel(A0_57, A1_58)
	if DialoguePanel.IsOpened() then
		DialoguePanel.Close(A0_57)
	end
	if PlotDialoguePanel.IsOpened() then
		if A1_58 then
			PlotDialoguePanel.Close(A0_57)
		else
			PlotDialoguePanel.DelayClose(A0_57)
			local L3_59 = L3_59
		end
	end
end
function IsDialoguePanelOpened()
	return DialoguePanel.IsOpened()
end
local L1_1 = L1_1
L1_1("DialoguePanel.tGlobalRanking")
function L1_1()
	DialoguePanel.OnMentorStoneRank()
end
local L2_2 = L2_2
L2_2("ON_MENTORSTONE_GET_RANKING", L1_1)
function L2_2()
	repeat
		local L0_60 = L0_60
		L0_60 = L0_60(arg3)
		if arg4 then
			if arg2 == TARGET.NPC and L0_60 and (L0_60.dwTemplateID == 494 or L0_60.dwTemplateID == 495 or L0_60.dwTemplateID == 496 or L0_60.dwTemplateID == 5926) then
				OpenBookExchangePanel(arg1, arg2, arg3)
			else
				PlotDialoguePanel.Open(arg0, arg1, arg2, arg3, arg5)
				local L6_66 = L6_66
				do break end -- pseudo-goto
				L6_66 = arg2
				if L6_66 == TARGET.NPC and L0_60 then
					L6_66 = L0_60.dwTemplateID
					if L6_66 ~= 494 then
						L6_66 = L0_60.dwTemplateID
						if L6_66 ~= 495 then
							L6_66 = L0_60.dwTemplateID
							if L6_66 ~= 496 then
								L6_66 = L0_60.dwTemplateID
								if L6_66 ~= 5926 then
									goto lbl_66
								end
							end
						end
					end
					L6_66 = OpenBookExchangePanel
					L6_66(arg1, arg2, arg3)
				::lbl_66::
				else
					L6_66 = DialoguePanel
					L6_66 = L6_66.Open
					local L2_62 = L2_62
					local L3_63 = L3_63
					local L4_64 = L4_64
					L6_66(L2_62, L3_63, L4_64, arg3)
					local L5_65 = L5_65
				end
			end
		end
	until true
end
RegisterEvent("OPEN_WINDOW", L2_2)
local L5_5 = L5_5
L5_5 = RegisterCustomData
L5_5("ProgressBar.Anchor")
L5_5 = RegisterCustomData
L5_5("ProgressBar.Verion")
L5_5 = RegisterEvent
L5_5("CUSTOM_UI_MODE_SET_DEFAULT", function()
	ProgressBar.ApplyDefaultAnchor()
end)
L5_5 = RegisterEvent
L5_5("ON_ENTER_CUSTOM_UI_MODE", function()
	ProgressBar.OnCustomUIMode("enter")
	local L1_67 = L1_67
end)
L5_5 = RegisterEvent
L5_5("ON_LEAVE_CUSTOM_UI_MODE", function()
	ProgressBar.OnCustomUIMode("leave")
	local L1_68 = L1_68
end)
function L5_5(A0_69, A1_70)
	local L3_71 = L3_71
	L3_71(A0_69, A1_70)
	local L4_72 = L4_72
end
ShowFullScreenSFX = L5_5
function L5_5()
	FullScreenSFX.Hide()
end
HideFullScreenSFX = L5_5
function L5_5(A0_73, A1_74)
	local L3_75 = L3_75
	L3_75(A0_73, A1_74)
	local L4_76 = L4_76
end
OpenInternetExplorer = L5_5
function L5_5(A0_77, A1_78, A2_79)
	if not IsLocalFileExist("caption.ini") or not Ini.Open("caption.ini") then
		local L3_80 = L3_80
		L3_80 = L3_80("data/public/caption.ini")
	end
	if not L3_80 then
		return
	end
	if A0_77 == "d" then
		L3_80:WriteInteger("FontConfig", A1_78, A2_79)
	elseif A0_77 == "f" then
		L3_80:WriteFloat("FontConfig", A1_78, A2_79)
	elseif A0_77 == "s" then
		local L7_84 = L7_84
		L7_84(L3_80, "FontConfig", A1_78, A2_79)
		local L8_85 = L8_85
	end
	L8_85 = L3_80
	L7_84 = L3_80.Save
	L7_84(L8_85, "caption.ini")
	L7_84 = k3dcmd
	L8_85 = "reload_caption"
	L7_84(L8_85)
end
Global_SetCaptionParam = L5_5
function L5_5(A0_86)
	local L6_92, L5_91 = "caption.ini", L5_91
	L5_91 = L5_91(L6_92)
	if L5_91 then
		L5_91 = Ini
		L5_91 = L5_91.Open
		L6_92 = "caption.ini"
		L5_91 = L5_91(L6_92)
		if L5_91 then
			goto lbl_16
		end
	end
	L5_91 = Ini
	L5_91 = L5_91.Open
	L6_92 = "data/public/caption.ini"
	L5_91 = L5_91(L6_92)
	::lbl_16::
	if not L5_91 then
		return
	end
	L6_92 = ipairs
	L6_92, _FOR_, _FOR_ = L6_92(A0_86)
	for _FORV_5_, _FORV_6_ in L6_92, _FOR_, _FOR_ do
		if _FORV_6_.vtype == "d" then
			L5_91:WriteInteger("FontConfig", _FORV_6_.key, _FORV_6_.value)
		elseif _FORV_6_.vtype == "f" then
			L5_91:WriteFloat("FontConfig", _FORV_6_.key, _FORV_6_.value)
		elseif _FORV_6_.vtype == "s" then
			L5_91:WriteString("FontConfig", _FORV_6_.key, _FORV_6_.value)
			local L11_97 = L11_97
		end
	end
	L7_93 = L5_91
	L6_92 = L5_91.Save
	L8_94 = "caption.ini"
	L6_92(L7_93, L8_94)
	L6_92 = k3dcmd
	L7_93 = "reload_caption"
	L6_92(L7_93)
end
Global_SetCaptionParams = L5_5
function L5_5()
	if not IsLocalFileExist("caption.ini") or not Ini.Open("caption.ini") then
		local L0_98 = L0_98
		local L0_98, L1_99 = L0_98("data/public/caption.ini"), L1_99
	end
	if not L0_98 then
		return
	end
	L1_99 = {}
	L1_99.szFontFile = L0_98:ReadString("FontConfig", "FontFile", "")
	local L4_102 = L4_102
	local L5_103 = L5_103
	local L4_102, L6_104 = L4_102(L5_103, "FontConfig", "FontZoomInScale", 1.0E-4), L6_104
	L1_99.fFontZoomInScale = L4_102
	L5_103 = L0_98
	L4_102 = L0_98.Close
	L4_102(L5_103)
	return L1_99
end
Global_GetCaptionFontConfig = L5_5
L5_5 = Station
L5_5 = L5_5.SetUIScale
Station_SetUIScale = L5_5
L5_5 = Station
L5_5 = L5_5.GetUIScale
Station_GetUIScale = L5_5
L5_5 = {}
g_tTraceButtonData = L5_5
L5_5 = g_tTraceButtonData
L5_5.bChangeTargetFace = false
L5_5 = RegisterCustomData
L5_5("g_tTraceButtonData.bChangeTargetFace")
function L5_5()
	repeat
		local L2_107, L8_113 = IsVersionExp(), L8_113
		if not L2_107 then
			return
		end
		L2_107 = clone
		L8_113 = tUrl
		L2_107 = L2_107(L8_113)
		L8_113 = nil
		;({}).ACCOUNT_FREEZE_PLAYER_WEB = 44
		;({}).ACCOUNT_FREEZE_PLAYER_FARMER = 45
		;({}).LOGIN, ({}).REQUEST_LOGIN_GAME_NEED_BIND_PHONE = {}, 66
		local L5_110 = L5_110
		L6_111("ui/string/url.lua", {}, false, false)
		L8_113 = L5_110.tUrl
		if L8_113 then
			L6_111 = pairs
			L7_112 = L8_113
			L6_111, L7_112, _FOR_ = L6_111(L7_112)
			for _FORV_6_, _FORV_7_ in L6_111, L7_112, _FOR_ do
				if not tUrl[_FORV_6_] then
					tUrl[_FORV_6_] = _FORV_7_
				end
			end
			break -- pseudo-goto
		end
		tUrl = L2_107
	until true
end
L5_5()
local L4_4 = L4_4
