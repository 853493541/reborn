local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8
L0_0 = table
local L0_0, L12_12 = L0_0.insert, L12_12
L1_1 = table
L1_1 = L1_1.remove
L2_2 = math
L2_2 = L2_2.max
L3_3 = math
L3_3 = L3_3.min
L4_4 = math
L4_4 = L4_4.ceil
L5_5 = math
L5_5 = L5_5.floor
L6_6 = math
L6_6 = L6_6.sin
L7_7 = math
L7_7 = L7_7.cos
L8_8 = math
L8_8 = L8_8.pi
L12_12 = setmetatable
local L10_10 = L10_10
;({}).__call = function(A0_13, ...)
	return A0_13:ctor(...)
end
;({}).__index = function(A0_14, A1_15)
	local L2_16 = L2_16
	L2_16 = L2_16(A0_14, "elems")
	if L2_16 and L2_16[A1_15] then
		return L2_16[A1_15].raw
	else
		local L3_17 = L3_17
		local L4_18 = L4_18
		do return L3_17(L4_18, A1_15) end
		local L5_19 = L5_19
	end
end
;({}).__metatable = true
local L12_12, L11_11 = L12_12(L10_10, {}), L11_11
XGUI = L12_12
L12_12 = XGUI
function L10_10(A0_20)
	local L1_21
	L1_21 = {}
	L1_21.raw = A0_20
	return L1_21
end
function L11_11(A0_22, A1_23)
	local L3_24 = L3_24
	local L4_25 = _ENV(A1_23)
	L3_24[1] = L4_25
	L3_24[2] = _ENV(A1_23)
	A0_22.elems = L3_24
	return A0_22
end
L12_12.ctor = L11_11
function L11_11(A0_26, A1_27)
	local L2_28, L3_29
	L2_28 = _ENV
	L3_29 = A0_26.elems
	local L4_30 = L4_30
	local L4_30, L5_31 = L4_30(A1_27)
	L2_28(L3_29, L4_30, L5_31)
	return A0_26
end
L12_12.Add = L11_11
function L11_11()
	local L0_32 = L0_32
	L0_32 = L0_32("Lowest/__XGUI")
	if not L0_32 then
		local L1_33 = L1_33
		local L1_33, L2_34 = L1_33("__XGUI"), L2_34
		L0_32 = L1_33
	end
	return L0_32
end
function L12_12.AppendContentFromIni(A0_35, A1_36, A2_37, A3_38)
	local L4_39 = _ENV()
	local L5_40 = L5_40
	L5_40 = L5_40(L4_39, "WndContainer_AppendContentTemp")
	L5_40:Clear()
	local L6_41 = L6_41
	L6_41 = nil
	if A3_38 then
		L6_41 = L5_40:AppendContentFromIni(A1_36, A2_37, A3_38)
	else
		L6_41 = L5_40:AppendContentFromIni(A1_36, A2_37)
	end
	if L6_41 then
		local L7_42, L8_43 = L7_42, L8_43
		local L9_44 = L9_44
		local L10_45 = L10_45
		L7_42(L8_43, L9_44, L10_45, true)
		local L11_46 = L11_46
	end
	return L6_41
end
function L12_12.ClearAllContent(A0_47)
	if not A0_47 then
		return
	end
	A0_47 = A0_47:GetFirstChild()
	while A0_47 do
		A0_47:Destroy()
		local L1_48, L2_49 = A0_47:GetNext(), L2_49
		A0_47 = L1_48
	end
end
function L12_12.UncheckBrothers(A0_50)
	if A0_50 then
		local L1_51 = A0_50:GetType()
		if L1_51 == "WndCheckBox" then
			L1_51 = A0_50.__group_id
			local L2_52 = A0_50:GetParent():GetFirstChild()
			while L2_52 do
				if L2_52:GetType() == "WndCheckBox" and L2_52.__group_id == L1_51 and L2_52 ~= A0_50 then
					L2_52:Check(false)
					local L5_55 = L5_55
				end
				L5_55 = L2_52.GetNext
				local L5_55, L4_54 = L5_55(L2_52), L4_54
				L2_52 = L5_55
			end
		end
	end
end
function L12_12.SeparateShow(A0_56)
	local L5_61 = A0_56:GetBaseType()
	if L5_61 == "Wnd" then
		L5_61 = A0_56.GetParent
		L5_61 = L5_61(A0_56)
		L5_61 = L5_61.GetFirstChild
		L5_61 = L5_61(L5_61)
		while L5_61 do
			L5_61:SetVisible(L5_61 == A0_56)
			L5_61 = L5_61:GetNext()
		end
	else
		L5_61 = A0_56.GetParent
		L5_61 = L5_61(A0_56)
		L2_58 = 0
		L3_59 = L5_61:GetItemCount()
		L3_59 = L3_59 - 1
		L4_60 = 1
		for _FORV_5_ = L2_58, L3_59, L4_60 do
			local L7_63, L8_64 = L7_63, L8_64
			L8_64(L5_61:Lookup(_FORV_5_), L5_61 == L5_61:Lookup(_FORV_5_))
			local L9_65 = L9_65
		end
	end
	return A0_56
end
function L12_12.DrawShape(A0_66, A1_67, A2_68, A3_69, A4_70, A5_71, A6_72, A7_73, A8_74)
	local L9_75, L13_79, L14_80 = L9_75, A1_67, L14_80
	L14_80 = A2_68
	L9_75 = L9_75(L13_79, L14_80)
	L13_79 = A4_70 * 64
	L14_80 = _ENV
	local L14_80, L12_78 = L14_80(128 * A3_69 / 360), L12_78
	L12_78 = L8_8
	L12_78 = L12_78 * (L9_75.nFaceDirection - L14_80)
	L12_78 = L12_78 / 128
	if L9_75.nFaceDirection > 256 - L14_80 then
		L12_78 = L12_78 - L8_8 - L8_8
	end
	if A3_69 <= 45 then
	end
	if A3_69 == 360 then
	end
	A0_66:SetTriangleFan(GEOMETRY_TYPE.TRIANGLE)
	A0_66:SetD3DPT(D3DPT.TRIANGLEFAN)
	A0_66:ClearTriangleFanPoint()
	if A1_67 == TARGET.DOODAD then
		A0_66:AppendDoodadID(L9_75.dwID, A5_71, A6_72, A7_73, A8_74)
	else
		A0_66:AppendCharacterID(L9_75.dwID, false, A5_71, A6_72, A7_73, A8_74)
	end
	local L15_81 = L15_81
	local L16_82 = L16_82
	repeat
		local L17_83 = L17_83
		local L18_84 = L18_84
		if A1_67 == TARGET.DOODAD then
			({})[1] = Scene_PlaneGameWorldPosToScene(L9_75.nX + L7_7(L12_78) * L13_79, L9_75.nY + L6_6(L12_78) * L13_79) - L17_83
			;({})[2] = 0
			;({})[3] = Scene_PlaneGameWorldPosToScene(L9_75.nX + L7_7(L12_78) * L13_79, L9_75.nY + L6_6(L12_78) * L13_79) - L18_84
			A0_66:AppendDoodadID(L9_75.dwID, A5_71, A6_72, A7_73, A8_74, {})
		else
			local L19_85, L20_86 = L19_85, L20_86
			local L21_87 = L21_87
			local L22_88 = L22_88
			local L23_89 = L23_89
			local L24_90 = L24_90
			local L25_91 = L25_91
			local L26_92 = L26_92
			local L27_93 = L27_93
			local L28_94 = L28_94
			local L29_95 = L29_95
			L29_95[1] = L19_85 - L17_83
			L29_95[2] = 0
			local L29_95[3], L30_96 = L20_86 - L18_84, L30_96
			L21_87(L22_88, L23_89, L24_90, L25_91, L26_92, L27_93, L28_94, L29_95)
		end
		L21_87 = L8_8
		L21_87 = L21_87 / L16_82
		L12_78 = L12_78 + L21_87
	until L15_81 < L12_78
end
function L12_12.DrawBorder(A0_97, A1_98, A2_99, A3_100, A4_101, A5_102, A6_103, A7_104, A8_105)
	local L9_106, L14_111, L15_112, L21_118, L22_119, L23_120 = L9_106, A1_98, L15_112, L21_118, L22_119, L23_120
	L15_112 = A2_99
	L9_106 = L9_106(L14_111, L15_112)
	L14_111 = A4_101 * 64
	L15_112 = 5 * L14_111
	L15_112 = L15_112 / 64
	L15_112 = L15_112 / 20
	L15_112 = 1 + L15_112
	L21_118 = _ENV
	L22_119 = 128 * A3_100
	L22_119 = L22_119 / 360
	L21_118 = L21_118(L22_119)
	L22_119 = L8_8
	L23_120 = L9_106.nFaceDirection
	L23_120 = L23_120 - L21_118
	L22_119 = L22_119 * L23_120
	L22_119 = L22_119 / 128
	L23_120 = L9_106.nFaceDirection
	if L23_120 > 256 - L21_118 then
		L23_120 = L8_8
		L23_120 = L22_119 - L23_120
		L22_119 = L23_120 - L8_8
	end
	L23_120 = A3_100 / 180
	L23_120 = L23_120 * L8_8
	L23_120 = L22_119 + L23_120
	if A3_100 <= 45 then
	end
	if A3_100 == 360 then
		L23_120 = L23_120 + L8_8 / 20
	end
	local L17_114 = L17_114
	A0_97:SetTriangleFan(GEOMETRY_TYPE.TRIANGLE)
	A0_97:SetD3DPT(D3DPT.TRIANGLESTRIP)
	A0_97:ClearTriangleFanPoint()
	local L18_115 = L18_115
	repeat
		({})[1] = L14_111
		;({})[2] = L14_111 - L15_112
		local L19_116 = L19_116
		local _FOR_, _FOR_, _FOR_, L20_117 = ipairs({})
		for _FORV_22_, _FORV_23_ in _FOR_, _FOR_, _FOR_ do
			if A1_98 == TARGET.DOODAD then
				({})[1] = Scene_PlaneGameWorldPosToScene(L9_106.nX + L7_7(L22_119) * _FORV_23_, L9_106.nY + L6_6(L22_119) * _FORV_23_) - L18_115
				;({})[2] = 0
				;({})[3] = Scene_PlaneGameWorldPosToScene(L9_106.nX + L7_7(L22_119) * _FORV_23_, L9_106.nY + L6_6(L22_119) * _FORV_23_) - L19_116
				A0_97:AppendDoodadID(L9_106.dwID, A5_102, A6_103, A7_104, A8_105, {})
			else
				local L29_126 = L29_126
				local L30_127 = L30_127
				local L31_128 = L31_128
				local L32_129 = L32_129
				local L33_130 = L33_130
				local L34_131 = L34_131
				local L35_132 = L35_132
				local L36_133 = L36_133
				;({})[1] = L29_126 - L18_115
				;({})[2] = 0
				local ({})[3], L37_134 = L30_127 - L19_116, L37_134
				L31_128(L32_129, L33_130, L34_131, L35_132, L36_133, L37_134, A8_105, {})
			end
		end
		L24_121 = L8_8
		L24_121 = L24_121 / L17_114
		L22_119 = L22_119 + L24_121
	until L23_120 < L22_119
end
