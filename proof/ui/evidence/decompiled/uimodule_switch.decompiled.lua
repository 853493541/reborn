local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9
L0_0 = {}
local L0_0.zhkr, L13_13, L14_14 = "BSFY_0", L13_13, L14_14
L0_0.zhtw = "BSFY_0"
L0_0.vivn = "BSFY_0"
function L1_1()
	local L0_15, L1_16
	return
end
function L2_2(A0_17)
	local L1_18, L2_19
	L1_18 = _G
	L1_18 = L1_18[A0_17]
	if L1_18 then
		L1_18 = _G
		L2_19 = _ENV
		L1_18[A0_17] = L2_19
	end
end
function L3_3(A0_20, A1_21, A2_22, A3_23, A4_24)
	local L5_25, L6_26, L7_27
	L5_25 = _G
	L5_25 = L5_25[A0_20]
	if not L5_25 then
		return
	end
	if not A3_23 then
		A3_23 = "OnFrameCreate"
	end
	L5_25 = nil
	L6_26 = _G
	L6_26 = L6_26[A0_20]
	L6_26 = L6_26[A3_23]
	if L6_26 then
		L6_26 = _G
		L6_26 = L6_26[A0_20]
		L5_25 = L6_26[A3_23]
	end
	L6_26 = _G
	L6_26 = L6_26[A0_20]
	function L7_27()
		if _ENV then
			L3_31 = _ENV
			L3_31()
		end
		L3_31 = A1_21
		if L3_31 then
			L3_31 = ipairs
			L4_32 = A1_21
			L3_31, L4_32, _FOR_ = L3_31(L4_32)
			for _FORV_3_, _FORV_4_ in L3_31, L4_32, _FOR_ do
				this:Lookup(_FORV_4_):Enable(false)
			end
		end
		L3_31 = A4_24
		if L3_31 then
			L3_31 = ipairs
			L4_32 = A4_24
			L3_31, L4_32, _FOR_ = L3_31(L4_32)
			for _FORV_3_, _FORV_4_ in L3_31, L4_32, _FOR_ do
				this:Lookup(_FORV_4_):Hide()
			end
		end
		L3_31 = A2_22
		if L3_31 then
			L3_31 = ipairs
			L4_32 = A2_22
			L3_31, L4_32, _FOR_ = L3_31(L4_32)
			for _FORV_3_, _FORV_4_ in L3_31, L4_32, _FOR_ do
				this:Lookup(_FORV_4_[1], _FORV_4_[2]):Hide()
			end
		end
	end
	L6_26[A3_23] = L7_27
end
function L4_4(A0_37, A1_38, A2_39, A3_40, A4_41)
	local L9_46 = _G[A0_37]
	if not L9_46 then
		return
	end
	L9_46 = Station
	L9_46 = L9_46.Lookup
	L10_47 = A1_38
	L9_46 = L9_46(L10_47)
	if not L9_46 then
		return
	end
	if A2_39 then
		L10_47 = ipairs
		L10_47, _FOR_, _FOR_ = L10_47(A2_39)
		for _FORV_9_, _FORV_10_ in L10_47, _FOR_, _FOR_ do
			L9_46:Lookup(_FORV_10_):Enable(false)
		end
	end
	if A4_41 then
		L10_47 = ipairs
		L10_47, _FOR_, _FOR_ = L10_47(A4_41)
		for _FORV_9_, _FORV_10_ in L10_47, _FOR_, _FOR_ do
			L9_46:Lookup(_FORV_10_):Hide()
		end
	end
	if A3_40 then
		L10_47 = ipairs
		L10_47, L7_44, _FOR_ = L10_47(A3_40)
		for _FORV_9_, _FORV_10_ in L10_47, L7_44, _FOR_ do
			L9_46:Lookup(_FORV_10_[1], _FORV_10_[2]):Hide()
		end
	end
end
L5_5 = 0
L6_6 = 0
L7_7 = 3600000
function L8_8()
	local L0_52, L1_53
	L0_52 = {}
	L1_53 = {}
	;({}).fnAction = _ENV
	;({})[1] = ""
	;({})[2] = "Handle_CardBuy"
	;({})[1] = ""
	;({})[2] = "Handle_CardSell"
	;({})[1] = ""
	;({})[2] = "Handle_MoneyBuy"
	;({})[1] = ""
	;({})[2] = "Handle_MoneySell"
	;({})[1] = {}
	;({})[2] = {}
	;({})[3] = {}
	;({})[4] = {}
	;({}).Param, ({})[1] = {}, "PayPathPanel"
	;({}).Param, ({})[2] = {}, {}
	;({}).Param, ({})[3] = {}, {}
	;({}).zhkr = "BSFY_0"
	;({}).fnAction = _ENV
	;({})[1] = "Wnd_All"
	;({})[2] = "Handle_CityName"
	;({})[1] = {}
	;({}).Param, ({})[1] = {}, "WorldMap"
	;({}).Param, ({})[2] = {}, {}
	;({}).Param, ({})[3] = {}, {}
	;({}).vivn = "BSFY_0"
	;({}).fnAction = _ENV
	;({})[1] = "TradingPage_Totle/CheckBox_Contraband"
	;({}).Param, ({})[1] = {}, "AuctionPanel"
	;({}).Param, ({})[2] = {}, {}
	;({}).Param, ({})[3] = {}, {}
	;({}).fnAction = L4_4
	;({})[1] = "Wnd_Minimap/Wnd_Over/Btn_Sns"
	;({}).Param, ({})[1] = {}, "Minimap"
	;({}).Param, ({})[2] = {}, "Normal/Minimap"
	;({}).Param, ({})[3] = {}
	;({}).Param, ({})[4] = {}
	;({}).Param, ({})[5] = {}, {}
	;({}).zhtw = "BSFY_0"
	;({}).fnAction = _ENV
	;({})[1] = "PageSet_CheckOut/Page_Buy/WndScroll_Buy"
	;({})[2] = "Handle_Price/Text_PriceRewards"
	local L12_64 = L12_64
	;({})[1] = "PageSet_CheckOut/Page_Buy/WndScroll_Buy"
	;({})[2] = "Handle_Price/Image_PriceRewards"
	local L13_65 = L13_65
	;({})[1] = "PageSet_CheckOut/Page_Buy/WndScroll_Buy"
	;({})[2] = "Handle_Price_Dis/Text_PriceRewards_Dis"
	local L14_66 = L14_66
	local L15_67 = L15_67
	;({})[1] = "PageSet_CheckOut/Page_Buy/WndScroll_Buy"
	local ({})[2], L16_68 = "Handle_Price_Dis/Image_PriceRewards_Dis", L16_68
	;({})[1] = {}
	;({})[2] = {}
	;({})[3] = {}
	;({})[4] = {}
	L16_68.Param, ({})[1] = {}, "CoinShop_CheckOut"
	L16_68.Param, ({})[2] = {}, {}
	L16_68.Param, ({})[3] = {}, {}
	L16_68.zhtw = "BSFY_0"
	L1_53[1] = L12_64
	L1_53[2] = L13_65
	L1_53[3] = L14_66
	L1_53[4] = L15_67
	L1_53[5] = L16_68
	L0_52.FIRST_LOADING_END = L1_53
	L1_53 = {}
	L12_64 = {}
	L13_65 = L4_4
	L12_64.fnAction = L13_65
	L13_65 = {}
	L14_66 = "CoinShop_Entrance"
	L15_67 = "Normal/CoinShop_Entrance"
	L16_68 = nil
	;({})[1] = "Wnd_TopRight/Wnd_Other"
	;({})[2] = "Handle_Currency1/Text_Currency1"
	local L9_61 = L9_61
	local L10_62 = L10_62
	;({})[1] = "Wnd_TopRight/Wnd_Other"
	local ({})[2], L11_63 = "Handle_Currency1/Image_Currency1", L11_63
	L9_61[1] = L10_62
	L9_61[2] = L11_63
	L13_65[1] = L14_66
	L13_65[2] = L15_67
	L13_65[3] = L16_68
	L13_65[4] = L9_61
	L12_64.Param = L13_65
	L13_65 = nil
	L12_64.zhtw = "BSFY_0"
	L12_64[1] = L13_65
	L1_53[1] = L12_64
	L0_52.COINSHOP_ON_OPEN = L1_53
	return L0_52
end
L9_9 = {}
L9_9.zhcn = 2
L9_9.zhkr = 3
L9_9.zhtw = 3
L9_9.vivn = 3
function L13_13()
	local L1_69
	L1_69 = true
	return L1_69
end
I18n_CanForbidLetterAndSign = L13_13
function L13_13()
	local L0_70, L1_71, L2_72, L3_73 = GetVersion()
	if L2_72 ~= "zhcn" then
		L3_73 = true
		return L3_73
	end
	L3_73 = false
	return L3_73
end
I18n_IsShieldAddonDownload = L13_13
function L13_13(A0_74, A1_75)
	local L2_76, L3_77
	if not A0_74 then
		A0_74 = 8
	end
	L2_76 = _ENV
	L2_76 = L2_76[A1_75]
	if not L2_76 then
		L2_76 = 2
	end
	L3_77 = L2_76 * A0_74
	return L3_77
end
GetI18nWordBytes = L13_13
function L13_13(A0_78)
	local L1_79, L2_80, L3_81, L5_83, L8_86 = GetVersion()
	if L3_81 == "zhcn" then
		return
	end
	L5_83 = _ENV
	L5_83 = L5_83()
	L8_86 = L5_83[A0_78]
	if not L8_86 then
		return
	end
	L9_87 = pairs
	L10_88 = L8_86
	L9_87, L10_88, _FOR_ = L9_87(L10_88)
	for _FORV_9_, _FORV_10_ in L9_87, L10_88, _FOR_ do
		if L3_81 and _FORV_10_[L3_81] and L0_0[L3_81] == _FORV_10_[L3_81] then
			if type(_FORV_10_.Param) == "table" then
				_FORV_10_.fnAction(unpack(_FORV_10_.Param))
				break -- pseudo-goto
			end
			_FORV_10_.fnAction(_FORV_10_.Param)
		end
		repeat
		until true
	end
end
InitUIModuleSwitch = L13_13
L13_13 = RegisterEvent
L14_14 = "FIRST_LOADING_END"
L13_13(L14_14, InitUIModuleSwitch)
L13_13 = RegisterEvent
L14_14 = "LOGIN_GAME"
L13_13(L14_14, InitUIModuleSwitch)
L13_13 = RegisterEvent
L14_14 = "COINSHOP_ON_OPEN"
L13_13(L14_14, InitUIModuleSwitch)
function L13_13()
	local L0_92, L1_93, L2_94 = GetVersion()
	if L2_94 ~= "zhcn" then
		local L3_95 = L3_95
		L3_95 = L3_95("Normal/LoginPassword")
		if L3_95 and L2_94 == "zhtw" then
			local L6_98 = L3_95:Lookup("WndPassword/Wnd_PasswordContent/Btn_TowCode")
			L6_98 = L6_98.Hide
			L6_98(L6_98)
			local L5_97 = L5_97
		end
	end
end
Module_Switch_EnterPassword = L13_13
function L13_13()
	local L0_99
	L0_99 = {}
	local L0_99.zhcn, L2_101, L3_102, L4_103 = 50, L2_101, L3_102, L4_103
	L0_99.zhtw = 50
	L2_101 = GetVersion
	L2_101, L3_102, L4_103 = L2_101()
	if L0_99[L4_103] then
		return L0_99[L4_103]
	end
	return 50
end
I18n_GetInsuranceRechargeMoney = L13_13
L13_13 = GetVersion
L13_13, L14_14 = L13_13()
if L13_13() ~= "zhcn" and L13_13() ~= "zhtw" then
	function _G.NumberToChinese(A0_104)
		local L1_105
		return A0_104
	end
end
