local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L19_19, L20_20, L21_21, L22_22, L23_23, L24_24, L25_25
L0_0 = "ui/Config/Default/MessageBox/%s.ini"
L1_1 = "MessageBox/%s"
L2_2 = 85
L3_3 = 0.18
L4_4 = 15
L5_5 = 7
function L6_6()
	local L1_27 = Station.GetFocusWindow
	L1_27 = L1_27()
	if L1_27 then
		local L2_28, L3_29 = L2_28, L1_27:GetType()
		if L3_29 == "WndEdit" then
			L3_29 = true
			return L3_29
		end
	end
	L3_29 = false
	return L3_29
end
function L7_7()
	local L1_31 = Station.GetFocusWindow
	L1_31 = L1_31()
	if L1_31 then
		local L2_32 = L2_32
	end
	if L1_31:GetRoot() then
		local L3_33, L4_34 = L3_33, L1_31:GetRoot():GetName()
		if L4_34 == "PopupMenuPanel" then
			L4_34 = true
			return L4_34
		end
	end
	L4_34 = false
	return L4_34
end
function L8_8(A0_35)
	local L1_36 = L1_36
	L1_36 = L1_36("Topmost2/MB_" .. A0_35)
	if not L1_36 then
		local L2_37 = L2_37
		local L4_39 = "Topmost/MB_" .. A0_35
		L2_37 = L2_37(L4_39)
		L1_36 = L2_37
	end
	return L1_36
end
IsMessageBoxOpened = L8_8
function L8_8()
	if this.fnAutoClose and this.fnAutoClose() then
		local L0_40 = L0_40
		L0_40(this)
		return
	end
	L0_40 = this
	L0_40 = L0_40.hWndOption
	local L1_41, L2_42 = L0_40:GetFirstChild(), L2_42
	L2_42 = 1
	while L1_41 do
		if L1_41.nCountDownTime then
			if math.floor(L1_41.nCountDownTime - (GetTickCount() - L1_41.dwStartTime) / 1000 + 0.5) < 0 then
			end
			local L9_49 = L9_49
			local L10_50 = L10_50
			L1_41:Lookup("", "Text_Option" .. L2_42):SetText(FormatString(g_tStrings.MSG_BRACKET, L1_41.szOption, 0))
			if L1_41.fnDisable then
				L1_41.bDisable = L1_41.fnDisable()
			end
			if L10_50 == 0 then
				if L1_41.bDelayCountDown then
					L9_49:SetText(L1_41.szOption)
				end
				L1_41:Enable(not L1_41.bDisable, true)
				if L1_41.fnCountDownEnd then
					L9_49:SetText(L1_41.szOption)
					L1_41.fnCountDownEnd(L1_41)
				end
			else
				if L1_41.bDelayCountDown then
					goto lbl_103
				end
				L1_41:Enable(not L1_41.bDisable, true)
				do break end -- pseudo-goto
				L9_49 = L1_41.fnDisable
				if L9_49 then
					L9_49 = L1_41.fnDisable
					L9_49 = L9_49()
					L1_41.bDisable = L9_49
					L10_50 = L1_41
					L9_49 = L1_41.Enable
					L9_49(L10_50, not L1_41.bDisable, true)
				end
			end
		end
		::lbl_103::
		repeat
		until true
		L2_42 = L2_42 + 1
		L10_50 = L1_41
		L9_49 = L1_41.GetNext
		L9_49 = L9_49(L10_50)
		L1_41 = L9_49
	end
	L9_49 = this
	L10_50 = L9_49
	L9_49 = L9_49.IsValid
	L9_49 = L9_49(L10_50)
	if not L9_49 then
		return
	end
	L9_49 = this
	L9_49 = L9_49.bMessageCountDown
	if L9_49 then
		L9_49 = this
		L9_49 = L9_49.nUpdateTime
		L10_50 = this
		L10_50 = L10_50.nUpdateInterval
		L9_49 = L9_49 + L10_50
		L10_50 = GetCurrentTime
		L10_50 = L10_50()
		L10_50 = L10_50 * 1000
		L10_50 = L10_50 + GetTime()
		if L9_49 < L10_50 then
			L9_49 = this
			L10_50 = L9_49
			L9_49 = L9_49.UpdateMessage
			L9_49(L10_50)
		end
	end
end
function L9_9()
	if this.bModal then
		UILock.Unlock(this.szLevel)
	end
	if not this.bInitiative and this.fnCancelAction then
		this:fnCancelAction()
	end
	if this.szPrevFocus and not this.bDisablePrevFocus then
		Station.SetFocusWindow(this.szPrevFocus)
	end
	if not this.szCloseSound then
	end
	PlaySound(SOUND.UI_SOUND, g_sound.CloseFrame)
	local L1_51 = L1_51
	local L2_52 = L2_52
	L1_51(L2_52, this.szName, this)
	local L3_53 = L3_53
end
function L10_10(A0_54)
	if A0_54 == "UI_SCALED" then
		this:UpdateAnchor()
	elseif A0_54 == "COINSHOP_MODULE_FRAME_SHOW" then
		local L1_55, L2_56 = L1_55, L2_56
		L1_55(L2_56, arg0)
		local L3_57 = L3_57
	end
end
function L11_11()
	local L0_58
	L0_58 = GetKeyName
	local L1_59 = Station.GetMessageKey()
	L0_58 = L0_58(L1_59, Station.GetMessageKey())
	if L0_58 == "Enter" then
		L1_59 = this
		L1_59 = L1_59.bForbidConfirmByEnter
		if not L1_59 then
			L1_59 = this
			L1_59 = L1_59.hWndOption
			local L2_60 = L2_60
			L2_60 = L2_60(L1_59, "Btn_Option1")
			if L2_60.nCountDownTime and L2_60.nCountDownTime - (GetTickCount() - L2_60.dwStartTime) / 1000 > 0 then
				return
			end
			if L2_60 then
				local L3_61 = L3_61
				local L4_62 = L4_62
				L3_61(L4_62, L2_60.OnLButtonClick)
				local L5_63 = L5_63
				L3_61 = 1
				return L3_61
			end
		end
	end
	L1_59 = 0
	return L1_59
end
function L12_12()
	local L0_64 = this:GetParent()
	if L0_64.bDisable and L0_64.szDisableTip ~= "" then
		local L7_71 = L7_71
		local L8_72 = L8_72
		local L9_73 = L9_73
		local L10_74 = L10_74
		local L11_75 = L11_75
		;({})[1] = L7_71
		;({})[2] = L8_72
		;({})[3] = L9_73
		local ({})[4], L12_76 = L10_74, L12_76
		L11_75(L12_76, 400, {})
	end
	L7_71 = this
	L8_72 = L7_71
	L7_71 = L7_71.GetType
	L7_71 = L7_71(L8_72)
	if L7_71 == "Text" then
		L7_71 = this
		L8_72 = L7_71
		L7_71 = L7_71.IsLink
		L7_71 = L7_71(L8_72)
		if L7_71 then
			L7_71 = this
			L8_72 = L7_71
			L7_71 = L7_71.GetFontScheme
			L7_71 = L7_71(L8_72)
			L8_72 = this
			L8_72.nFont = L7_71
			L8_72 = this
			L9_73 = L8_72
			L8_72 = L8_72.SetFontColor
			L10_74 = 0
			L11_75 = 200
			L12_76 = 72
			L8_72(L9_73, L10_74, L11_75, L12_76)
			L8_72 = this
			L9_73 = L8_72
			L8_72 = L8_72.GetParent
			L8_72 = L8_72(L9_73)
			L10_74 = L8_72
			L9_73 = L8_72.FormatAllItemPos
			L9_73(L10_74)
		end
	end
end
function L13_13()
	HideTip()
	if this:GetType() == "Text" and this:IsLink() and this.nFont then
		this:SetFontScheme(this.nFont)
		this:GetParent():FormatAllItemPos()
		local L1_77, L2_78 = L1_77, L2_78
	end
end
function L14_14()
	local L0_79 = this:GetName()
	local L1_80 = this:GetRoot()
	if L0_79 == "Btn_Close" then
		local L2_81 = L2_81
		L2_81(L1_80.szName)
		local L3_82 = L3_82
	end
end
function L15_15()
	if this:GetName() == "CheckBox_Msg" then
		local L0_83 = this:GetRoot()
		if L0_83 and L0_83.fnCheckBoxCheck then
			L0_83.fnCheckBoxCheck()
			local L1_84 = L1_84
		end
	end
end
function L16_16()
	if this:GetName() == "CheckBox_Msg" then
		local L0_85 = this:GetRoot()
		if L0_85 and L0_85.fnCheckBoxUncheck then
			L0_85.fnCheckBoxUncheck()
			local L1_86 = L1_86
		end
	end
end
function L17_17(A0_87)
	repeat
		if A0_87.bModal then
			A0_87:SetSize(Station.GetClientSize())
			A0_87:SetRelPos(0, 0)
			if A0_87.x and A0_87.y then
				local L10_97 = L10_97
				A0_87.hWndAll:SetRelPos(A0_87.x - A0_87.hWndAll:GetSize() / 2, A0_87.y - A0_87.hWndAll:GetSize() / 2)
				break -- pseudo-goto
			end
			L10_97:SetRelPos((Station.GetClientSize() - A0_87.hWndAll:GetSize()) / 2, (Station.GetClientSize() - A0_87.hWndAll:GetSize()) / 2)
			break -- pseudo-goto
		end
		L10_97 = A0_87.SetPoint
		local L4_91 = L4_91
		local L5_92 = L5_92
		local L6_93 = L6_93
		local L7_94 = L7_94
		L10_97(L4_91, L5_92, L6_93, L7_94, A0_87.Anchor.r, A0_87.Anchor.x, A0_87.Anchor.y)
		local L8_95 = L8_95
		L4_91 = A0_87
		L10_97 = A0_87.CorrectPos
		L10_97(L4_91)
	until true
end
function L18_18(A0_98, A1_99, A2_100)
	if A0_98 == "countdown_s" then
		return tostring(math.max(tonumber(A1_99) - (GetCurrentTime() - tonumber(A2_100)), 0))
	elseif A0_98 == "countdown_ms" then
		local L3_101 = L3_101
		L3_101 = L3_101(tonumber(A1_99) - (GetCurrentTime() - tonumber(A2_100)), 0)
		local L4_102 = L4_102
		local L5_103 = L5_103
		local L4_102, L5_103, L6_104 = L4_102(L5_103, false)
		local L7_105 = L7_105
		local L8_106 = L8_106
		return L7_105(L8_106, L5_103, L6_104)
	end
end
function L19_19(A0_107)
	local L1_108
	L1_108 = A0_107.hhandleMsg
	L1_108:Clear()
	local L2_109 = L2_109
	L2_109 = L2_109(A0_107.szMessage, "{%$([^%s]+)%s*([^%s]*)%s*([^%s]*)}", _ENV)
	L1_108:AppendItemFromString(L2_109)
	local L5_112 = L5_112
	L5_112 = L1_108.FormatAllItemPos
	L5_112(L1_108)
	L5_112 = GetCurrentTime
	L5_112 = L5_112()
	L5_112 = L5_112 * 1000
	local L4_111 = GetTime()
	L5_112 = L5_112 + L4_111
	A0_107.nUpdateTime = L5_112
end
function L20_20(A0_113, A1_114)
	local L2_115
	L2_115 = A1_114.szIconFile
	if L2_115 then
		L2_115 = A1_114.nIconFrame
		if L2_115 then
			L2_115 = A0_113.hImageIcon
			if L2_115 then
				goto lbl_11
			end
		end
	end
	do return end
	::lbl_11::
	L2_115 = A0_113.hIcon
	A0_113.hImageIcon:FromUITex(A1_114.szIconFile, A1_114.nIconFrame)
	local L3_116 = A0_113:GetW()
	local L4_117 = L2_115:GetW()
	L2_115:SetRelX((L3_116 - L4_117) / 2)
	local L7_120 = L7_120
	L7_120 = L2_115.GetParent
	L7_120 = L7_120(L2_115)
	L7_120 = L7_120.FormatAllItemPos
	L7_120(L7_120)
	local L6_119 = L6_119
end
function L21_21(A0_121, A1_122)
	local L2_123
	L2_123 = A1_122.szTitle
	if L2_123 then
		L2_123 = A0_121.hTextTtile
		if L2_123 then
			goto lbl_8
		end
	end
	do return end
	::lbl_8::
	L2_123 = A0_121.hTextTtile
	L2_123:SetText(A1_122.szTitle)
	local L3_124 = A0_121:GetW()
	local L4_125 = L2_123:GetW()
	L2_123:SetRelX((L3_124 - L4_125) / 2)
	local L7_128 = L7_128
	L7_128 = L2_123.GetParent
	L7_128 = L7_128(L2_123)
	L7_128 = L7_128.FormatAllItemPos
	L7_128(L7_128)
	local L6_127 = L6_127
end
function L22_22(A0_129, A1_130)
	local L2_131
	L2_131 = A1_130.tItemList
	if L2_131 then
		L2_131 = A0_129.hItemList
		if L2_131 then
			goto lbl_8
		end
	end
	do return end
	::lbl_8::
	L2_131 = A0_129.hItemList
	L7_136 = L2_131
	L6_135 = L2_131.Clear
	L6_135(L7_136)
	L6_135 = ipairs
	L7_136 = A1_130.tItemList
	L6_135, L7_136, _FOR_ = L6_135(L7_136)
	for _FORV_6_, _FORV_7_ in L6_135, L7_136, _FOR_ do
		if not _FORV_7_.nCount then
		end
		local L11_140 = L11_140
		local L12_141 = L12_141
		local L13_142 = L13_142
		local L14_143 = L14_143
		local L15_144 = L15_144
		L14_143(L15_144, nil, _FORV_7_.dwTabType, _FORV_7_.dwIndex, L13_142)
		local L16_145 = L16_145
	end
	L7_136 = L2_131
	L6_135 = L2_131.FormatAllItemPos
	L6_135(L7_136)
end
function L23_23(A0_146, A1_147)
	L13_159 = A0_146.hWndAll
	local L12_158, L13_159, L18_164, L19_165, L20_166, L21_167, L22_168, L23_169 = A0_146.hWndAll.Lookup, L13_159, L18_164, L19_165, L20_166, L21_167, L22_168, L23_169
	L18_164 = "CheckBox_Msg"
	L12_158 = L12_158(L13_159, L18_164)
	L13_159 = A0_146.bShowCheckBox
	if not L13_159 or A1_147 <= 0 then
		L18_164 = L12_158
		L13_159 = L12_158.Hide
		L13_159(L18_164)
		return
	end
	L13_159 = math
	L13_159 = L13_159.min
	L18_164 = A1_147
	L19_165 = A0_146.nCheckBoxBindOption
	if not L19_165 then
		L19_165 = 1
	end
	L13_159 = L13_159(L18_164, L19_165)
	L18_164 = A0_146.hWndOption
	L19_165 = L18_164
	L18_164 = L18_164.Lookup
	L20_166 = "Btn_Option"
	L21_167 = L13_159
	L20_166 = L20_166 .. L21_167
	L18_164 = L18_164(L19_165, L20_166)
	L20_166 = L12_158
	L19_165 = L12_158.Lookup
	L21_167 = ""
	L22_168 = "Text_Msg"
	L19_165 = L19_165(L20_166, L21_167, L22_168)
	L20_166 = A0_146.szCheckBoxText
	if L20_166 then
		L20_166 = A0_146.szCheckBoxText
		if L20_166 ~= "" then
			L21_167 = L19_165
			L20_166 = L19_165.SetText
			L22_168 = A0_146.szCheckBoxText
			L20_166(L21_167, L22_168)
		end
	end
	L21_167 = L12_158
	L20_166 = L12_158.Show
	L20_166(L21_167)
	L21_167 = L12_158
	L20_166 = L12_158.Check
	L22_168 = A0_146.bCheckBoxDefault
	L23_169 = WNDEVENT_FIRETYPE
	L23_169 = L23_169.PREVENT
	L20_166(L21_167, L22_168, L23_169)
	L20_166 = A0_146.hWndAll
	L21_167 = L20_166
	L20_166 = L20_166.GetAbsPos
	L20_166, L21_167 = L20_166(L21_167)
	L23_169 = L18_164
	L22_168 = L18_164.GetAbsPos
	L22_168, L23_169 = L22_168(L23_169)
	local L10_156, L11_157 = L12_158:GetSize()
	L12_158:SetRelPos(L22_168 - L20_166, L23_169 - L21_167 - L11_157)
	local L14_160, L15_161 = L14_160, L15_161
	local L16_162, L17_163 = L16_162, L17_163
	if L17_163 + A0_146.hhandleMsg:GetSize() + _ENV > L23_169 - L11_157 then
		A0_146.hWndOption:SetRelY(A0_146.hWndOption:GetRelY() + (L17_163 + A0_146.hhandleMsg:GetSize() + _ENV - (L23_169 - L11_157)))
		L12_158:SetRelY(L12_158:GetRelY() + (L17_163 + A0_146.hhandleMsg:GetSize() + _ENV - (L23_169 - L11_157)))
		local L24_170, L25_171 = L24_170, L25_171
		A0_146.hWndCon:SetH(A0_146.hWndCon:GetSize() + (L17_163 + A0_146.hhandleMsg:GetSize() + _ENV - (L23_169 - L11_157) - L5_5))
		local L26_172, L27_173 = L26_172, L27_173
		A0_146.hTotal:SetH(A0_146.hTotal:GetSize() + (L17_163 + A0_146.hhandleMsg:GetSize() + _ENV - (L23_169 - L11_157) - L5_5))
		local L28_174 = L28_174
		local L29_175, L30_176 = L29_175, L30_176
		A0_146.hTotal:Lookup("Image_Bg"):SetH(A0_146.hTotal:Lookup("Image_Bg"):GetSize() + (L17_163 + A0_146.hhandleMsg:GetSize() + _ENV - (L23_169 - L11_157) - L5_5))
		local L31_177 = L31_177
		local L32_178, L33_179 = L32_178, L33_179
		A0_146.hTotal:Lookup("Image_BgPopUp_Glassmorphism"):SetH(A0_146.hTotal:Lookup("Image_BgPopUp_Glassmorphism"):GetSize() + L31_177)
		local L34_180, L35_181 = L34_180, L35_181
		A0_146.hWndAll:SetH(A0_146.hWndAll:GetSize() + L31_177)
		local L38_184 = L38_184
		A0_146:SetSize(A0_146.hWndAll:GetSize())
		local L39_185 = L39_185
		A0_146:UpdateAnchor()
	end
end
function L24_24(A0_186)
	local L1_187
	L1_187 = Station
	L1_187 = L1_187.CloseWindow
	local L2_188, L3_189 = A0_186:GetRoot()
	L1_187(L2_188, L3_189)
end
function L25_25(A0_190, A1_191)
	local L9_199, L10_200, L11_201, L12_202, L13_203, L17_207 = L9_199, L10_200, L11_201, L12_202, L13_203, L17_207
	if not A0_190.szName then
		L9_199 = Log
		L10_200 = "msg name must be set!"
		return L9_199(L10_200)
	end
	L9_199 = Station
	L9_199 = L9_199.CloseWindow
	L10_200 = "MB_"
	L11_201 = A0_190.szName
	L10_200 = L10_200 .. L11_201
	L9_199(L10_200)
	L9_199 = A0_190.szSkin
	if not L9_199 then
		L9_199 = "MessageBox"
	end
	L10_200 = string
	L10_200 = L10_200.format
	L11_201 = _ENV
	L12_202 = L9_199
	L10_200 = L10_200(L11_201, L12_202)
	L9_199 = L10_200
	L10_200 = Station
	L10_200 = L10_200.OpenWindow
	L11_201 = L9_199
	L12_202 = "MB_"
	L13_203 = A0_190.szName
	L12_202 = L12_202 .. L13_203
	L10_200 = L10_200(L11_201, L12_202)
	L11_201 = A0_190.szSkin
	if L11_201 then
		L11_201 = string
		L11_201 = L11_201.format
		L12_202 = L0_0
		L13_203 = A0_190.szSkin
		L11_201 = L11_201(L12_202, L13_203)
		L10_200.szIniFile = L11_201
	end
	L11_201 = A0_190.x
	L10_200.x = L11_201
	L11_201 = A0_190.y
	L10_200.y = L11_201
	L11_201 = A0_190.bModal
	L10_200.bModal = L11_201
	L11_201 = A0_190.szName
	L10_200.szName = L11_201
	L11_201 = A0_190.fnAutoClose
	L10_200.fnAutoClose = L11_201
	L11_201 = A0_190.fnAction
	L10_200.fnAction = L11_201
	L11_201 = A0_190.fnCancelAction
	L10_200.fnCancelAction = L11_201
	L11_201 = A0_190.szCloseSound
	L10_200.szCloseSound = L11_201
	L10_200.nUpdateInterval = -1
	L11_201 = A0_190.args
	L10_200.args = L11_201
	L11_201 = A0_190.szLevel
	if not L11_201 then
		L11_201 = A0_190.bModal
		if L11_201 then
			L11_201 = "Topmost2"
			if L11_201 then
				goto lbl_69
			end
		end
		L11_201 = "Topmost"
	end
	::lbl_69::
	L10_200.szLevel = L11_201
	L11_201 = A0_190.bForbidConfirmByEnter
	L10_200.bForbidConfirmByEnter = L11_201
	L10_200.bMessageBox = true
	L10_200.nUpdateTime = 0
	L11_201 = A0_190.bDisablePrevFocus
	L10_200.bDisablePrevFocus = L11_201
	L11_201 = A0_190.szPrevFocus
	if not L11_201 then
		L11_201 = Station
		L11_201 = L11_201.GetFocusWindow
		L11_201 = L11_201()
	end
	L10_200.szPrevFocus = L11_201
	L11_201 = GetCurrentTime
	L11_201 = L11_201()
	L11_201 = L11_201 * 1000
	L12_202 = GetTime
	L12_202 = L12_202()
	L11_201 = L11_201 + L12_202
	L10_200.nCreateTime = L11_201
	L11_201 = A0_190.tCheckBoxConfig
	L12_202 = L11_201 or L12_202
	if not L11_201 then
		L12_202 = {}
	end
	L13_203 = L11_201 and true or L13_203
	L10_200.bShowCheckBox = L13_203
	L13_203 = L12_202.nBindOption
	if not L13_203 then
		L13_203 = 1
	end
	L10_200.nCheckBoxBindOption = L13_203
	L13_203 = L12_202.szText
	L10_200.szCheckBoxText = L13_203
	L13_203 = L12_202.fnCheck
	L10_200.fnCheckBoxCheck = L13_203
	L13_203 = L12_202.fnUncheck
	L10_200.fnCheckBoxUncheck = L13_203
	L13_203 = L12_202.bDefaultChecked
	L13_203 = L13_203 and true or L13_203
	L10_200.bCheckBoxDefault = L13_203
	L13_203 = A0_190.bRichText
	if L13_203 then
		L13_203 = A0_190.szMessage
		if L13_203 then
			goto lbl_132
		end
	end
	L13_203 = GetFormatText
	L17_207 = A0_190.szMessage
	L13_203 = L13_203(L17_207, 18)
	::lbl_132::
	L17_207 = L13_203
	L13_203 = L13_203.gsub
	L13_203 = L13_203(L17_207, "{%$([^%s]+)%s*([^%s]*)%s*([^%s]*)}", function(A0_226, A1_227, A2_228)
		if A0_226 == "countdown_s" or A0_226 == "countdown_ms" then
			if A2_228 == "" then
				if _ENV.nUpdateInterval ~= -1 or not 1000 then
				end
				_ENV.nUpdateInterval = math.min(_ENV.nUpdateInterval, 1000)
				local L3_229 = L3_229
				local L4_230 = L4_230
				local L5_231 = L5_231
				local L6_232 = L6_232
				local L7_233 = L7_233
				local L8_234 = L8_234
				local L3_229, L9_235 = L3_229 .. L4_230 .. L5_231 .. L6_232 .. L7_233 .. L8_234 .. "}", L9_235
				return L3_229
			end
			L3_229 = _ENV
			L3_229.bMessageCountDown = true
		end
	end)
	L10_200.szMessage = L13_203
	L13_203 = A0_190.bVisibleWhenHideUI
	if L13_203 then
		L17_207 = L10_200
		L13_203 = L10_200.ShowWhenUIHide
		L13_203(L17_207)
	end
	L13_203 = L10_200.bModal
	if L13_203 then
		L13_203 = UILock
		L13_203 = L13_203.Lock
		L17_207 = L10_200.szLevel
		L13_203(L17_207, A0_190.bVisibleWhenHideUI)
	end
	L13_203 = A0_190.bShowClose
	if not L13_203 then
		L13_203 = false
	end
	L10_200.bShowClose = L13_203
	L13_203 = L8_8
	L10_200.OnFrameBreathe = L13_203
	L13_203 = L9_9
	L10_200.OnFrameDestroy = L13_203
	L13_203 = L10_10
	L10_200.OnEvent = L13_203
	L13_203 = L11_11
	L10_200.OnFrameKeyDown = L13_203
	L13_203 = L12_12
	L10_200.OnItemMouseEnter = L13_203
	L13_203 = L13_13
	L10_200.OnItemMouseLeave = L13_203
	L13_203 = L15_15
	L10_200.OnCheckBoxCheck = L13_203
	L13_203 = L16_16
	L10_200.OnCheckBoxUncheck = L13_203
	L13_203 = A0_190.UpdateAnchor
	if not L13_203 then
		L13_203 = L17_17
	end
	L10_200.UpdateAnchor = L13_203
	L13_203 = L19_19
	L10_200.UpdateMessage = L13_203
	L13_203 = L14_14
	L10_200.OnLButtonClick = L13_203
	L17_207 = L10_200
	L13_203 = L10_200.ChangeRelation
	L13_203(L17_207, L10_200.szLevel)
	L17_207 = L10_200
	L13_203 = L10_200.RegisterEvent
	L13_203(L17_207, "UI_SCALED")
	L13_203 = IsModuleLoaded
	L17_207 = "CoinShop"
	L13_203 = L13_203(L17_207)
	if L13_203 then
		L13_203 = CoinShop_Main
		L13_203 = L13_203.IsOpened
		L13_203 = L13_203()
		if L13_203 then
			L17_207 = L10_200
			L13_203 = L10_200.RegisterEvent
			L13_203(L17_207, "COINSHOP_MODULE_FRAME_SHOW")
		end
	end
	L17_207 = L10_200
	L13_203 = L10_200.UpdateMessage
	L13_203(L17_207)
	L17_207 = L10_200
	L13_203 = L10_200.BringToTop
	L13_203(L17_207)
	L13_203 = GetTickCount
	L13_203 = L13_203()
	L17_207 = L10_200.hWndAll
	_FOR_ = 1
	for _FORV_17_ = _FOR_, _FOR_, _FOR_ do
		if A0_190[_FORV_17_] and A0_190[_FORV_17_].szOption then
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Show()
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).fnAction = A0_190[_FORV_17_].fnAction
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).nIndex = _FORV_17_
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).szSound = A0_190[_FORV_17_].szSound
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).bDelayCountDown = A0_190[_FORV_17_].bDelayCountDown
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).nCountDownTime = A0_190[_FORV_17_].nCountDownTime
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).fnDisable = A0_190[_FORV_17_].fnDisable
			if not A0_190[_FORV_17_].szDisableTip then
			end
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).szDisableTip = ""
			if A0_190[_FORV_17_].bDelayCountDown then
				L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).fnCountDownEnd = A0_190[_FORV_17_].fnCountDownEnd
			else
				if not A0_190[_FORV_17_].fnCountDownEnd then
				end
				L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).fnCountDownEnd = L24_24
			end
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).szOption = A0_190[_FORV_17_].szOption
			if A0_190[_FORV_17_].dwStartTime then
				L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).dwStartTime = A0_190[_FORV_17_].dwStartTime
			else
				L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).dwStartTime = L13_203
			end
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).OnLButtonClick = function()
				local L0_236, L1_237 = this:GetRoot(), L1_237
				L1_237 = nil
				if this.fnAction then
					if L0_236.args then
						L1_237 = this.fnAction(unpack(L0_236.args))
					else
						L1_237 = this.fnAction()
					end
				elseif L0_236.fnAction then
					if L0_236.args then
						local L5_241 = L5_241
						repeat
							L5_241 = L5_241(_ENV, unpack(L0_236.args))
							L1_237 = L5_241
							do break end -- pseudo-goto
							L5_241 = L0_236.fnAction
							L5_241 = L5_241(_ENV)
							L1_237 = L5_241
						until true
					end
				end
				if L0_236 then
					L0_236.bInitiative = true
				end
				L5_241 = this
				L5_241 = L5_241.szSound
				if L5_241 then
					if L0_236 then
						L5_241 = L0_236.IsValid
						L5_241 = L5_241(L0_236)
						if L5_241 then
							L5_241 = this
							L5_241 = L5_241.szSound
							L0_236.szCloseSound = L5_241
					end
					else
						L5_241 = PlaySound
						L5_241(SOUND.UI_SOUND, this.szSound)
					end
				end
				if L0_236 then
					L5_241 = L0_236.IsValid
					L5_241 = L5_241(L0_236)
					if L5_241 and L1_237 ~= false then
						L5_241 = Station
						L5_241 = L5_241.CloseWindow
						local L3_239, L4_240 = L0_236:GetName()
						L5_241(L3_239, L4_240, L0_236:GetName())
					end
				end
			end
			if A0_190[_FORV_17_].nFont then
				L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):SetFontScheme(A0_190[_FORV_17_].nFont)
			end
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):SetText(A0_190[_FORV_17_].szOption)
			if 0 < L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):GetTextExtent() then
			end
			if A0_190[_FORV_17_].bDelayCountDown and A0_190[_FORV_17_].nCountDownTime then
			end
			if A0_190[_FORV_17_].fnDisable then
				L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).bDisable = A0_190[_FORV_17_].fnDisable()
				if false then
				end
			end
			L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Enable(not L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1).bDisable, true)
			if A0_190[_FORV_17_].fnInit then
				A0_190[_FORV_17_].fnInit((L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1)))
			end
		end
	end
	if L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):GetTextExtent() >= L2_2 then
		_FOR_ = 1
		for _FORV_17_ = _FOR_, _FOR_, _FOR_ do
			L10_200.hWndOption:Lookup("Btn_Option" .. _FORV_17_):Lookup("", "Text_Option" .. _FORV_17_):SetW((L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):GetTextExtent()))
			L10_200.hWndOption:Lookup("Btn_Option" .. _FORV_17_):SetW(L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):GetTextExtent() + L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):GetTextExtent() * L3_3)
			L10_200.hWndOption:Lookup("Btn_Option" .. _FORV_17_):Lookup("", ""):SetW(L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):GetTextExtent() + L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):GetTextExtent() * L3_3)
			L10_200.hWndOption:Lookup("Btn_Option" .. _FORV_17_):Lookup("", "Text_Option" .. _FORV_17_):SetRelX((L10_200.hWndOption:Lookup("Btn_Option" .. _FORV_17_):GetW() - L10_200.hWndOption:Lookup("Btn_Option" .. 0 + 1):Lookup("", "Text_Option" .. 0 + 1):GetTextExtent()) / 2)
			L10_200.hWndOption:Lookup("Btn_Option" .. _FORV_17_):Lookup("", ""):FormatAllItemPos()
		end
	end
	_FOR_ = 1
	for _FORV_17_ = _FOR_, _FOR_, _FOR_ do
		L10_200.hWndOption:Lookup("Btn_Option" .. _FORV_17_):Hide()
	end
	L17_207:Lookup("Btn_Close"):Show(L10_200.bShowClose)
	local L15_205, L16_206 = L15_205, L16_206
	if 1 < 0 + 1 then
	end
	if not A0_190.bOrgMinW then
		L10_200.hhandleMsg:SetW((math.max(L10_200.hWndOption:Lookup("Btn_Option1"):GetSize() * (0 + 1) + 40 + (0 + 1 - 1) * 10, (L10_200.hhandleMsg:GetW()))))
	end
	L10_200.hhandleMsg:FormatAllItemPos()
	if not A0_190.bOrgMinW then
		L10_200.hhandleMsg:SetW((L10_200.hhandleMsg:GetAllItemSize()))
		L10_200.hWndOption:SetW((math.max(L10_200.hWndOption:Lookup("Btn_Option1"):GetSize() * (0 + 1) + 40 + (0 + 1 - 1) * 10, (L10_200.hhandleMsg:GetAllItemSize()))))
		L16_206:SetW((math.max(L10_200.hWndOption:Lookup("Btn_Option1"):GetSize() * (0 + 1) + 40 + (0 + 1 - 1) * 10, (L10_200.hhandleMsg:GetAllItemSize()))))
	end
	L10_200.hhandleMsg:SetHeightByAllItemHeight()
	L10_200.hhandleMsg:RefeshAnchorPosSize()
	L16_206:FormatAllContentPos()
	L16_206:RefeshAnchorPosSize()
	local L18_208, L19_209 = L18_208, L19_209
	L10_200:SetSize(L17_207:GetSize())
	local L20_210 = L20_210
	local L21_211 = L21_211
	local L22_212 = L22_212
	if L15_205:Lookup("Image_Bg_OrnamentCenterUp") and L15_205:Lookup("Image_Bg_OrnamentCenterDown") then
		if L15_205:Lookup("Image_Bg"):PtInItem(L15_205:Lookup("Image_Bg_OrnamentCenterUp"):GetAbsPos()) then
		end
		if L15_205:Lookup("Image_Bg"):PtInItem(L15_205:Lookup("Image_Bg_OrnamentCenterDown"):GetAbsPos()) then
		end
		L15_205:Lookup("Image_Bg_OrnamentCenterUp"):Show((L15_205:Lookup("Image_Bg"):PtInItem(L15_205:Lookup("Image_Bg_OrnamentCenterUp"):GetAbsPos() + L15_205:Lookup("Image_Bg_OrnamentCenterUp"):GetSize(), L15_205:Lookup("Image_Bg_OrnamentCenterUp"):GetAbsPos() + L15_205:Lookup("Image_Bg_OrnamentCenterUp"):GetSize())))
		L15_205:Lookup("Image_Bg_OrnamentCenterDown"):Show((L15_205:Lookup("Image_Bg"):PtInItem(L15_205:Lookup("Image_Bg_OrnamentCenterDown"):GetAbsPos() + L15_205:Lookup("Image_Bg_OrnamentCenterDown"):GetSize(), L15_205:Lookup("Image_Bg_OrnamentCenterDown"):GetAbsPos() + L15_205:Lookup("Image_Bg_OrnamentCenterDown"):GetSize())))
	end
	L20_20(L10_200, A0_190)
	L21_21(L10_200, A0_190)
	L22_22(L10_200, A0_190)
	if A0_190.x and A0_190.y then
		L10_200:SetRelPos(A0_190.x - L17_207:GetSize() / 2, A0_190.y - L17_207:GetSize() / 2)
		L10_200.Anchor = GetFrameAnchor(L10_200, "TOPCENTER")
	else
		while Station.Lookup(L10_200.szLevel):GetFirstChild() do
			if Station.Lookup(L10_200.szLevel):GetFirstChild().bMessageBox and Station.Lookup(L10_200.szLevel):GetFirstChild().bModal == L10_200.bModal and Station.Lookup(L10_200.szLevel):GetFirstChild() ~= L10_200 then
				local L30_220 = L30_220
				local L31_221 = L31_221
				local L33_223 = L33_223
				local L34_224 = L34_224
				;({})[1] = Station.Lookup(L10_200.szLevel):GetFirstChild():GetAbsPos()
				local ({})[2], L35_225 = Station.Lookup(L10_200.szLevel):GetFirstChild():GetAbsPos() + Station.Lookup(L10_200.szLevel):GetFirstChild():GetSize(), L35_225
				table.insert({}, {})
			end
		end
		table.sort({}, function(A0_242, A1_243)
			local L2_244, L3_245
			L2_244 = A0_242[1]
			L3_245 = A1_243[1]
			L2_244 = L2_244 < L3_245
			return L2_244
		end)
		_FOR_, _FOR_, _FOR_ = pairs({})
		for _FORV_30_, _FORV_31_ in _FOR_, _FOR_, _FOR_ do
			if not (300 >= _FORV_31_[2]) and not (300 + L17_207:GetSize() <= _FORV_31_[1]) then
			end
		end
		if IsMobileStreamingEnable() then
			({}).s = "CENTER"
			;({}).r = "CENTER"
			;({}).x = 0
			L10_200.Anchor, ({}).y = {}, 0
		else
			({}).s = "TOPCENTER"
			;({}).r = "TOPCENTER"
			L10_200.Anchor, ({}).y, ({}).x = {}, _FORV_31_[2], 0
		end
	end
	L10_200:UpdateAnchor()
	if A0_190.szAlignment == "CENTER" then
		L19_209:SetHAlign(1)
	elseif A0_190.szAlignment == "RIGHT" then
		L19_209:SetHAlign(2)
	else
		L19_209:SetHAlign(0)
	end
	if A0_190.szVAlignment == "CENTER" then
		L19_209:SetVAlign(1)
	elseif A0_190.szVAlignment == "RIGHT" then
		L19_209:SetVAlign(2)
	else
		L19_209:SetVAlign(0)
	end
	if A0_190.nRowSpace and 0 <= A0_190.nRowSpace then
		L19_209:SetRowSpacing(A0_190.nRowSpace)
	end
	L19_209:FormatAllItemPos()
	L15_205:FormatAllItemPos()
	L23_23(L10_200, L20_210)
	if A0_190.bFocus or Cursor.IsVisible() and not L6_6() and not L7_7() then
		Station.SetFocusWindow(L10_200)
	end
	if not A1_191 then
		PlaySound(SOUND.UI_SOUND, g_sound.OpenFrame)
	end
	FireUIEvent("ON_MESSAGE_BOX_OPEN", A0_190.szName, L10_200)
end
MessageBox = L25_25
function L25_25()
	local L0_246
	local L1_247 = Station.Lookup("Topmost2"):GetFirstChild()
	while L1_247 do
		if L1_247.bMessageBox then
			L0_246 = L1_247
		end
		L1_247 = L1_247:GetNext()
	end
	if not L0_246 then
		L1_247 = Station.Lookup("Topmost"):GetFirstChild()
		while L1_247 do
			if L1_247.bMessageBox then
				L0_246 = L1_247
			end
			L1_247 = L1_247:GetNext()
		end
	end
	if L0_246 then
		local L2_248 = L0_246:GetName()
		local L5_251 = string.find(L2_248, "ReadyConfirm")
		if L5_251 ~= 4 then
			L5_251 = Station
			L5_251 = L5_251.CloseWindow
			L5_251(L2_248)
			local L4_250 = L4_250
			L5_251 = true
			return L5_251
		end
	end
	L2_248 = false
	return L2_248
end
CloseLastMessageBox = L25_25
function L25_25(A0_252)
	if Station.Lookup("Topmost2/MB_" .. A0_252) then
		Station.CloseWindow("MB_" .. A0_252)
		return true
	end
	if Station.Lookup("Topmost/MB_" .. A0_252) then
		local L1_253 = L1_253
		local L3_255 = "MB_" .. A0_252
		L1_253(L3_255)
		L1_253 = true
		return L1_253
	end
	L1_253 = false
	return L1_253
end
CloseMessageBox = L25_25
function L25_25(A0_256)
	if Station.Lookup("Topmost2/MB_" .. A0_256) then
		return Station.Lookup("Topmost2/MB_" .. A0_256)
	end
	if Station.Lookup("Topmost/MB_" .. A0_256) then
		local L3_258 = L3_258
		return L3_258("Topmost/MB_" .. A0_256)
	end
end
GetMessageBoxFrame = L25_25
