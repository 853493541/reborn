local L0_0, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L21_21, L22_22, L23_23, L24_24, L25_25, L26_26, L27_27, L28_28, L29_29, L30_30, L31_31, L32_32, L33_33, L34_34, L35_35, L36_36, L37_37, L38_38, L39_39 = L0_0, "TweenNew", L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L21_21, L22_22, L23_23, L24_24, L25_25, L26_26, L27_27, L28_28, L29_29, L30_30, L31_31, L32_32, L33_33, L34_34, L35_35, L36_36, L37_37, L38_38, L39_39
L4_4 = ExportExternalLib
L0_0(L3_3, L4_4)
L0_0 = {}
L3_3 = {}
L4_4 = 0
L5_5 = {}
L6_6 = {}
L7_7 = 0
L8_8 = 0
L9_9 = 35
function L10_10(A0_40, A1_41)
	local L2_42, L3_43
	L2_42 = math
	local L2_42, L9_49, L10_50, L14_54, L15_55, L16_56, L17_57, L18_58, L19_59 = L2_42.max, L9_49, L10_50, L14_54, L15_55, L16_56, L17_57, L18_58, L19_59
	L3_43 = 0
	L7_47 = math
	L7_47 = L7_47.min
	L8_48 = 1
	L9_49 = A1_41
	L7_47, L8_48, L9_49, L10_50, L14_54, L15_55, L16_56, L17_57, L18_58, L19_59 = L7_47(L8_48, L9_49)
	L2_42 = L2_42(L3_43, L7_47, L8_48, L9_49, L10_50, L14_54, L15_55, L16_56, L17_57, L18_58, L19_59, L7_47(L8_48, L9_49))
	A1_41 = L2_42
	if A0_40 then
		L2_42 = #A0_40
		if not (L2_42 < 2) then
			goto lbl_17
		end
	end
	do return A1_41 end
	::lbl_17::
	L2_42 = 1
	L3_43 = #A0_40
	L3_43 = L3_43 - 1
	L7_47 = 1
	for L8_48 = L2_42, L3_43, L7_47 do
		L9_49 = A0_40[L8_48]
		L9_49 = L9_49.x
		L10_50 = L8_48 + 1
		L10_50 = A0_40[L10_50]
		L10_50 = L10_50.x
		if A1_41 >= L9_49 and A1_41 <= L10_50 then
			L14_54 = A1_41 - L9_49
			L15_55 = L10_50 - L9_49
			L15_55 = L15_55 + 1.0E-4
			L14_54 = L14_54 / L15_55
			L15_55 = math
			L15_55 = L15_55.max
			L16_56 = 0
			L17_57 = math
			L17_57 = L17_57.min
			L18_58 = 1
			L19_59 = L14_54
			L17_57, L18_58, L19_59 = L17_57(L18_58, L19_59)
			L15_55 = L15_55(L16_56, L17_57, L18_58, L19_59, L17_57(L18_58, L19_59))
			L14_54 = L15_55
			L15_55 = A0_40[L8_48]
			L15_55 = L15_55.y
			L16_56 = A0_40[L8_48]
			L16_56 = L16_56.rightTangentY
			L16_56 = L15_55 + L16_56
			L17_57 = L8_48 + 1
			L17_57 = A0_40[L17_57]
			L17_57 = L17_57.y
			L18_58 = L8_48 + 1
			L18_58 = A0_40[L18_58]
			L18_58 = L18_58.leftTangentY
			L17_57 = L17_57 + L18_58
			L18_58 = L8_48 + 1
			L18_58 = A0_40[L18_58]
			L18_58 = L18_58.y
			L19_59 = 1 - L14_54
			return L19_59 * L19_59 * L19_59 * L15_55 + 3 * L19_59 * L19_59 * L14_54 * L16_56 + 3 * L19_59 * L14_54 * L14_54 * L17_57 + L14_54 * L14_54 * L14_54 * L18_58
		end
	end
	L2_42 = #A0_40
	L2_42 = A0_40[L2_42]
	L2_42 = L2_42.y
	return L2_42
end
function L11_11(A0_60)
	local L7_67 = L7_67
	if not A0_60 or A0_60 == "" then
		L7_67 = nil
		return L7_67
	end
	L7_67 = string
	L7_67 = L7_67.find
	L7_67 = L7_67(A0_60, ";")
	if L7_67 then
		L7_67 = string
		L7_67 = L7_67.split
		L7_67 = L7_67(A0_60, ";")
		if not L7_67 or L7_67[1] == nil or L7_67[1] == "" then
			return nil
		end
		if not tonumber(L7_67[1]) or tonumber(L7_67[1]) < 2 then
			return nil
		end
		local _FOR_, L5_65 = 1, L5_65
		local L6_66 = L6_66
		for _FORV_7_ = _FOR_, _FOR_, _FOR_ do
			if L7_67[_FORV_7_ + 1] == nil or L7_67[_FORV_7_ + 1] == "" then
				return nil
			end
			if not string.split(L7_67[_FORV_7_ + 1], ",") or #string.split(L7_67[_FORV_7_ + 1], ",") < 6 then
				return nil
			end
			if not tonumber(string.split(L7_67[_FORV_7_ + 1], ",")[1]) then
			end
			if not tonumber(string.split(L7_67[_FORV_7_ + 1], ",")[2]) then
			end
			if not tonumber(string.split(L7_67[_FORV_7_ + 1], ",")[3]) then
			end
			if not tonumber(string.split(L7_67[_FORV_7_ + 1], ",")[4]) then
			end
			local L12_72 = L12_72
			if not tonumber(L12_72[5]) then
			end
			local L13_73 = L13_73
			if not tonumber(L12_72[6]) then
			end
			local L14_74 = L14_74
			local L15_75 = L15_75
			local L16_76 = L16_76
			;({}).x = L13_73
			;({}).y = L14_74
			;({}).leftTangentX = L15_75
			;({}).rightTangentY, ({}).rightTangentX, ({}).leftTangentY = 0, 0, L16_76
			table.insert(L6_66, {})
			local L17_77 = L17_77
		end
		L8_68 = #L6_66
		if L8_68 < 2 then
			L8_68 = nil
			return L8_68
		end
		L8_68 = {}
		L8_68.bIsExact = true
		L8_68.tControlPoints = L6_66
		return L8_68
	end
	L7_67 = {}
	L7_67.bIsExact = false
	L5_65 = string
	L5_65 = L5_65.split
	L6_66 = A0_60
	L8_68 = ","
	L5_65 = L5_65(L6_66, L8_68)
	L7_67.tSamples = L5_65
	return L7_67
end
function L12_12(A0_78, A1_79, A2_80)
	local L3_81 = L3_81
	L3_81 = L3_81(A0_78, A1_79, A2_80, "")
	if not L3_81(A0_78, A1_79, A2_80, "") or not L3_81 then
		return
	end
	local L7_84 = L7_84
	local L8_85 = string.gsub(L3_81, "\"", "")
	L3_81 = L8_85
	L8_85 = _ENV
	return L8_85(L3_81)
end
function L13_13(A0_86, A1_87, A2_88, A3_89, A4_90, A5_91)
	local L6_92, L7_93
	if not A2_88 then
		L6_92 = A0_86
		L7_93 = A1_87
		return L6_92, L7_93
	end
	L6_92 = A3_89 or L6_92
	if not A3_89 then
		L6_92 = 0
	end
	L7_93 = L6_92
	if not A5_91 then
		if not A4_90 then
		end
		L7_93 = L7_93 + 0
	end
	if not A0_86 then
	end
	A0_86 = math.max(0, L7_93)
	if A1_87 == nil then
		A1_87 = L6_92
	else
		local L8_94 = L8_94
		local L9_95 = L9_95
		local L8_94, L10_96 = L8_94(L9_95, L6_92), L10_96
		A1_87 = L8_94
	end
	L8_94 = A0_86
	L9_95 = A1_87
	return L8_94, L9_95
end
function L14_14(A0_97, A1_98, A2_99)
	local L3_100, L4_101
	L3_100 = A0_97.nTotalTime
	if not L3_100 then
		L3_100 = 0
	end
	L4_101 = nil
	L3_100, L4_101 = _ENV(L3_100, L4_101, A0_97.bPosEnable, A0_97.nPosDelay, A0_97.nPosTime, false)
	L3_100, L4_101 = _ENV(L3_100, L4_101, A0_97.bScaleEnable, A0_97.nScaleDelay, A0_97.nScaleTime, false)
	L3_100, L4_101 = _ENV(L3_100, L4_101, A0_97.bPivotScaleEnable, A0_97.nPivotScaleDelay, A0_97.nPivotScaleTime, false)
	L3_100, L4_101 = _ENV(L3_100, L4_101, A0_97.bAlphaEnable, A0_97.nAlphaDelay, A0_97.nAlphaTime, false)
	L3_100, L4_101 = _ENV(L3_100, L4_101, A0_97.bRotateEnable, A0_97.nRotateDelay, A0_97.nRotateTime, false)
	L3_100, L4_101 = _ENV(L3_100, L4_101, A0_97.bPivotRotateEnable, A0_97.nPivotRotateDelay, A0_97.nPivotRotateTime, false)
	L3_100, L4_101 = _ENV(L3_100, L4_101, A0_97.bDistanceEnable, A0_97.nDistanceDelay, A0_97.nDistanceTime, false)
	L3_100, L4_101 = _ENV(L3_100, L4_101, A0_97.bSizeEnable, A0_97.nSizeDelay, A0_97.nSizeTime, false)
	L3_100, L4_101 = _ENV(L3_100, L4_101, A0_97.bVisibleEnable, A0_97.nVisibleDelay, 0, true)
	local L8_105 = L8_105
	local L9_106 = L9_106
	local L10_107 = L10_107
	local L8_105, L9_106, L11_108 = L8_105(L9_106, L10_107, A0_97.bPlayEnable, A0_97.nPlayDelay, 0, true)
	L4_101 = L9_106
	L3_100 = L8_105
	A0_97.nTotalTime = L3_100
	L8_105 = math
	L8_105 = L8_105.max
	L9_106 = A0_97.nTotalTime
	L10_107 = A1_98
	L8_105 = L8_105(L9_106, L10_107)
	A1_98 = L8_105
	A2_99 = L4_101 or A2_99
	if L4_101 ~= nil and (A2_99 ~= nil or not L4_101) then
		L8_105 = math
		L8_105 = L8_105.min
		L9_106 = A2_99
		L10_107 = L4_101
		L8_105 = L8_105(L9_106, L10_107)
		A2_99 = L8_105
	end
	L8_105 = A1_98
	L9_106 = A2_99
	return L8_105, L9_106
end
function L15_15(A0_109, A1_110)
	local L2_111, L3_112, L4_113
	L2_111 = {}
	L2_111.bNewTween = true
	L3_112 = 0
	L4_113 = nil
	local L5_114 = L5_114
	L5_114 = L5_114(A0_109, A1_110, "StepCount", 0)
	local L6_115 = L6_115
	L6_115 = L6_115(A0_109, A1_110, "OffsetX", 0.5)
	local L7_116 = L7_116
	L7_116 = L7_116(A0_109, A1_110, "OffsetY", 0.5)
	local L8_117 = L8_117
	L8_117 = L8_117(A0_109, A1_110, "TweenType", "Once")
	local L9_118 = L9_118
	L9_118 = L9_118(A0_109, A1_110, "RepeatCount", 0)
	if L8_117 == "Once" then
		L9_118 = 1
	elseif L9_118 < 0 then
		L9_118 = 0
	end
	if A0_109:ReadInteger(A1_110, "EndForceTo", 0) ~= 0 then
		L2_111.bEndForceTo = true
	end
	local L10_119, L14_123 = L10_119(L11_120, L12_121, L13_122, 0), L14_123
	if L10_119 ~= 0 then
		L2_111.bUseImageReplace = true
	end
	L2_111.szType = L8_117
	L2_111.nRepeatCount = L9_118
	L10_119 = 1
	L11_120 = L5_114
	L12_121 = 1
	for L13_122 = L10_119, L11_120, L12_121 do
		L14_123 = {}
		local L15_124 = L15_124
		L15_124 = L15_124 .. "_Step" .. L13_122
		L14_123.nTotalTime = A0_109:ReadInteger(L15_124, "TotalTime", 1000)
		L14_123.szType = L8_117
		L14_123.fOffsetX = L6_115
		L14_123.fOffsetY = L7_116
		if A0_109:ReadInteger(L15_124, "PosEnable", 0) ~= 0 then
			L14_123.bPosEnable = true
			L14_123.bPosOriginEnable = 0 < A0_109:ReadInteger(L15_124, "PosOriginEnable", 0)
			L14_123.nPosFromX = A0_109:ReadInteger(L15_124, "PosFromX", 0)
			L14_123.nPosFromY = A0_109:ReadInteger(L15_124, "PosFromY", 0)
			L14_123.nPosToX = A0_109:ReadInteger(L15_124, "PosToX", 0)
			L14_123.nPosToY = A0_109:ReadInteger(L15_124, "PosToY", 0)
			L14_123.nPosTime = A0_109:ReadInteger(L15_124, "PosTime", 1000)
			L14_123.nPosDelay = A0_109:ReadInteger(L15_124, "PosDelayTime", 0)
			L14_123.bPosIgnoreX = 0 < A0_109:ReadInteger(L15_124, "PosIgnoreX", 0)
			L14_123.bPosIgnoreY = 0 < A0_109:ReadInteger(L15_124, "PosIgnoreY", 0)
			L14_123.tPosCurve = _ENV(A0_109, L15_124, "PosCurve")
		end
		if A0_109:ReadInteger(L15_124, "ScaleEnable", 0) ~= 0 then
			L14_123.bScaleEnable = true
			L14_123.fScaleFromX = A0_109:ReadFloat(L15_124, "ScaleFromX", 1.0E-4)
			L14_123.fScaleFromY = A0_109:ReadFloat(L15_124, "ScaleFromY", 1.0E-4)
			L14_123.fScaleToX = A0_109:ReadFloat(L15_124, "ScaleToX", 1.0E-4)
			L14_123.fScaleToY = A0_109:ReadFloat(L15_124, "ScaleToY", 1.0E-4)
			L14_123.nScaleTime = A0_109:ReadInteger(L15_124, "ScaleTime", 1000)
			L14_123.nScaleDelay = A0_109:ReadInteger(L15_124, "ScaleDelayTime", 0)
			L14_123.bScaleIgnoreX = 0 < A0_109:ReadInteger(L15_124, "ScaleIgnoreX", 0)
			L14_123.bScaleIgnoreY = 0 < A0_109:ReadInteger(L15_124, "ScaleIgnoreY", 0)
			L14_123.tScaleCurve = _ENV(A0_109, L15_124, "ScaleCurve")
			L14_123.fScaleFromX = math.max(L14_123.fScaleFromX, 1.0E-4)
			L14_123.fScaleFromY = math.max(L14_123.fScaleFromY, 1.0E-4)
			L14_123.fScaleToX = math.max(L14_123.fScaleToX, 1.0E-4)
			L14_123.fScaleToY = math.max(L14_123.fScaleToY, 1.0E-4)
		end
		if A0_109:ReadInteger(L15_124, "PivotScaleEnable", 0) ~= 0 then
			L14_123.bPivotScaleEnable = true
			L14_123.fPivotScaleFromX = A0_109:ReadFloat(L15_124, "PivotScaleFromX", 1)
			L14_123.fPivotScaleFromY = A0_109:ReadFloat(L15_124, "PivotScaleFromY", 1)
			L14_123.fPivotScaleToX = A0_109:ReadFloat(L15_124, "PivotScaleToX", 1)
			L14_123.fPivotScaleToY = A0_109:ReadFloat(L15_124, "PivotScaleToY", 1)
			L14_123.nPivotScaleTime = A0_109:ReadInteger(L15_124, "PivotScaleTime", 1000)
			L14_123.nPivotScaleDelay = A0_109:ReadInteger(L15_124, "PivotScaleDelayTime", 0)
			L14_123.bPivotScaleIgnoreX = 0 < A0_109:ReadInteger(L15_124, "PivotScaleIgnoreX", 0)
			L14_123.bPivotScaleIgnoreY = 0 < A0_109:ReadInteger(L15_124, "PivotScaleIgnoreY", 0)
			L14_123.tPivotScaleCurve = _ENV(A0_109, L15_124, "PivotScaleCurve")
			L14_123.fPivotScaleFromX = math.max(L14_123.fPivotScaleFromX, 1.0E-4)
			L14_123.fPivotScaleFromY = math.max(L14_123.fPivotScaleFromY, 1.0E-4)
			L14_123.fPivotScaleToX = math.max(L14_123.fPivotScaleToX, 1.0E-4)
			L14_123.fPivotScaleToY = math.max(L14_123.fPivotScaleToY, 1.0E-4)
		end
		if A0_109:ReadInteger(L15_124, "AlphaEnable", 0) ~= 0 then
			L14_123.bAlphaEnable = true
			L14_123.bAlphaIgnoreFrom = A0_109:ReadInteger(L15_124, "AlphaIgnoreFrom", 0) == 1
			L14_123.nAlphaFrom = A0_109:ReadInteger(L15_124, "AlphaFrom", 0)
			L14_123.nAlphaTo = A0_109:ReadInteger(L15_124, "AlphaTo", 0)
			L14_123.nAlphaTime = A0_109:ReadInteger(L15_124, "AlphaTime", 1000)
			L14_123.nAlphaDelay = A0_109:ReadInteger(L15_124, "AlphaDelayTime", 0)
			L14_123.tAlphaCurve = _ENV(A0_109, L15_124, "AlphaCurve")
		end
		if A0_109:ReadInteger(L15_124, "RotateEnable", 0) ~= 0 then
			L14_123.bRotateEnable = true
			L14_123.nRotateFrom = A0_109:ReadInteger(L15_124, "RotateFrom", 0)
			L14_123.nRotateTo = A0_109:ReadInteger(L15_124, "RotateTo", 0)
			L14_123.nRotateTime = A0_109:ReadInteger(L15_124, "RotateTime", 1000)
			L14_123.nRotateDelay = A0_109:ReadInteger(L15_124, "RotateDelayTime", 0)
			L14_123.tRotateCurve = _ENV(A0_109, L15_124, "RotateCurve")
		end
		if A0_109:ReadInteger(L15_124, "PivotRotateEnable", 0) ~= 0 then
			L14_123.bPivotRotateEnable = true
			L14_123.bPivotRotateIgnoreFrom = 0 < A0_109:ReadInteger(L15_124, "PivotRotateIgnoreFrom", 0)
			L14_123.nPivotRotateFrom = A0_109:ReadInteger(L15_124, "PivotRotateFrom", 0)
			L14_123.nPivotRotateTo = A0_109:ReadInteger(L15_124, "PivotRotateTo", 0)
			L14_123.nPivotRotateTime = A0_109:ReadInteger(L15_124, "PivotRotateTime", 1000)
			L14_123.nPivotRotateDelay = A0_109:ReadInteger(L15_124, "PivotRotateDelayTime", 0)
			L14_123.tPivotRotateCurve = _ENV(A0_109, L15_124, "PivotRotateCurve")
		end
		if A0_109:ReadInteger(L15_124, "DistanceEnable", 0) ~= 0 then
			L14_123.bDistanceEnable = true
			L14_123.nDistanceX = A0_109:ReadInteger(L15_124, "DistanceX", 0)
			L14_123.nDistanceY = A0_109:ReadInteger(L15_124, "DistanceY", 0)
			L14_123.bDistanceIgnoreX = 0 < A0_109:ReadInteger(L15_124, "DistanceIgnoreX", 0)
			L14_123.bDistanceIgnoreY = 0 < A0_109:ReadInteger(L15_124, "DistanceIgnoreY", 0)
			L14_123.nDistanceTime = A0_109:ReadInteger(L15_124, "DistanceTime", 1000)
			L14_123.nDistanceDelay = A0_109:ReadInteger(L15_124, "DistanceDelayTime", 0)
			L14_123.tDistanceCurve = _ENV(A0_109, L15_124, "DistanceCurve")
		end
		if A0_109:ReadInteger(L15_124, "SizeEnable", 0) ~= 0 then
			L14_123.bSizeEnable = true
			L14_123.nSizeFromW = A0_109:ReadInteger(L15_124, "SizeFromW", 0)
			L14_123.nSizeFromH = A0_109:ReadInteger(L15_124, "SizeFromH", 0)
			L14_123.nSizeToW = A0_109:ReadInteger(L15_124, "SizeToW", 0)
			L14_123.nSizeToH = A0_109:ReadInteger(L15_124, "SizeToH", 0)
			L14_123.nSizeTime = A0_109:ReadInteger(L15_124, "SizeTime", 1000)
			L14_123.nSizeDelay = A0_109:ReadInteger(L15_124, "SizeDelayTime", 0)
			L14_123.bSizeIgnoreW = 0 < A0_109:ReadInteger(L15_124, "SizeIgnoreW", 0)
			L14_123.bSizeIgnoreH = 0 < A0_109:ReadInteger(L15_124, "SizeIgnoreH", 0)
			L14_123.tSizeCurve = _ENV(A0_109, L15_124, "SizeCurve")
		end
		if A0_109:ReadInteger(L15_124, "VisibleEnable", 0) ~= 0 then
			L14_123.bVisibleEnable = true
			L14_123.nVisibleDelay = A0_109:ReadInteger(L15_124, "VisibleDelayTime", 0)
			L14_123.bVisibleValue = 0 < A0_109:ReadInteger(L15_124, "VisibleValue", 1)
		end
		if A0_109:ReadInteger(L15_124, "PlayEnable", 0) ~= 0 then
			L14_123.bPlayEnable = true
			local L20_129 = A0_109:ReadInteger(L15_124, "PlayDelayTime", 0)
			L14_123.nPlayDelay = L20_129
		end
		L20_129 = L14_14
		L4_113, L20_129 = L20_129(L14_123, L3_112, L4_113)
		L3_112 = L20_129
		L20_129 = table
		L20_129 = L20_129.insert
		local L17_126 = L17_126
		L20_129(L17_126, L14_123)
		local L18_127 = L18_127
	end
	L2_111.nTotalTime = L3_112
	L10_119 = L4_113 or L10_119
	if not L4_113 then
		L10_119 = 0
	end
	L2_111.nLoopStartTime = L10_119
	return L2_111
end
LoadTween = L15_15
function L15_15(A0_130, A1_131, A2_132)
	local L3_133
	L3_133 = A1_131 - A0_130
	L3_133 = L3_133 * A2_132
	L3_133 = A0_130 + L3_133
	return L3_133
end
function L16_16(A0_134, A1_135, A2_136)
	if A0_134 <= A2_136 then
		return 0
	elseif A1_135 <= A0_134 then
		return 1
	else
		local L3_137 = L3_137
		local L4_138 = L4_138
		local L6_140 = (A0_134 - A2_136) / (A1_135 - A2_136)
		return L3_137(L4_138, L6_140)
	end
end
function L17_17(A0_141, A1_142)
	if not A1_142 then
		return A0_141
	end
	if A1_142.bIsExact then
		if not A1_142.tControlPoints or #A1_142.tControlPoints < 2 then
			return A0_141
		end
		return _ENV(A1_142.tControlPoints, A0_141)
	else
		if not A1_142.tSamples or 2 > #A1_142.tSamples then
			return A0_141
		end
		local L2_143 = L2_143
		local L2_143, L3_144 = L2_143(A0_141 * L9_9)
		if 1.0E-4 < L3_144 then
			if A1_142.tSamples[L2_143 + 1] ~= nil and A1_142.tSamples[L2_143 + 1] ~= "" and A1_142.tSamples[L2_143 + 2] ~= nil and A1_142.tSamples[L2_143 + 2] ~= "" then
				return L15_15(tonumber(A1_142.tSamples[L2_143 + 1]), tonumber(A1_142.tSamples[L2_143 + 2]), L3_144)
			end
		elseif A1_142.tSamples[L2_143 + 1] ~= nil and A1_142.tSamples[L2_143 + 1] ~= "" then
			local L4_145 = L4_145
			return L4_145(A1_142.tSamples[L2_143 + 1])
		end
	end
	return A0_141
end
function L18_18(A0_146, A1_147, A2_148, A3_149)
	local L4_150, L5_151
	if A2_148 <= 0 then
		return A1_147
	end
	if not A3_149 then
		A3_149 = 0
	end
	L4_150 = math
	L4_150 = L4_150.max
	L5_151 = 0
	L4_150 = L4_150(L5_151, math.min(A3_149, A2_148))
	A3_149 = L4_150
	L4_150 = A2_148 - A3_149
	if L4_150 <= 0 then
		return A1_147
	end
	L5_151 = A1_147
	if A0_146 == "Loop" then
		if A1_147 <= A3_149 then
			return A1_147
		end
		local L5_151, L8_154 = A3_149 + math.modf((A1_147 - A3_149) / L4_150) * L4_150, L8_154
		break -- pseudo-goto
	end
	if A0_146 == "PingPong" then
		if A1_147 <= A3_149 then
			return A1_147
		end
		L8_154 = math
		L8_154 = L8_154.modf
		local L8_154, L7_153 = L8_154((A1_147 - A3_149) / L4_150)
		repeat
			if L8_154 % 2 == 0 then
				L5_151 = A3_149 + L7_153 * L4_150
			else
				L5_151 = A2_148 - L7_153 * L4_150
				do break end -- pseudo-goto
				L5_151 = A1_147
			end
		until true
	end
	return L5_151
end
function L21_21(A0_155, A1_156, A2_157, A3_158, A4_159, A5_160, A6_161)
	local L7_162
	L7_162 = A1_156 + A2_157
	local L8_163 = L8_163
	L8_163 = L8_163(A5_160, A0_155, A3_158, A6_161)
	local L9_164 = L9_164
	L9_164 = L9_164(L8_163, L7_162, A2_157)
	local L10_165 = L10_165
	local L11_166 = L11_166
	do return L10_165(L11_166, A4_159) end
	local L12_167 = L12_167
end
function L22_22(A0_168, A1_169, A2_170, A3_171, A4_172, A5_173)
	repeat
		if A2_170 <= 0 then
			return A3_171 <= A1_169 and A1_169 <= A3_171 + A4_172
		end
		local L6_174 = L6_174
		local L7_175 = L7_175
		L6_174 = L6_174(L7_175, A1_169, A2_170, A5_173)
		L7_175 = A3_171 + A4_172
		if A0_168 == "Once" then
			if A3_171 <= L6_174 and L6_174 <= L7_175 then
				return true
			end
			local L8_176 = L8_176
			if not L8_8 then
			end
			local L9_177 = L9_177
			local L8_176, L10_178 = L8_176(L9_177, 0), L10_178
			L9_177 = L6_174 > L7_175
			do return L9_177 end
			break -- pseudo-goto
		end
		L8_176 = A3_171 <= L6_174 and L6_174 <= L7_175
		return L8_176
	until true
end
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_179, A1_180, A2_181, A3_182, A4_183)
	if not A1_180.bScaleEnable or A2_181 <= A1_180.nScaleDelay then
		return
	end
	local L5_184 = L5_184
	L5_184 = L5_184(A2_181, A1_180.nScaleTime, A1_180.nScaleDelay, A3_182, A1_180.tScaleCurve, A1_180.szType, A4_183)
	if not A1_180.bScaleIgnoreX then
		A0_179:Scale(math.max(L15_15(A1_180.fScaleFromX, A1_180.fScaleToX, L5_184) / A1_180._fScaleX, 1.0E-4), 1)
		A1_180._fScaleX = L15_15(A1_180.fScaleFromX, A1_180.fScaleToX, L5_184)
		A0_179:SetAbsX(A0_179:GetAbsX() + A0_179:GetW() * A1_180.fOffsetX - A0_179:GetW() * A1_180.fOffsetX)
	end
	if not A1_180.bScaleIgnoreY then
		local L6_185 = A0_179:GetAbsY()
		L6_185 = L6_185 + A0_179:GetH() * A1_180.fOffsetY
		local L7_186 = L7_186
		L7_186 = L7_186(A1_180.fScaleFromY, A1_180.fScaleToY, L5_184)
		local L8_187 = L8_187
		L8_187 = L8_187(L7_186 / A1_180._fScaleY, 1.0E-4)
		A0_179:Scale(1, L8_187)
		A1_180._fScaleY = L7_186
		local L9_188, L10_189 = L9_188, L10_189
		local L12_191 = A0_179:GetH() * A1_180.fOffsetY
		L12_191 = L6_185 - L12_191
		L9_188(L10_189, L12_191)
	end
end
L23_23(L24_24, L25_25)
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_192, A1_193, A2_194, A3_195, A4_196)
	if not A1_193.bPivotScaleEnable or A2_194 <= A1_193.nPivotScaleDelay or not _ENV(A1_193.szType, A2_194, A3_195, A1_193.nPivotScaleDelay, A1_193.nPivotScaleTime, A4_196) then
		return
	end
	local L5_197 = L5_197
	local L6_198 = L6_198
	local L7_199 = L7_199
	L5_197 = L5_197(L6_198, L7_199, A1_193.nPivotScaleDelay, A3_195, A1_193.tPivotScaleCurve, A1_193.szType, A4_196)
	L6_198 = A1_193._fPivotScaleX
	if not L6_198 then
		L6_198 = 1
	end
	L7_199 = A1_193._fPivotScaleY
	if not L7_199 then
		L7_199 = 1
	end
	if not A1_193.bPivotScaleIgnoreX then
		L6_198 = math.max(L15_15(A1_193.fPivotScaleFromX, A1_193.fPivotScaleToX, L5_197), 1.0E-4)
	end
	if not A1_193.bPivotScaleIgnoreY then
		local L12_204 = L12_204
		L12_204 = L12_204(L15_15(A1_193.fPivotScaleFromY, A1_193.fPivotScaleToY, L5_197), 1.0E-4)
		L7_199 = L12_204
	end
	L12_204 = A0_192.SetPivotPos
	if L12_204 then
		L12_204 = A0_192.SetPivotScale
		if L12_204 then
			goto lbl_68
		end
	end
	do return end
	::lbl_68::
	L12_204 = A0_192.SetPivotScale
	local L9_201 = L9_201
	local L10_202 = L10_202
	L12_204(L9_201, L10_202, L7_199)
	local L11_203 = L11_203
	A0_192._TweenPivotScaleX = L6_198
	A0_192._TweenPivotScaleY = L7_199
	A1_193._fPivotScaleX = L6_198
	A1_193._fPivotScaleY = L7_199
end
L23_23(L24_24, L25_25)
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_205, A1_206, A2_207, A3_208, A4_209)
	if not A0_205 then
		return
	end
	if not A1_206.bPosEnable or not _ENV(A1_206.szType, A2_207, A3_208, A1_206.nPosDelay, A1_206.nPosTime, A4_209) then
		return
	end
	local L5_210 = L5_210
	L5_210 = L5_210(A0_205)
	if not L5_210 then
		L5_210 = A0_205
	end
	if not L5_210 or not L5_210:IsValid() then
		return
	end
	local L6_211 = L6_211
	L6_211 = L6_211(A2_207, A1_206.nPosTime, A1_206.nPosDelay, A3_208, A1_206.tPosCurve, A1_206.szType, A4_209)
	local L7_212 = L5_210:GetParent()
	if not A1_206.bPosIgnoreX then
		if A1_206.bPosOriginEnable then
		end
		A0_205:SetRelX(L15_15(A1_206.nPosFromX, A1_206.nPosToX, L6_211) + L5_210.nStartRelPosX)
		if L7_212 then
			A0_205:SetAbsX(L7_212:GetAbsX() + A0_205:GetRelX())
		else
			A0_205:SetAbsX(A0_205:GetRelX())
		end
	end
	if not A1_206.bPosIgnoreY then
		if A1_206.bPosOriginEnable then
		end
		A0_205:SetRelY(L15_15(A1_206.nPosFromY, A1_206.nPosToY, L6_211) + L5_210.nStartRelPosY)
		if L7_212 then
			local L14_219 = L14_219
			A0_205:SetAbsY(L7_212:GetAbsY() + A0_205:GetRelY())
			break -- pseudo-goto
		end
		local L12_217, L13_218 = L12_217, L13_218
		repeat
			L13_218(A0_205, A0_205:GetRelY())
		until true
	end
	L12_217 = A0_205
	L14_219 = A0_205.GetType
	L14_219 = L14_219(L12_217)
	if L14_219 == "Handle" then
		L12_217 = A0_205
		L14_219 = A0_205.FormatAllItemPos
		L14_219(L12_217)
	else
		L12_217 = A0_205
		L14_219 = A0_205.GetType
		L14_219 = L14_219(L12_217)
		if L14_219 ~= "WndContainer" then
			L12_217 = A0_205
			L14_219 = A0_205.GetType
			L14_219 = L14_219(L12_217)
			if L14_219 ~= "WndFlexContainer" then
				goto lbl_120
			end
		end
		L12_217 = A0_205
		L14_219 = A0_205.FormatAllContentPos
		L13_218 = true
		L14_219(L12_217, L13_218)
	end
	::lbl_120::
end
L23_23(L24_24, L25_25)
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_220, A1_221, A2_222, A3_223, A4_224)
	if not A1_221.bDistanceEnable or not _ENV(A1_221.szType, A2_222, A3_223, A1_221.nDistanceDelay, A1_221.nDistanceTime, A4_224) then
		return
	end
	local L5_225 = L5_225
	L5_225 = L5_225(A2_222, A1_221.nDistanceTime, A1_221.nDistanceDelay, A3_223, A1_221.tDistanceCurve, A1_221.szType, A4_224)
	if not A1_221.bPosIgnoreX then
		A0_220:SetRelX(L15_15(0, A1_221.nDistanceX, L5_225) + A0_220.nStartRelPosX)
	end
	if not A1_221.bPosIgnoreY then
		A0_220:SetRelY(L15_15(0, A1_221.nDistanceY, L5_225) + A0_220.nStartRelPosY)
	end
	if A0_220:GetType() == "Handle" then
		A0_220:FormatAllItemPos()
	elseif A0_220:GetType() == "WndContainer" or A0_220:GetType() == "WndFlexContainer" then
		A0_220:FormatAllContentPos(true)
	end
	local L6_226 = L6_226
	L6_226 = L6_226(A0_220)
	if not L6_226 then
		L6_226 = A0_220
	end
	local L7_227 = L6_226:GetParent()
	local L8_228, L9_229 = L8_228, L9_229
	local L10_230 = L10_230
	local L13_233 = L13_233
	L13_233 = L13_233 + A0_220:GetRelY()
	L8_228(L9_229, L10_230, L13_233)
end
L23_23(L24_24, L25_25)
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_234, A1_235, A2_236, A3_237, A4_238)
	if not A1_235.bAlphaEnable then
		return
	end
	if A0_234.IsCommonAlpha ~= nil and A0_234:IsCommonAlpha() then
		A0_234.bCommonAlpha = true
		A0_234:SetCommonAlpha(false)
	end
	if A2_236 < A1_235.nAlphaDelay then
		if not A0_234.bInit and not A1_235.bAlphaIgnoreFrom then
			A0_234.bInit = true
			A0_234:SetAlpha(A1_235.nAlphaFrom)
		end
		return
	end
	if not _ENV(A1_235.szType, A2_236, A3_237, A1_235.nAlphaDelay, A1_235.nAlphaTime, A4_238) then
		return
	end
	local L5_239 = L5_239
	local L10_244 = L10_244
	local L11_245 = L11_245
	local L5_239, L12_246 = L5_239(L10_244, L11_245, A1_235.nAlphaDelay, A3_237, A1_235.tAlphaCurve, A1_235.szType, A4_238), L12_246
	L10_244 = L15_15
	L11_245 = A1_235.nAlphaFrom
	L12_246 = A1_235.nAlphaTo
	L10_244 = L10_244(L11_245, L12_246, L5_239)
	L12_246 = A0_234
	L11_245 = A0_234.SetAlpha
	L11_245(L12_246, L10_244)
	local L9_243 = L9_243
end
L23_23(L24_24, L25_25)
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_247, A1_248, A2_249, A3_250, A4_251)
	if not A1_248.bRotateEnable or A2_249 <= A1_248.nRotateDelay or not _ENV(A1_248.szType, A2_249, A3_250, A1_248.nRotateDelay, A1_248.nRotateTime, A4_251) then
		return
	end
	local L5_252 = L5_252
	L5_252 = L5_252(A2_249, A1_248.nRotateTime, A1_248.nRotateDelay, A3_250, A1_248.tRotateCurve, A1_248.szType, A4_251)
	local L6_253 = L6_253
	L6_253 = L6_253(A1_248.nRotateFrom, A1_248.nRotateTo, L5_252)
	local L7_254 = A0_247:GetAbsX()
	L7_254 = L7_254 + A0_247:GetW() * A1_248.fOffsetX
	local L8_255 = A0_247:GetAbsY()
	L8_255 = L8_255 + A0_247:GetH() * A1_248.fOffsetY
	local L13_260 = L13_260
	L13_260(A0_247, L7_254 - A0_247:GetW() * A1_248.fOffsetX, L8_255 - A0_247:GetH() * A1_248.fOffsetY)
	L13_260 = A0_247.SetRotate
	local L10_257 = L10_257
	local L12_259 = L6_253 / 180 * math.pi
	L13_260(L10_257, L12_259)
end
L23_23(L24_24, L25_25)
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_261, A1_262, A2_263, A3_264, A4_265)
	if not A1_262.bPivotRotateEnable or A2_263 <= A1_262.nPivotRotateDelay or not _ENV(A1_262.szType, A2_263, A3_264, A1_262.nPivotRotateDelay, A1_262.nPivotRotateTime, A4_265) then
		return
	end
	if not A0_261.SetPivotPos or not A0_261.SetPivotRotate then
		return
	end
	local L5_266 = L5_266
	local L6_267 = L6_267
	local L5_266, L12_273 = L5_266(L6_267, A1_262.nPivotRotateTime, A1_262.nPivotRotateDelay, A3_264, A1_262.tPivotRotateCurve, A1_262.szType, A4_265), L12_273
	L6_267 = A1_262.nPivotRotateFrom
	L12_273 = A1_262.bPivotRotateIgnoreFrom
	if L12_273 then
		L12_273 = A1_262._nPivotRotateFrom
		L6_267 = L12_273 or L6_267
		if not L12_273 then
			L6_267 = 0
		end
	end
	L12_273 = L15_15
	L12_273 = L12_273(L6_267, A1_262.nPivotRotateTo, L5_266)
	local L8_269, L9_270 = L8_269, L9_270
	local L11_272 = L12_273 / 180 * math.pi
	L8_269(L9_270, L11_272)
	L8_269 = L12_273 / 180
	L9_270 = math
	L9_270 = L9_270.pi
	L8_269 = L8_269 * L9_270
	A1_262._fPivotRotate = L8_269
end
L23_23(L24_24, L25_25)
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_274, A1_275, A2_276, A3_277, A4_278)
	if not A1_275.bSizeEnable or not _ENV(A1_275.szType, A2_276, A3_277, A1_275.nSizeDelay, A1_275.nSizeTime, A4_278) then
		return
	end
	local L5_279 = L5_279
	local L10_284 = L10_284
	local L11_285 = L11_285
	local L5_279, L12_286 = L5_279(L10_284, L11_285, A1_275.nSizeDelay, A3_277, A1_275.tSizeCurve, A1_275.szType, A4_278), L12_286
	L10_284 = A1_275.bSizeIgnoreW
	if not L10_284 then
		L10_284 = L15_15
		L11_285 = A1_275.nSizeFromW
		L12_286 = A1_275.nSizeToW
		L10_284 = L10_284(L11_285, L12_286, L5_279)
		L12_286 = A0_274
		L11_285 = A0_274.SetW
		L11_285(L12_286, L10_284)
	end
	L10_284 = A1_275.bSizeIgnoreH
	if not L10_284 then
		L10_284 = L15_15
		L11_285 = A1_275.nSizeFromH
		L12_286 = A1_275.nSizeToH
		L10_284 = L10_284(L11_285, L12_286, L5_279)
		L12_286 = A0_274
		L11_285 = A0_274.SetH
		L11_285(L12_286, L10_284)
	end
	L10_284 = ImageMgr_GetOrgObj
	L11_285 = A0_274
	L10_284 = L10_284(L11_285)
	if not L10_284 then
		L10_284 = A0_274
	end
	L12_286 = L10_284
	L11_285 = L10_284.GetParent
	L11_285 = L11_285(L12_286)
end
L23_23(L24_24, L25_25)
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_287, A1_288, A2_289, A3_290, A4_291)
	if not A1_288.bVisibleEnable then
		return
	end
	local L5_292 = L5_292
	local L5_292, L9_296 = L5_292(A1_288.szType, A2_289, A3_290, A4_291), L9_296
	L9_296 = A1_288._bVisibleExecuted
	if not L9_296 then
		L9_296 = A1_288.nVisibleDelay
		if L5_292 >= L9_296 then
			A1_288._bVisibleExecuted = true
			L9_296 = A0_287.Show
			local L7_294 = L7_294
			L9_296(L7_294, A1_288.bVisibleValue)
			local L8_295 = L8_295
		end
	end
	L9_296 = A1_288.szType
	if L9_296 ~= "Once" then
		L9_296 = A1_288.nVisibleDelay
		if L5_292 < L9_296 then
			A1_288._bVisibleExecuted = false
		end
	end
end
L23_23(L24_24, L25_25)
L23_23 = table
L23_23 = L23_23.insert
L24_24 = L5_5
function L25_25(A0_297, A1_298, A2_299, A3_300, A4_301)
	if not A1_298.bPlayEnable then
		return
	end
	local L5_302 = L5_302
	local L8_305 = L8_305
	local L5_302, L9_306 = L5_302(L8_305, A2_299, A3_300, A4_301), L9_306
	L8_305 = A1_298._bPlayExecuted
	if not L8_305 then
		L8_305 = A1_298.nPlayDelay
		if L5_302 >= L8_305 then
			A1_298._bPlayExecuted = true
			L8_305 = A0_297.Play
			if L8_305 then
				L9_306 = A0_297
				L8_305 = A0_297.Play
				L8_305(L9_306)
			end
		end
	end
	L8_305 = A1_298.szType
	if L8_305 ~= "Once" then
		L8_305 = A1_298.nPlayDelay
		if L5_302 < L8_305 then
			A1_298._bPlayExecuted = false
		end
	end
end
L23_23(L24_24, L25_25)
function L23_23(A0_307)
	if A0_307.bEndForceTo then
		ToEnd(A0_307.obj)
	end
	if A0_307.bUseImageReplace then
		FreeImage(A0_307.obj)
	end
	if A0_307.obj and A0_307.obj.bCommonAlpha then
		A0_307.obj.bCommonAlpha = nil
		local L2_309 = L2_309
		L2_309(A0_307.obj, true)
		local L3_310 = L3_310
	end
	L2_309 = A0_307.fnFinish
	if L2_309 then
		L2_309 = A0_307.fnFinish
		L2_309()
	end
end
function L24_24(A0_311)
	local L1_312, L2_313, L3_314
	L1_312 = A0_311.nRepeatCount
	if L1_312 then
		L1_312 = A0_311.nRepeatCount
		if not (L1_312 <= 0) then
			goto lbl_9
		end
	end
	L1_312 = false
	do return L1_312 end
	::lbl_9::
	L1_312 = A0_311.nTotalTime
	L2_313 = A0_311.szType
	if L2_313 == "PingPong" then
		L1_312 = L1_312 * 2
	end
	if L1_312 <= 0 then
		L2_313 = true
		return L2_313
	end
	L2_313 = A0_311.nCurTime
	L3_314 = A0_311.nRepeatCount
	L3_314 = L1_312 * L3_314
	L2_313 = L2_313 >= L3_314
	return L2_313
end
function L25_25()
	local L0_315 = GetTickCount()
	_ENV = L0_315
	L0_315 = m_nOldTime
	if not L0_315 then
		L0_315 = _ENV
	end
	m_nOldTime = L0_315
	L0_315 = _ENV
	L4_319 = m_nOldTime
	L0_315 = L0_315 - L4_319
	L8_8 = L0_315
	L0_315 = _ENV
	m_nOldTime = L0_315
	L0_315 = 0
	L4_319 = pairs
	L9_324 = L0_0
	L4_319, L9_324, L10_325 = L4_319(L9_324)
	for L14_329, L15_330 in L4_319, L9_324, L10_325 do
		if L15_330.obj and L15_330.obj:IsValid() then
			L15_330.nCurTime = L15_330.nCurTime + L8_8
			_FOR_, _FOR_, _FOR_ = ipairs(L15_330)
			for _FORV_9_, _FORV_10_ in _FOR_, _FOR_, _FOR_ do
				_FOR_, _FOR_, _FOR_ = ipairs(L5_5)
				for _FORV_14_, _FORV_15_ in _FOR_, _FOR_, _FOR_ do
					_FORV_15_(L15_330.obj, _FORV_10_, L15_330.nCurTime, L15_330.nTotalTime, L15_330.nLoopStartTime)
				end
			end
			L13_328 = L24_24
			L16_331 = L15_330
			L13_328 = L13_328(L16_331)
			if L13_328 then
				L13_328 = L0_0
				L13_328[L14_329] = nil
				L13_328 = L23_23
				L16_331 = L15_330
				L13_328(L16_331)
			end
			repeat
				L0_315 = L0_315 + 1
				do break end -- pseudo-goto
				L13_328 = L0_0
				L13_328[L14_329] = nil
			until true
		end
	end
	if L0_315 == 0 then
		L4_319 = m_refUpdate
		if L4_319 ~= nil then
			L4_319 = UnRegisterEvent
			L9_324 = "RENDER_FRAME_UPDATE"
			L10_325 = m_refUpdate
			L4_319(L9_324, L10_325)
			L4_319 = nil
			m_refUpdate = L4_319
			L4_319 = nil
			m_nOldTime = L4_319
		end
	end
end
function L26_26(A0_337, A1_338)
	local L2_339 = L2_339
	local L2_339, L3_340 = L2_339 .. A1_338, L3_340
	return L2_339
end
GetKey = L26_26
function L26_26(A0_341)
	local L1_342 = L1_342
	L1_342 = L1_342(A0_341)
	if L1_342 and L1_342:IsValid() then
		L1_342:Show()
	end
	local L2_343 = L2_343
	L2_343(A0_341)
	local L3_344 = L3_344
end
FreeImage = L26_26
function L26_26(A0_345, A1_346)
	local L2_347, L3_348, L4_349
	L4_349 = 0
	L9_354 = ipairs
	L9_354, _FOR_, _FOR_ = L9_354(A0_345)
	for _FORV_8_, _FORV_9_ in L9_354, _FOR_, _FOR_ do
		if L4_349 < _FORV_9_.nPosTime + _FORV_9_.nPosDelay then
			if not _FORV_9_.bPosIgnoreX then
				L2_347 = _FORV_9_.nPosToX
			end
			if not _FORV_9_.bPosIgnoreY then
				L3_348 = _FORV_9_.nPosToY
			end
			L4_349 = _FORV_9_.nPosTime + _FORV_9_.nPosDelay
		end
	end
	if L2_347 then
		L10_355 = A1_346
		L9_354 = A1_346.SetAbsX
		L11_356 = A1_346.GetParent
		L11_356 = L11_356(A1_346)
		L11_356 = L11_356.GetAbsX
		L11_356 = L11_356(L11_356)
		L11_356 = L11_356 + L2_347
		L9_354(L10_355, L11_356)
	end
	if L3_348 then
		L10_355 = A1_346
		L9_354 = A1_346.SetAbsY
		L11_356 = A1_346.GetParent
		L11_356 = L11_356(A1_346)
		L11_356 = L11_356.GetAbsY
		L11_356 = L11_356(L11_356)
		L11_356 = L11_356 + L3_348
		L9_354(L10_355, L11_356)
	end
end
MoveToTargetPos = L26_26
function L26_26(A0_357)
	local L1_358 = A0_357:GetName()
	local L2_359 = A0_357:GetParent()
	while L2_359 ~= nil do
		local L1_358, L5_362 = L2_359:GetName() .. "_" .. L1_358, L5_362
		L5_362 = L2_359.GetParent
		local L5_362, L4_361 = L5_362(L2_359), L4_361
		L2_359 = L5_362
	end
	return L1_358
end
GetObjPath = L26_26
function L26_26(A0_363)
	local L1_364
	L1_364 = _ENV
	L1_364 = L1_364[A0_363]
	L1_364 = L1_364 ~= nil
	return L1_364
end
IsPlaying = L26_26
function L26_26(A0_365)
	if A0_365.IsCommonAlpha ~= nil and A0_365:IsCommonAlpha() then
		A0_365.bCommonAlpha = true
		local L2_366 = L2_366
		L2_366(A0_365, false)
		local L3_367 = L3_367
	end
end
function L27_27(A0_368, A1_369, A2_370)
	local L3_371, L4_372, L5_373, L6_374, L7_375, L8_376
	L4_372 = 1
	L5_373 = 1
	L6_374 = nil
	L7_375 = 0
	L8_376 = 0
	if A0_368 then
		L13_381 = A0_368
		L12_380 = A0_368.IsValid
		L12_380 = L12_380(L13_381)
		if L12_380 and A1_369 then
			goto lbl_16
		end
	end
	L12_380 = nil
	do return L12_380 end
	::lbl_16::
	A0_368.bInit = false
	A1_369.obj = A0_368
	A1_369._obj = A0_368
	A1_369.nCurTime = 0
	L13_381 = A0_368
	L12_380 = A0_368.GetParent
	L12_380 = L12_380(L13_381)
	L3_371 = L12_380
	if L3_371 then
		L13_381 = A0_368
		L12_380 = A0_368.GetAbsX
		L12_380 = L12_380(L13_381)
		L13_381 = L3_371.GetAbsX
		L13_381 = L13_381(L3_371)
		L7_375 = L12_380 - L13_381
		L13_381 = A0_368
		L12_380 = A0_368.GetAbsY
		L12_380 = L12_380(L13_381)
		L13_381 = L3_371.GetAbsY
		L13_381 = L13_381(L3_371)
		repeat
			L8_376 = L12_380 - L13_381
			do break end -- pseudo-goto
			L13_381 = A0_368
			L12_380 = A0_368.GetAbsX
			L12_380 = L12_380(L13_381)
			L7_375 = L12_380
			L13_381 = A0_368
			L12_380 = A0_368.GetAbsY
			L12_380 = L12_380(L13_381)
			L8_376 = L12_380
		until true
	end
	A0_368.nStartRelPosX = L7_375
	A0_368.nStartRelPosY = L8_376
	L12_380 = ImageMgr_GetOrgObj
	L13_381 = A0_368
	L12_380 = L12_380(L13_381)
	L6_374 = L12_380
	if L6_374 and L6_374 ~= A0_368 then
		L13_381 = L6_374
		L12_380 = L6_374.IsValid
		L12_380 = L12_380(L13_381)
		if L12_380 then
			L6_374.nStartRelPosX = L7_375
			L6_374.nStartRelPosY = L8_376
		end
	end
	L12_380 = A1_369.obj
	L12_380 = L12_380.GetPivotScale
	if L12_380 then
		L12_380 = A1_369.obj
		L13_381 = L12_380
		L12_380 = L12_380.GetPivotScale
		L12_380, L13_381 = L12_380(L13_381)
		L5_373 = L13_381
		L4_372 = L12_380
		L12_380 = A1_369.obj
		L13_381 = L4_372 or L13_381
		if not L4_372 then
			L13_381 = 1
		end
		L12_380._TweenPivotScaleX = L13_381
		L12_380 = A1_369.obj
		L13_381 = L5_373 or L13_381
		if not L5_373 then
			L13_381 = 1
		end
		L12_380._TweenPivotScaleY = L13_381
	else
		L12_380 = A1_369.obj
		L12_380._TweenPivotScaleX = 1
		L12_380 = A1_369.obj
		L12_380._TweenPivotScaleY = 1
	end
	L12_380 = ipairs
	L13_381 = A1_369
	L12_380, L13_381, _FOR_ = L12_380(L13_381)
	for _FORV_12_, _FORV_13_ in L12_380, L13_381, _FOR_ do
		_FORV_13_._fScaleX = 1
		_FORV_13_._fScaleY = 1
		if not A1_369.obj._TweenPivotScaleX then
		end
		_FORV_13_._fPivotScaleX = 1
		if not A1_369.obj._TweenPivotScaleY then
		end
		_FORV_13_._fPivotScaleY = 1
		if A1_369.obj.GetPivotRotate then
			_FORV_13_._fPivotRotate = A1_369.obj:GetPivotRotate()
			_FORV_13_._nPivotRotateFrom = _FORV_13_._fPivotRotate * 180 / math.pi
		else
			_FORV_13_._fPivotRotate = 0
			_FORV_13_._nPivotRotateFrom = 0
		end
		if A1_369.obj.SetPivotPos and (_FORV_13_.bPivotScaleEnable or _FORV_13_.bPivotRotateEnable) then
			A1_369.obj:SetPivotPos(_FORV_13_.fOffsetX, _FORV_13_.fOffsetY)
			local L17_385 = L17_385
		end
		_FORV_13_._bVisibleExecuted = nil
		if not A2_370 then
			L17_385 = _FORV_13_.bPlayEnable
			if L17_385 then
				goto lbl_139
			end
		end
		L17_385 = nil
		::lbl_139::
		_FORV_13_._bPlayExecuted = L17_385
	end
	return A0_368
end
function L28_28(A0_386)
	local L1_387
	L1_387 = {}
	L4_390 = pairs
	L5_391 = A0_386
	L4_390, L5_391, L6_392 = L4_390(L5_391)
	for L10_396, _FORV_6_ in L4_390, L5_391, L6_392 do
		if type(L10_396) == "number" and type(_FORV_6_) == "table" then
			local _FOR_, _FOR_, _FOR_, L9_395 = pairs(_FORV_6_)
			repeat
				for _FORV_11_, _FORV_12_ in _FOR_, _FOR_, _FOR_ do
					L9_395[_FORV_11_] = _FORV_12_
				end
				L1_387[L10_396] = L9_395
				do break end -- pseudo-goto
				L1_387[L10_396] = _FORV_6_
			until true
		end
	end
	return L1_387
end
function L29_29(A0_397, A1_398)
	local L6_403 = math.max
	L7_404 = 0
	L10_407 = tonumber
	L11_408 = A1_398
	L10_407 = L10_407(L11_408)
	if not L10_407 then
		L10_407 = 0
	end
	L6_403 = L6_403(L7_404, L10_407)
	A0_397.nCurTime = L6_403
	L7_404 = ipairs
	L10_407 = A0_397
	L7_404, L10_407, L11_408 = L7_404(L10_407)
	for L12_409, _FORV_7_ in L7_404, L10_407, L11_408 do
		_FOR_, _FOR_, _FOR_ = ipairs(_ENV)
		for _FORV_11_, _FORV_12_ in _FOR_, _FOR_, _FOR_ do
			_FORV_12_(A0_397.obj, _FORV_7_, L6_403, A0_397.nTotalTime, A0_397.nLoopStartTime)
		end
	end
	L7_404 = true
	return L7_404
end
function L30_30(A0_416, A1_417, A2_418, A3_419, A4_420)
	local L5_421
	L5_421 = A1_417
	if not (A0_416 and A0_416:IsValid()) or not A1_417 then
		return false
	end
	if A4_420 then
		L5_421 = _ENV(A1_417)
	end
	local L9_425 = L27_27(A0_416, L5_421, A3_419)
	A0_416 = L9_425
	if not A0_416 then
		L9_425 = false
		return L9_425
	end
	L9_425 = L29_29
	local L7_423 = L7_423
	do return L9_425(L7_423, A2_418) end
	local L8_424 = L8_424
end
function L31_31(A0_426, A1_427)
	if not (A0_426 and A0_426:IsValid()) or not A1_427 then
		return false
	end
	local L2_428 = L2_428
	local L3_429 = L3_429
	L2_428(L3_429, 0)
	local L4_430 = L4_430
	L2_428 = true
	return L2_428
end
function L32_32(A0_431, A1_432, A2_433, A3_434, A4_435)
	if A0_431 then
		local L5_436 = A0_431:IsValid()
		if L5_436 then
			goto lbl_8
		end
	end
	do return end
	::lbl_8::
	L5_436 = _ENV
	L5_436 = L5_436[A0_431]
	if L5_436 then
		L23_23(L5_436)
	end
	if A2_433.bUseImageReplace then
		A2_433.obj = ImageMgr_Create(A0_431)
	else
		A2_433.obj = A0_431
	end
	if not A0_431 or not A0_431:IsValid() then
		Stop(A0_431)
		return
	end
	A2_433.fnFinish = A1_432
	L27_27(A2_433.obj, A2_433, true)
	if A2_433.bUseImageReplace then
		A0_431:Hide()
		local L11_442 = L11_442
		L11_442(A2_433.obj, A0_431:GetParent():GetAbsX() + A0_431:GetRelX(), A0_431:GetParent():GetAbsY() + A0_431:GetRelY())
		L11_442 = A2_433.obj
		L11_442 = L11_442.SetImageType
		L11_442(L11_442, 6)
		L11_442 = A0_431.GetRelX
		L11_442 = L11_442(A0_431)
		A2_433.obj.nStartRelPosX = L11_442
		A2_433.obj.nStartRelPosY = A0_431:GetRelY()
		A0_431.nStartRelPosY, A0_431.nStartRelPosX = A0_431:GetRelY(), L11_442
	end
	L11_442 = L31_31
	L11_442(A2_433.obj, A2_433)
	L11_442 = _ENV
	L11_442[A0_431] = A2_433
	L11_442 = m_refUpdate
	if not L11_442 then
		L11_442 = RegisterEvent
		local L7_438 = L7_438
		local L11_442, L8_439 = L11_442(L7_438, L25_25), L8_439
		m_refUpdate = L11_442
	end
end
Play = L32_32
function L32_32(A0_443)
	if not A0_443 then
		return
	end
	local L1_444 = L1_444
	L1_444 = L1_444(A0_443)
	if not L1_444 then
		L1_444 = A0_443
	end
	if L1_444 then
		local L2_445 = L1_444:IsValid()
		if L2_445 then
			goto lbl_17
		end
	end
	do return end
	::lbl_17::
	L2_445 = _ENV
	L2_445 = L2_445[L1_444]
	if not L2_445 then
		return
	end
	local L3_446 = L3_446
	local L4_447 = L4_447
	if not L2_445.nTotalTime then
	end
	L3_446(L4_447, 0)
	local L5_448 = L5_448
end
ToEnd = L32_32
function L32_32(A0_449, A1_450, A2_451)
	if not IsPlaying(A0_449) then
		return
	end
	if A1_450 == nil or A1_450 then
		ToEnd(A0_449)
	end
	if A0_449.bCommonAlpha then
		A0_449.bCommonAlpha = nil
		local L3_452 = L3_452
		L3_452(A0_449, true)
	end
	if A2_451 == nil or A2_451 then
		L3_452 = _ENV
		L3_452 = L3_452[A0_449]
		if L3_452 and L3_452.bUseImageReplace then
			local L4_453 = L4_453
			L4_453(L3_452.obj)
			local L5_454 = L5_454
		end
		L4_453 = _ENV
		L4_453[A0_449] = nil
	end
end
Stop = L32_32
function L32_32(A0_455)
	L3_458 = _ENV
	L1_456, L3_458, L4_459 = L1_456(L3_458)
	for L5_460, _FORV_5_ in L1_456, L3_458, L4_459 do
		if A0_455 and _FORV_5_.fnFinish then
			_FORV_5_.fnFinish()
		end
		if _FORV_5_.bUseImageReplace then
			FreeImage(_FORV_5_.obj)
			local L7_462 = L7_462
		end
	end
	L1_456 = {}
	_ENV = L1_456
	L1_456 = {}
	L1_1 = L1_456
	L1_456 = {}
	m_tTest = L1_456
end
StopAllAni = L32_32
function L32_32(A0_463)
	local L1_464 = A0_463:GetRoot()
	local L2_465 = L2_465
	local L3_466 = L3_466
	local L2_465, L4_467 = L2_465 .. L3_466 .. "_AniBind.ini", L4_467
	return L2_465
end
function L33_33()
	local L0_468, L1_469
	L0_468 = _ENV
	L0_468 = 0 < L0_468
	return L0_468
end
function L34_34()
	local L1_470
	L1_470 = _ENV
	L1_470 = L1_470 + 1
	_ENV = L1_470
end
EnterEditorSeekContext = L34_34
function L34_34()
	local L1_471
	L1_471 = _ENV
	if 0 < L1_471 then
		L1_471 = _ENV
		L1_471 = L1_471 - 1
		_ENV = L1_471
	end
end
LeaveEditorSeekContext = L34_34
function L34_34(A0_472)
	local L1_473, L2_474, L3_475, L4_476, L5_477, L6_478, L7_479, L8_480, L9_481
	L2_474 = 0
	L3_475 = 0
	L4_476 = 0
	L5_477 = 0
	L6_478 = 0
	L7_479 = 0
	L8_480 = 1
	L9_481 = 1
	if not A0_472 or not A0_472:IsValid() then
		return nil
	end
	L1_473 = _ENV[A0_472]
	if L1_473 then
		return L1_473
	end
	L2_474, L3_475 = A0_472:GetRelPos()
	L4_476, L5_477 = A0_472:GetAbsPos()
	L6_478, L7_479 = A0_472:GetSize()
	if A0_472.GetPivotScale then
		local L10_482 = A0_472:GetPivotScale()
		L9_481 = A0_472:GetPivotScale()
		L8_480 = L10_482
	end
	L10_482 = {}
	if not L2_474 then
	end
	L10_482.nRelX = 0
	if not L3_475 then
	end
	L10_482.nRelY = 0
	if not L4_476 then
	end
	L10_482.nAbsX = 0
	if not L5_477 then
	end
	L10_482.nAbsY = 0
	if not L6_478 then
	end
	L10_482.nW = 0
	if not L7_479 then
	end
	L10_482.nH = 0
	if not A0_472.GetAlpha or not A0_472:GetAlpha() then
	end
	L10_482.nAlpha = nil
	if not A0_472.IsVisible or not A0_472:IsVisible() then
	end
	L10_482.bVisible = nil
	if not A0_472.GetRotate or not A0_472:GetRotate() then
	end
	L10_482.fRotate = nil
	if not L8_480 then
	end
	L10_482.fPivotScaleX = 1
	if not L9_481 then
	end
	L10_482.fPivotScaleY = 1
	if not A0_472.GetPivotRotate or not A0_472:GetPivotRotate() then
	end
	L10_482.fPivotRotate = nil
	if A0_472.IsCommonAlpha then
		local L11_483, L12_484 = A0_472:IsCommonAlpha(), L12_484
		if L11_483 then
			goto lbl_118
		end
	end
	L11_483 = nil
	::lbl_118::
	L10_482.bCommonAlpha = L11_483
	L10_482.fAppliedScaleX = 1
	L10_482.fAppliedScaleY = 1
	L1_473 = L10_482
	L10_482 = _ENV
	L10_482[A0_472] = L1_473
	return L1_473
end
function L35_35(A0_485)
	local L1_486
	if A0_485 then
		L1_486 = _ENV
		L1_486[A0_485] = nil
	end
end
function L36_36(A0_487)
	local L1_488, L2_489, L3_490
	L2_489 = 1
	L3_490 = 1
	if not A0_487 or not A0_487:IsValid() then
		return false
	end
	L1_488 = _ENV[A0_487]
	if not L1_488 then
		return false
	end
	if A0_487.Scale then
		L2_489 = 1 / L1_488.fAppliedScaleX or L2_489
		if L1_488.fAppliedScaleX == 0 or not (1 / L1_488.fAppliedScaleX) then
			L2_489 = 1
		end
		L3_490 = 1 / L1_488.fAppliedScaleY or L3_490
		if L1_488.fAppliedScaleY == 0 or not (1 / L1_488.fAppliedScaleY) then
			L3_490 = 1
		end
		if math.abs(L2_489 - 1) > 1.0E-4 then
			A0_487:Scale(L2_489, 1)
		end
		if math.abs(L3_490 - 1) > 1.0E-4 then
			A0_487:Scale(1, L3_490)
		end
	end
	if A0_487.SetPivotScale then
		if not L1_488.fPivotScaleX then
		end
		if not L1_488.fPivotScaleY then
		end
		A0_487:SetPivotScale(1, 1)
	end
	if L1_488.fPivotRotate ~= nil and A0_487.SetPivotRotate then
		A0_487:SetPivotRotate(L1_488.fPivotRotate)
	end
	if L1_488.fRotate ~= nil and A0_487.SetRotate then
		A0_487:SetRotate(L1_488.fRotate)
	end
	if L1_488.nW ~= nil then
		A0_487:SetW(L1_488.nW)
	end
	if L1_488.nH ~= nil then
		A0_487:SetH(L1_488.nH)
	end
	if L1_488.nAlpha ~= nil then
		A0_487:SetAlpha(L1_488.nAlpha)
	end
	if L1_488.bCommonAlpha ~= nil and A0_487.SetCommonAlpha then
		A0_487:SetCommonAlpha(L1_488.bCommonAlpha)
	end
	if L1_488.bVisible ~= nil then
		A0_487:Show(L1_488.bVisible)
	end
	if not L1_488.nRelX then
	end
	A0_487:SetRelX(0)
	if not L1_488.nRelY then
	end
	A0_487:SetRelY(0)
	local L4_491, L5_492 = L4_491, L5_492
	if not L1_488.nAbsX then
	end
	local L6_493 = L6_493
	if not L1_488.nAbsY then
	end
	L4_491(L5_492, L6_493, 0)
	local L7_494 = L7_494
	L1_488.fAppliedScaleX = 1
	L1_488.fAppliedScaleY = 1
	L4_491 = true
	return L4_491
end
function L37_37(A0_495, A1_496, A2_497)
	local L3_498
	if not (A0_495 and A0_495:IsValid()) or not A1_496 then
		return nil, nil
	end
	L3_498 = _ENV(A0_495)
	L36_36(A0_495)
	local L4_499 = L4_499
	local L5_500 = L5_500
	local L6_501 = L6_501
	local L4_499, L7_502 = L4_499(L5_500, L6_501, A2_497), L7_502
	A0_495 = L4_499
	L4_499 = A0_495
	L5_500 = L3_498
	return L4_499, L5_500
end
function L38_38(A0_503, A1_504)
	if not A0_503 or not A1_504 then
		return
	end
	L4_507 = ipairs
	L5_508 = A1_504
	L4_507, L5_508, L6_509 = L4_507(L5_508)
	for L7_510, _FORV_6_ in L4_507, L5_508, L6_509 do
		if _FORV_6_._fScaleX then
			A0_503.fAppliedScaleX = _FORV_6_._fScaleX
		end
		if _FORV_6_._fScaleY then
			A0_503.fAppliedScaleY = _FORV_6_._fScaleY
		end
	end
end
function L39_39(A0_511, A1_512, A2_513, A3_514)
	local L4_515
	local L8_519 = _ENV(A0_511, A1_512, A3_514)
	L4_515 = _ENV(A0_511, A1_512, A3_514)
	A0_511 = L8_519
	if not A0_511 then
		L8_519 = false
		return L8_519
	end
	L8_519 = L29_29
	L8_519(A1_512, A2_513)
	L8_519 = L38_38
	local L6_517 = L6_517
	L8_519(L6_517, A1_512)
	local L7_518 = L7_518
	L8_519 = true
	return L8_519
end
function PlaySingleAnimation(A0_520, A1_521, A2_522, A3_523)
	if not A0_520 or not A0_520:IsValid() then
		return false
	end
	if not (A1_521 and A1_521 ~= "" and A2_522) or A2_522 == "" then
		return false
	end
	local L4_524 = L4_524
	L4_524 = L4_524(A1_521)
	if not L4_524 then
		return false
	end
	local L5_525 = L5_525
	L5_525 = L5_525(L4_524, A2_522)
	L4_524:Close()
	if not L5_525 then
		return false
	end
	if _ENV() then
		L36_36(A0_520)
		L35_35(A0_520)
	end
	local L6_526 = L6_526
	local L7_527 = L7_527
	local L8_528 = L8_528
	local L9_529 = L9_529
	local L10_530 = L10_530
	L6_526(L7_527, L8_528, L9_529, L10_530, A2_522)
	local L11_531 = L11_531
	L6_526 = true
	return L6_526
end
function SetSingleAnimationTime(A0_532, A1_533, A2_534, A3_535)
	local L4_536, L5_537
	if not _ENV() then
		return false
	end
	if not A0_532 or not A0_532:IsValid() then
		return false
	end
	if not (A1_533 and A1_533 ~= "" and A2_534) or A2_534 == "" then
		return false
	end
	L4_536 = Ini.Open(A1_533)
	if not L4_536 then
		return false
	end
	L5_537 = LoadTween(L4_536, A2_534)
	L4_536:Close()
	if not L5_537 then
		return false
	end
	if IsPlaying(A0_532) then
		L34_34(A0_532)
		Stop(A0_532, false, true)
	end
	local L6_538 = L6_538
	local L7_539 = L7_539
	local L8_540 = L8_540
	local L9_541 = L9_541
	do return L6_538(L7_539, L8_540, L9_541, false) end
	local L10_542 = L10_542
end
function StopSingleAnimation(A0_543, A1_544)
	local L2_545
	if not A0_543 then
		L2_545 = false
		return L2_545
	end
	L2_545 = A0_543
	if A1_544 and A1_544 ~= "" then
		L2_545 = A0_543:ComponentLookup(A1_544)
	end
	if L2_545 and IsPlaying(L2_545) then
		local L5_548 = L5_548
		L5_548(L2_545, false, true)
		local L6_549 = L6_549
		L5_548 = _ENV
		L5_548 = L5_548()
		if L5_548 then
			L5_548 = L35_35
			L6_549 = L2_545
			L5_548(L6_549)
		end
		L5_548 = true
		return L5_548
	end
	L5_548 = _ENV
	L5_548 = L5_548()
	if L5_548 then
		L5_548 = L35_35
		L6_549 = L2_545
		L5_548(L6_549)
	end
	L5_548 = false
	return L5_548
end
function PlayAnimationGroup(A0_550, A1_551, A2_552, A3_553)
	local L14_564 = L14_564
	if not A0_550 then
		L14_564 = false
		return L14_564
	end
	if not A1_551 then
		L14_564 = _ENV
		L14_564 = L14_564(A0_550)
		A1_551 = L14_564
	end
	L14_564 = Ini
	L14_564 = L14_564.Open
	L14_564 = L14_564(A1_551)
	if not L14_564 then
		return false
	end
	local L5_555 = L5_555
	L5_555 = L5_555(L14_564, A2_552, "TweenFile", "")
	local L6_556 = L6_556
	local L9_559 = L9_559
	local L6_556, L10_560 = L6_556(L9_559, A2_552, "Count", 0), L10_560
	if L6_556 == 0 then
		L10_560 = L14_564
		L9_559 = L14_564.Close
		L9_559(L10_560)
		L9_559 = false
		return L9_559
	end
	L9_559 = _UPVALUE1_
	L10_560 = A0_550
	L9_559(L10_560)
	L9_559 = false
	L10_560 = 0
	_FOR_ = 1
	local L13_563 = L13_563
	for _FORV_14_ = _FOR_, _FOR_, _FOR_ do
		local L15_565 = L15_565
		if L14_564:ReadString(A2_552, "Control_" .. _FORV_14_, "") ~= "" and L14_564:ReadString(A2_552, "TweenName_" .. _FORV_14_, "") ~= "" then
			if L14_564:ReadString(A2_552, "Control_" .. _FORV_14_, "") == A0_550:GetName() then
			else
			end
			if A0_550:ComponentLookup((L14_564:ReadString(A2_552, "Control_" .. _FORV_14_, ""))) and L14_564:ReadString(A2_552, "TweenFile_" .. _FORV_14_, L5_555) and L14_564:ReadString(A2_552, "TweenFile_" .. _FORV_14_, L5_555) ~= "" then
				if L33_33() then
					L36_36((A0_550:ComponentLookup((L14_564:ReadString(A2_552, "Control_" .. _FORV_14_, "")))))
					L35_35((A0_550:ComponentLookup((L14_564:ReadString(A2_552, "Control_" .. _FORV_14_, "")))))
				end
				if Ini.Open((L14_564:ReadString(A2_552, "TweenFile_" .. _FORV_14_, L5_555))) then
					local L20_570 = L20_570
					Ini.Open((L14_564:ReadString(A2_552, "TweenFile_" .. _FORV_14_, L5_555))):Close()
					if LoadTween(Ini.Open((L14_564:ReadString(A2_552, "TweenFile_" .. _FORV_14_, L5_555))), (L14_564:ReadString(A2_552, "TweenName_" .. _FORV_14_, ""))) then
						L10_560 = L10_560 + 1
						local L21_571 = L21_571
						local L22_572 = L22_572
						local L23_573 = L23_573
						local L24_574 = L24_574
						local L25_575 = L25_575
						Play(A0_550:ComponentLookup((L14_564:ReadString(A2_552, "Control_" .. _FORV_14_, ""))), L15_565, LoadTween(Ini.Open((L14_564:ReadString(A2_552, "TweenFile_" .. _FORV_14_, L5_555))), (L14_564:ReadString(A2_552, "TweenName_" .. _FORV_14_, ""))), L21_571, L22_572)
						local L26_576 = L26_576
					end
				end
			end
		end
	end
	L17_567 = L14_564
	L16_566 = L14_564.Close
	L16_566(L17_567)
	L16_566 = L13_563
	L16_566()
	L16_566 = true
	return L16_566
end
function SetAnimationGroupTime(A0_577, A1_578, A2_579, A3_580)
	local L4_581, L5_582
	L5_582 = {}
	L10_587 = _ENV
	L10_587 = L10_587()
	if not L10_587 then
		L10_587 = false
		return L10_587
	end
	if not A0_577 then
		L10_587 = false
		return L10_587
	end
	L10_587 = _UPVALUE1_
	L10_587 = L10_587(A0_577, A1_578, A2_579)
	L4_581 = L10_587
	if L4_581 then
		L10_587 = #L4_581
		if L10_587 ~= 0 then
			goto lbl_25
		end
	end
	L10_587 = false
	do return L10_587 end
	::lbl_25::
	L10_587 = ipairs
	L10_587, _FOR_, _FOR_ = L10_587(L4_581)
	for _FORV_9_, _FORV_10_ in L10_587, _FOR_, _FOR_ do
		if _FORV_10_.hControl and _FORV_10_.hControl:IsValid() and not L5_582[_FORV_10_.hControl] then
			L34_34(_FORV_10_.hControl)
			if IsPlaying(_FORV_10_.hControl) then
				Stop(_FORV_10_.hControl, false, true)
			end
			L36_36(_FORV_10_.hControl)
			L5_582[_FORV_10_.hControl] = true
		end
	end
	L10_587 = ipairs
	L10_587, L7_584, _FOR_ = L10_587(L4_581)
	for _FORV_9_, _FORV_10_ in L10_587, L7_584, _FOR_ do
		if Ini.Open(_FORV_10_.szTweenFile) then
			Ini.Open(_FORV_10_.szTweenFile):Close()
			if LoadTween(Ini.Open(_FORV_10_.szTweenFile), _FORV_10_.szTweenName) then
				local L14_591 = L14_591
				local L15_592 = L15_592
				local L16_593 = L16_593
				L16_593(_FORV_10_.hControl, LoadTween(Ini.Open(_FORV_10_.szTweenFile), _FORV_10_.szTweenName), A3_580, false)
				local L17_594 = L17_594
			end
		end
	end
	L10_587 = true
	return L10_587
end
function StopAnimationGroup(A0_595, A1_596, A2_597, A3_598)
	if not A0_595 then
		return false
	end
	if not A1_596 then
		A1_596 = _ENV(A0_595)
	end
	local L4_599 = L4_599
	L4_599 = L4_599(A1_596)
	if not L4_599 then
		_UPVALUE1_(A0_595)
		return false
	end
	local L5_600 = L5_600
	L5_600 = L5_600(L4_599, A2_597, "Count", 0)
	if L5_600 == 0 then
		L9_604 = L4_599.Close
		L9_604(L4_599)
		L9_604 = _UPVALUE1_
		L9_604(A0_595)
		L9_604 = false
		return L9_604
	end
	L9_604 = 1
	_FOR_ = 1
	for _FORV_9_ = L9_604, _FOR_, _FOR_ do
		if L4_599:ReadString(A2_597, "Control_" .. _FORV_9_, "") ~= "" and L4_599:ReadString(A2_597, "TweenName_" .. _FORV_9_, "") ~= "" and A0_595:ComponentLookup((L4_599:ReadString(A2_597, "Control_" .. _FORV_9_, ""))) then
			local L15_610 = L15_610
			Stop(A0_595:ComponentLookup((L4_599:ReadString(A2_597, "Control_" .. _FORV_9_, ""))), true, true)
			local L16_611 = L16_611
			if L33_33() then
				L35_35((A0_595:ComponentLookup((L4_599:ReadString(A2_597, "Control_" .. _FORV_9_, "")))))
			end
		end
	end
	L10_605 = L4_599
	L9_604 = L4_599.Close
	L9_604(L10_605)
	L9_604 = _UPVALUE1_
	L10_605 = A0_595
	L9_604(L10_605)
	L9_604 = true
	return L9_604
end
function PlayEnterAnimation(A0_612, A1_613, A2_614)
	local L4_615 = L4_615
	local L5_616 = L5_616
	local L6_617 = L6_617
	do return L4_615(L5_616, L6_617, "Animation_In", A2_614) end
	local L7_618 = L7_618
end
function StopEnterAnimation(A0_619, A1_620, A2_621)
	local L4_622 = L4_622
	local L5_623 = L5_623
	local L6_624 = L6_624
	do return L4_622(L5_623, L6_624, "Animation_In", A2_621) end
	local L7_625 = L7_625
end
function PlayExitAnimation(A0_626, A1_627, A2_628)
	local L4_629 = L4_629
	local L5_630 = L5_630
	local L6_631 = L6_631
	do return L4_629(L5_630, L6_631, "Animation_Out", A2_628) end
	local L7_632 = L7_632
end
function StopExitAnimation(A0_633, A1_634, A2_635)
	local L4_636 = L4_636
	local L5_637 = L5_637
	local L6_638 = L6_638
	do return L4_636(L5_637, L6_638, "Animation_Out", A2_635) end
	local L7_639 = L7_639
end
