local ({}).bShowActionBar, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L19_19, L20_20, L21_21, L22_22, L23_23, L24_24, L25_25, L26_26, L27_27, L28_28, L29_29, L30_30, L31_31, L32_32, L33_33, L34_34, L35_35 = true, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L19_19, L20_20, L21_21, L22_22, L23_23, L24_24, L25_25, L26_26, L27_27, L28_28, L29_29, L30_30, L31_31, L32_32, L33_33, L34_34, L35_35
L2_2.bShowStateValue = true
L2_2.bShowSimpleBlood = false
L2_2.bShowPlayerSimpleBlood = false
L2_2.bShowSelfDebuff = true
L2_2.bStandard = false
L2_2.DefaultAnchor = nil
L2_2.Anchor = nil
L2_2.nVersion = 0
L2_2.nCurrentVersion = 2
L2_2.bIsEnemy = false
L2_2.nDispelCount = 0
L2_2.bPassengerInvincible = false
Target = L2_2
L2_2 = RegisterCustomData
L3_3 = "Target.bShowActionBar"
L2_2(L3_3)
L2_2 = RegisterCustomData
L3_3 = "Target.bShowStateValue"
L2_2(L3_3)
L2_2 = RegisterCustomData
L3_3 = "Target.bShowSimpleBlood"
L2_2(L3_3)
L2_2 = RegisterCustomData
L3_3 = "Target.bShowSelfDebuff"
L2_2(L3_3)
L2_2 = RegisterCustomData
L3_3 = "Target.bStandard"
L2_2(L3_3)
L2_2 = RegisterCustomData
L3_3 = "Target.bShowPlayerSimpleBlood"
L2_2(L3_3)
L2_2 = RegisterCustomData
L3_3 = "Target.Anchor"
L2_2(L3_3)
L2_2 = RegisterCustomData
L3_3 = "Target.nVersion"
L2_2(L3_3)
L2_2 = {}
L2_2.NONE = 1
L2_2.PREPARE = 2
L2_2.DONE = 3
L2_2.BREAK = 4
L2_2.FADE = 5
ACTION_STATE = L2_2
L2_2 = {}
L2_2.NORMAL = 7
L2_2.UNBREAKABLE = 6
PROGRESS_BAR_TYPE = L2_2
L2_2 = nil
L3_3 = IsMobileStreamingEnable
L3_3 = L3_3()
if L3_3 then
	L3_3 = "ui/traits/mobilestreaming/config/default/TargetCommon.ini"
	if L3_3 then
		goto lbl_60
	end
end
L3_3 = "ui/config/default/TargetCommon.ini"
::lbl_60::
L4_4 = 2500
L5_5 = 5000
L6_6 = nil
L7_7 = nil
L8_8 = nil
L9_9 = nil
L10_10 = nil
L11_11 = nil
L12_12 = false
L13_13 = 0
L14_14 = {}
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.TANG_MEN
L14_14[L15_15] = "Handle_TM"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.MING_JIAO
L14_14[L15_15] = "Handle_MJ"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.CANG_JIAN
L14_14[L15_15] = "Handle_CJ"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.CANG_YUN
L14_14[L15_15] = "Handle_CangYun"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.CHANG_GE
L14_14[L15_15] = "Handle_CG"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.SHAO_LIN
L14_14[L15_15] = "Handle_SL"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.CHUN_YANG
L14_14[L15_15] = "Handle_CY"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.BA_DAO
L14_14[L15_15] = "Handle_BaDao"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.TIAN_CE
L14_14[L15_15] = "Handle_TC"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.YAN_TIAN
L14_14[L15_15] = "Handle_YT"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.YAO_ZONG
L14_14[L15_15] = "Handle_YZ"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.DAO_ZONG
L14_14[L15_15] = "Handle_DZ"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.WAN_HUA
L14_14[L15_15] = "Handle_WH"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.WAN_LING
L14_14[L15_15] = "Handle_WL"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.DUAN_SHI
L14_14[L15_15] = "Handle_DS"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.GAI_BANG
L14_14[L15_15] = "Handle_GB"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.WU_DU
L14_14[L15_15] = "Handle_WD"
L15_15 = KUNGFU_TYPE
L15_15 = L15_15.WU_XIANG
L14_14[L15_15] = "Handle_Wx"
L15_15 = nil
L16_16 = nil
L17_17 = 3
L18_18 = {}
L18_18[0] = 27
L18_18[1] = 45
L19_19 = 65
L20_20 = 50
L21_21 = 150
L22_22 = 5
L23_23 = 15
L24_24 = false
L25_25 = Target
function L26_26(A0_42, A1_43, A2_44, A3_45, A4_46)
	local L5_47
	if not A4_46 then
		L5_47 = _ENV
		if L5_47 then
			goto lbl_7
		end
	end
	do return end
	::lbl_7::
	L5_47 = _ENV
	L5_47 = L5_47.dwMountType
	L5_47 = L5_47 == KUNGFU_TYPE.CHANG_GE
	if L5_47 then
		local L6_48 = L6_48
		local L7_49 = L7_49
		L6_48(L7_49, _ENV)
		local L8_50 = L8_50
	end
end
L25_25.BuffMonitor = L26_26
L25_25 = Target
function L26_26(A0_51, A1_52, A2_53, A3_54)
	if Buff_IsMobileBuff(A0_51, A1_52) then
		return 2
	end
	if A2_53 then
		if A3_54 == UI_GetClientPlayerID() or CanDispelBuff(A2_53, A0_51, A1_52) then
			return 0
		else
			return 1
		end
	else
		local L4_55 = L4_55
		local L5_56 = L5_56
		local L6_57 = L6_57
		local L4_55, L7_58 = L4_55(L5_56, L6_57, A1_52), L7_58
		if L4_55 then
			L4_55 = 0
			return L4_55
		else
			L4_55 = 1
			return L4_55
		end
	end
end
L25_25.BuffHandleSeclector = L26_26
L25_25 = Target
function L26_26(A0_59, A1_60, A2_61, A3_62)
	local L5_64 = L5_64
	if not A3_62 and Target.bShowSelfDebuff and A0_59 ~= 0 and IsPlayer(A0_59) and Target.dwFullyType == TARGET.NPC then
		L5_64 = UI_GetClientPlayerID
		L5_64 = L5_64()
		if A0_59 ~= L5_64 then
			L5_64 = true
			return L5_64
		end
	end
	L5_64 = false
	return L5_64
end
L25_25.IsFliterBuff = L26_26
function L25_25(A0_65, A1_66, A2_67)
	if A0_65 and Target.bIsEnemy or not A0_65 and not Target.bIsEnemy then
		local L3_68 = L3_68
		local L4_69 = L4_69
		local L3_68, L5_70 = L3_68(L4_69, A2_67), L5_70
		if L3_68 then
			L3_68 = true
			return L3_68
		end
	end
	L3_68 = false
	return L3_68
end
CanDispelBuff = L25_25
function L25_25()
	if Target.nVersion ~= Target.nCurrentVersion then
		if Target.Anchor and Target.DefaultAnchor and Target.Anchor.s == Target.DefaultAnchor.s and Target.Anchor.r == Target.DefaultAnchor.r and Target.Anchor.y < Target.DefaultAnchor.y then
			Target.Anchor.y = Target.DefaultAnchor.y
		end
		local L0_71 = L0_71
		L0_71("TARGET_ANCHOR_CHANGED")
		local L1_72 = L1_72
	end
	L0_71 = Target
	L1_72 = Target
	L1_72 = L1_72.nCurrentVersion
	L0_71.nVersion = L1_72
	L0_71 = nil
	_ENV = L0_71
end
L26_26 = Target
function L27_27()
	this:RegisterEvent("NPC_STATE_UPDATE")
	this:RegisterEvent("NPC_LEAVE_SCENE")
	this:RegisterEvent("PLAYER_STATE_UPDATE")
	this:RegisterEvent("PLAYER_ENTER_SCENE")
	this:RegisterEvent("PLAYER_LEAVE_SCENE")
	this:RegisterEvent("UPDATE_RELATION")
	this:RegisterEvent("UPDATE_ALL_RELATION")
	this:RegisterEvent("PLAYER_LEVEL_UP")
	this:RegisterEvent("OT_ACTION_PROGRESS_BREAK")
	this:RegisterEvent("PARTY_UPDATE_BASE_INFO")
	this:RegisterEvent("UPDATE_PLAYER_SCHOOL_ID")
	this:RegisterEvent("SET_SHOW_VALUE_BY_PERCENTAGE")
	this:RegisterEvent("SET_SHOW_VALUE_TWO_FORMAT")
	this:RegisterEvent("SET_TARGET_SHOW_STATE_VALUE")
	this:RegisterEvent("PARTY_SET_MARK")
	this:RegisterEvent("SET_SHOW_STANDARD_TARGET")
	this:RegisterEvent("SET_SHOW_VALUE_SIMPLE_BLOOD")
	this:RegisterEvent("UI_SCALED")
	this:RegisterEvent("ON_ENTER_CUSTOM_UI_MODE")
	this:RegisterEvent("ON_LEAVE_CUSTOM_UI_MODE")
	this:RegisterEvent("TARGET_ANCHOR_CHANGED")
	this:RegisterEvent("NPC_DROP_TARGET_UPDATE")
	this:RegisterEvent("CUSTOM_DATA_LOADED")
	this:RegisterEvent("CHANGE_CAMP")
	this:RegisterEvent("UI_ON_DAMAGE_EVENT")
	this:RegisterEvent("CHANGE_CAMP_FLAG")
	this:RegisterEvent("TARGET_MINI_AVATAR_MISC")
	this:RegisterEvent("SET_MINI_AVATAR")
	this:RegisterEvent("SKILL_MOUNT_KUNG_FU")
	this:RegisterEvent("SKILL_UNMOUNT_KUNG_FU")
	this:RegisterEvent("ON_NEW_PROXY_SKILL_LIST_NOTIFY")
	this:RegisterEvent("ON_CLEAR_PROXY_SKILL_LIST_NOTIFY")
	this:RegisterEvent("ON_NPC_LEAVE_FIGHT")
	this:RegisterEvent("BUFF_UPDATE")
	Target.DefaultAnchor = this:GetDefaultAnchor()
	Target.Init(this)
	Target.UpdateAnchor(this)
	Target.UpdateKungfu(this)
	local L1_74 = L1_74
	L1_74(this, g_tStrings.TARGET)
	L1_74 = _ENV
	if L1_74 then
		L1_74 = _ENV
		L1_74()
	end
	L1_74 = this
	local L2_75, L3_76 = L2_75, this:GetAbsPos()
	L2_75.nY = this:GetAbsPos()
	L1_74.nX = L3_76
end
L26_26.OnFrameCreate = L27_27
L26_26 = nil
L27_27 = Target
function L28_28(A0_77)
	local L1_78 = L1_78
	L1_78 = L1_78(A0_77, "", "Handle_Buff")
	local L2_79, L3_80 = L2_79, L3_80
	L2_79 = L2_79(L3_80, "", "Handle_Debuff")
	L3_80 = _ENV
	if not L3_80 then
		L3_80 = {}
		L3_80.boxtexts, ({})[1] = {}, "<box>w=30 h=30 postype=7 eventid=262912</box>"
		L3_80.boxtexts, ({})[2] = {}, "<box>w=25 h=25 postype=7 eventid=262912</box>"
		L3_80.boxtexts, ({})[3] = {}, "<box>w=25 h=25 postype=7 eventid=262912</box>"
		L3_80.func_fliter = IsFliterBuff
		L3_80.func_monitor = BuffMonitor
		L3_80.func_selector = BuffHandleSeclector
		L3_80.showtime = true
		L3_80.show_pausecd = true
		_ENV = L3_80
	end
	L3_80 = A0_77.dwBuffMgrID
	if L3_80 then
		L3_80 = _ENV
		L3_80 = L3_80.owner
		if L3_80 then
			L3_80 = _ENV
			L3_80 = L3_80.owner
			L3_80 = L3_80 ~= Target.dwID
		end
	end
	_ENV.hbuff = L1_78
	_ENV.hdebuff = L2_79
	_ENV.owner = Target.dwID
	_ENV.owner_type = Target.dwType
	_ENV.buff_light = Target.bIsEnemy
	_ENV.debuff_light = not Target.bIsEnemy
	if not A0_77.dwBuffMgrID then
		A0_77.dwBuffMgrID = BuffMgr.Register(_ENV)
	else
		BuffMgr.Modify(A0_77.dwBuffMgrID, _ENV, true)
		local L7_84 = L7_84
	end
	L7_84 = BuffMgr
	L7_84 = L7_84.RefreshBuffs
	local L5_82 = L5_82
	L7_84(L5_82, L3_80)
	local L6_83 = L6_83
end
L27_27.UpdateBuffParam = L28_28
L27_27 = Target
function L28_28(A0_85)
	local L3_86 = L3_86
	local L3_86, L4_87 = L3_86(A0_85, "", "Image_NPCMark"), L4_87
	L4_87 = L3_86
	L3_86 = L3_86.Hide
	L3_86(L4_87)
end
L27_27.Init = L28_28
L27_27 = Target
function L28_28()
	this:CorrectPos()
	local L2_90 = L2_90
	local L3_91 = L3_91
	local L3_91, L4_92 = L3_91(this, "TOPLEFT", "TOPLEFT"), L4_92
	L2_90.Anchor = L3_91
	L2_90 = FireEvent
	L3_91 = "TARGET_ANCHOR_CHANGED"
	L2_90(L3_91)
end
L27_27.OnFrameDragEnd = L28_28
L27_27 = Target
function L28_28(A0_93)
	local L1_94
	L1_94 = Target
	L1_94 = L1_94.Anchor
	if not L1_94 then
		L1_94 = Target
		L1_94 = L1_94.DefaultAnchor
	end
	local L4_97 = L4_97
	local L5_98 = L5_98
	local L6_99 = L6_99
	local L7_100 = L7_100
	local L8_101 = L8_101
	L4_97(L5_98, L6_99, L7_100, L8_101, L1_94.r, L1_94.x, L1_94.y)
	local L9_102 = L9_102
	L5_98 = A0_93
	L4_97 = A0_93.CorrectPos
	L4_97(L5_98)
	L4_97 = Target
	L4_97 = L4_97.UpdateOB
	L4_97()
end
L27_27.UpdateAnchor = L28_28
L27_27 = Target
function L28_28()
	if this.nUpdateStatus and Target.dwFullyType == TARGET.PLAYER and GetTargetHandle(this.dwType, this.dwID) then
		local L2_105 = L2_105
		Target.UpdateEnergy(this, (GetTargetHandle(this.dwType, this.dwID)))
		local L3_106 = L3_106
	end
	L2_105 = Target
	L2_105 = L2_105.UpdateAction
	L3_106 = this
	L2_105(L3_106)
	L2_105 = Target
	L2_105 = L2_105.UpdateSubHealth
	L3_106 = this
	L2_105(L3_106)
end
L27_27.OnFrameBreathe = L28_28
L27_27 = Target
function L28_28(A0_107)
	repeat
		if A0_107.dwType ~= TARGET.PLAYER then
			return
		end
		local L1_108 = L1_108
		L1_108 = L1_108(A0_107.dwID)
		if L1_108 then
			local L2_109 = L1_108.GetActualKungfuMount()
			if not L2_109 or A0_107.dwMountType == L2_109.dwMountType then
				goto lbl_34
			end
			A0_107.dwMountType = L2_109.dwMountType
			Target.UpdateKungfuInfo(A0_107, L1_108)
			local L5_112 = L5_112
			L5_112 = IsPlayerManaHide
			local L5_112, L4_111 = L5_112(A0_107.dwMountType), L4_111
			L5_112 = not L5_112
			A0_107.bHaveMana = L5_112
			break -- pseudo-goto
		end
		A0_107.dwMountType = nil
	until true
	::lbl_34::
end
L27_27.UpdateMountType = L28_28
L27_27 = Target
function L28_28(A0_113)
	Target.UpdateLM(A0_113)
	Target.UpdateName(A0_113)
	Target.UpdateLevel(A0_113)
	Target.UpdateAction(A0_113)
	Target.UpdateHead(A0_113)
	Target.UpdateKungfu(A0_113)
	Target.UpdateTargetMark(A0_113)
	Target.UpdateCamp(A0_113)
	Target.UpdateInvincible(A0_113)
	local L2_114 = L2_114
end
L27_27.UpdateState = L28_28
L27_27 = Target
function L28_28(A0_115)
	repeat
		local L1_116 = GetClientPlayer()
		local L2_117, L3_118 = L2_117, L3_118
		L2_117 = L2_117(L3_118, "", "Image_NPCMark")
		L3_118 = nil
		if (L1_116.IsInParty() or IsPlayerInOBDungeon()) and TeamClient_GetMarkIndex(A0_115.dwID) then
			L3_118 = PARTY_MARK_ICON_FRAME_LIST[TeamClient_GetMarkIndex(A0_115.dwID)]
		end
		if L3_118 then
			local L6_121 = L6_121
			L6_121(L2_117, PARTY_MARK_ICON_PATH, L3_118)
			local L7_122 = L7_122
			L7_122 = L2_117
			L6_121 = L2_117.Show
			L6_121(L7_122)
			break -- pseudo-goto
		end
		L7_122 = L2_117
		L6_121 = L2_117.Hide
		L6_121(L7_122)
	until true
end
L27_27.UpdateTargetMark = L28_28
function L27_27(A0_123)
	repeat
		local L1_124 = L1_124
		L1_124 = L1_124(A0_123:GetParent(), "Image_Flash")
		local L2_125 = A0_123:GetPercentage()
		if L2_125 < 1 then
			local L5_128, L6_129 = L5_128, L6_129
			local L7_130, L8_131 = L7_130, L8_131
			local L9_132, L10_133 = L9_132, L10_133
			local L13_136 = L13_136
			L1_124:SetAbsPos(L5_128 + L7_130 * L2_125 - L9_132, L1_124:GetAbsPos())
			local L14_137 = L14_137
			L1_124:Show()
			break -- pseudo-goto
		end
		L6_129 = L1_124
		L5_128 = L1_124.Hide
		L5_128(L6_129)
	until true
end
L28_28 = Target
function L29_29(A0_138, A1_139)
	if A0_138.szShow and A0_138.szShow ~= "" then
		local L2_140 = L2_140
		L2_140 = L2_140(A0_138, "", "Handle_Energy/" .. A0_138.szShow)
		if L2_140 then
			if IsInTreasureHuntMap() then
				L2_140:Hide()
			else
				L2_140:Show()
				local L3_141 = L3_141
				local L4_142 = L4_142
				local L5_143 = L5_143
				local L6_144 = L6_144
				local L3_141, L7_145 = L3_141(L4_142, L5_143, L6_144, _ENV), L7_145
				A0_138.nUpdateStatus = L3_141
			end
		end
	end
end
L28_28.UpdateEnergy = L29_29
