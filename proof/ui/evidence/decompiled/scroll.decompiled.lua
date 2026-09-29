local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L19_19, L20_20, L21_21, L22_22, L23_23, L24_24, L25_25, L26_26, L27_27, L28_28, L29_29, L30_30, L31_31, L32_32, L33_33
L0_0 = {}
local L1_1, L36_36 = {}, L36_36
L2_2 = {}
L3_3 = 2000
L4_4 = 3.5
L5_5 = 0.5
L6_6 = 2
function L7_7(A0_37, A1_38, A2_39)
	local L3_40
	local L7_44 = L7_44
	if not A1_38 or not A2_39 then
		return
	end
	L3_40 = A1_38.bInit
	if L3_40 then
		return
	end
	A1_38.bInit = true
	L3_40 = {}
	A1_38.tData = L3_40
	A1_38.bOnPanGesture = false
	L3_40 = A1_38.tData
	L3_40.hList = A2_39
	L7_44 = A2_39.GetSize
	L3_40.nWndH, L7_44 = L7_44(A2_39)
	_ = L7_44
	L7_44 = A1_38.GetScrollPos
	L7_44 = L7_44(A1_38)
	local L5_42, L6_43 = A1_38:GetStepCount(), L6_43
	L3_40.nStepCnt = L5_42
	L6_43 = A0_37.tList
	L6_43 = L6_43[3]
	L3_40.nStepSize = L6_43
	L6_43 = -L7_44
	L6_43 = L6_43 * L3_40.nStepSize
	L3_40.nRelPos = L6_43
	L6_43 = L3_40.nStepSize
	L6_43 = L5_42 * L6_43
	L3_40.nLength = L6_43
	L3_40.bScrolling = false
	L3_40.nDistance = 0
	L3_40.bCanBeStretched = false
	L3_40.nMotionTime = 0
	L6_43 = _ENV
	L3_40.nElasticity = L6_43
	L3_40.nCoeFromElogation = 0
	L3_40.nCoeFromSlide = 0
	L3_40.nStaFromElogation = 0
	L3_40.nStaFromSlide = 0
	L3_40.nElongationRatio = 0
end
function L8_8(A0_45, A1_46)
	local L2_47
	if 0 < A0_45 and A1_46 < A0_45 then
		return A1_46
	end
	if A0_45 < 0 then
		L2_47 = -A1_46
		if A0_45 < L2_47 then
			L2_47 = -A1_46
			return L2_47
		end
	end
	return A0_45
end
function L9_9(A0_48, A1_49)
	local L2_50
	if 0 < A0_48 and A0_48 < A1_49 then
		return A1_49
	end
	if A0_48 < 0 then
		L2_50 = -A1_49
		if A0_48 > L2_50 then
			L2_50 = -A1_49
			return L2_50
		end
	end
	return A0_48
end
function L10_10(A0_51, A1_52)
	local L2_53
	L2_53 = A1_52 * A1_52
	L2_53 = A0_51 / L2_53
	return L2_53
end
function L11_11(A0_54, A1_55)
	local L2_56, L3_57
	L2_56 = 3 * A0_54
	L3_57 = A1_55 * A1_55
	L3_57 = L3_57 * A1_55
	L2_56 = L2_56 / L3_57
	return L2_56
end
function L12_12(A0_58, A1_59, A2_60)
	local L3_61, L4_62
	if A2_60 <= A0_58 then
		L3_61 = 0
		return L3_61
	end
	L3_61 = A0_58 - A2_60
	L3_61 = A1_59 * L3_61
	L4_62 = A0_58 - A2_60
	L3_61 = L3_61 * L4_62
	return L3_61
end
function L13_13(A0_63, A1_64, A2_65, A3_66)
	if A1_64 <= A0_63 or A3_66 <= A0_63 then
		return 0
	end
	local L4_67 = L4_67
	local L5_68 = L5_68
	local L4_67, L6_69 = L4_67(L5_68, A3_66), L6_69
	A1_64 = L4_67
	L4_67 = 0.3333333333333333 * A2_65
	L4_67 = L4_67 * A1_64
	L4_67 = L4_67 * A1_64
	L4_67 = L4_67 * A1_64
	L5_68 = A2_65 * A3_66
	L5_68 = L5_68 * A1_64
	L5_68 = L5_68 * A1_64
	L4_67 = L4_67 - L5_68
	L5_68 = A2_65 * A3_66
	L5_68 = L5_68 * A3_66
	L5_68 = L5_68 * A1_64
	L4_67 = L4_67 + L5_68
	L5_68 = 0.3333333333333333 * A2_65
	L5_68 = L5_68 * A0_63
	L5_68 = L5_68 * A0_63
	L5_68 = L5_68 * A0_63
	L6_69 = A2_65 * A3_66
	L6_69 = L6_69 * A0_63
	L6_69 = L6_69 * A0_63
	L5_68 = L5_68 - L6_69
	L6_69 = A2_65 * A3_66
	L6_69 = L6_69 * A3_66
	L6_69 = L6_69 * A0_63
	L5_68 = L5_68 + L6_69
	L4_67 = L4_67 - L5_68
	return L4_67
end
function L14_14(A0_70)
	if not A0_70 then
		return
	end
	tData = A0_70.tData
	if not tData then
		return
	end
	if tData.nRelPos > 0 then
		return _ENV(tData.nRelPos * tData.nElasticity / tData.nWndH, 1)
	end
	if tData.nRelPos < -tData.nLength then
		local L1_71 = L1_71
		local L2_72 = L2_72
		do return L1_71(L2_72, 1) end
		local L3_73 = L3_73
	end
	L1_71 = 0
	return L1_71
end
function L15_15(A0_74)
	local L1_75
	if not A0_74 then
		return
	end
	L1_75 = A0_74.tData
	tData = L1_75
	L1_75 = tData
	if not L1_75 then
		return
	end
	L1_75 = tData
	L1_75.nElongationRatio = _ENV(A0_74)
	L1_75 = tData
	L1_75 = L1_75.nElongationRatio
	if L1_75 == 0 then
		L1_75 = tData
		L1_75.nStaFromElogation = 0
		L1_75 = tData
		L1_75.nCoeFromElogation = 0
		return
	end
	L1_75 = tData
	L1_75.nStaFromElogation = math.abs(tData.nElongationRatio * L5_5)
	L1_75 = tData
	local L2_76 = L2_76
	L2_76 = L2_76(tData.nStaFromElogation, 0.5 * L5_5)
	L1_75.nStaFromElogation = L2_76
	L1_75 = tData
	L1_75 = L1_75.nElongationRatio
	L1_75 = -L1_75
	L2_76 = tData
	L2_76 = L2_76.nWndH
	L1_75 = L1_75 * L2_76
	L2_76 = tData
	L2_76 = L2_76.nElasticity
	L1_75 = L1_75 / L2_76
	L2_76 = tData
	local L3_77 = L3_77
	local L4_78 = L4_78
	local L3_77, L5_79 = L3_77(L4_78, tData.nStaFromElogation), L5_79
	L2_76.nCoeFromElogation = L3_77
	return
end
function L16_16(A0_80)
	if not A0_80 then
		return
	end
	tData = A0_80.tData
	if not tData then
		return
	end
	tData.bScrolling = true
	tData.nMotionTime = 0
	if not tData.nSpeed then
		tData.nSpeed = 0
	end
	if not tData.bCanBeStretched then
		if 0 < tData.nSpeed and 0 <= tData.nRelPos then
			tData.nStaFromSlide = 0
			tData.nCoeFromSlide = 0
			return
		end
		if 0 > tData.nSpeed and tData.nRelPos <= -tData.nLength then
			tData.nStaFromSlide = 0
			tData.nCoeFromSlide = 0
			return
		end
	else
		local L1_81 = L1_81
		L1_81(A0_80)
		L1_81 = tData
		L1_81 = L1_81.nSpeed
		L1_81 = L1_81 * tData.nElongationRatio
		if 0 < L1_81 then
			L1_81 = tData
			L1_81.nStaFromSlide = 0
			L1_81 = tData
			L1_81.nCoeFromSlide = 0
			return
		end
	end
	L1_81 = tData
	L1_81 = L1_81.nSpeed
	if not L1_81 then
		L1_81 = tData
		L1_81.nStaFromSlide = 0
		L1_81 = tData
		L1_81.nCoeFromSlide = 0
		return
	end
	L1_81 = tData
	L1_81 = L1_81.nSpeed
	L1_81 = L1_81 * tData.nSpeed
	L1_81 = L1_81 / (L3_3 * L3_3)
	tData.nStaFromSlide = math.abs(L1_81 * L4_4)
	tData.nStaFromSlide = L9_9(tData.nStaFromSlide, 0.5 * L4_4)
	tData.nCoeFromSlide = L10_10(tData.nSpeed, tData.nStaFromSlide)
	if not tData.bCanBeStretched then
		local L2_82 = L2_82
		local L3_83 = L3_83
		local L4_84 = L4_84
		local L5_85 = L5_85
		local L2_82, L6_86 = L2_82(L3_83, L4_84, L5_85, tData.nStaFromSlide), L6_86
		L3_83 = tData
		L3_83 = L3_83.nRelPos
		L3_83 = L2_82 + L3_83
		if 0 < L3_83 then
			L3_83 = 0
		end
		L4_84 = tData
		L4_84 = L4_84.nLength
		L4_84 = -L4_84
		if L3_83 < L4_84 then
			L4_84 = tData
			L4_84 = L4_84.nLength
			L3_83 = -L4_84
		end
		L4_84 = tData
		L4_84 = L4_84.nRelPos
		L2_82 = L3_83 - L4_84
		L4_84 = tData
		L5_85 = tData
		L5_85 = L5_85.nSpeed
		L6_86 = tData
		L6_86 = L6_86.nSpeed
		L5_85 = L5_85 * L6_86
		L6_86 = tData
		L6_86 = L6_86.nSpeed
		L5_85 = L5_85 * L6_86
		L6_86 = 9 * L2_82
		L6_86 = L6_86 * L2_82
		L5_85 = L5_85 / L6_86
		L4_84.nCoeFromSlide = L5_85
		L4_84 = tData
		L5_85 = 3 * L2_82
		L6_86 = tData
		L6_86 = L6_86.nSpeed
		L5_85 = L5_85 / L6_86
		L4_84.nStaFromSlide = L5_85
	end
	return
end
function L17_17(A0_87, A1_88)
	local L2_89, L3_90, L4_91, L5_92
	if not A0_87 then
		return
	end
	L2_89 = A0_87.tData
	tData = L2_89
	L2_89 = tData
	if L2_89 then
		L2_89 = tData
		L2_89 = L2_89.bScrolling
		if L2_89 then
			goto lbl_14
		end
	end
	do return end
	::lbl_14::
	L2_89 = tData
	L2_89 = L2_89.nMotionTime
	L3_90 = A1_88 / 1000
	L2_89 = L2_89 + L3_90
	L3_90 = 0
	L4_91 = 0
	L5_92 = 0
	local L6_93 = L6_93
	L6_93 = L6_93(tData.nMotionTime, tData.nCoeFromElogation, tData.nStaFromElogation)
	local L7_94 = L7_94
	L7_94 = L7_94(tData.nMotionTime, tData.nCoeFromSlide, tData.nStaFromSlide)
	local L8_95 = L8_95
	L8_95 = L8_95(A0_87)
	if L8_95 then
		if L6_93 then
			if L2_89 < tData.nStaFromElogation then
				L3_90 = L13_13(tData.nMotionTime, L2_89, tData.nCoeFromElogation, tData.nStaFromElogation)
			else
				L3_90 = -L8_95 * tData.nWndH / tData.nElasticity
			end
		else
			if not L7_94 then
				L16_16(A0_87)
				return
			end
			L15_15(A0_87)
			local L14_101 = L14_101
			L3_90 = L14_101 * L13_13(0, A1_88 / 1000, tData.nCoeFromElogation, tData.nStaFromElogation)
			L14_101 = tData
			L14_101.nCoeFromElogation = 0
			L14_101 = tData
			L14_101.nStaFromElogation = 0
		end
	end
	L5_92 = L5_92 + L3_90
	if L7_94 then
		L14_101 = L13_13
		L14_101 = L14_101(tData.nMotionTime, L2_89, tData.nCoeFromSlide, tData.nStaFromSlide)
		L4_91 = L14_101
		L5_92 = L5_92 + L4_91
		L14_101 = L5_92 * L4_91
		if L14_101 < 0 then
			L14_101 = tData
			L14_101.nCoeFromSlide = 0
			L14_101 = tData
			L14_101.nStaFromSlide = 0
		end
	end
	if L5_92 == 0 or L5_92 ~= L5_92 then
		L14_101 = tData
		L14_101.bScrolling = false
		return
	end
	L14_101 = tData
	L14_101 = L14_101.nRelPos
	L14_101 = L14_101 + L5_92
	if not tData.bCanBeStretched then
		if 0 < L14_101 then
			L14_101 = 0
		end
		if L14_101 < -tData.nLength then
			L14_101 = -tData.nLength
		end
	end
	tData.nRelPos = L14_101
	if tData.hList then
		tData.hList:SetItemStartRelPos(0, tData.nRelPos)
		local L10_97 = L10_97
		L10_97 = L10_97(-tData.nRelPos / tData.nStepSize + 0.5)
		if L10_97 > tData.nStepCnt then
			L10_97 = tData.nStepCnt
		end
		if L10_97 < 0 then
			L10_97 = 0
		end
		local L11_98, L12_99 = L11_98, L12_99
		L11_98(L12_99, L10_97)
		local L13_100 = L13_100
	end
	L10_97 = tData
	L10_97.nMotionTime = L2_89
end
L18_18 = {}
L19_19 = nil
L20_20 = 0
function L21_21()
	local L0_102, L1_103, L2_104 = GetTickCount(), L1_103, L2_104
	L1_103 = _ENV
	L1_103 = L0_102 - L1_103
	_ENV = L0_102
	L2_104 = L18_18
	L2_104 = #L2_104
	L6_108 = L2_104
	_FOR_ = -1
	for _FORV_6_ = L6_108, _FOR_, _FOR_ do
		L17_17(L18_18[L2_104], L1_103)
		if not L18_18[L2_104] or not L18_18[L2_104].tData.bScrolling then
			table.remove(L18_18, L2_104)
			local L10_112 = L10_112
		end
	end
	L6_108 = L18_18
	L6_108 = #L6_108
	if not L6_108 then
		L6_108 = UnRegisterEvent
		L7_109 = "RENDER_FRAME_UPDATE"
		L8_110 = L19_19
		L6_108(L7_109, L8_110)
		L6_108 = nil
		L19_19 = L6_108
	end
end
function L22_22(A0_113)
	table.insert(_ENV, A0_113)
	if not L19_19 then
		L20_20 = GetTickCount()
		local L2_114 = L2_114
		local L2_114, L3_115 = L2_114("RENDER_FRAME_UPDATE", L21_21), L3_115
		L19_19 = L2_114
	end
end
function L23_23(A0_116)
	local L1_117
	L1_117 = 1
	while true do
		local L5_121 = string.find(A0_116, "/", L1_117)
		if not L5_121 then
			break
		end
		L1_117 = L5_121 + 1
	end
	L5_121 = string
	L5_121 = L5_121.sub
	local L3_119 = L3_119
	do return L5_121(L3_119, L1_117) end
	local L4_120 = L4_120
end
function L24_24(A0_122, A1_123)
	local L2_124, L3_125, L4_126
	L2_124 = _ENV
	local L2_124, L7_129 = L2_124[A0_122], L7_129
	if not L2_124 then
		return
	end
	L2_124 = nil
	L3_125 = _ENV
	L3_125 = L3_125[A0_122]
	L3_125 = L3_125[A1_123]
	L4_126 = L3_125
	L7_129 = type
	local L7_129, L6_128 = L7_129(L3_125), L6_128
	if L7_129 == "table" then
		L4_126 = L3_125.nIndex
		L2_124 = L3_125.type
	end
	L7_129 = L0_0
	L7_129 = L7_129[A0_122]
	L7_129 = L7_129[L4_126]
	L6_128 = L7_129
	return L6_128, L2_124
end
function L25_25(A0_130, A1_131, A2_132)
	local L3_133 = L3_133
	L3_133 = L3_133(A1_131, A0_130)
	if not L3_133 then
		return
	end
	local L4_134 = L4_134
	L4_134 = L4_134(L1_1[A1_131].szFramePath)
	local L5_135 = L5_135
	L5_135 = L5_135(L4_134, L3_133.tList[1], L3_133.tList[2])
	if not L5_135 then
		L5_135 = L4_134:Lookup(L3_133.tList[1], "")
		if L5_135:GetName() ~= L3_133.tList[2] then
			Log("lua[error] scroll.lua frame " .. L4_134:GetName() .. " can not Lookup " .. L3_133.tList[2])
			return
		end
	end
	L5_135:FormatAllItemPos()
	local L6_136 = L6_136
	function L6_136(A0_146, A1_147)
		if A0_146 and A0_146 ~= "" then
			local L2_148 = L2_148
			L2_148 = L2_148(_ENV, A0_146)
			if not L2_148 then
				return
			end
			if A1_147 then
				L2_148:Show()
			else
				L2_148:Hide()
				local L3_149, L4_150 = L3_149, L4_150
			end
		end
	end
	local L7_137 = L7_137
	L7_137 = L7_137(L4_134, L3_133.szScroll)
	local L8_138, L9_139 = L5_135:GetSize()
	local L10_140, L11_141 = L5_135:GetAllItemSize()
	local L12_142 = L12_142
	L12_142 = L12_142((L11_141 - L9_139) / L3_133.tList[3])
	L7_137:SetStepCount(L12_142)
	if L7_137.bInit then
		L7_137.tData.nStepSize = L3_133.tList[3]
		L7_137.tData.nLength = L12_142 * L7_137.tData.nStepSize
		L7_137.tData.hList = L5_135
		L7_137.tData.nStepCnt = L12_142
	end
	if 0 < L12_142 then
		L7_137:Show()
		L6_136(L3_133.szBtnUp, true)
		L6_136(L3_133.szBtnDown, true)
	else
		L7_137:Hide()
		L6_136(L3_133.szBtnUp, false)
		L6_136(L3_133.szBtnDown, false)
	end
	if A2_132 then
		local L13_143, L14_144 = L13_143, L14_144
		L13_143(L14_144, 0)
		local L15_145 = L15_145
	end
end
function L26_26(A0_151)
	local L1_152, L2_153, L3_154
	L1_152 = _ENV
	L1_152 = L1_152[A0_151]
	L1_152 = L1_152.tEnv
	L2_153 = _ENV
	L2_153 = L2_153[A0_151]
	L3_154 = L1_152[A0_151]
	L3_154 = L3_154.OnLButtonDown
	L2_153.OnLButtonDown = L3_154
	L2_153 = L1_152[A0_151]
	function L3_154()
		local L0_155 = this:GetRoot()
		local L1_156 = L0_155:GetName()
		local L2_157 = this:GetName()
		local L3_158 = L3_158
		local L4_159 = L4_159
		L3_158, L4_159 = L3_158(L4_159, L2_157)
		if L3_158 then
			if L4_159 == "up" then
				L0_155:Lookup(L3_158.szScroll):ScrollPrev()
			elseif L4_159 == "down" then
				L0_155:Lookup(L3_158.szScroll):ScrollNext()
				local L7_162 = L7_162
			end
			return
		end
		L7_162 = _ENV
		L7_162 = L7_162[A0_151]
		L7_162 = L7_162.OnLButtonDown
		if L7_162 then
			L7_162 = _ENV
			L7_162 = L7_162[A0_151]
			L7_162 = L7_162.OnLButtonDown
			return L7_162()
		end
	end
	L2_153.OnLButtonDown = L3_154
	L2_153 = _ENV
	L2_153 = L2_153[A0_151]
	L3_154 = L1_152[A0_151]
	L3_154 = L3_154.OnLButtonDown
	L2_153.OnLButtonDownNew = L3_154
end
function L27_27(A0_163)
	local L1_164, L2_165, L3_166
	L1_164 = _ENV
	L1_164 = L1_164[A0_163]
	L1_164 = L1_164.tEnv
	L2_165 = _ENV
	L2_165 = L2_165[A0_163]
	L3_166 = L1_164[A0_163]
	L3_166 = L3_166.OnLButtonHold
	L2_165.OnLButtonHold = L3_166
	L2_165 = L1_164[A0_163]
	function L3_166()
		local L0_167 = this:GetRoot()
		local L1_168 = L0_167:GetName()
		local L2_169 = this:GetName()
		local L3_170 = L3_170
		local L4_171 = L4_171
		L3_170, L4_171 = L3_170(L4_171, L2_169)
		if L3_170 then
			if L4_171 == "up" then
				L0_167:Lookup(L3_170.szScroll):ScrollPrev()
			elseif L4_171 == "down" then
				L0_167:Lookup(L3_170.szScroll):ScrollNext()
				local L7_174 = L7_174
			end
			if this.bInit then
				this.tData.bScrolling = false
			end
			return
		end
		L7_174 = _ENV
		L7_174 = L7_174[A0_163]
		L7_174 = L7_174.OnLButtonHold
		if L7_174 then
			L7_174 = _ENV
			L7_174 = L7_174[A0_163]
			L7_174 = L7_174.OnLButtonHold
			return L7_174()
		end
	end
	L2_165.OnLButtonHold = L3_166
	L2_165 = _ENV
	L2_165 = L2_165[A0_163]
	L3_166 = L1_164[A0_163]
	L3_166 = L3_166.OnLButtonHold
	L2_165.OnLButtonHoldNew = L3_166
end
function L28_28(A0_175)
	local L1_176, L2_177, L3_178
	L1_176 = _ENV
	L1_176 = L1_176[A0_175]
	L1_176 = L1_176.tEnv
	L2_177 = _ENV
	L2_177 = L2_177[A0_175]
	L3_178 = L1_176[A0_175]
	L3_178 = L3_178.OnItemMouseWheel
	L2_177.OnItemMouseWheel = L3_178
	L2_177 = L1_176[A0_175]
	function L3_178()
		local L0_179 = this:GetRoot()
		local L1_180 = L0_179:GetName()
		local L2_181 = Station.GetMessageWheelDelta()
		local L3_182 = this:GetName()
		local L4_183 = L4_183
		L4_183 = L4_183(L1_180, L3_182)
		if L4_183 then
			local L7_186 = L7_186
			L0_179:Lookup(L4_183.szScroll):ScrollNext(L2_181)
			local L8_187 = L8_187
			L8_187 = this
			L8_187 = L8_187.bInit
			if L8_187 then
				L8_187 = this
				L8_187 = L8_187.tData
				L8_187.bScrolling = false
			end
			L8_187 = true
			return L8_187
		end
		L7_186 = _ENV
		L8_187 = A0_175
		L7_186 = L7_186[L8_187]
		L7_186 = L7_186.OnItemMouseWheel
		if L7_186 then
			L7_186 = _ENV
			L8_187 = A0_175
			L7_186 = L7_186[L8_187]
			L7_186 = L7_186.OnItemMouseWheel
			return L7_186()
		end
	end
	L2_177.OnItemMouseWheel = L3_178
	L2_177 = _ENV
	L2_177 = L2_177[A0_175]
	L3_178 = L1_176[A0_175]
	L3_178 = L3_178.OnItemMouseWheel
	L2_177.OnItemMouseWheelNew = L3_178
end
function L29_29(A0_188)
	local L1_189, L2_190, L3_191
	L1_189 = _ENV
	L1_189 = L1_189[A0_188]
	L1_189 = L1_189.tEnv
	L2_190 = _ENV
	L2_190 = L2_190[A0_188]
	L3_191 = L1_189[A0_188]
	L3_191 = L3_191.OnItemPanGesture
	L2_190.OnItemPanGesture = L3_191
	L2_190 = L1_189[A0_188]
	function L3_191()
		local L0_192 = this:GetRoot()
		local L1_193 = L0_192:GetName()
		local L2_194 = this:GetName()
		local L3_195 = L3_195
		L3_195 = L3_195(L1_193, L2_194)
		if L3_195 then
			if not L0_192:Lookup(L3_195.szScroll) then
				return
			end
			if not L0_192:Lookup(L3_195.tList[1], L3_195.tList[2]) and L0_192:Lookup(L3_195.tList[1], ""):GetName() ~= L3_195.tList[2] then
				Log("lua[error] scroll.lua frame " .. L0_192:GetName() .. " can not Lookup " .. L3_195.tList[2])
				return
			end
			if not L0_192:Lookup(L3_195.szScroll).bInit then
				L7_7(L3_195, L0_192:Lookup(L3_195.szScroll), (L0_192:Lookup(L3_195.tList[1], "")))
			end
			local L6_198 = L6_198
			local L9_201 = L9_201
			L0_192:Lookup(L3_195.szScroll).tData.nDistance = arg1
			L0_192:Lookup(L3_195.szScroll).tData.bScrolling = false
			L6_198.bOnPanGesture = true
			local L10_202 = L10_202
			if not this:GetWndOwner():IsResponsePan() then
				this:GetWndOwner():SetPan(false)
				return
			end
			if L10_202 == GESTURE_STATE.ENDED or L10_202 == GESTURE_STATE.CANCELLED then
				L0_192:Lookup(L3_195.szScroll).tData.nDistance = 0
				L6_198.bOnPanGesture = false
				L16_16(L6_198)
				L22_22(L6_198)
				this:GetWndOwner():SetPan(false)
			end
			if L10_202 == GESTURE_STATE.CHANGED then
				this:GetWndOwner():SetPan(true)
				if arg1 - L0_192:Lookup(L3_195.szScroll).tData.nDistance and arg2 then
					L0_192:Lookup(L3_195.szScroll).tData.nSpeed = (arg1 - L0_192:Lookup(L3_195.szScroll).tData.nDistance) / (arg2 * 0.001)
					L0_192:Lookup(L3_195.szScroll).tData.nSpeed = L8_8(L0_192:Lookup(L3_195.szScroll).tData.nSpeed, L3_3)
					if L0_192:Lookup(L3_195.szScroll).tData.bCanBeStretched then
						if 0 < L0_192:Lookup(L3_195.szScroll).tData.nRelPos + (arg1 - L0_192:Lookup(L3_195.szScroll).tData.nDistance) and arg1 - L0_192:Lookup(L3_195.szScroll).tData.nDistance > 0 then
							if 0 > L0_192:Lookup(L3_195.szScroll).tData.nRelPos then
							else
							end
						end
						if not (L0_192:Lookup(L3_195.szScroll).tData.nRelPos + (arg1 - L0_192:Lookup(L3_195.szScroll).tData.nDistance) < -L0_192:Lookup(L3_195.szScroll).tData.nLength and 0 > ((L0_192:Lookup(L3_195.szScroll).tData.nRelPos + (arg1 - L0_192:Lookup(L3_195.szScroll).tData.nDistance)) * (1 - L0_192:Lookup(L3_195.szScroll).tData.nElasticity * (L0_192:Lookup(L3_195.szScroll).tData.nRelPos + (arg1 - L0_192:Lookup(L3_195.szScroll).tData.nDistance)) / L0_192:Lookup(L3_195.szScroll).tData.nWndH * (L0_192:Lookup(L3_195.szScroll).tData.nElasticity * (L0_192:Lookup(L3_195.szScroll).tData.nRelPos + (arg1 - L0_192:Lookup(L3_195.szScroll).tData.nDistance)) / L0_192:Lookup(L3_195.szScroll).tData.nWndH)) - L0_192:Lookup(L3_195.szScroll).tData.nRelPos) * (1 - L0_192:Lookup(L3_195.szScroll).tData.nElasticity * L0_192:Lookup(L3_195.szScroll).tData.nRelPos / L0_192:Lookup(L3_195.szScroll).tData.nWndH * (L0_192:Lookup(L3_195.szScroll).tData.nElasticity * L0_192:Lookup(L3_195.szScroll).tData.nRelPos / L0_192:Lookup(L3_195.szScroll).tData.nWndH))) then
							goto lbl_217
						end
						if L0_192:Lookup(L3_195.szScroll).tData.nRelPos > -L0_192:Lookup(L3_195.szScroll).tData.nLength then
							local L12_204 = L12_204
							repeat
								do break end -- pseudo-goto
								do break end -- pseudo-goto
								if 0 < L0_192:Lookup(L3_195.szScroll).tData.nRelPos + (arg1 - L0_192:Lookup(L3_195.szScroll).tData.nDistance) then
								end
								if 0 < -L12_204.nLength then
								end
							until true
						end
					end
					::lbl_217::
					if -L12_204.nLength - L12_204.nRelPos then
						L12_204.nRelPos = L12_204.nRelPos + (-L12_204.nLength - L12_204.nRelPos)
						L9_201:SetItemStartRelPos(0, L12_204.nRelPos)
						if math.floor(-L12_204.nRelPos / L12_204.nStepSize + 0.5) > L12_204.nStepCnt then
						end
						if 0 > L12_204.nStepCnt then
						end
						local L13_205 = L13_205
						local L14_206, L15_207 = L14_206, L15_207
						L6_198:SetScrollPos(0)
						local L16_208 = L16_208
					end
				end
			end
			L16_208 = true
			return L16_208
		end
		L6_198 = _ENV
		L9_201 = A0_188
		L6_198 = L6_198[L9_201]
		L6_198 = L6_198.OnItemPanGesture
		if L6_198 then
			L6_198 = _ENV
			L9_201 = A0_188
			L6_198 = L6_198[L9_201]
			L6_198 = L6_198.OnItemPanGesture
			return L6_198()
		end
	end
	L2_190.OnItemPanGesture = L3_191
	L2_190 = _ENV
	L2_190 = L2_190[A0_188]
	L3_191 = L1_189[A0_188]
	L3_191 = L3_191.OnItemPanGesture
	L2_190.OnItemPanGestureNew = L3_191
end
function L30_30(A0_209)
	local L1_210, L2_211, L3_212
	L1_210 = _ENV
	L1_210 = L1_210[A0_209]
	L1_210 = L1_210.tEnv
	L2_211 = _ENV
	L2_211 = L2_211[A0_209]
	L3_212 = L1_210[A0_209]
	L3_212 = L3_212.OnScrollBarPosChanged
	L2_211.OnScrollBarPosChanged = L3_212
	L2_211 = L1_210[A0_209]
	function L3_212()
		local L0_213 = this:GetRoot()
		local L1_214 = L0_213:GetName()
		local L2_215 = this:GetName()
		local L3_216 = L3_216
		L3_216 = L3_216(L1_214, L2_215)
		if L3_216 then
			if not L0_213:Lookup(L3_216.tList[1], L3_216.tList[2]) and L0_213:Lookup(L3_216.tList[1], ""):GetName() ~= L3_216.tList[2] then
				Log("lua[error] scroll.lua frame " .. L0_213:GetName() .. " can not Lookup " .. L3_216.tList[2])
				return
			end
			local L5_218 = L5_218
			if L3_216.szBtnUp and L3_216.szBtnUp ~= "" then
				if this:GetScrollPos() == 0 then
					L0_213:Lookup(L3_216.szBtnUp):Enable(false)
				else
					L0_213:Lookup(L3_216.szBtnUp):Enable(true)
				end
			end
			if L3_216.szBtnDown and L3_216.szBtnDown ~= "" then
				if this:GetScrollPos() == this:GetStepCount() then
					L0_213:Lookup(L3_216.szBtnDown):Enable(false)
				else
					L0_213:Lookup(L3_216.szBtnDown):Enable(true)
				end
			end
			local L6_219 = L6_219
			local L7_220 = L7_220
			if not this.bInit then
				L5_218:SetItemStartRelPos(0, -this:GetScrollPos() * L3_216.tList[3])
			elseif not this.tData.bOnPanGesture and not this.tData.bScrolling then
				this.tData.nRelPos = -this:GetScrollPos() * L3_216.tList[3]
				local L8_221 = L8_221
				local L9_222, L10_223 = L9_222, L10_223
				local L11_224 = L11_224
				L10_223(L11_224, 0, L8_221)
				local L12_225 = L12_225
			end
			return
		end
		L5_218 = _ENV
		L5_218 = L5_218[L1_214]
		L5_218 = L5_218.OnScrollBarPosChanged
		if L5_218 then
			L5_218 = _ENV
			L5_218 = L5_218[L1_214]
			L5_218 = L5_218.OnScrollBarPosChanged
			L5_218()
		end
	end
	L2_211.OnScrollBarPosChanged = L3_212
	L2_211 = _ENV
	L2_211 = L2_211[A0_209]
	L3_212 = L1_210[A0_209]
	L3_212 = L3_212.OnScrollBarPosChanged
	L2_211.OnScrollBarPosChangedNew = L3_212
end
function L31_31(A0_226, A1_227)
	local L2_228, L3_229, L4_230
	L2_228 = _ENV
	L2_228 = L2_228[A0_226]
	L2_228 = L2_228.tEnv
	L3_229 = L2_228[A0_226]
	if L3_229 then
		L3_229 = _ENV
		L3_229 = L3_229[A0_226]
		if L3_229 then
			L3_229 = L2_228[A0_226]
			L3_229 = L3_229[A1_227]
			L4_230 = _ENV
			L4_230 = L4_230[A0_226]
			local L5_231 = L5_231
			local L5_231, L6_232 = L5_231 .. "New", L6_232
			L4_230 = L4_230[L5_231]
			if L3_229 == L4_230 then
				L3_229 = L2_228[A0_226]
				L4_230 = _ENV
				L4_230 = L4_230[A0_226]
				L4_230 = L4_230[A1_227]
				L3_229[A1_227] = L4_230
			end
		end
	end
end
function L32_32(A0_233, A1_234, A2_235)
	if not _ENV[A0_233] then
		_ENV[A0_233] = {}
	end
	if not L1_1[A0_233] then
		L1_1[A0_233] = {}
	end
	local L3_236 = L3_236
	L3_236(_ENV[A0_233], A2_235)
	L3_236 = _ENV
	L3_236 = L3_236[A0_233]
	L3_236 = #L3_236
	local L4_237 = L4_237
	L4_237 = L4_237(A2_235.szBtnUp)
	;({}).nIndex = L3_236
	L1_1[A0_233][L4_237], ({}).type = {}, "up"
	L4_237 = L23_23(A2_235.szBtnDown)
	;({}).nIndex = L3_236
	L1_1[A0_233][L4_237], ({}).type = {}, "down"
	L4_237 = L23_23(A2_235.szScroll)
	L1_1[A0_233][L4_237] = L3_236
	local L5_238 = L5_238
	local L5_238, L6_239 = L5_238(A2_235.tList[2]), L6_239
	L4_237 = L5_238
	L5_238 = L1_1
	L5_238 = L5_238[A0_233]
	L5_238[L4_237] = L3_236
	L5_238 = L1_1
	L5_238 = L5_238[A0_233]
	L5_238.szFramePath = A1_234
end
function L33_33(A0_240, A1_241, A2_242, A3_243, A4_244)
	local L5_245 = L5_245
	local L5_245, L6_246 = L5_245(A0_240), L6_246
	L6_246 = A4_244[3]
	if not L6_246 then
		A4_244[3] = 10
	end
	L6_246 = {}
	L6_246.szBtnUp = A1_241
	L6_246.szBtnDown = A2_242
	L6_246.szScroll = A3_243
	L6_246.tList = A4_244
	local L7_247 = L7_247
	local L8_248 = L8_248
	local L9_249 = L9_249
	L7_247(L8_248, L9_249, L6_246)
	local L10_250 = L10_250
end
RegisterScrollControl = L33_33
function L33_33(A0_251)
	local L1_252
	L1_252 = _ENV
	L1_252[A0_251] = nil
	L1_252 = L1_1
	L1_252[A0_251] = nil
end
UnRegisterScrollAllControl = L33_33
function L33_33(A0_253, A1_254)
	UnRegisterScrollEvent(A0_253)
	_ENV[A0_253] = {}
	if A1_254 then
		_ENV[A0_253].tEnv = GetAddonEnv()
	else
		_ENV[A0_253].tEnv = _G
	end
	L28_28(A0_253)
	L26_26(A0_253)
	L27_27(A0_253)
	L30_30(A0_253)
	local L2_255 = L2_255
	L2_255(A0_253)
	local L3_256 = L3_256
end
RegisterScrollEvent = L33_33
function L33_33(A0_257)
	if _ENV[A0_257] then
		L31_31(A0_257, "OnScrollBarPosChanged")
		L31_31(A0_257, "OnItemMouseWheel")
		L31_31(A0_257, "OnLButtonDown")
		L31_31(A0_257, "OnLButtonHold")
		local L1_258 = L1_258
		local L2_259 = L2_259
		L1_258(L2_259, "OnItemPanGesture")
		local L3_260 = L3_260
		L1_258 = _ENV
		L1_258[A0_257] = nil
	end
end
UnRegisterScrollEvent = L33_33
function L33_33(A0_261)
	if A0_261 == "SCROLL_UPDATE_LIST" then
		local L1_262 = L1_262
		local L2_263 = L2_263
		local L3_264 = L3_264
		L1_262(L2_263, L3_264, arg2)
		local L4_265 = L4_265
	end
end
L36_36 = RegisterEvent
local L35_35 = L35_35
L36_36(L35_35, function(A0_266)
	_ENV(A0_266)
	local L2_267 = L2_267
end)
function L36_36(A0_268, A1_269, A2_270, A3_271)
	local L12_280, L13_281 = L12_280, L13_281
	if not A3_271 then
		A3_271 = 10
		L12_280 = Station
		L12_280 = L12_280.GetUIScale
		L12_280 = L12_280()
		A3_271 = A3_271 / L12_280
	end
	L13_281 = A1_269
	L12_280 = A1_269.GetAbsPos
	L12_280, L13_281 = L12_280(L13_281)
	local L6_274, L7_275 = A1_269:GetSize()
	local L8_276, L9_277 = A0_268:GetAbsPos()
	local L10_278, L11_279 = A0_268:GetSize()
	if L13_281 < L9_277 then
		A2_270:ScrollPrev(math.ceil((L9_277 - L13_281) / A3_271))
	elseif L13_281 + L7_275 > L9_277 + L11_279 then
		local L14_282 = L14_282
		local L15_283 = L15_283
		L14_282(L15_283, math.ceil((L13_281 + L7_275 - L9_277 - L11_279) / A3_271))
	end
end
ScrollToSelect = L36_36
function L36_36(A0_284, A1_285, A2_286, A3_287, A4_288)
	local L8_292 = L8_292
	if not A4_288 then
		A4_288 = 10
		L8_292 = Station
		L8_292 = L8_292.GetUIScale
		L8_292 = L8_292()
		A4_288 = A4_288 / L8_292
	end
	L8_292 = A0_284.GetAbsX
	L8_292 = L8_292(A0_284)
	local L6_290, L7_291 = A0_284:GetW(), L7_291
	if A1_285 < L8_292 then
		L7_291 = A3_287.ScrollPrev
		L7_291(A3_287, math.ceil((L8_292 - A1_285) / A4_288))
	else
		L7_291 = A1_285 + A2_286
		if L7_291 > L8_292 + L6_290 then
			L7_291 = A3_287.ScrollNext
			local L9_293 = L9_293
			local L10_294 = math.ceil((A1_285 + A2_286 - L8_292 - L6_290) / A4_288)
			L7_291(L9_293, L10_294, math.ceil((A1_285 + A2_286 - L8_292 - L6_290) / A4_288))
		end
	end
end
ScrollToSelectHor = L36_36
