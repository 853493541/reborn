local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L19_19, L20_20
L0_0 = 0
g_nSearchAlliesIndex = L0_0
L0_0 = 0
g_nSearchEnemyIndex = L0_0
L0_0 = 0
g_nLastTabDownFrame = L0_0
L0_0 = false
g_nTabPlayerPriority = L0_0
L0_0 = false
g_bPetControlState = L0_0
L0_0 = 256
L1_1 = {}
L1_1.ENMEY = "Enmey"
L1_1.ALLY = "Ally"
L1_1.EYASHA = "EYASHA"
L2_2 = {}
L3_3 = L1_1.ENMEY
L4_4 = {}
L4_4.nVersion = 2
L4_4.bOnlyPlayer = false
L4_4.bPlayerFirst = false
L4_4.bOnlyNearDis = false
L4_4.bWeakness = false
L4_4.bMidAxisFirst = false
L4_4.bSureTarget = false
L4_4.nCoolTime = 16
L4_4.bRedFirst = false
L5_5 = {}
L6_6 = {}
L6_6.nRadius = 2560
L6_6.nAngle = 15
L6_6.nSelLevel = 3
L6_6.szArea = "MidAxis"
L5_5[1] = L6_6
L6_6 = {}
L6_6.nRadius = 512
L6_6.nAngle = 85
L6_6.nSelLevel = 2
L6_6.szArea = "Inner"
L5_5[2] = L6_6
L6_6 = {}
L6_6.nRadius = 1280
L6_6.nAngle = 114
L6_6.nSelLevel = 1
L6_6.szArea = "Outer"
L5_5[3] = L6_6
L4_4.tArea = L5_5
L2_2[L3_3] = L4_4
L3_3 = L1_1.ALLY
L4_4 = {}
L4_4.nVersion = 2
L4_4.bOnlyPlayer = false
L4_4.bPlayerFirst = false
L4_4.bOnlyNearDis = false
L4_4.bWeakness = false
L4_4.bMidAxisFirst = false
L4_4.bSureTarget = false
L4_4.nCoolTime = 16
L4_4.bTeammate = false
L5_5 = {}
L6_6 = {}
L6_6.nRadius = 2560
L6_6.nAngle = 15
L6_6.nSelLevel = 3
L6_6.szArea = "MidAxis"
L5_5[1] = L6_6
L6_6 = {}
L6_6.nRadius = 512
L6_6.nAngle = 85
L6_6.nSelLevel = 2
L6_6.szArea = "Inner"
L5_5[2] = L6_6
L6_6 = {}
L6_6.nRadius = 1280
L6_6.nAngle = 114
L6_6.nSelLevel = 1
L6_6.szArea = "Outer"
L5_5[3] = L6_6
L4_4.tArea = L5_5
L2_2[L3_3] = L4_4
L3_3 = L1_1.EYASHA
L4_4 = {}
L4_4.nVersion = 2
L4_4.bOnlyPlayer = false
L4_4.bPlayerFirst = true
L4_4.bOnlyNearDis = false
L4_4.bWeakness = false
L4_4.bMidAxisFirst = false
L4_4.bSureTarget = false
L4_4.nCoolTime = 16
L4_4.bTeammate = true
L5_5 = {}
L6_6 = {}
L6_6.nRadius = 2560
L6_6.nAngle = L0_0
L6_6.nSelLevel = 3
L6_6.szArea = "MidAxis"
L5_5[1] = L6_6
L6_6 = {}
L6_6.nRadius = 512
L6_6.nAngle = L0_0
L6_6.nSelLevel = 2
L6_6.szArea = "Inner"
L5_5[2] = L6_6
L6_6 = {}
L6_6.nRadius = 1280
L6_6.nAngle = L0_0
L6_6.nSelLevel = 1
L6_6.szArea = "Outer"
L5_5[3] = L6_6
L4_4.tArea = L5_5
L2_2[L3_3] = L4_4
L3_3 = {}
L4_4 = L1_1.ENMEY
L5_5 = {}
L3_3[L4_4] = L5_5
L4_4 = L1_1.ALLY
L5_5 = {}
L3_3[L4_4] = L5_5
L4_4 = L1_1.EYASHA
L5_5 = {}
L3_3[L4_4] = L5_5
L4_4 = L1_1.ENMEY
function L5_5(A0_21, A1_22)
	if A0_21 ~= nil then
		return A0_21
	end
	return A1_22
end
function L6_6(A0_23)
	local L1_24
	L1_24 = _ENV
	L1_24 = L1_24[A0_23]
	return L1_24
end
function L7_7(A0_25)
	local L1_26
	if A0_25 == 1 then
		L1_26 = true
		g_bPetControlState = L1_26
	else
		L1_26 = false
		g_bPetControlState = L1_26
	end
end
SetPetControlState = L7_7
function L7_7(A0_27, A1_28, A2_29)
	if g_bPetControlState then
		return A0_27.SearchForPetEnemy(A1_28, A2_29)
	else
		local L4_30 = L4_30
		do return L4_30(A1_28, A2_29) end
		local L5_31 = L5_31
	end
end
SearchForEnemy = L7_7
function L7_7()
	local L0_32 = GetControlPlayer()
	if g_bPetControlState then
		do return L0_32.GetPet() end
		local L1_33 = L1_33
	end
	return L0_32
end
GetSearchTargetPlayer = L7_7
function L7_7(A0_34)
	local L1_35
	local L4_38, L5_39, L6_40 = L4_38, A0_34, L6_40
	L4_38 = L4_38(L5_39)
	if L4_38 then
		L4_38 = GetPlayer
		L5_39 = A0_34
		L4_38 = L4_38(L5_39)
		L1_35 = L4_38
	else
		L4_38 = GetNpc
		L5_39 = A0_34
		L4_38 = L4_38(L5_39)
		L1_35 = L4_38
	end
	if not L1_35 then
		return
	end
	L4_38 = GetSearchTargetPlayer
	L4_38 = L4_38()
	L5_39 = L4_38.nX
	L6_40 = L1_35.nX
	L5_39 = L5_39 - L6_40
	L6_40 = L4_38.nX
	L6_40 = L6_40 - L1_35.nX
	L5_39 = L5_39 * L6_40
	L6_40 = L4_38.nY
	L6_40 = L6_40 - L1_35.nY
	L6_40 = L6_40 * (L4_38.nY - L1_35.nY)
	L5_39 = L5_39 + L6_40
	return L5_39
end
CalHorizontalDistance = L7_7
function L7_7(A0_41)
	local L1_42
	local L4_45, L5_46, L6_47 = L4_45, A0_41, L6_47
	L4_45 = L4_45(L5_46)
	if L4_45 then
		L4_45 = GetPlayer
		L5_46 = A0_41
		L4_45 = L4_45(L5_46)
		L1_42 = L4_45
	else
		L4_45 = GetNpc
		L5_46 = A0_41
		L4_45 = L4_45(L5_46)
		L1_42 = L4_45
	end
	L4_45 = GetSearchTargetPlayer
	L4_45 = L4_45()
	L5_46 = L4_45.nX
	L6_47 = L1_42.nX
	L5_46 = L5_46 - L6_47
	L6_47 = L4_45.nX
	L6_47 = L6_47 - L1_42.nX
	L5_46 = L5_46 * L6_47
	L6_47 = L4_45.nY
	L6_47 = L6_47 - L1_42.nY
	L6_47 = L6_47 * (L4_45.nY - L1_42.nY)
	L5_46 = L5_46 + L6_47
	L6_47 = L4_45.nZ
	L6_47 = L6_47 - L1_42.nZ
	L6_47 = L6_47 * (L4_45.nZ - L1_42.nZ)
	L6_47 = L6_47 / 64
	L5_46 = L5_46 + L6_47
	return L5_46
end
function L8_8(A0_48)
	local L1_49, L2_50, L3_51, L4_52 = GetSearchTargetPlayer(), L2_50, L3_51, L4_52
	L2_50 = A0_48.nX
	L3_51 = L1_49.nX
	L2_50 = L2_50 - L3_51
	L3_51 = A0_48.nY
	L4_52 = L1_49.nY
	L3_51 = L3_51 - L4_52
	L4_52 = L2_50 * L2_50
	L4_52 = L4_52 + L3_51 * L3_51
	local L5_53 = L5_53
	local L6_54 = L6_54
	L5_53 = L5_53(L6_54, L2_50)
	if L5_53 < 0 then
		L6_54 = math
		L6_54 = L6_54.pi
		L6_54 = 2 * L6_54
		L5_53 = L5_53 + L6_54
	end
	L6_54 = math
	L6_54 = L6_54.pi
	L6_54 = 2 * L6_54
	L6_54 = L6_54 * L1_49.nFaceDirection
	L6_54 = L6_54 / 255
	local L7_55 = L7_55
	L7_55 = L7_55(L6_54 - L5_53)
	if L7_55 > math.pi * 3 / 4 then
		L7_55 = 2 * math.pi - L7_55
	end
	if L7_55 > math.pi then
		L7_55 = L7_55 - math.pi
	end
	if L7_55 > math.pi / 2 then
		L7_55 = math.pi - L7_55
	end
	local L8_56 = L8_56
	L8_56 = L8_56(L4_52)
	local L9_57 = L9_57
	local L9_57, L10_58 = L9_57(L7_55), L10_58
	L4_52 = L8_56 * L9_57
	return L4_52
end
function L9_9(A0_59, A1_60)
	local L2_61 = L2_61
	local L2_61, L3_62 = L2_61(A0_59), L3_62
	L3_62 = nil
	if L2_61 then
		if not CanSelectPlayer(A0_59) then
			return false
		end
		L3_62 = GetPlayer(A0_59)
	else
		if not CanSelectNpc(A0_59) then
			return false
		end
		local L4_63 = L4_63
		local L4_63, L5_64 = L4_63(A0_59), L5_64
		L3_62 = L4_63
	end
	if not L3_62 then
		L4_63 = false
		return L4_63
	end
	L4_63 = L3_62.nSelectableType
	L5_64 = SELECTABLE_TYPE
	L5_64 = L5_64.SELECTABLE_NONE
	if L4_63 == L5_64 then
		L4_63 = false
		return L4_63
	end
	L4_63 = L3_62.nSelectableType
	L5_64 = SELECTABLE_TYPE
	L5_64 = L5_64.SELECTABLE_NOT_ENEMY
	if L4_63 == L5_64 then
		L4_63 = _ENV
		L4_63 = L4_63.ENMEY
		if A1_60 == L4_63 then
			L4_63 = false
			return L4_63
		end
	end
	L4_63 = true
	return L4_63
end
function L10_10(A0_65, A1_66)
	local L2_67
	L2_67 = {}
	if not A0_65 then
		return L2_67
	end
	L5_70 = ipairs
	L6_71 = A0_65
	L5_70, L6_71, L7_72 = L5_70(L6_71)
	for _FORV_6_, _FORV_7_ in L5_70, L6_71, L7_72 do
		if _ENV(_FORV_7_, A1_66) then
			table.insert(L2_67, _FORV_7_)
			local L10_75 = L10_75
		end
	end
	return L2_67
end
function L11_11(A0_76, A1_77)
	local L2_78, L4_80 = L2_78, L4_4
	L2_78 = L2_78(L4_80)
	L4_80 = L2_78.bPlayerFirst
	if L4_80 then
		L4_80 = A0_76.bPlayer
		if L4_80 ~= A1_77.bPlayer then
			L4_80 = A0_76.bPlayer
			if L4_80 then
				L4_80 = true
				return L4_80
			else
				L4_80 = false
				return L4_80
			end
		end
	end
	L4_80 = A0_76.bIsInScreen
	if L4_80 ~= A1_77.bIsInScreen then
		L4_80 = A0_76.bIsInScreen
		if L4_80 then
			L4_80 = true
			return L4_80
		else
			L4_80 = false
			return L4_80
		end
	end
	L4_80 = g_nTabPlayerPriority
	if L4_80 then
		L4_80 = A0_76.bPet
		if L4_80 ~= A1_77.bPet then
			L4_80 = A0_76.bPet
			L4_80 = not L4_80
			return L4_80
		end
	end
	L4_80 = L2_78.bOnlyNearDis
	if L4_80 then
		L4_80 = A0_76.nDis
		if L4_80 ~= A1_77.nDis then
			L4_80 = A0_76.nDis
			L4_80 = L4_80 < A1_77.nDis
			return L4_80
		end
	end
	L4_80 = L2_78.bRedFirst
	if L4_80 then
		L4_80 = A0_76.nRed
		if L4_80 ~= A1_77.nRed then
			L4_80 = A0_76.nRed
			L4_80 = L4_80 > A1_77.nRed
			return L4_80
		end
	end
	L4_80 = L2_78.bWeakness
	if L4_80 then
		L4_80 = A0_76.nLife
		if L4_80 ~= A1_77.nLife then
			L4_80 = A0_76.nLife
			L4_80 = L4_80 < A1_77.nLife
			return L4_80
		end
	end
	L4_80 = A0_76.nSelLevel
	if L4_80 ~= A1_77.nSelLevel then
		L4_80 = A0_76.nSelLevel
		L4_80 = L4_80 > A1_77.nSelLevel
		return L4_80
	end
	L4_80 = L2_78.bMidAxisFirst
	if L4_80 then
		L4_80 = A0_76.nAxisDis
		if L4_80 ~= A1_77.nAxisDis then
			L4_80 = A0_76.nAxisDis
			L4_80 = L4_80 < A1_77.nAxisDis
			return L4_80
		end
	end
	L4_80 = A0_76.nCount
	if L4_80 ~= A1_77.nCount then
		L4_80 = A0_76.nCount
		L4_80 = L4_80 > A1_77.nCount
		return L4_80
	end
	L4_80 = A0_76.nIndex
	L4_80 = L4_80 < A1_77.nIndex
	return L4_80
end
function L12_12(A0_81)
	local L1_82
	L1_82 = _ENV
	L4_85 = L4_4
	L1_82 = L1_82[L4_85]
	L4_85 = pairs
	L4_85, L3_84, _FOR_ = L4_85(L1_82)
	for _FORV_5_, _FORV_6_ in L4_85, L3_84, _FOR_ do
		if _FORV_6_ == A0_81 then
			return _FORV_5_
		end
	end
	L4_85 = false
	return L4_85
end
function L13_13()
	local L0_86
	L0_86 = _ENV
	local L3_89, L4_90, L5_91 = L4_4, L4_90, L5_91
	L0_86 = L0_86[L3_89]
	L3_89 = GetClientPlayer
	L3_89 = L3_89()
	L4_90 = L3_89.GetTarget
	L4_90, L5_91 = L4_90()
	if L4_90 == TARGET.NO_TARGET then
		return
	end
	if 0 < #L0_86 then
		return L0_86[#L0_86]
	end
	return
end
function L14_14(A0_92)
	local L2_93 = L2_93
	L2_93(_ENV[L4_4], A0_92)
	local L3_94 = L3_94
end
function L15_15(A0_95)
	if not A0_95 then
		_ENV[L4_4] = {}
		return
	end
	local L1_96 = L1_96
	L1_96 = L1_96(A0_95)
	if L1_96 and 0 < L1_96 then
		local L2_97 = L2_97
		local L3_98 = L3_98
		L2_97(L3_98, L1_96)
		local L4_99 = L4_99
	end
end
function L16_16(A0_100)
	local L1_101, L4_104, L8_108, L9_109 = L1_101, L4_4, L8_108, L9_109
	L1_101 = L1_101(L4_104)
	L4_104 = GetClientPlayer
	L4_104 = L4_104()
	L8_108 = L4_104.GetTarget
	L8_108, L9_109 = L8_108()
	if L8_108 == TARGET.NO_TARGET then
		L15_15()
		return A0_100[1].dwID, nil
	end
	if L1_101.bSureTarget and GetLogicFrameCount() - g_nLastTabDownFrame > L1_101.nCoolTime and L9_109 == A0_100[1].dwID then
		return L9_109
	end
	_FOR_, _FOR_, _FOR_ = ipairs(A0_100)
	for _FORV_8_, _FORV_9_ in _FOR_, _FOR_, _FOR_ do
		if not L12_12(_FORV_9_.dwID) and L9_109 ~= _FORV_9_.dwID then
			return _FORV_9_.dwID, L9_109
		end
	end
	L7_107 = L15_15
	L7_107()
	L7_107 = A0_100[1]
	L7_107 = L7_107.dwID
	if L7_107 == L9_109 then
		L7_107 = A0_100[2]
		if L7_107 then
			L7_107 = A0_100[2]
			L7_107 = L7_107.dwID
			return L7_107
		end
	end
	L7_107 = A0_100[1]
	L7_107 = L7_107.dwID
	return L7_107
end
function L17_17(A0_112, A1_113)
	if A0_112 == TARGET.PLAYER then
		return InteractPlayer(A1_113)
	elseif A0_112 == TARGET.NPC then
		return InteractNpc(A1_113)
	elseif A0_112 == TARGET.DOODAD then
		return InteractDoodad(A1_113)
	elseif A0_112 == TARGET.FURNITURE then
		return InteractLandObject(A1_113)
	elseif A0_112 == TARGET.DUMMY then
		local L2_114 = L2_114
		do return L2_114(A1_113) end
		local L3_115 = L3_115
	end
	L2_114 = false
	return L2_114
end
InteractTarget = L17_17
function L17_17(A0_116)
	if not A0_116 then
		return
	end
	L4_120 = pairs
	L5_121 = A0_116
	L4_120, L5_121, _FOR_ = L4_120(L5_121)
	for _FORV_4_, _FORV_5_ in L4_120, L5_121, _FOR_ do
		if _FORV_5_.Type == TARGET.DOODAD and IsCorpseAndCanLoot(_FORV_5_.ID) then
			return _FORV_5_.Type, _FORV_5_.ID, _FORV_5_.TemplateID
		end
	end
	L4_120 = pairs
	L5_121 = A0_116
	L4_120, L5_121, _FOR_ = L4_120(L5_121)
	for _FORV_4_, _FORV_5_ in L4_120, L5_121, _FOR_ do
		if _FORV_5_.Type == TARGET.DOODAD then
			return _FORV_5_.Type, _FORV_5_.ID, _FORV_5_.TemplateID
		elseif _FORV_5_.Type == TARGET.NPC then
			if g_nTabPlayerPriority then
				if CanSelectNpc(_FORV_5_.ID) and GetNpc(_FORV_5_.ID).dwEmployer == 0 then
					return _FORV_5_.Type, _FORV_5_.ID, _FORV_5_.TemplateID
				end
			else
				if not CanSelectNpc(_FORV_5_.ID) then
					goto lbl_86
				end
				do return _FORV_5_.Type, _FORV_5_.ID, _FORV_5_.TemplateID end
				repeat
					do break end -- pseudo-goto
					if _FORV_5_.Type == TARGET.FURNITURE or _FORV_5_.Type == TARGET.DUMMY then
						return _FORV_5_.Type, _FORV_5_.ID, _FORV_5_.TemplateID
					end
				until true
			end
		end
		::lbl_86::
	end
	L4_120 = pairs
	L5_121 = A0_116
	L4_120, L5_121, L9_125 = L4_120(L5_121)
	for _FORV_4_, _FORV_5_ in L4_120, L5_121, L9_125 do
		if _FORV_5_.Type == TARGET.PLAYER and CanSelectPlayer(_FORV_5_.ID) then
			return _FORV_5_.Type, _FORV_5_.ID, _FORV_5_.TemplateID
		end
	end
	L4_120 = pairs
	L5_121 = A0_116
	L4_120, L5_121, L9_125 = L4_120(L5_121)
	for _FORV_4_, _FORV_5_ in L4_120, L5_121, L9_125 do
		if _FORV_5_.Type == TARGET.NPC and g_nTabPlayerPriority then
			if CanSelectNpc(_FORV_5_.ID) and GetNpc(_FORV_5_.ID).dwEmployer ~= 0 then
				return _FORV_5_.Type, _FORV_5_.ID, _FORV_5_.TemplateID
			end
		end
	end
	L4_120 = TARGET
	L4_120 = L4_120.NO_TARGET
	L5_121 = 0
	L9_125 = nil
	return L4_120, L5_121, L9_125
end
GetFitObject = L17_17
function L17_17(A0_126, A1_127)
	local L2_128
	if A0_126 == TARGET.NPC then
		if GetNpc(A1_127) then
			L2_128 = GetNpc(A1_127).szName
		end
	else
		if A0_126 == TARGET.DOODAD then
			if not GetDoodad(A1_127) then
				goto lbl_51
			end
			local L6_132 = L6_132
			L2_128 = Table_GetDoodadName(GetDoodad(A1_127).dwTemplateID, GetDoodad(A1_127).dwNpcTemplateID)
			break -- pseudo-goto
		end
		L6_132 = TARGET
		L6_132 = L6_132.ITEM
		if A0_126 == L6_132 then
			L6_132 = GetItem
			L6_132 = L6_132(A1_127)
			if not L6_132 then
				goto lbl_51
			end
			L2_128 = GetItemNameByItem(L6_132)
			break -- pseudo-goto
		end
		L6_132 = TARGET
		L6_132 = L6_132.PLAYER
		if A0_126 == L6_132 then
			L6_132 = GetPlayer
			local L6_132, L4_130 = L6_132(A1_127), L4_130
			repeat
				if L6_132 then
					L2_128 = L6_132.szName
				end
			until true
		end
	end
	::lbl_51::
	return L2_128
end
GetTargetName = L17_17
function L17_17()
	local L0_133, L2_135, L3_136, L4_137 = GetLogicFrameCount(), L2_135, L3_136, L4_137
	L2_135 = GetControlPlayer
	L2_135 = L2_135()
	L3_136 = nil
	L4_137 = 0
	local L5_138 = L5_138
	local L6_139 = L6_139
	L6_139 = L6_139(1920, 42)
	local L7_140 = L7_140
	local L8_141 = L8_141
	L8_141 = L8_141(640, -42)
	if L7_140 + L8_141(640, -42) == 0 then
		return
	end
	if L0_133 - g_nLastTabDownFrame > 32 then
		g_nSearchAlliesIndex = 0
	else
		g_nSearchAlliesIndex = g_nSearchAlliesIndex + 1
	end
	if g_nSearchAlliesIndex >= L7_140 + L8_141(640, -42) then
		g_nSearchAlliesIndex = 0
	end
	L3_136, L4_137 = GetClientPlayer().GetTarget()
	if L3_136 == TARGET.NO_TARGET then
		g_nSearchAlliesIndex = 0
	end
	if L7_140 <= g_nSearchAlliesIndex then
		L5_138 = L8_141[g_nSearchAlliesIndex - L7_140 + 1]
	else
		L5_138 = L6_139[g_nSearchAlliesIndex + 1]
	end
	if L5_138 == L4_137 then
		g_nSearchAlliesIndex = g_nSearchAlliesIndex + 1
	end
	if g_nSearchAlliesIndex >= L7_140 + L8_141(640, -42) then
		g_nSearchAlliesIndex = 0
	end
	if L7_140 <= g_nSearchAlliesIndex then
		L5_138 = L8_141[g_nSearchAlliesIndex - L7_140 + 1]
	else
		L5_138 = L6_139[g_nSearchAlliesIndex + 1]
	end
	g_nLastTabDownFrame = GetLogicFrameCount()
	if L5_138 ~= 0 then
		if IsPlayer(L5_138) then
			SelectTarget(TARGET.PLAYER, L5_138)
		else
			local L9_142 = L9_142
			local L10_143 = L10_143
			L10_143(TARGET.NPC, L5_138)
			local L11_144 = L11_144
		end
	end
end
SearchAlliesVer1 = L17_17
function L17_17()
	local L0_145, L2_147, L3_148, L4_149 = GetLogicFrameCount(), L2_147, L3_148, L4_149
	L2_147 = GetControlPlayer
	L2_147 = L2_147()
	L3_148 = nil
	L4_149 = 0
	local L5_150 = L5_150
	local L6_151 = L6_151
	L6_151 = L6_151(L2_147, 1920, 42)
	local L7_152 = L7_152
	local L8_153 = L8_153
	L8_153 = L8_153(L2_147, 640, -42)
	if L7_152 + L8_153(L2_147, 640, -42) == 0 then
		return
	end
	L6_151 = _ENV(L6_151, L1_1.ENMEY)
	L7_152 = #L6_151
	L8_153 = _ENV(L8_153, L1_1.ENMEY)
	if L0_145 - g_nLastTabDownFrame > 32 then
		g_nSearchEnemyIndex = 0
	else
		g_nSearchEnemyIndex = g_nSearchEnemyIndex + 1
	end
	if g_nSearchEnemyIndex >= L7_152 + #L8_153 then
		g_nSearchEnemyIndex = 0
	end
	L3_148, L4_149 = GetClientPlayer().GetTarget()
	if L3_148 == TARGET.NO_TARGET then
		g_nSearchEnemyIndex = 0
	end
	if L7_152 <= g_nSearchEnemyIndex then
		L5_150 = L8_153[g_nSearchEnemyIndex - L7_152 + 1]
	else
		L5_150 = L6_151[g_nSearchEnemyIndex + 1]
	end
	if L5_150 == L4_149 then
		g_nSearchEnemyIndex = g_nSearchEnemyIndex + 1
	end
	if g_nSearchEnemyIndex >= L7_152 + #L8_153 then
		g_nSearchEnemyIndex = 0
	end
	if L7_152 <= g_nSearchEnemyIndex then
		L5_150 = L8_153[g_nSearchEnemyIndex - L7_152 + 1]
	else
		L5_150 = L6_151[g_nSearchEnemyIndex + 1]
	end
	g_nLastTabDownFrame = GetLogicFrameCount()
	if L5_150 ~= 0 then
		if IsPlayer(L5_150) then
			SelectTarget(TARGET.PLAYER, L5_150)
		else
			local L9_154 = L9_154
			local L10_155 = L10_155
			L10_155(TARGET.NPC, L5_150)
			local L11_156 = L11_156
		end
	end
end
function L18_18(A0_157)
	local L1_158, L5_162, L6_163 = L1_158, L4_4, L6_163
	L1_158 = L1_158(L5_162)
	L5_162 = GetLogicFrameCount
	L5_162 = L5_162()
	L6_163 = GetClientPlayer
	L6_163 = L6_163()
	local L4_161 = GetClientTeam()
	local L7_164 = L7_164
	_FOR_, _FOR_, _FOR_ = pairs(A0_157)
	for _FORV_10_, _FORV_11_ in _FOR_, _FOR_, _FOR_ do
		_FOR_, _FOR_, _FOR_ = ipairs(_FORV_11_.tTarget)
		for _FORV_17_, _FORV_18_ in _FOR_, _FOR_, _FOR_ do
			if L1_158.bOnlyPlayer and not IsPlayer(_FORV_18_) then
			end
			if L1_158.bTeammate and (not IsPlayer(_FORV_18_) or not L4_161.IsPlayerInTeam(_FORV_18_)) then
			end
			local L21_178 = L21_178
			if GetOperationMode() ~= CLASSICAL_MODE and GetCastingSkill() and GetCastingSkill() and GetSkillInfoEx(L6_163.GetSkillRecipeKey(GetCastingSkill()), L6_163.dwID).MaxRadius * GetSkillInfoEx(L6_163.GetSkillRecipeKey(GetCastingSkill()), L6_163.dwID).MaxRadius <= CalHorizontalDistance(_FORV_18_) then
			end
			if not true then
				if IsPlayer(_FORV_18_) then
				elseif GetNpc(_FORV_18_) and GetNpc(_FORV_18_).dwEmployer ~= 0 then
				end
				if not GetNpc(_FORV_18_) then
				end
				if not true then
					if not L21_178[_FORV_18_] then
						({}).dwID = _FORV_18_
						;({}).nIndex = 0
						;({}).nCount = 0
						;({}).bPet, ({}).bPlayer, ({}).nSelLevel = true, IsPlayer(_FORV_18_), 0
						table.insert(L7_164, {})
						L21_178[_FORV_18_] = #L7_164
					end
					L7_164[#L7_164].bPlayer, L7_164[#L7_164].nIndex = IsPlayer(_FORV_18_), #L7_164
					L7_164[#L7_164].nCount = L7_164[#L7_164].nCount + 1
					if _FORV_11_.nLevel > L7_164[#L7_164].nSelLevel then
						L7_164[#L7_164].nSelLevel = _FORV_11_.nLevel
						if g_nTabPlayerPriority and L7_164[#L7_164].bPet and 0 < L7_164[#L7_164].nSelLevel then
							L7_164[#L7_164].nSelLevel = L7_164[#L7_164].nSelLevel - 1
						end
					end
					if L1_158.bMidAxisFirst then
						L7_164[#L7_164].nAxisDis = L8_8((GetNpc(_FORV_18_)))
					end
					if L1_158.bOnlyNearDis then
						L7_164[#L7_164].nDis = L7_7(_FORV_18_)
					end
					if L1_158.bWeakness then
						L7_164[#L7_164].nLife = GetNpc(_FORV_18_).nCurrentLife
					end
					if L1_158.bRedFirst then
						L7_164[#L7_164].nRed = 0
						if IsEnemy(L6_163.dwID, _FORV_18_) then
							L7_164[#L7_164].nRed = 1
						end
					end
				end
			end
		end
	end
	L22_179 = #L7_164
	if L22_179 == 0 then
		return
	end
	L22_179 = {}
	L23_180 = ipairs
	L24_181 = L7_164
	L23_180, L24_181, L25_182 = L23_180(L24_181)
	for L28_185, L29_186 in L23_180, L24_181, L25_182 do
		if IsPlayer(L29_186.dwID) then
		else
		end
		if GetNpc(L29_186.dwID) then
			({})[1] = GetNpc(L29_186.dwID).nX
			;({})[2] = GetNpc(L29_186.dwID).nY
			;({})[3] = GetNpc(L29_186.dwID).nZ
			table.insert(L22_179, {})
		else
			({})[1] = 0
			;({})[2] = 0
			;({})[3] = 0
			table.insert(L22_179, {})
		end
	end
	L23_180 = Scene_GameWorldPositionListToScreenPointList
	L24_181 = L22_179
	L25_182 = #L22_179
	L23_180 = L23_180(L24_181, L25_182)
	L24_181 = Station
	L24_181 = L24_181.GetClientSize
	L24_181, L25_182 = L24_181()
	L28_185 = ipairs
	L29_186 = L7_164
	L28_185, L29_186, _FOR_ = L28_185(L29_186)
	for _FORV_14_, _FORV_15_ in L28_185, L29_186, _FOR_ do
		L7_164[_FORV_14_].bIsInScreen = false
		if L23_180[_FORV_14_ * 2 - 1] and L23_180[_FORV_14_ * 2] and 0 < L23_180[_FORV_14_ * 2 - 1] and L24_181 > L23_180[_FORV_14_ * 2 - 1] and 0 < L23_180[_FORV_14_ * 2] and L25_182 > L23_180[_FORV_14_ * 2] then
			L7_164[_FORV_14_].bIsInScreen = true
		end
	end
	L28_185 = table
	L28_185 = L28_185.sort
	L29_186 = L7_164
	L28_185(L29_186, L11_11)
	L28_185 = g_nLastTabDownFrame
	L28_185 = L5_162 - L28_185
	L29_186 = L1_158.nCoolTime
	if L28_185 > L29_186 then
		L28_185 = L15_15
		L28_185()
	end
	L28_185 = L16_16
	L29_186 = L7_164
	L28_185, L29_186 = L28_185(L29_186)
	if not L1_158.bOnlyNearDis then
		if L29_186 and L28_185 ~= L29_186 then
			L14_14(L29_186)
		end
		g_nLastTabDownFrame = GetLogicFrameCount()
	end
	if L28_185 ~= 0 then
		if IsPlayer(L28_185) then
			SelectTarget(TARGET.PLAYER, L28_185)
		else
			SelectTarget(TARGET.NPC, L28_185)
		end
	end
	if IsMobileStreamingEnable() then
		FireUIEvent("MOBILE_PLAYER_SUCCESS_SEARCHENEMY")
	end
end
function L19_19(A0_187, A1_188)
	local L2_189
	L2_189 = {}
	local L5_192, L8_195, L9_196 = L5_192, L8_195, L9_196
	if not A1_188 then
		L5_192 = _ENV
		L8_195 = A0_187
		L5_192 = L5_192(L8_195)
		A1_188 = L5_192
	end
	L5_192 = GetControlPlayer
	L5_192 = L5_192()
	L8_195 = nil
	L9_196 = nil
	L10_197 = pairs
	L10_197, _FOR_, _FOR_ = L10_197(A1_188.tArea)
	for _FORV_9_, _FORV_10_ in L10_197, _FOR_, _FOR_ do
		if A0_187 == L1_1.ENMEY then
			if L5_192.bBirdMove and L5_192.nFlyFlag ~= 0 or GetOperationMode() == JOYSTICK_MODE then
				L8_195, L9_196 = SearchForEnemy(L5_192, _FORV_10_.nRadius, L0_0)
			else
				L8_195, L9_196 = SearchForEnemy(L5_192, _FORV_10_.nRadius, _FORV_10_.nAngle)
			end
		elseif A0_187 == L1_1.ALLY or A0_187 == L1_1.EYASHA then
			L8_195, L9_196 = L5_192.SearchForAllies(_FORV_10_.nRadius, _FORV_10_.nAngle)
		end
		L8_195 = L10_10(L8_195, A0_187)
		L9_196 = #L8_195
		if L9_196 ~= 0 then
			({}).nLevel = _FORV_10_.nSelLevel
			;({}).tTarget = L8_195
			table.insert(L2_189, {})
		end
	end
	return L2_189
end
function L20_20(A0_202)
	local L1_203 = GetControlPlayer()
	if not L1_203 then
		return 0
	end
	local L2_204 = L2_204
	local L3_205 = L3_205
	local L4_206 = L4_206
	local L2_204, L3_205, L5_207 = L2_204(L3_205, L4_206, _ENV)
	return L3_205
end
GetAreaTargetNum = L20_20
function L20_20()
	if g_tAutoChooseData and g_tAutoChooseData.bChoose then
		return
	end
	local L0_208 = GetClientPlayer()
	if L0_208.bSprintFlag then
		L0_208.AimAtSprintDashTarget(1920, 1)
		return
	end
	local L1_209 = L1_209
	L1_209 = L1_209(L1_1.ENMEY)
	if L1_209.nVersion == 1 then
		L17_17()
	elseif L1_209.nVersion == 2 then
		L4_4 = L1_1.ENMEY
		local L2_210 = L2_210
		L2_210 = L2_210(L1_1.ENMEY)
		local L3_211 = L3_211
		L3_211(L2_210)
		local L4_212 = L4_212
	end
end
SearchEnemy = L20_20
function L20_20(A0_213)
	_ENV = L1_1.ENMEY
	local L1_214 = L1_214
	L1_214 = L1_214(L1_1.ENMEY, A0_213)
	local L2_215 = L2_215
	do return L2_215(L1_214) end
	local L3_216 = L3_216
end
SearchEnemyVer2 = L20_20
function L20_20()
	local L0_217 = L0_217
	L0_217 = L0_217(L1_1.ALLY)
	if L0_217.nVersion == 1 then
		SearchAlliesVer1()
	elseif L0_217.nVersion == 2 then
		L4_4 = L1_1.ALLY
		local L1_218 = L1_218
		L1_218 = L1_218(L1_1.ALLY)
		local L2_219 = L2_219
		L2_219(L1_218)
		local L3_220 = L3_220
	end
end
SearchAllies = L20_20
function L20_20()
	_ENV = L1_1.ENMEY
	local L0_221 = L0_221
	L0_221 = L0_221(L1_1.EYASHA)
	local L1_222 = L1_222
	L1_222(L0_221)
	local L2_223 = L2_223
end
SearchEYaShaTargets = L20_20
function L20_20()
	if GooseDuckKillLogic.IsInGooseDuckMap() then
		SearchEYaShaTargets()
	else
		SearchEnemy()
	end
end
SearchNextTarget = L20_20
function L20_20()
	local L0_224 = L0_224
	L0_224 = L0_224(L4_4)
	if L0_224.nVersion == 2 then
		local L1_225 = L13_13()
		if L1_225 and L1_225 ~= 0 then
			if IsPlayer(L1_225) then
				SelectTarget(TARGET.PLAYER, L1_225)
			else
				SelectTarget(TARGET.NPC, L1_225)
				local L4_228 = L4_228
			end
			L4_228 = L15_15
			L4_228(L1_225)
			L4_228 = GetLogicFrameCount
			L4_228 = L4_228()
			g_nLastTabDownFrame = L4_228
		end
	end
end
SelectPrevTarget = L20_20
function L20_20(A0_229, A1_230, A2_231, A3_232, A4_233)
	local L9_238, L8_237 = A4_233, L8_237
	L10_239 = L1_1
	L10_239 = L10_239.ENMEY
	L8_237 = L8_237(L9_238, L10_239)
	A4_233 = L8_237
	L8_237 = L6_6
	L9_238 = A4_233
	L8_237 = L8_237(L9_238)
	if not L8_237 then
		return
	end
	L9_238 = pairs
	L10_239 = L8_237.tArea
	L9_238, L10_239, L11_240 = L9_238(L10_239)
	for _FORV_9_, _FORV_10_ in L9_238, L10_239, L11_240 do
		if A0_229 == _FORV_10_.szArea then
			L8_237.tArea[_FORV_9_].nRadius = _ENV(A1_230, L8_237.tArea[_FORV_9_].nRadius)
			L8_237.tArea[_FORV_9_].nAngle = _ENV(A2_231, L8_237.tArea[_FORV_9_].nAngle)
			local L14_243 = L14_243
			L14_243.nSelLevel = _ENV(A3_232, L8_237.tArea[_FORV_9_].nSelLevel)
			break
		end
	end
end
SearchTarget_SetAreaSettting = L20_20
function L20_20()
	if SearchTarget_IsOldVerion() then
		return
	end
	local L0_244 = L0_244
	L0_244 = L0_244(L1_1.ENMEY)
	StorageServer.SetData("UISetting_BoolValues2", "TAB_PLAYER", not L0_244.bOnlyPlayer)
	SearchTarget_SetSettings("OnlyPlayer", not L0_244.bOnlyPlayer, true)
	local L4_248 = L4_248
	L4_248 = FireUIEvent
	local L2_246 = L2_246
	local L3_247 = not L0_244.bOnlyPlayer
	L4_248(L2_246, L3_247)
end
SearchTarget_SwitchOnlyPlayer = L20_20
function L20_20(A0_249, A1_250, A2_251)
	if SearchTarget_IsOldVerion() then
		return
	end
	if A2_251 and A0_249 == "OnlyPlayer" then
		if A1_250 then
			OutputMessage("MSG_SYS", g_tStrings.WRENCH_OPEN_TAB_PLAYER)
		else
			OutputMessage("MSG_SYS", g_tStrings.WRENCH_CLOSE_TAB_PLAYER)
		end
	end
	SearchTarget_SetOtherSettting(A0_249, A1_250, "Enmey")
	local L4_252 = L4_252
	local L5_253 = L5_253
	L4_252(L5_253, A1_250, "Ally")
	local L6_254 = L6_254
end
SearchTarget_SetSettings = L20_20
function L20_20(A0_255, A1_256, A2_257)
	A2_257 = _ENV(A2_257, L1_1.ENMEY)
	local L3_258 = L3_258
	L3_258 = L3_258(A2_257)
	if not L3_258 then
		return
	end
	if A0_255 == "OnlyPlayer" then
		L3_258.bOnlyPlayer = _ENV(A1_256, L3_258.bOnlyPlayer)
	elseif A0_255 == "PlayerFirst" then
		L3_258.bPlayerFirst = _ENV(A1_256, L3_258.bPlayerFirst)
	elseif A0_255 == "Weakness" then
		L3_258.bWeakness = _ENV(A1_256, L3_258.bWeakness)
	elseif A0_255 == "MidAxisFirst" then
		L3_258.bMidAxisFirst = _ENV(A1_256, L3_258.bMidAxisFirst)
	elseif A0_255 == "OnlyNearDis" then
		L3_258.bOnlyNearDis = _ENV(A1_256, L3_258.bOnlyNearDis)
	elseif A0_255 == "nVersion" then
		L3_258.nVersion = _ENV(A1_256, L3_258.nVersion)
	elseif A0_255 == "CoolTime" then
		L3_258.nCoolTime = _ENV(A1_256, L3_258.nCoolTime)
	elseif A0_255 == "SureTarget" then
		L3_258.bSureTarget = _ENV(A1_256, L3_258.bSureTarget)
	elseif A2_257 == L1_1.ENMEY and A0_255 == "RedFirst" then
		L3_258.bRedFirst = _ENV(A1_256, L3_258.bRedFirst)
	elseif A2_257 == L1_1.ALLY and A0_255 == "Teammate" then
		local L4_259 = L4_259
		local L5_260 = L5_260
		local L4_259, L6_261 = L4_259(L5_260, L3_258.bTeammate), L6_261
		L3_258.bTeammate = L4_259
	end
end
SearchTarget_SetOtherSettting = L20_20
function L20_20()
	local L0_262, L1_263
	L0_262 = _ENV
	L1_263 = L1_1
	L1_263 = L1_263.ENMEY
	L0_262 = L0_262[L1_263]
	L0_262 = L0_262.nVersion
	L0_262 = L0_262 == 1
	return L0_262
end
SearchTarget_IsOldVerion = L20_20
function L20_20()
	local L1_264
	L1_264 = g_nTabPlayerPriority
	return L1_264
end
IsPlayerPriority = L20_20
function L20_20(A0_265)
	local L1_266
	g_nTabPlayerPriority = A0_265
end
SetPlayerPriority = L20_20
function L20_20(A0_267)
	local L1_268
	L1_268 = "yellow2"
	if 4 < A0_267 then
		L1_268 = "red2"
	elseif 2 < A0_267 then
		L1_268 = "orange2"
	elseif -3 < A0_267 then
		L1_268 = "yellow2"
	elseif -6 < A0_267 then
		L1_268 = "green2"
	else
		L1_268 = "gray2"
	end
	return L1_268
end
GetTargetLevelFontColor = L20_20
