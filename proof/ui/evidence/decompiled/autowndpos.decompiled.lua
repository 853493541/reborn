local L0_0, L1_1, L2_2
L2_2 = false
local _g_AutoPosFrameCloseDisable, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11 = L2_2, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11
_g_AutoPosFrame2 = L1_1
_g_AutoPosFrame1 = L0_0
L0_0 = {}
_g_tbView = L0_0
L0_0 = nil
_g_tbUIViewCenterPos = L0_0
function L0_0(A0_18)
	if A0_18 == 1 then
		if _g_AutoPosFrame1 then
			if _g_AutoPosFrame1._AutoPosInfo and _g_AutoPosFrame1._AutoPosInfo.fnAutoClose then
				_g_AutoPosFrame1._AutoPosInfo.fnAutoClose()
			end
			_g_AutoPosFrame1 = nil
		end
	elseif _g_AutoPosFrame2 then
		if _g_AutoPosFrame2._AutoPosInfo and _g_AutoPosFrame2._AutoPosInfo.fnAutoClose then
			_g_AutoPosFrame2._AutoPosInfo.fnAutoClose()
			local L1_19 = L1_19
		end
		L1_19 = nil
		_g_AutoPosFrame2 = L1_19
	end
end
_ColseAutoPosFrame = L0_0
function L0_0()
	if _g_AutoPosFrame2 and (not _g_AutoPosFrame2:IsValid() or not _g_AutoPosFrame2:IsVisible()) then
		_g_AutoPosFrame2 = nil
	end
	repeat
		if _g_AutoPosFrame1 then
			if _g_AutoPosFrame1:IsValid() then
				local L1_20 = _g_AutoPosFrame1:IsVisible()
				if L1_20 then
					goto lbl_38
				end
			end
			L1_20 = _g_AutoPosFrame2
			_g_AutoPosFrame1 = L1_20
			L1_20 = nil
			_g_AutoPosFrame2 = L1_20
			break -- pseudo-goto
		end
		L1_20 = _g_AutoPosFrame2
		_g_AutoPosFrame1 = L1_20
		L1_20 = nil
		_g_AutoPosFrame2 = L1_20
	until true
	::lbl_38::
end
function L1_1(A0_21)
	if _g_AutoPosFrameCloseDisable then
		return
	end
	A0_21:BringToTop()
	_ENV()
	if A0_21 == _g_AutoPosFrame1 or A0_21 == _g_AutoPosFrame2 then
		CorrectAutoPosFrameAfterClientResize()
		return
	end
	if not A0_21._AutoPosInfo then
		return
	end
	_g_AutoPosFrameCloseDisable = true
	if _g_AutoPosFrame1 then
		if _g_AutoPosFrame2 then
			if A0_21._AutoPosInfo.szFriendly then
				local L3_23 = L3_23
				if L3_23 == _g_AutoPosFrame2:GetName() then
					L3_23 = _ColseAutoPosFrame
					L3_23(1)
					L3_23 = _g_AutoPosFrame2
					_g_AutoPosFrame1 = L3_23
					L3_23 = nil
					_g_AutoPosFrame2 = L3_23
				end
			end
			L3_23 = _g_AutoPosFrame1
			L3_23 = L3_23._AutoPosInfo
			L3_23 = L3_23.nSize
			L3_23 = L3_23 + A0_21._AutoPosInfo.nSize
			if 3 < L3_23 then
				L3_23 = _ColseAutoPosFrame
				L3_23(1)
				L3_23 = _ColseAutoPosFrame
				L3_23(2)
				_g_AutoPosFrame1 = A0_21
				L3_23 = nil
				_g_AutoPosFrame2 = L3_23
			else
				L3_23 = _ColseAutoPosFrame
				L3_23(2)
				_g_AutoPosFrame2 = A0_21
				do break end -- pseudo-goto
				L3_23 = A0_21._AutoPosInfo
				L3_23 = L3_23.szOnly
				if L3_23 then
					L3_23 = A0_21._AutoPosInfo
					L3_23 = L3_23.szOnly
					if L3_23 == _g_AutoPosFrame1._AutoPosInfo.szOnly then
						L3_23 = _ColseAutoPosFrame
						L3_23(1)
						_g_AutoPosFrame1 = A0_21
				end
				else
					L3_23 = _g_AutoPosFrame1
					L3_23 = L3_23._AutoPosInfo
					L3_23 = L3_23.nSize
					L3_23 = L3_23 + A0_21._AutoPosInfo.nSize
					if 4 < L3_23 then
						L3_23 = _ColseAutoPosFrame
						L3_23(1)
						repeat
							_g_AutoPosFrame1 = A0_21
							do break end -- pseudo-goto
							_g_AutoPosFrame2 = A0_21
							do break end -- pseudo-goto
							_g_AutoPosFrame1 = A0_21
						until true
					end
				end
			end
		end
	end
	L3_23 = false
	_g_AutoPosFrameCloseDisable = L3_23
	L3_23 = CorrectAutoPosFrameAfterClientResize
	L3_23()
end
CorrectAutoPosFrameWhenShow = L1_1
function L1_1(A0_24)
	if _g_AutoPosFrameCloseDisable then
		return
	end
	_ENV()
	if A0_24 == _g_AutoPosFrame2 then
		_g_AutoPosFrame2 = nil
	elseif A0_24 == _g_AutoPosFrame1 then
		_g_AutoPosFrame1 = _g_AutoPosFrame2
		_g_AutoPosFrame2 = nil
	end
	CorrectAutoPosFrameAfterClientResize()
end
CorrectAutoPosFrameWhenHide = L1_1
function L1_1()
	YogaAutoPos_LayoutAll()
	if _g_AutoPosFrameCloseDisable then
		return
	end
	_ENV()
	if _g_AutoPosFrame1 then
		_g_AutoPosFrame1:SetPoint("TOPLEFT", 0, 0, "TOPLEFT", 10, 150)
		FireUIEvent("CORRECT_AUTO_POS", _g_AutoPosFrame1:GetName())
	end
	if _g_AutoPosFrame2 then
		local L0_25, L1_26 = L0_25, L1_26
		local L4_29 = L4_29
		local L5_30 = L5_30
		local L6_31 = L6_31
		local L7_32 = L7_32
		L0_25(L1_26, L4_29, L5_30, L6_31, L7_32, "TOPRIGHT", 0, 0)
		local L8_33 = L8_33
		L0_25 = FireUIEvent
		L1_26 = "CORRECT_AUTO_POS"
		L4_29 = _g_AutoPosFrame2
		L5_30 = L4_29
		L4_29 = L4_29.GetName
		L4_29, L5_30, L6_31, L7_32, L8_33 = L4_29(L5_30)
		L0_25(L1_26, L4_29, L5_30, L6_31, L7_32, L8_33, L4_29(L5_30))
	end
end
CorrectAutoPosFrameAfterClientResize = L1_1
function L1_1(A0_34)
	_ENV()
	local L1_35 = L1_35
	L1_35 = false
	_g_AutoPosFrameCloseDisable = true
	if _g_AutoPosFrame2 then
		_ColseAutoPosFrame(2)
		L1_35 = true
	end
	if _g_AutoPosFrame1 then
		_ColseAutoPosFrame(1)
		L1_35 = true
	end
	if L1_35 and not A0_34 then
		local L2_36 = L2_36
		local L3_37 = L3_37
		L2_36(L3_37, g_sound.CloseFrame)
		local L4_38 = L4_38
	end
	L2_36 = false
	_g_AutoPosFrameCloseDisable = L2_36
	return L1_35
end
CorrectAutoPosFrameEscClose = L1_1
function L1_1(A0_39, A1_40, A2_41, A3_42, A4_43)
	local L5_44, L6_45, L7_46, L8_47 = IsMobileStreamingEnable(), L6_45, L7_46, L8_47
	if L5_44 then
		return
	end
	L6_45 = false
	L7_46 = A0_39._AutoPosInfo
	if not L7_46 then
		L7_46 = {}
		A0_39._AutoPosInfo = L7_46
		L6_45 = true
	end
	L7_46 = A0_39._AutoPosInfo
	L7_46.nSize = A1_40
	L7_46 = A0_39._AutoPosInfo
	L7_46.szOnly = A2_41
	L7_46 = A0_39._AutoPosInfo
	L7_46.szFriendly = A3_42
	L7_46 = A0_39._AutoPosInfo
	L7_46.fnAutoClose = A4_43
	if not L6_45 then
		L7_46 = A0_39._AutoPosInfo
		L7_46 = L7_46.NewOnFrameShow
		L8_47 = A0_39.OnFrameShow
		if L7_46 == L8_47 then
			goto lbl_36
		end
	end
	L7_46 = A0_39._AutoPosInfo
	L8_47 = A0_39.OnFrameShow
	L7_46.OnFrameShow = L8_47
	function L7_46()
		CorrectAutoPosFrameWhenShow(this)
		local L0_48 = L0_48
		L0_48 = L0_48(this)
		if not L0_48 then
			return
		end
		if this._AutoPosInfo.OnFrameShow then
			return this._AutoPosInfo.OnFrameShow()
		elseif L0_48.OnFrameShow then
			do return L0_48.OnFrameShow() end
			local L1_49 = L1_49
		end
	end
	A0_39.OnFrameShow = L7_46
	L7_46 = A0_39._AutoPosInfo
	L8_47 = A0_39.OnFrameShow
	L7_46.NewOnFrameShow = L8_47
	::lbl_36::
	if not L6_45 then
		L7_46 = A0_39._AutoPosInfo
		L7_46 = L7_46.NewOnFrameHide
		L8_47 = A0_39.OnFrameHide
		if L7_46 == L8_47 then
			goto lbl_51
		end
	end
	L7_46 = A0_39._AutoPosInfo
	L8_47 = A0_39.OnFrameHide
	L7_46.OnFrameHide = L8_47
	function L7_46()
		CorrectAutoPosFrameWhenHide(this)
		if this._AutoPosInfo.OnFrameHide then
			return this._AutoPosInfo.OnFrameHide()
		elseif GetFrameSelf(this).OnFrameHide then
			local L1_50 = GetFrameSelf(this)
			L1_50 = L1_50.OnFrameHide
			return L1_50()
		end
	end
	A0_39.OnFrameHide = L7_46
	L7_46 = A0_39._AutoPosInfo
	L8_47 = A0_39.OnFrameHide
	L7_46.NewOnFrameHide = L8_47
	::lbl_51::
end
InitFrameAutoPosInfo = L1_1
function L1_1(A0_51, A1_52, A2_53)
	local L3_54, L4_55, L7_58, L8_59, L10_61, L11_62, L12_63, L13_64, L14_65, L24_75 = A0_51:GetAbsPos()
	L8_59 = A0_51
	L7_58 = A0_51.GetSize
	L7_58, L8_59 = L7_58(L8_59)
	if A1_52 then
		if A1_52 == "TOPLEFT" then
		elseif A1_52 == "TOPCENTER" then
			L10_61 = L7_58 / 2
			L3_54 = L3_54 + L10_61
		elseif A1_52 == "TOPRIGHT" then
			L3_54 = L3_54 + L7_58
		elseif A1_52 == "RIGHTCENTER" then
			L10_61 = L3_54 + L7_58
			L11_62 = L8_59 / 2
			L4_55 = L4_55 + L11_62
			L3_54 = L10_61
		elseif A1_52 == "BOTTOMRIGHT" then
			L10_61 = L3_54 + L7_58
			L4_55 = L4_55 + L8_59
			L3_54 = L10_61
		elseif A1_52 == "BOTTOMCENTER" then
			L10_61 = L7_58 / 2
			L10_61 = L3_54 + L10_61
			L4_55 = L4_55 + L8_59
			L3_54 = L10_61
		elseif A1_52 == "BOTTOMLEFT" then
			L4_55 = L4_55 + L8_59
		elseif A1_52 == "LEFTCENTER" then
			L10_61 = L8_59 / 2
			L4_55 = L4_55 + L10_61
		elseif A1_52 == "CENTER" then
			L10_61 = L7_58 / 2
			L10_61 = L3_54 + L10_61
			L11_62 = L8_59 / 2
			L4_55 = L4_55 + L11_62
			L3_54 = L10_61
		else
			A1_52 = "TOPLEFT"
		end
		L10_61 = 0
		L8_59 = 0
		L7_58 = L10_61
	end
	L10_61 = L7_58 / 2
	L10_61 = L3_54 + L10_61
	L11_62 = L8_59 / 2
	L11_62 = L4_55 + L11_62
	L12_63 = Station
	L12_63 = L12_63.GetClientSize
	L12_63, L13_64 = L12_63()
	L14_65 = L12_63 / 2
	L24_75 = L13_64 / 2
	;({})[1], ({})[1] = {}, L3_54
	;({})[1], ({})[2] = {}, L4_55
	;({})[2], ({})[1] = {}, L10_61 - L14_65
	;({})[2], ({})[2] = {}, L4_55
	;({})[3], ({})[1] = {}, L3_54 + L7_58 - L12_63
	;({})[3], ({})[2] = {}, L4_55
	;({})[4], ({})[1] = {}, L3_54 + L7_58 - L12_63
	;({})[4], ({})[2] = {}, L11_62 - L24_75
	;({})[5], ({})[1] = {}, L3_54 + L7_58 - L12_63
	;({})[5], ({})[2] = {}, L4_55 + L8_59 - L13_64
	;({})[6], ({})[1] = {}, L10_61 - L14_65
	;({})[6], ({})[2] = {}, L4_55 + L8_59 - L13_64
	;({})[7], ({})[1] = {}, L3_54
	;({})[7], ({})[2] = {}, L4_55 + L8_59 - L13_64
	;({})[8], ({})[1] = {}, L3_54
	;({})[8], ({})[2] = {}, L11_62 - L24_75
	;({})[9], ({})[1] = {}, L10_61 - L14_65
	;({})[9], ({})[2] = {}, L11_62 - L24_75
	if A2_53 then
		({}).TOPLEFT = 1
		;({}).TOPCENTER = 2
		;({}).TOPRIGHT = 3
		;({}).RIGHTCENTER = 4
		;({}).BOTTOMRIGHT = 5
		;({}).BOTTOMCENTER = 6
		;({}).BOTTOMLEFT = 7
		;({}).LEFTCENTER = 8
		;({}).CENTER = 9
		if not A1_52 then
		end
		;({}).s = A2_53
		;({}).r = A2_53
		;({}).x = ({})[({})[A2_53]][1]
		;({}).y = ({})[({})[A2_53]][2]
		return {}
	else
		local L16_67 = L16_67
		local L19_70 = L19_70
		local L20_71 = L20_71
		local L21_72 = L21_72
		L19_70[1] = L20_71
		L19_70[2] = L21_72
		L19_70[3] = L22_73
		L19_70[4] = "RIGHTCENTER"
		L19_70[5] = "BOTTOMRIGHT"
		L19_70[6] = "BOTTOMCENTER"
		L19_70[7] = "BOTTOMLEFT"
		L19_70[8] = "LEFTCENTER"
		L19_70[9] = "CENTER"
		L20_71 = 1
		L21_72 = nil
		L22_73 = ipairs
		L23_74 = L16_67
		L22_73, L23_74, _FOR_ = L22_73(L23_74)
		for _FORV_20_, _FORV_21_ in L22_73, L23_74, _FOR_ do
			if not L21_72 then
				L21_72 = _FORV_21_[1] * _FORV_21_[1] + _FORV_21_[2] * _FORV_21_[2]
			elseif _FORV_21_[1] * _FORV_21_[1] + _FORV_21_[2] * _FORV_21_[2] <= L21_72 then
				L21_72, L20_71 = _FORV_21_[1] * _FORV_21_[1] + _FORV_21_[2] * _FORV_21_[2], _FORV_20_
			end
		end
		L22_73 = {}
		L23_74 = A1_52 or L23_74
		if not A1_52 then
			L23_74 = L19_70[L20_71]
		end
		L22_73.s = L23_74
		L23_74 = L19_70[L20_71]
		L22_73.r = L23_74
		L23_74 = L16_67[L20_71]
		L23_74 = L23_74[1]
		L22_73.x = L23_74
		L23_74 = L16_67[L20_71]
		L23_74 = L23_74[2]
		L22_73.y = L23_74
		return L22_73
	end
end
GetFrameAnchor = L1_1
function L1_1(A0_76)
	local L1_77, L2_78, L5_81, L6_82, L8_84, L9_85, L10_86, L11_87 = A0_76:GetAbsPos()
	L6_82 = A0_76
	L5_81 = A0_76.GetSize
	L5_81, L6_82 = L5_81(L6_82)
	L8_84 = L5_81 / 2
	L8_84 = L1_77 + L8_84
	L9_85 = L6_82 / 2
	L9_85 = L2_78 + L9_85
	L10_86 = Station
	L10_86 = L10_86.GetClientSize
	L10_86, L11_87 = L10_86()
	if L8_84 > L10_86 / 2 then
		if L9_85 > L11_87 / 2 then
			return "BOTTOMRIGHT"
		else
			return "TOPRIGHT"
		end
	elseif L9_85 > L11_87 / 2 then
		return "BOTTOMLEFT"
	else
		return "TOPLEFT"
	end
end
GetFrameAnchorCorner = L1_1
function L1_1(A0_88, A1_89)
	local L2_90, L3_91, L6_94, L7_95, L9_97, L10_98, L11_99, L12_100, L18_106, L20_107 = A0_88:GetAbsPos()
	L7_95 = A0_88
	L6_94 = A0_88.GetSize
	L6_94, L7_95 = L6_94(L7_95)
	L9_97 = L6_94 / 2
	L9_97 = L2_90 + L9_97
	L10_98 = L7_95 / 2
	L10_98 = L3_91 + L10_98
	L11_99 = Station
	L11_99 = L11_99.GetClientSize
	L11_99, L12_100 = L11_99()
	L18_106 = L11_99 / 2
	L20_107 = L12_100 / 2
	if A1_89 then
		if A1_89 == "VERTICAL" then
			if L10_98 > L20_107 then
				return "BOTTOM"
			else
				return "TOP"
			end
		elseif A1_89 == "HORIZEN" then
			if L9_97 > L18_106 then
				return "RIGHT"
			else
				return "LEFT"
			end
		end
	else
		({})[1] = "LEFT"
		;({})[2] = "RIGHT"
		;({})[3] = "TOP"
		;({})[4] = "BOTTOM"
		;({})[1] = L9_97
		;({})[2] = L11_99 - L9_97
		;({})[3] = L10_98
		local ({})[4], L17_105 = L12_100 - L10_98, L17_105
		local _FOR_, _FOR_, _FOR_, L16_104 = pairs({})
		for _FORV_18_, _FORV_19_ in _FOR_, _FOR_, _FOR_ do
			if not nil then
			elseif _FORV_19_ < L16_104[_FORV_18_] then
			end
		end
		return L17_105[_FORV_18_]
	end
end
GetFrameAnchorEdge = L1_1
L1_1 = 1
AUTOPOS_TYPE_LEFT = L1_1
L1_1 = 2
AUTOPOS_TYPE_RIGHT = L1_1
L1_1 = 3
AUTOPOS_TYPE_FIXED = L1_1
L1_1 = 20
L2_2 = {}
L5_5 = AUTOPOS_TYPE_LEFT
L6_6 = {}
L6_6.s = "TOPLEFT"
L6_6.r = "TOPLEFT"
L6_6.x = 40
L6_6.y = 80
L2_2[L5_5] = L6_6
L5_5 = AUTOPOS_TYPE_RIGHT
L6_6 = {}
L6_6.s = "TOPRIGHT"
L6_6.r = "TOPRIGHT"
L6_6.x = 40
L6_6.y = 80
L2_2[L5_5] = L6_6
L5_5 = {}
g_tWindowAnchor = L5_5
L5_5 = RegisterCustomData
L6_6 = "g_tWindowAnchor"
L5_5(L6_6)
function L5_5(A0_108, A1_109)
	local L2_110
	L2_110 = A0_108.OnFrameShow
	A0_108._AutoPos_OnFrameShow = L2_110
	function L2_110()
		if _ENV._AutoPos_OnFrameShow then
			_ENV._AutoPos_OnFrameShow()
		end
		local L0_111, L1_112 = _ENV:GetName(), L1_112
		if L0_111 then
			L1_112 = #L0_111
			if L1_112 ~= 0 then
				goto lbl_17
			end
		end
		do return end
		::lbl_17::
		L1_112 = nil
		if g_tWindowAnchor[L0_111] then
			L1_112 = g_tWindowAnchor[L0_111]
		else
			L1_112 = clone(_ENV[A1_109])
			_ENV[A1_109].x = _ENV[A1_109].x + L1_1
			_ENV[A1_109].y = _ENV[A1_109].y + L1_1
		end
		if L1_112 then
			local L4_115 = L4_115
			local L5_116 = L5_116
			local L6_117 = L6_117
			local L7_118 = L7_118
			local L8_119 = L8_119
			L4_115(L5_116, L6_117, L7_118, L8_119, L1_112.r, L1_112.x, L1_112.y)
			local L9_120 = L9_120
			L4_115 = _ENV
			L5_116 = L4_115
			L4_115 = L4_115.CorrectPos
			L4_115(L5_116)
		end
	end
	A0_108.OnFrameShow = L2_110
	L2_110 = A0_108.OnFrameDragEnd
	A0_108._AutoPos_OnFrameDragEnd = L2_110
	function L2_110()
		local L2_123 = _ENV._AutoPos_OnFrameDragEnd
		if L2_123 then
			L2_123 = _ENV
			L2_123 = L2_123._AutoPos_OnFrameDragEnd
			L2_123()
		end
		L2_123 = this
		L2_123 = L2_123.CorrectPos
		L2_123(L2_123)
		L2_123 = GetFrameAnchor
		L2_123 = L2_123(this)
		tAnchor = L2_123
		L2_123 = _ENV
		L2_123 = L2_123.GetName
		local L2_123, L1_122 = L2_123(L2_123), L1_122
		if L2_123 then
			L1_122 = #L2_123
			if L1_122 ~= 0 then
				goto lbl_24
			end
		end
		do return end
		::lbl_24::
		L1_122 = g_tWindowAnchor
		L1_122[L2_123] = tAnchor
	end
	A0_108.OnFrameDragEnd = L2_110
end
AutoPos_Register = L5_5
L5_5 = {}
L5_5.REMIND = 1
L5_5.BOTTOMRIGHT = 2
L5_5.NEW_PANEL = 3
YOGA_AUTO_POS = L5_5
function L5_5(A0_124)
	local L2_126, L3_127, L4_128 = Station.GetClientSize, L3_127, L4_128
	L2_126, L3_127 = L2_126()
	L4_128 = L2_126
	return L4_128, L3_127
end
function L6_6()
	local L1_130, L2_131, L3_132 = Station.GetClientSize, L2_131, L3_132
	L1_130, L2_131 = L1_130()
	L2_131 = L2_131 - 230
	L3_132 = L1_130
	return L3_132, L2_131
end
L7_7 = {}
L8_8 = YOGA_AUTO_POS
L8_8 = L8_8.BOTTOMRIGHT
L9_9 = {}
L10_10 = YG_FLEX_DIRECTION
L10_10 = L10_10.COLUMN_REVERSE
L9_9.FlexDirection = L10_10
L10_10 = YG_FLEX_WRAP
L10_10 = L10_10.NO_WRAP
L9_9.FlexWrap = L10_10
L10_10 = YG_DIRECTION
L10_10 = L10_10.LTR
L9_9.Direction = L10_10
L10_10 = YG_ALIGN
L10_10 = L10_10.CENTER
L9_9.AlignContent = L10_10
L10_10 = YG_JUSTIFY
L10_10 = L10_10.CENTER
L9_9.JustifyContent = L10_10
L7_7[L8_8] = L9_9
L8_8 = YOGA_AUTO_POS
L8_8 = L8_8.REMIND
L9_9 = {}
L10_10 = YG_FLEX_DIRECTION
L10_10 = L10_10.COLUMN_REVERSE
L9_9.FlexDirection = L10_10
L10_10 = YG_FLEX_WRAP
L10_10 = L10_10.WRAP
L9_9.FlexWrap = L10_10
L10_10 = YG_DIRECTION
L10_10 = L10_10.RTL
L9_9.Direction = L10_10
L10_10 = YG_ALIGN
L10_10 = L10_10.FLEX_END
L9_9.AlignContent = L10_10
L7_7[L8_8] = L9_9
L8_8 = YOGA_AUTO_POS
L8_8 = L8_8.NEW_PANEL
L9_9 = {}
L10_10 = YG_FLEX_DIRECTION
L10_10 = L10_10.COLUMN_REVERSE
L9_9.FlexDirection = L10_10
L10_10 = YG_FLEX_WRAP
L10_10 = L10_10.NO_WRAP
L9_9.FlexWrap = L10_10
L10_10 = YG_DIRECTION
L10_10 = L10_10.RTL
L9_9.Direction = L10_10
L10_10 = YG_ALIGN
L10_10 = L10_10.CENTER
L9_9.AlignItems = L10_10
L10_10 = YG_JUSTIFY
L10_10 = L10_10.FLEX_START
L9_9.JustifyContent = L10_10
L9_9.fnGetArea = L6_6
L7_7[L8_8] = L9_9
L8_8 = {}
function L9_9(A0_133, A1_134)
	local L2_135
	L2_135 = _ENV
	L2_135 = L2_135[A1_134]
	if not L2_135 then
		return
	end
	L5_138 = ipairs
	L5_138, L4_137, _FOR_ = L5_138(L2_135)
	for _FORV_6_, _FORV_7_ in L5_138, L4_137, _FOR_ do
		if _FORV_7_ == A0_133 then
			return _FORV_6_
		end
	end
end
function L10_10(A0_139)
	local L1_140, L2_141, L3_142
	L1_140 = _ENV
	local L1_140, L7_146, L11_150, L12_151 = L1_140[A0_139], L7_146, L11_150, L12_151
	if not L1_140 then
		return
	end
	L2_141 = {}
	L3_142 = L7_7
	L3_142 = L3_142[A0_139]
	L7_146 = clone
	L11_150 = L3_142
	L7_146 = L7_146(L11_150)
	L11_150 = L3_142.fnGetArea
	if not L11_150 then
		L11_150 = L3_3
	end
	L12_151 = L11_150
	L12_151 = L12_151()
	L7_146.Height, L7_146.Width = L12_151()
	L7_146.MinHeight, L7_146.MinWidth = L12_151()
	table.insert(L2_141, L7_146)
	_FOR_, _FOR_, _FOR_ = ipairs(L1_140)
	for _FORV_11_, _FORV_12_ in _FOR_, _FOR_, _FOR_ do
		local L13_152 = L13_152
		if _FORV_12_:IsValid() then
			({}).Height, ({}).Width = _FORV_12_:GetSize()
			table.insert(L2_141, {})
		end
	end
	L14_153 = YogaLayout
	L19_158 = L2_141
	L14_153 = L14_153(L19_158)
	L19_158 = ipairs
	L19_158, _FOR_, _FOR_ = L19_158(L14_153)
	for _FORV_12_, _FORV_13_ in L19_158, _FOR_, _FOR_ do
		if L1_140[_FORV_12_]:IsValid() and L1_140[_FORV_12_]:IsVisible() then
			L1_140[_FORV_12_]:SetAbsPos(_FORV_13_.Left, _FORV_13_.Right)
			L1_140[_FORV_12_]:CorrectPos()
		end
	end
end
function L11_11(A0_159, A1_160)
	if _ENV(A0_159, A1_160) then
		return
	end
	A0_159._YogaAutoPos_OnFrameShow = A0_159.OnFrameShow
	function A0_159.OnFrameShow()
		if _ENV._AutoPos_OnFrameShow then
			_ENV._AutoPos_OnFrameShow()
		end
		local L0_164 = L0_164
		L0_164 = L0_164(_ENV, A1_160)
		table.remove(L8_8[A1_160], L0_164)
		table.insert(L8_8[A1_160], _ENV)
		local L3_167 = L3_167
		L3_167 = L10_10
		L3_167(A1_160)
		local L2_166 = L2_166
	end
	A0_159._YogaAutoPos_OnFrameDestroy = A0_159.OnFrameDestroy
	function A0_159.OnFrameDestroy()
		if _ENV._YogaAutoPos_OnFrameDestroy then
			_ENV._YogaAutoPos_OnFrameDestroy()
		end
		local L0_168 = L0_168
		L0_168 = L0_168(_ENV, A1_160)
		table.remove(L8_8[A1_160], L0_168)
		local L3_171 = L3_171
		L3_171 = L10_10
		L3_171(A1_160)
		local L2_170 = L2_170
	end
	A0_159._YogaAutoPos_OnFrameHide = A0_159.OnFrameHide
	function A0_159.OnFrameHide()
		if _ENV._YogaAutoPos_OnFrameHide then
			_ENV._YogaAutoPos_OnFrameHide()
		end
		L10_10(A1_160)
		local L1_172 = L1_172
	end
	if not L8_8[A1_160] then
	end
	L8_8[A1_160] = {}
	table.insert(L8_8[A1_160], A0_159)
	local L4_163 = L4_163
	L4_163 = L10_10
	L4_163(A1_160)
	local L3_162 = L3_162
end
YogaAutoPos_Register = L11_11
function L11_11(A0_173)
	_ENV(A0_173)
	local L2_174 = L2_174
end
YogaAutoPos_Layout = L11_11
function L11_11()
	L2_177 = _ENV
	L0_175, L2_177, L3_178 = L0_175(L2_177)
	for _FORV_3_, _FORV_4_ in L0_175, L2_177, L3_178 do
		L10_10(_FORV_3_)
	end
end
YogaAutoPos_LayoutAll = L11_11
function L11_11()
	local L0_181 = IsMobileStreamingEnable()
	if not this or not L0_181 then
		return
	end
	if not this._AutoViewMutex then
	end
	this._AutoViewMutex = {}
	if not this._AutoViewMutex.bFirst then
		this._AutoViewMutex.bFirst = true
		if GetFrameSelf(this).OnFrameShow then
			this._AutoViewMutex.OnFrameShow = GetFrameSelf(this).OnFrameShow
		end
		function this.OnFrameShow()
			CheckUIViewMutex(this)
			_AddUIBackGround(this)
			local L1_185 = L1_185
			L1_185 = this
			L1_185 = L1_185._AutoViewMutex
			L1_185 = L1_185.OnFrameShow
			if L1_185 then
				L1_185 = this
				L1_185 = L1_185._AutoViewMutex
				L1_185 = L1_185.OnFrameShow
				return L1_185()
			end
		end
		local L1_182 = L1_182
		L1_182 = L1_182(this)
		L1_182 = L1_182.OnFrameHide
		if L1_182 then
			L1_182 = this
			L1_182 = L1_182._AutoViewMutex
			local L2_183 = L2_183
			local L2_183, L3_184 = L2_183(this), L3_184
			L2_183 = L2_183.OnFrameHide
			L1_182.OnFrameHide = L2_183
		end
		L1_182 = this
		function L2_183()
			CloseLevelUIView(this)
			_UnUIBackGround(this)
			local L1_186 = L1_186
			L1_186 = this
			L1_186 = L1_186._AutoViewMutex
			L1_186 = L1_186.OnFrameHide
			if L1_186 then
				L1_186 = this
				L1_186 = L1_186._AutoViewMutex
				L1_186 = L1_186.OnFrameHide
				return L1_186()
			end
		end
		L1_182.OnFrameHide = L2_183
	end
end
StreamingClientViewMutex_Register = L11_11
function L11_11(A0_187)
	local L9_196 = L9_196
	if not A0_187 then
		return
	end
	L9_196 = this
	L9_196 = L9_196.IsViewMutex
	L9_196 = L9_196(L9_196)
	if not L9_196 then
		return
	end
	local L2_189, L3_190 = A0_187:GetViewLevel(), L3_190
	L3_190 = _g_tbView
	if not L3_190 then
		L3_190 = {}
	end
	_g_tbView = L3_190
	L3_190 = _g_tbView
	L3_190 = L3_190[L2_189]
	if L3_190 and L3_190[1] then
		_FOR_ = 1
		for _FORV_9_ = _FOR_, _FOR_, _FOR_ do
			local L10_197 = L10_197
			local L11_198 = L11_198
			if L2_189 == 1 or L10_197 == L3_190[_FORV_9_]:GetViewMutexKey() then
				_CloseUIViewMutex(L3_190[_FORV_9_])
				L3_190[_FORV_9_] = nil
				bCloseUI = true
			end
		end
		L13_200 = _AddUIViewMutex
		L13_200(A0_187, L2_189)
		repeat
			do break end -- pseudo-goto
			L10_197 = _AddUIViewMutex
			L11_198 = A0_187
			L13_200 = L2_189
			L10_197(L11_198, L13_200)
		until true
	end
	L10_197 = FireUIEvent
	L11_198 = "CORRECT_AUTO_POS"
	L13_200 = A0_187.GetName
	L13_200 = L13_200(A0_187)
	L10_197(L11_198, L13_200, L13_200(A0_187))
	L10_197 = FireUIEvent
	L11_198 = "UI_SM_OPEN_LEVEL_PANEL"
	L13_200 = A0_187.GetName
	L13_200 = L13_200(A0_187)
	L10_197(L11_198, L13_200, L2_189)
	local L7_194 = L7_194
end
CheckUIViewMutex = L11_11
function L11_11(A0_201)
	local L1_202, L8_209 = A0_201:IsViewMutex(), L8_209
	if not L1_202 then
		return
	end
	L8_209 = A0_201.GetViewLevel
	L8_209 = L8_209(A0_201)
	if not #_g_tbView[L8_209] then
	end
	if _g_tbView and _g_tbView[L8_209] and 0 < 0 then
		_FOR_ = 1
		for _FORV_8_ = _FOR_, _FOR_, _FOR_ do
			if _g_tbView[L8_209][_FORV_8_] == A0_201 then
				_g_tbView[L8_209][_FORV_8_] = nil
				do break end
				local L9_210 = L9_210
			end
		end
	end
	L9_210 = A0_201.GetViewMutexKey
	L9_210 = L9_210(A0_201)
	local L4_205 = L4_205
	local L5_206 = L5_206
	L4_205(L5_206, L9_210)
	L4_205 = FireUIEvent
	L5_206 = "CORRECT_AUTO_POS"
	local L6_207, L7_208 = A0_201:GetName()
	L4_205(L5_206, L6_207, L7_208, A0_201:GetName())
end
CloseLevelUIView = L11_11
function L11_11(A0_211, A1_212)
	if not _g_tbView[A1_212] then
		_g_tbView[A1_212] = {}
	end
	local L2_213 = L2_213
	local L3_214 = L3_214
	L2_213(L3_214, A0_211)
	local L4_215 = L4_215
end
_AddUIViewMutex = L11_11
function L11_11(A0_216)
	repeat
		local L1_217 = L1_217
		L1_217 = L1_217(A0_216)
		if L1_217 then
			if L1_217.Close then
				L1_217.Close()
			elseif L1_217.OnHideView then
				L1_217.OnHideView()
			else
				local L4_220 = L4_220
				L4_220(A0_216:GetName())
				do break end -- pseudo-goto
				L4_220 = UILog
				L4_220("---_CloseUIViewMutex luaScript==nil---")
				local L3_219 = L3_219
			end
		end
	until true
end
_CloseUIViewMutex = L11_11
function L11_11(A0_221, A1_222)
	local L2_223, L3_224
	L2_223 = _g_tbView
	if not L2_223 then
		return
	end
	L2_223 = nil
	L3_224 = 1
	L6_227 = pairs
	L7_228 = _g_tbView
	L6_227, L7_228, L8_229 = L6_227(L7_228)
	for L9_230, L10_231 in L6_227, L7_228, L8_229 do
		if A0_221 < L9_230 then
			L3_224 = #L10_231
			L11_232 = 1
			L12_233 = L3_224
			_FOR_ = 1
			for _FORV_12_ = L11_232, L12_233, _FOR_ do
				L2_223 = L10_231[_FORV_12_]
				if L2_223 and A1_222 == L2_223:GetViewMutexKey() then
					_CloseUIViewMutex(L2_223)
					_g_tbView[L9_230][_FORV_12_] = nil
				end
			end
		end
	end
end
_CloseLastLevelUIView = L11_11
function L11_11(A0_236)
	if not A0_236 then
		return
	end
	local L1_237 = A0_236:IsAddBackGround()
	if not L1_237 then
		return
	end
	if not A0_236:GetParent() then
		return
	end
	local L2_238 = A0_236:GetParent():GetName()
	if L2_238 then
		local L3_239 = L3_239
		local L4_240 = L4_240
		local L5_241 = L5_241
		local L6_242 = L6_242
		local L7_243 = L7_243
		local L8_244 = L8_244
		L3_239(L4_240, L5_241, L6_242, L7_243, L8_244, A0_236)
		local L9_245 = L9_245
	end
end
_AddUIBackGround = L11_11
function L11_11(A0_246)
	if not A0_246 then
		return
	end
	local L1_247 = A0_246:IsAddBackGround()
	if not L1_247 then
		return
	end
	local L2_248 = A0_246:GetParent():GetName()
	if L2_248 then
		local L3_249 = L3_249
		L3_249(L2_248)
		local L4_250 = L4_250
	end
end
_UnUIBackGround = L11_11
L11_11 = {}
;({}).szGroup = "szGroup"
;({}).nGap = 2
local L12_12 = L12_12
local L13_13 = L13_13
local L14_14 = L14_14
local L15_15 = L15_15
local L16_16 = L16_16
L13_13[1] = L14_14
L13_13[2] = L15_15
L13_13[3] = L16_16
L13_13[4] = "Normal/PendantPanel"
L13_13[5] = "Normal/ClothingPendantPanel"
local L13_13[6], L17_17 = "Normal/EquipRecommend", L17_17
L12_12.tbUIViewName = L13_13
L11_11.CharacterPanel = L12_12
UIViewCenterPosConfig = L11_11
function L11_11(A0_251)
	if not UIViewCenterPosConfig or not UIViewCenterPosConfig[A0_251] then
		return
	end
	if not _g_tbUIViewCenterPos then
	end
	_g_tbUIViewCenterPos = {}
	if not _g_tbUIViewCenterPos.tbGroupKey then
	end
	_g_tbUIViewCenterPos.tbGroupKey = {}
	_g_tbUIViewCenterPos.tbGroupKey[A0_251] = 1
	_SetUIViewCenterPos()
	local L1_252 = L1_252
	L1_252 = _g_tbUIViewCenterPos
	L1_252 = L1_252.register_fun_ref
	if not L1_252 then
		L1_252 = _g_tbUIViewCenterPos
		local L2_253 = L2_253
		local L3_254 = L3_254
		local L2_253, L4_255 = L2_253(L3_254, _SetUIViewCenterPos), L4_255
		L1_252.register_fun_ref = L2_253
	end
end
RegisterMoreUIViewCenterPos = L11_11
function L11_11()
	_SetUIViewCenterPos()
end
OnUIViewCenterPos = L11_11
function L11_11(A0_256)
	local L1_257
	L1_257 = _g_tbUIViewCenterPos
	local L6_262 = L6_262
	if L1_257 then
		L1_257 = _g_tbUIViewCenterPos
		L1_257 = L1_257.tbGroupKey
		if L1_257 then
			L1_257 = _g_tbUIViewCenterPos
			L1_257 = L1_257.tbGroupKey
			L1_257 = L1_257[A0_256]
			if L1_257 then
				goto lbl_16
			end
		end
	end
	L1_257 = nil
	_g_tbUIViewCenterPos = L1_257
	do return end
	::lbl_16::
	L1_257 = UIViewCenterPosConfig
	L1_257 = L1_257[A0_256]
	if L1_257 then
		L6_262 = 0
		L7_263 = pairs
		L7_263, _FOR_, _FOR_ = L7_263(L1_257.tbUIViewName)
		for _FORV_6_, _FORV_7_ in L7_263, _FOR_, _FOR_ do
			if _IsShowUIView(_FORV_7_) then
				L6_262 = L6_262 + 1
			end
		end
		if L6_262 == 0 then
			L7_263 = _g_tbUIViewCenterPos
			L7_263 = L7_263.tbGroupKey
			L7_263[A0_256] = nil
		end
	end
	L6_262 = next
	L7_263 = _g_tbUIViewCenterPos
	L7_263 = L7_263.tbGroupKey
	L6_262 = L6_262(L7_263)
	if not L6_262 then
		L6_262 = UnRegisterEvent
		L7_263 = "UI_SCALED"
		L9_265 = _SetUIViewCenterPos
		L6_262(L7_263, L9_265)
		L6_262 = nil
		_g_tbUIViewCenterPos = L6_262
	end
end
UnRegisterMoreUIViewCenterPos = L11_11
function L11_11()
	local L0_266
	L0_266 = _g_tbUIViewCenterPos
	if L0_266 then
		L0_266 = _g_tbUIViewCenterPos
		L0_266 = L0_266.tbGroupKey
		if L0_266 then
			goto lbl_9
		end
	end
	do return end
	::lbl_9::
	L0_266 = nil
	L3_269 = pairs
	L3_269, L2_268, _FOR_ = L3_269(_g_tbUIViewCenterPos.tbGroupKey)
	for _FORV_4_, _FORV_5_ in L3_269, L2_268, _FOR_ do
		L0_266 = UIViewCenterPosConfig[_FORV_4_]
		if L0_266 then
			_SetUIViewPos(L0_266.tbUIViewName, L0_266.nGap)
		end
	end
end
_SetUIViewCenterPos = L11_11
function L11_11(A0_273)
	local L1_274 = L1_274
	L1_274 = L1_274(A0_273)
	if L1_274 then
		local L2_275, L3_276 = L1_274:IsVisible(), L3_276
	end
	return L2_275
end
_IsShowUIView = L11_11
function L11_11(A0_277, A1_278)
	local L2_279
	local L16_293, L21_298, L22_299 = L16_293, L21_298, L22_299
	if not A0_277 then
		return
	end
	if not A1_278 then
		A1_278 = 0
	end
	L2_279 = {}
	L6_283 = pairs
	L12_289 = A0_277
	L6_283, L12_289, L13_290 = L6_283(L12_289)
	for L14_291, L15_292 in L6_283, L12_289, L13_290 do
		L16_293 = Station
		L16_293 = L16_293.Lookup
		L21_298 = L15_292
		L16_293 = L16_293(L21_298)
		if L16_293 then
			L22_299 = L16_293
			L21_298 = L16_293.IsVisible
			L21_298 = L21_298(L22_299)
			if L21_298 then
				L21_298 = table
				L21_298 = L21_298.insert
				L22_299 = L2_279
				;({}).hFrame = L16_293
				;({}).szPanelPath = L15_292
				L21_298(L22_299, {})
			end
		end
	end
	L6_283 = #L2_279
	if L6_283 < 1 then
		return
	end
	L12_289 = L2_279[1]
	L12_289 = L12_289.hFrame
	L13_290 = Station
	L13_290 = L13_290.GetClientSize
	L13_290, L14_291 = L13_290()
	L13_290 = L13_290 / 2
	L14_291 = L14_291 / 2
	L16_293 = L12_289
	L15_292 = L12_289.GetAbsX
	L15_292 = L15_292(L16_293)
	L21_298 = L12_289
	L16_293 = L12_289.GetAbsY
	L16_293 = L16_293(L21_298)
	L22_299 = L12_289
	L21_298 = L12_289.GetSize
	L21_298, L22_299 = L21_298(L22_299)
	_FOR_ = 1
	for _FORV_18_ = _FOR_, _FOR_, _FOR_ do
		if L2_279[_FORV_18_] and 2 < _FORV_18_ then
		end
	end
	local L18_295 = L18_295
	local L19_296 = L19_296
	L12_289:SetAbsPos(L15_292 + (L13_290 - (L15_292 + L21_298 / 2 + (0 + L2_279[_FORV_18_].hFrame:GetW() + A1_278) / 2)), L16_293 + (L14_291 - (L16_293 + L22_299 / 2)))
	local L20_297 = L20_297
	_FOR_ = 1
	for _FORV_22_ = _FOR_, _FOR_, _FOR_ do
		if L2_279[_FORV_22_] and L2_279[_FORV_22_ - 1] then
			local L23_300, L24_301 = L23_300, L24_301
			local L25_302 = L25_302
			local L26_303 = L26_303
			local L27_304 = L27_304
			L2_279[_FORV_22_].hFrame:SetPoint("TOPLEFT", 0, 0, L2_279[_FORV_22_ - 1].szPanelPath, "TOPRIGHT", A1_278, 0)
		end
	end
end
_SetUIViewPos = L11_11
function L11_11(A0_309, A1_310)
	L4_313 = A0_309.tSub
	L2_311, L4_313, L7_314 = L2_311(L4_313)
	for _FORV_5_, _FORV_6_ in L2_311, L4_313, L7_314 do
		if _FORV_6_.szName == A1_310 then
			return _FORV_6_
		end
	end
end
function L12_12(A0_315, A1_316)
	local L2_317
	L2_317 = 0
	if A1_316.fnGetWidth then
		L2_317 = A1_316.fnGetWidth()
	else
		local L3_318, L4_319 = A0_315:GetW(), L4_319
		L2_317 = L3_318
	end
	return L2_317
end
function L13_13(A0_320)
	local L1_321
	L1_321 = A0_320.tMainInfo
	local L6_326 = L1_321.fnGetStartPos
	if L6_326 then
		L6_326 = L1_321.fnGetStartPos
		L6_326 = L6_326()
		if L6_326 then
			goto lbl_11
		end
	end
	L7_327 = A0_320
	L6_326 = A0_320.GetW
	L6_326 = L6_326(L7_327)
	::lbl_11::
	L7_327 = ipairs
	L7_327, _FOR_, _FOR_ = L7_327(A0_320.tSub)
	for _FORV_6_, _FORV_7_ in L7_327, _FOR_, _FOR_ do
		if _FORV_7_.fnIsOpened and _FORV_7_.fnIsOpened() then
			L6_326 = L6_326 + _ENV(_FORV_7_.fnGetFrame(), _FORV_7_)
		end
	end
	L8_328 = A0_320
	L7_327 = A0_320.SetW
	L10_330 = L6_326
	L7_327(L8_328, L10_330)
	L7_327 = CorrectAutoPosFrameAfterClientResize
	L7_327()
end
L14_14 = {}
L15_15 = 2
function L16_16(A0_332, A1_333)
	local L2_334
	if A1_333 < 0 then
		return
	end
	L2_334 = _ENV
	L2_334 = #L2_334
	if L2_334 > L15_15 then
		L2_334 = _ENV
		L2_334 = L2_334[A1_333]
		local L3_335 = L3_335
		L3_335 = L3_335(A0_332, L2_334)
		if L3_335.fnIsOpened and L3_335.fnIsOpened() and L3_335.fnClose then
			L3_335.fnClose()
		else
			local L4_336 = L4_336
			L4_336(A1_333 + 1)
			local L5_337 = L5_337
		end
	end
end
function L17_17(A0_338, A1_339)
	local L2_340, L3_341
	if not L2_340 then
		return
	end
	L3_341 = false
	_FOR_, _FOR_, _FOR_ = ipairs(L2_340)
	for _FORV_7_, _FORV_8_ in _FOR_, _FOR_, _FOR_ do
		if _FORV_8_ == A1_339 then
			L3_341 = true
			break
		end
	end
	if not L3_341 then
		table.insert(L2_340, A1_339)
		local L4_342 = L4_342
		local L5_343 = L5_343
		L4_342(L5_343, 1)
		local L6_344 = L6_344
	end
end
function RegisterSubPanel(A0_345, A1_346, A2_347)
	local L3_348
	function L3_348(A0_358)
		local L1_359
		repeat
			L1_359 = this
			local L10_368, L11_369 = GetFrameSelf, L11_369
			L11_369 = this
			L10_368 = L10_368(L11_369)
			if not L10_368 then
				return
			end
			if A0_358 == "CORRECT_AUTO_POS" then
				L11_369 = arg0
				if L11_369 ~= L1_359:GetName() then
					return
				end
				L11_369 = L1_359.tMainInfo
				if L11_369 then
					L11_369 = L1_359.tSub
					if L11_369 then
						goto lbl_23
					end
				end
				do return end
				::lbl_23::
				L11_369 = L1_359.tMainInfo
				_FOR_, _FOR_, _FOR_ = ipairs(L1_359.tSub)
				local L7_365, L8_366, L9_367 = L7_365, L8_366, L9_367
				for _FORV_10_, _FORV_11_ in _FOR_, _FOR_, _FOR_ do
					if _FORV_11_.fnIsOpened and _FORV_11_.fnIsOpened() then
						local L20_378 = L20_378
						_FORV_11_.fnGetFrame():SetPoint("TOPLEFT", 0, 0, L11_369.fnGetPath(), "TOPLEFT", L8_366, L9_367)
						local L21_379 = L21_379
						L21_379 = L20_378.BringToTop
						L21_379(L20_378)
						L21_379 = _ENV
						L21_379 = L21_379(L20_378, L7_365)
						L8_366 = L8_366 + L21_379
					end
				end
				break -- pseudo-goto
			end
			if A0_358 == "OPEN_SUB_PANEL" then
				L11_369 = arg0
				L8_366 = L1_359
				L7_365 = L1_359.GetName
				L7_365 = L7_365(L8_366)
				if L11_369 ~= L7_365 then
					return
				end
				L11_369 = L17_17
				L7_365 = this
				L8_366 = arg1
				L11_369(L7_365, L8_366)
				L11_369 = L13_13
				L7_365 = this
				L11_369(L7_365)
			elseif A0_358 == "CLOSE_SUB_PANEL" then
				L11_369 = arg0
				L8_366 = L1_359
				L7_365 = L1_359.GetName
				L7_365 = L7_365(L8_366)
				if L11_369 ~= L7_365 then
					return
				end
				L11_369 = _UPVALUE3_
				L7_365 = this
				L8_366 = arg1
				L11_369(L7_365, L8_366)
				L11_369 = L13_13
				L7_365 = this
				L11_369(L7_365)
			elseif A0_358 == "UPDATE_FRAME_SIZE" then
				L11_369 = arg0
				L8_366 = L1_359
				L7_365 = L1_359.GetName
				L7_365 = L7_365(L8_366)
				if L11_369 ~= L7_365 then
					return
				end
				L11_369 = L13_13
				L7_365 = this
				L11_369(L7_365)
			end
		until true
	end
	local L8_353 = L8_353
	L9_354 = A0_345
	L8_353 = A0_345.RegisterEvent
	L10_355 = "CORRECT_AUTO_POS"
	L11_356 = L3_348
	L8_353(L9_354, L10_355, L11_356)
	L9_354 = A0_345
	L8_353 = A0_345.RegisterEvent
	L10_355 = "OPEN_SUB_PANEL"
	L11_356 = L3_348
	L8_353(L9_354, L10_355, L11_356)
	L9_354 = A0_345
	L8_353 = A0_345.RegisterEvent
	L10_355 = "CLOSE_SUB_PANEL"
	L11_356 = L3_348
	L8_353(L9_354, L10_355, L11_356)
	L9_354 = A0_345
	L8_353 = A0_345.RegisterEvent
	L10_355 = "UPDATE_FRAME_SIZE"
	L11_356 = L3_348
	L8_353(L9_354, L10_355, L11_356)
	L8_353 = {}
	A0_345.tOpenOrder = L8_353
	A0_345.tMainInfo = A1_346
	A0_345.tSub = A2_347
	L9_354 = A0_345
	L8_353 = A0_345.GetName
	L8_353 = L8_353(L9_354)
	L9_354 = ipairs
	L10_355 = A2_347
	L9_354, L10_355, L11_356 = L9_354(L10_355)
	for L12_357, _FORV_9_ in L9_354, L10_355, L11_356 do
		_G[_FORV_9_.szName].fnOldOnFrameCreate = _G[_FORV_9_.szName].OnFrameCreate
		_G[_FORV_9_.szName].OnFrameCreate = function()
			local L2_382 = L2_382
			L2_382 = L2_382[_ENV]
			L2_382 = L2_382.fnOldOnFrameCreate
			if L2_382 then
				L2_382 = _G
				L2_382 = L2_382[_ENV]
				L2_382 = L2_382.fnOldOnFrameCreate
				L2_382()
			end
			L2_382 = this
			L2_382 = L2_382.BringToTop
			L2_382(L2_382)
			L2_382 = DelayCall
			L2_382(1, function()
				local L1_383 = L1_383
				local L2_384 = L2_384
				L1_383(L2_384, _ENV, _ENV)
				local L3_385 = L3_385
			end)
		end
		_G[_FORV_9_.szName].fnOldOnFrameShow = _G[_FORV_9_.szName].OnFrameShow
		_G[_FORV_9_.szName].OnFrameShow = function()
			if _G[_ENV].fnOldOnFrameShow then
				_G[_ENV].fnOldOnFrameHide()
			end
			local L1_386 = L1_386
			local L2_387 = L2_387
			L1_386(L2_387, L4_349, _ENV)
			local L3_388 = L3_388
		end
		_G[_FORV_9_.szName].fnOldOnFrameDestroy = _G[_FORV_9_.szName].OnFrameDestroy
		_G[_FORV_9_.szName].OnFrameDestroy = function()
			local L2_391 = L2_391
			L2_391 = L2_391[_ENV]
			L2_391 = L2_391.fnOldOnFrameDestroy
			if L2_391 then
				L2_391 = _G
				L2_391 = L2_391[_ENV]
				L2_391 = L2_391.fnOldOnFrameDestroy
				L2_391()
			end
			L2_391 = DelayCall
			L2_391(1, function()
				local L1_392 = L1_392
				local L2_393 = L2_393
				L1_392(L2_393, _ENV, _ENV)
				local L3_394 = L3_394
			end)
		end
		_G[_FORV_9_.szName].fnOldOnFrameHide = _G[_FORV_9_.szName].OnFrameHide
		_G[_FORV_9_.szName].OnFrameHide = function()
			if _G[_ENV].fnOldOnFrameHide then
				_G[_ENV].fnOldOnFrameHide()
			end
			local L1_395 = L1_395
			local L2_396 = L2_396
			L1_395(L2_396, L4_349, _ENV)
			local L3_397 = L3_397
		end
	end
	function L9_354()
		local L2_400 = L2_400
		L2_400(10, function()
			L2_403 = _ENV
			L0_401, L2_403, L3_404 = L0_401(L2_403)
			for L4_405, _FORV_4_ in L0_401, L2_403, L3_404 do
				if _FORV_4_.fnIsOpened and _FORV_4_.fnIsOpened() then
					_FORV_4_.fnGetFrame():BringToTop()
					local L6_407, L7_408 = L6_407, L7_408
				end
			end
		end)
		L2_400 = 2
		return L2_400
	end
	A0_345.OnSetFocus = L9_354
	function L9_354()
		local L0_409
		L0_409 = this
		local L0_409, L8_417 = L0_409.tSub, L8_417
		L3_412 = ipairs
		L4_413 = L0_409
		L3_412, L4_413, L5_414 = L3_412(L4_413)
		for L6_415, L7_416 in L3_412, L4_413, L5_414 do
			L8_417 = L7_416.szName
			if _G[L8_417].OnFrameCreate then
				_G[L8_417].OnFrameCreate = _G[L8_417].fnOldOnFrameCreate
				_G[L8_417].fnOldOnFrameCreate = nil
			end
			if _G[L8_417].OnFrameShow then
				_G[L8_417].OnFrameShow = _G[L8_417].fnOldOnFrameShow
				_G[L8_417].fnOldOnFrameShow = nil
			end
			if _G[L8_417].OnFrameDestroy then
				_G[L8_417].OnFrameDestroy = _G[L8_417].fnOldOnFrameDestroy
				_G[L8_417].fnOldOnFrameDestroy = nil
			end
			if _G[L8_417].OnFrameHide then
				_G[L8_417].OnFrameHide = _G[L8_417].fnOldOnFrameHide
				_G[L8_417].fnOldOnFrameHide = nil
			end
		end
		L3_412 = 2
		return L3_412
	end
	A0_345.OnFrameDestroy = L9_354
end
