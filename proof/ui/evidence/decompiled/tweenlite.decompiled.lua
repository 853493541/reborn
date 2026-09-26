local L0_0, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L19_19, L20_20, L21_21 = L0_0, "tweenlite", L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L19_19, L20_20, L21_21
L4_4 = ExportExternalLib
L0_0(L3_3, L4_4)
L0_0 = nil
L3_3 = {}
L3_3.cnt = 0
L4_4 = {}
L4_4.cnt = 0
L5_5 = {}
L6_6 = {}
function L7_7(A0_22)
	return A0_22:GetAbsX()
end
L5_5.x = L7_7
function L7_7(A0_23)
	return A0_23:GetAbsY()
end
L5_5.y = L7_7
function L7_7(A0_24)
	return A0_24:GetW()
end
L5_5.w = L7_7
function L7_7(A0_25)
	return A0_25:GetH()
end
L5_5.h = L7_7
function L7_7(A0_26)
	return A0_26:GetRelX()
end
L5_5.relX = L7_7
function L7_7(A0_27)
	return A0_27:GetRelY()
end
L5_5.relY = L7_7
function L7_7(A0_28)
	return A0_28:GetAlpha()
end
L5_5.alpha = L7_7
function L7_7(A0_29)
	local L1_30
	L1_30 = A0_29._scaleX
	if not L1_30 then
		L1_30 = 1
	end
	return L1_30
end
L5_5.scale = L7_7
function L7_7(A0_31)
	local L1_32
	L1_32 = A0_31._scaleX
	if not L1_32 then
		L1_32 = 1
	end
	return L1_32
end
L5_5.scaleX = L7_7
function L7_7(A0_33)
	local L1_34
	L1_34 = A0_33._scaleY
	if not L1_34 then
		L1_34 = 1
	end
	return L1_34
end
L5_5.scaleY = L7_7
function L7_7(A0_35)
	return A0_35:GetRotate()
end
L5_5.angle = L7_7
function L7_7(A0_36)
	return A0_36:GetPercentage()
end
L5_5.percentage = L7_7
function L7_7(A0_37)
	return A0_37:GetFontColor()
end
L5_5.color = L7_7
function L7_7(A0_38)
	return 1
end
L5_5.offsetx = L7_7
function L7_7(A0_39)
	return 1
end
L5_5.offsety = L7_7
function L7_7(A0_40, A1_41, A2_42)
	if A2_42.offsetx then
		local L7_46 = L7_46
		return L7_46(A0_40, A1_41 + A2_42.offsetx * A0_40:GetW())
	end
	L7_46 = A0_40.SetAbsX
	do return L7_46(A0_40, A1_41) end
	local L5_44 = L5_44
end
L6_6.x = L7_7
function L7_7(A0_47, A1_48, A2_49)
	repeat
		if A2_49.offsety then
			local L7_53 = L7_53
			L7_53(A0_47, A1_48 + A2_49.offsety * A0_47:GetH())
			break -- pseudo-goto
		end
		L7_53 = A0_47.SetAbsY
		L7_53(A0_47, A1_48)
		local L5_51 = L5_51
	until true
end
L6_6.y = L7_7
function L7_7(A0_54, A1_55)
	do return A0_54:SetW(A1_55) end
	local L4_56 = L4_56
end
L6_6.w = L7_7
function L7_7(A0_57, A1_58)
	do return A0_57:SetH(A1_58) end
	local L4_59 = L4_59
end
L6_6.h = L7_7
L7_7 = nil
L8_8 = nil
function L9_9(A0_60, A1_61, A2_62)
	if A2_62.offsetx then
		A0_60:SetRelX(A1_61 + A2_62.offsetx * A0_60:GetW())
	else
		A0_60:SetRelX(A1_61)
	end
	if A0_60:GetBaseType() == "Item" then
		local L4_63 = L4_63
		local L7_66 = L7_66
		L4_63(L7_66, A0_60:GetParent():GetAbsX() + A0_60:GetRelX())
	end
end
L6_6.relX = L9_9
function L9_9(A0_67, A1_68, A2_69)
	if A2_69.offsety then
		return A0_67:SetRelY(A1_68 + A2_69.offsety * A0_67:GetH())
	else
		A0_67:SetRelY(A1_68)
	end
	if A0_67:GetBaseType() == "Item" then
		local L4_70 = L4_70
		local L7_73 = L7_73
		L4_70(L7_73, A0_67:GetParent():GetAbsY() + A0_67:GetRelY())
	end
end
L6_6.relY = L9_9
function L9_9(A0_74, A1_75)
	do return A0_74:SetAlpha(A1_75) end
	local L4_76 = L4_76
end
L6_6.alpha = L9_9
function L9_9(A0_77, A1_78, A2_79)
	local L3_80, L4_81, L5_82, L6_83
	L3_80 = A0_77._scaleX
	if not L3_80 then
		L3_80 = 1
	end
	A0_77._scaleX = L3_80
	L3_80 = A0_77._scaleY
	if not L3_80 then
		L3_80 = 1
	end
	A0_77._scaleY = L3_80
	L3_80 = A0_77._scaleX
	L3_80 = 1 / L3_80
	L3_80 = L3_80 * A1_78
	L4_81 = A0_77._scaleY
	L4_81 = 1 / L4_81
	L4_81 = L4_81 * A1_78
	L5_82 = nil
	L6_83 = nil
	if A2_79.offsetx then
		L5_82 = A0_77:GetAbsX() + A2_79.offsetx * A0_77:GetW()
	end
	if A2_79.offsety then
		L6_83 = A0_77:GetAbsY() + A2_79.offsety * A0_77:GetH()
	end
	A0_77:Scale(L3_80, L4_81)
	A0_77._scaleX = A1_78
	A0_77._scaleY = A1_78
	A0_77._scale = A1_78
	if A2_79.offsetx then
		A0_77:SetAbsX(L5_82 - A2_79.offsetx * A0_77:GetW())
	end
	if A2_79.offsety then
		local L7_84, L8_85 = L7_84, L8_85
		local L11_88 = L11_88
		L11_88 = L11_88 * A0_77:GetH()
		L11_88 = L6_83 - L11_88
		L7_84(L8_85, L11_88)
	end
end
L6_6.scale = L9_9
function L9_9(A0_89, A1_90, A2_91)
	local L3_92
	L3_92 = A0_89._scaleX
	if not L3_92 then
		L3_92 = 1
	end
	A0_89._scaleX = L3_92
	L3_92 = A0_89._scaleX
	L3_92 = 1 / L3_92
	L3_92 = L3_92 * A1_90
	local L4_93, L5_94 = L4_93, L5_94
	local L6_95 = L6_95
	L4_93(L5_94, L6_95, 1)
	local L7_96 = L7_96
	A0_89._scaleX = A1_90
end
L6_6.scaleX = L9_9
function L9_9(A0_97, A1_98, A2_99)
	local L3_100
	L3_100 = A0_97._scaleY
	if not L3_100 then
		L3_100 = 1
	end
	A0_97._scaleY = L3_100
	L3_100 = A0_97._scaleY
	L3_100 = 1 / L3_100
	L3_100 = L3_100 * A1_98
	local L4_101, L5_102 = L4_101, L5_102
	local L6_103 = L6_103
	L4_101(L5_102, L6_103, L3_100)
	local L7_104 = L7_104
	A0_97._scaleY = A1_98
end
L6_6.scaleY = L9_9
function L9_9(A0_105, A1_106)
	do return A0_105:SetRotate(A1_106) end
	local L4_107 = L4_107
end
L6_6.angle = L9_9
function L9_9(A0_108, A1_109)
	do return A0_108:SetPercentage(A1_109) end
	local L4_110 = L4_110
end
L6_6.percentage = L9_9
function L9_9(A0_111, A1_112)
	do return A0_111:SetFontColor(A1_112) end
	local L4_113 = L4_113
end
L6_6.color = L9_9
