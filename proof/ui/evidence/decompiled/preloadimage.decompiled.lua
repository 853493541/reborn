local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6
L0_0 = false
local L1_1, L11_11, L12_12 = {}, L11_11, L12_12
L2_2 = nil
L3_3 = "\\ui\\image\\icon\\"
L4_4 = {}
L5_5 = {}
L6_6 = g_tImage_PreLoad
L11_11 = RegisterUITable
L12_12 = "Image_PreLoad"
local L9_9 = L9_9
L11_11(L12_12, L9_9, L6_6.Title)
local L10_10 = L10_10
function L11_11(A0_16)
	local L1_17, L2_18 = L1_17, L2_18
	local L1_17, L3_19 = L1_17(L2_18, A0_16), L3_19
	if L1_17 then
		L2_18 = L1_17.szImagesPath
		return L2_18
	end
end
function L12_12(A0_20)
	local L3_23, L8_28 = L3_23, L8_28
	if not UnloadUIImage then
		return
	end
	L3_23 = _ENV
	L3_23 = L3_23[A0_20]
	if not L3_23 then
		L3_23 = Log
		L8_28 = "UnloadMapImages: already unload or haven't ever loaded"
		L3_23(L8_28)
		return
	end
	L3_23 = assert
	L8_28 = L2_2
	L8_28 = L8_28 == A0_20
	L3_23(L8_28)
	L3_23 = ""
	L8_28 = _ENV
	L8_28 = L8_28[A0_20]
	local L4_24 = L4_24
	_FOR_ = 1
	for _FORV_8_ = _FOR_, _FOR_, _FOR_ do
		local L9_29 = L9_29
		if L4_24:GetRow(_FORV_8_).szFile and L4_24:GetRow(_FORV_8_).szFile ~= "" then
			if L4_24:GetRow(_FORV_8_).bIcon == 1 then
				L3_23 = L3_3 .. string.lower(L4_24:GetRow(_FORV_8_).szFile)
			else
				L3_23 = string.lower(L4_24:GetRow(_FORV_8_).szFile)
			end
			if not L5_5[A0_20][L3_23] then
				UnloadUIImage(L3_23)
			end
		end
	end
	L10_30 = _ENV
	L10_30[A0_20] = nil
	L10_30 = L5_5
	L10_30[A0_20] = nil
	L10_30 = nil
	L2_2 = L10_30
	L10_30 = Log
	L11_31 = "UnloadMapImages finish! mapID:"
	L12_32 = A0_20
	L11_31 = L11_31 .. L12_32
	L10_30(L11_31)
end
function L9_9(A0_33)
	local L10_43 = L10_43
	if not LoadUIImage then
		return
	end
	L10_43 = _ENV
	L10_43 = L10_43(A0_33)
	if not L10_43 or L10_43 == "" then
		return
	end
	local L2_35 = L2_35
	L2_35 = L2_35 .. A0_33
	if not IsUITableRegister(L2_35) then
		RegisterUITable(L2_35, L10_43, L6_6.Title)
	end
	if L4_4[A0_33] ~= nil then
		local L3_36 = L3_36
		local L5_38 = "images already load! mapid:" .. A0_33
		L3_36(L5_38)
		return
	end
	L3_36 = L5_5
	L5_38 = {}
	L3_36[A0_33] = L5_38
	L3_36 = ""
	L5_38 = false
	local L6_39 = L6_39
	_FOR_ = 1
	for _FORV_10_ = _FOR_, _FOR_, _FOR_ do
		local L11_44 = L11_44
		if L6_39:GetRow(_FORV_10_).szFile and L6_39:GetRow(_FORV_10_).szFile ~= "" then
			if L6_39:GetRow(_FORV_10_).bIcon == 1 then
				L3_36 = L3_3 .. string.lower(L6_39:GetRow(_FORV_10_).szFile)
			else
				L3_36 = string.lower(L6_39:GetRow(_FORV_10_).szFile)
			end
			L5_38 = LoadUIImage(L3_36, false)
			if L5_38 == false then
				L5_5[A0_33][L3_36] = true
			end
		end
	end
	L12_45 = L4_4
	L12_45[A0_33] = L2_35
	L2_2 = A0_33
	L12_45 = Log
	L14_47 = "LoadMapImage finish! mapid:"
	L15_48 = A0_33
	L14_47 = L14_47 .. L15_48
	L12_45(L14_47)
end
function L10_10()
	local L2_51, L7_56 = L2_51, L7_56
	if not LoadUIImage then
		return
	end
	L2_51 = _ENV
	if L2_51 then
		L2_51 = Log
		L7_56 = "UI_PreLoadImage: already load"
		L2_51(L7_56)
		return
	end
	L2_51 = ""
	L7_56 = false
	local L3_52 = L3_52
	_FOR_ = 1
	local L6_55 = L6_55
	for _FORV_7_ = _FOR_, _FOR_, _FOR_ do
		if L3_52:GetRow(_FORV_7_).szFile and L3_52:GetRow(_FORV_7_).szFile ~= "" then
			if L3_52:GetRow(_FORV_7_).bIcon == 1 then
				L2_51 = L3_3 .. string.lower(L3_52:GetRow(_FORV_7_).szFile)
			else
				L2_51 = string.lower(L3_52:GetRow(_FORV_7_).szFile)
			end
			L7_56 = LoadUIImage(L2_51, false)
			if L7_56 == false then
				L1_1[L2_51] = true
			end
		end
	end
	L8_57 = true
	_ENV = L8_57
	L8_57 = Log
	L9_58 = "UI_PreLoadImage load image finish"
	L8_57(L9_58)
end
UI_PreLoadImage = L10_10
function L10_10()
	local L6_68 = L6_68
	if not UnloadUIImage then
		return
	end
	L6_68 = _ENV
	if L6_68 then
		L6_68 = L4_4
		L6_68 = L6_68[_ENV]
		if L6_68 then
			L6_68 = L8_8
			L6_68(_ENV)
		end
	end
	L6_68 = L0_0
	if not L6_68 then
		L6_68 = Log
		L6_68("OnLuaReset: already unload")
		local L1_63 = L1_63
		return
	end
	L6_68 = ""
	L1_63 = g_tTable
	L1_63 = L1_63.Image_PreLoad
	local L2_64 = L1_63:GetRowCount()
	_FOR_ = 1
	for _FORV_6_ = _FOR_, _FOR_, _FOR_ do
		if L1_63:GetRow(_FORV_6_).szFile and L1_63:GetRow(_FORV_6_).szFile ~= "" then
			if L1_63:GetRow(_FORV_6_).bIcon == 1 then
				L6_68 = L3_3 .. string.lower(L1_63:GetRow(_FORV_6_).szFile)
			else
				L6_68 = string.lower(L1_63:GetRow(_FORV_6_).szFile)
			end
			if not L1_1[L6_68] then
				UnloadUIImage(L6_68)
				local L10_72 = L10_72
			end
		end
	end
	L5_67 = {}
	L1_1 = L5_67
	L5_67 = false
	L0_0 = L5_67
	L5_67 = Log
	L7_69 = "ui unload image finish!\n"
	L5_67(L7_69)
end
RegisterEvent("PLAYER_ENTER_SCENE", function()
	local L0_73 = GetClientPlayer()
	local L2_75 = L2_75
	if arg0 ~= L0_73.dwID then
		return
	end
	L2_75 = GetClientScene
	L2_75 = L2_75()
	if not L2_75 then
		return
	end
	local L3_76 = L3_76
	_ENV(L2_75.dwMapID)
	local L4_77 = L4_77
end)
RegisterEvent("PLAYER_LEAVE_SCENE", function()
	local L0_78 = GetClientPlayer()
	local L2_80 = L2_80
	if not L0_78 or arg0 ~= L0_78.dwID then
		return
	end
	L2_80 = GetClientScene
	L2_80 = L2_80()
	if not L2_80 then
		return
	end
	local L3_81 = L3_81
	_ENV(L2_80.dwMapID)
	local L4_82 = L4_82
end)
local L13_13 = L13_13
local L14_14 = L14_14
RegisterEvent("UI_LUA_RESET", L10_10)
local L15_15 = L15_15
