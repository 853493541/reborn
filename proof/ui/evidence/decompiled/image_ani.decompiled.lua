local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7
function L1_1(A0_11)
	local L1_12, L2_13, L3_14
	L1_12 = _ENV
	if not L1_12 then
		L1_12 = {}
		L2_13 = {}
		L3_14 = {}
		L3_14.startPer = 0
		L3_14.endPer = 0.995
		L3_14.interval = 130
		L3_14.axisPos = 0.5
		L3_14.is_ver = false
		L2_13[1] = L3_14
		L3_14 = {}
		L3_14.startPer = -0.995
		L3_14.endPer = 0
		L3_14.interval = 130
		L3_14.axisPos = 0.5
		L3_14.is_ver = false
		L2_13[2] = L3_14
		L1_12.Flip = L2_13
		L2_13 = {}
		L3_14 = {}
		L3_14.startPer = 0
		L3_14.endPer = -0.995
		L3_14.interval = 130
		L3_14.axisPos = 0.5
		L3_14.is_ver = false
		L2_13[1] = L3_14
		L3_14 = {}
		L3_14.startPer = 0.995
		L3_14.endPer = 0
		L3_14.interval = 130
		L3_14.axisPos = 0.5
		L3_14.is_ver = false
		L2_13[2] = L3_14
		L1_12.FlipR = L2_13
		L2_13 = {}
		L3_14 = {}
		L3_14.startPer = 0
		L3_14.endPer = 0.995
		L3_14.interval = 130
		L3_14.axisPos = 0.5
		L3_14.is_ver = true
		L2_13[1] = L3_14
		L3_14 = {}
		L3_14.startPer = -0.995
		L3_14.endPer = 0
		L3_14.interval = 130
		L3_14.axisPos = 0.5
		L3_14.is_ver = true
		L2_13[2] = L3_14
		L1_12.FlipV = L2_13
		L2_13 = {}
		L3_14 = {}
		L3_14.startPer = 0
		L3_14.endPer = -0.995
		L3_14.interval = 130
		L3_14.axisPos = 0.5
		L3_14.is_ver = true
		L2_13[1] = L3_14
		L3_14 = {}
		L3_14.startPer = 0.995
		L3_14.endPer = 0
		L3_14.interval = 130
		L3_14.axisPos = 0.5
		L3_14.is_ver = true
		L2_13[2] = L3_14
		L1_12.FlipVR = L2_13
		L2_13 = {}
		L3_14 = {}
		L3_14.startPer = 0
		L3_14.endPer = 0.995
		L3_14.interval = 130
		L3_14.axisPos = 0
		L3_14.is_ver = false
		L2_13[1] = L3_14
		L3_14 = {}
		L3_14.startPer = -0.995
		L3_14.endPer = 0
		L3_14.interval = 130
		L3_14.axisPos = 1
		L3_14.is_ver = false
		L2_13[2] = L3_14
		L1_12.Page = L2_13
		_ENV = L1_12
	end
	L1_12 = _ENV
	L1_12 = L1_12[A0_11]
	return L1_12
end
L2_2 = nil
function L3_3(A0_15)
	local L1_16, L2_17
	L1_16 = _ENV
	if not L1_16 then
		L1_16 = {}
		L2_17 = {}
		L2_17.alpha_init = 50
		L2_17.alpha_end = 255
		L2_17.alpha_delta = 60
		L2_17.alpha_loop = 1
		L2_17.interval_self = 180
		L1_16.Flashing = L2_17
		L2_17 = {}
		L2_17.alpha_init = 255
		L2_17.alpha_end = 0
		L2_17.alpha_delta = -15
		L2_17.interval_self = 50
		L2_17.playtime = 1500
		L1_16.FadeOut = L2_17
		L2_17 = {}
		L2_17.alpha_init = 0
		L2_17.alpha_end = 255
		L2_17.alpha_delta = 15
		L2_17.interval_self = 50
		L2_17.playtime = 1500
		L1_16.FadeIn = L2_17
		L2_17 = {}
		L2_17.alpha_init = 255
		L2_17.alpha_end = 0
		L2_17.interval_self = 10
		L2_17.playtime = 1500
		L1_16.FadeOut1 = L2_17
		L2_17 = {}
		L2_17.alpha_init = 0
		L2_17.alpha_end = 255
		L2_17.interval_self = 10
		L2_17.playtime = 1500
		L1_16.FadeIn1 = L2_17
		_ENV = L1_16
	end
	L1_16 = _ENV
	L1_16 = L1_16[A0_15]
	return L1_16
end
L4_4 = {}
L5_5 = {}
L6_6 = nil
function L7_7(A0_18)
	if A0_18 and not _ENV then
		_ENV = RegisterEvent("RENDER_FRAME_UPDATE", L5_5.onActive)
	end
	if not A0_18 and _ENV then
		local L1_19 = L1_19
		local L2_20 = L2_20
		L1_19(L2_20, _ENV)
		local L3_21 = L3_21
		L1_19 = nil
		_ENV = L1_19
	end
end
L5_5.updateEvent = L7_7
function L7_7(A0_22, A1_23, A2_24, A3_25)
	if true then
		if not A2_24 then
			return
		end
		if not A0_22.loadedImage[A2_24] then
			if A3_25 then
				local L8_30 = LoadUIImage(A2_24, false, false, false)
				repeat
					if not L8_30 then
						goto lbl_26
					end
					L8_30 = A0_22.loadedImage
					L8_30[A2_24] = "uitex"
					do break end -- pseudo-goto
					L8_30 = LoadImage
					L8_30 = L8_30(A2_24)
					A0_22.loadedImage[A2_24] = L8_30
				until true
			end
		end
		::lbl_26::
		if A3_25 then
			L8_30 = A1_23.FromUITex
			L8_30(A1_23, A2_24, A3_25)
		end
	else
		L8_30 = A1_23.FromTextureFile
		local L5_27 = L5_27
		L8_30(L5_27, A2_24)
		local L6_28 = L6_28
	end
end
L5_5.loadImage = L7_7
function L7_7(A0_31, A1_32)
	local L2_33, L3_34
	L2_33 = A1_32._rtd
	L3_34 = 0
	if A1_32.rotate_delta then
		L3_34 = A0_31:GetRotate() + A1_32.rotate_delta
		if L3_34 > 2 * math.pi then
			L3_34 = L3_34 - 2 * math.pi
		end
		A0_31:SetRotate(L3_34)
	end
	if A1_32.per_delta then
		local L4_35 = A0_31:GetPercentage()
		if L4_35 == 1 then
			L4_35 = 0
		end
		L3_34 = math.min(1, L4_35 + A1_32.per_delta)
		A0_31:SetPercentage(L3_34)
	end
	L4_35 = A1_32.alpha_delta
	if L4_35 then
		L4_35 = A1_32.alpha_init
		L4_35 = L4_35 < A1_32.alpha_end
		local L5_36 = A0_31:GetAlpha()
		if not A1_32.alpha_loop or A1_32.alpha_loop == 1 then
			if L4_35 and L5_36 + A1_32.alpha_delta > A1_32.alpha_end or not L4_35 and L5_36 + A1_32.alpha_delta < A1_32.alpha_end then
				A0_31:SetAlpha(A1_32.alpha_end)
				if A1_32.alpha_loop and not A1_32.line then
					A1_32.alpha_loop = -1
				end
			else
				A0_31:SetAlpha(L5_36 + A1_32.alpha_delta)
			end
		elseif A1_32.alpha_loop == -1 then
			if L4_35 and L5_36 - A1_32.alpha_delta < A1_32.alpha_init or not L4_35 and L5_36 - A1_32.alpha_delta > A1_32.alpha_init then
				A0_31:SetAlpha(A1_32.alpha_init)
				if A1_32.alpha_loop then
					A1_32.alpha_loop = 1
				end
			else
				local L6_37, L7_38 = L6_37, L7_38
				L6_37(L7_38, L5_36 - A1_32.alpha_delta)
				local L8_39 = L8_39
			end
		end
	end
end
L5_5.selfPlay = L7_7
function L7_7(A0_40, A1_41)
	local L2_42, L3_43 = GetTickCount(), L3_43
	L3_43 = A1_41._rtd
	if L3_43.state_3DRotate then
		local L7_47 = _ENV.rotate3D(A0_40, A1_41, L2_42)
		if L7_47 then
			A1_41.delete = true
			L7_47 = A1_41.end_func
			if L7_47 then
				L7_47 = A1_41.end_func
				L7_47(A0_40)
			end
		end
		return
	end
	L7_47 = L3_43.state_gif
	if L7_47 then
		L7_47 = L3_43.endtime
		if L2_42 > L7_47 then
			L7_47 = _ENV
			L7_47 = L7_47.gifPlay
			L7_47(A0_40, A1_41)
		end
	end
	L7_47 = L3_43.state_self
	if L7_47 then
		L7_47 = L3_43.endtime_self
		if L2_42 > L7_47 then
			L7_47 = L3_43.interval_self
			L7_47 = L2_42 + L7_47
			L3_43.endtime_self = L7_47
			L7_47 = _ENV
			L7_47 = L7_47.selfPlay
			L7_47(A0_40, A1_41)
		end
	end
	L7_47 = L3_43.nLoopCount
	if L7_47 then
		L7_47 = L3_43.nCurrentLoop
		if L7_47 >= L3_43.nLoopCount then
			goto lbl_64
		end
	end
	L7_47 = L3_43.stoptime
	if L7_47 then
		L7_47 = L3_43.stoptime
		if L7_47 ~= -1 then
			L7_47 = L3_43.stoptime
			::lbl_64::
			if L2_42 > L7_47 then
				L7_47 = A1_41.fnEndAction
				if L7_47 then
					L7_47 = A1_41.fnEndAction
					L7_47(A0_40)
					local L5_45 = L5_45
					A1_41.fnEndAction = nil
				end
				A1_41.delete = true
				return
			end
		end
	end
end
L5_5.playing = L7_7
function L7_7(A0_48, A1_49)
	local L2_50, L3_51
	L2_50 = A1_49._rtd
	L3_51 = L2_50.index
	L3_51 = L3_51 + 1
	L2_50.index = L3_51
	L3_51 = L2_50.index
	if L3_51 > L2_50.framecount then
		L2_50.index = 1
	end
	L3_51 = L2_50.nCurrentLoop
	if L3_51 then
		L3_51 = L2_50.index
		if L3_51 >= L2_50.framecount then
			L3_51 = L2_50.nCurrentLoop
			L3_51 = L3_51 + 1
			L2_50.nCurrentLoop = L3_51
		end
	end
	L3_51 = A1_49.aFrames
	L3_51 = L3_51[L2_50.index]
	aFrame = L3_51
	L3_51 = aFrame
	L3_51 = L3_51.interval
	if not L3_51 then
		L3_51 = L2_50.interval
	end
	L2_50.endtime = GetTickCount() + L3_51
	local L4_52 = L4_52
	local L5_53 = L5_53
	local L6_54 = L6_54
	local L7_55 = L7_55
	L4_52(L5_53, L6_54, L7_55, aFrame.nframe)
	local L8_56 = L8_56
end
L5_5.gifPlay = L7_7
function L7_7()
	if true then
		L4_61 = _ENV
		L3_60 = L3_60(L4_61)
		if not L3_60 then
			L3_60 = pairs
			L4_61 = _ENV
			L3_60, L4_61, _FOR_ = L3_60(L4_61)
			for _FORV_3_, _FORV_4_ in L3_60, L4_61, _FOR_ do
				if not _FORV_4_.delete and _FORV_3_:IsValid() then
					L5_5.playing(_FORV_3_, _FORV_4_)
					break -- pseudo-goto
				end
				imageAni_stop(_FORV_3_)
				repeat
				until true
			end
		end
	else
		L3_60 = L5_5
		L3_60 = L3_60.updateEvent
		L4_61 = false
		L3_60(L4_61)
	end
end
L5_5.onActive = L7_7
function L7_7(A0_65, A1_66, A2_67, A3_68)
	local L4_69 = GetTickCount()
	_ENV[A0_65] = A2_67
	A2_67._rtd = {}
	A2_67._rtd.loadedImage = {}
	if not A2_67.interval then
	end
	A2_67._rtd.interval = 100
	if not A2_67.interval_self then
	end
	A2_67._rtd.interval_self = A2_67._rtd.interval
	A2_67._rtd.endtime_self = L4_69 + A2_67._rtd.interval_self
	A2_67._rtd.index = 1
	if A2_67.aFrames then
		A2_67._rtd.framecount = #A2_67.aFrames
	end
	if A1_66 == -1 then
		A2_67._rtd.stoptime = -1
	elseif A3_68 then
		A2_67._rtd.nLoopCount = A1_66
		A2_67._rtd.nCurrentLoop = 0
	else
		A2_67._rtd.stoptime = L4_69 + A1_66
	end
	if A2_67._rtd.framecount and A2_67._rtd.framecount > 0 then
		A2_67._rtd.state_gif = true
	end
	if A2_67._rtd.state_gif and A2_67.aFrames[1].interval then
		A2_67._rtd.endtime = GetTickCount() + A2_67.aFrames[1].interval
	else
		A2_67._rtd.endtime = GetTickCount() + A2_67._rtd.interval
	end
	if A2_67.image_type then
		A0_65:SetImageType(A2_67.image_type)
	end
	if A2_67.aFrames then
		if A2_67.load_all then
			_FOR_, _FOR_, _FOR_ = pairs(A2_67.aFrames)
			for _FORV_8_, _FORV_9_ in _FOR_, _FOR_, _FOR_ do
				L5_5.loadImage(A2_67._rtd, A0_65, _FORV_9_.filename, _FORV_9_.nframe)
			end
			break -- pseudo-goto
		end
		L10_75 = L5_5
		L10_75 = L10_75.loadImage
		L11_76 = A2_67._rtd
		L12_77 = A0_65
		L13_78 = A2_67.aFrames
		L13_78 = L13_78[1]
		L13_78 = L13_78.filename
		L14_79 = A2_67.aFrames
		L14_79 = L14_79[1]
		L14_79 = L14_79.nframe
		L10_75(L11_76, L12_77, L13_78, L14_79)
	end
	repeat
	until true
	L10_75 = A2_67.rotate_init
	if L10_75 then
		L11_76 = A0_65
		L10_75 = A0_65.SetRotate
		L12_77 = A2_67.rotate_init
		L10_75(L11_76, L12_77)
	end
	L10_75 = A2_67.per_init
	if L10_75 then
		L11_76 = A0_65
		L10_75 = A0_65.SetPercentage
		L12_77 = A2_67.per_init
		L10_75(L11_76, L12_77)
	end
	L10_75 = A2_67.alpha_init
	if L10_75 then
		L10_75 = A2_67.alpha_delta
		if L10_75 == nil then
			L10_75 = A2_67.alpha_end
			L11_76 = A2_67.alpha_init
			L10_75 = L10_75 - L11_76
			L11_76 = A2_67.interval_self
			L11_76 = A1_66 / L11_76
			L10_75 = L10_75 / L11_76
			A2_67.alpha_delta = L10_75
		end
		L11_76 = A0_65
		L10_75 = A0_65.SetAlpha
		L12_77 = A2_67.alpha_init
		L10_75(L11_76, L12_77)
	end
	L10_75 = A2_67.alpha_init
	if not L10_75 then
		L10_75 = A2_67.rotate_delta
		if not L10_75 then
			L10_75 = A2_67.per_delta
			if not L10_75 then
				goto lbl_163
			end
		end
	end
	L10_75 = A2_67._rtd
	L10_75.state_self = true
	::lbl_163::
end
L5_5.init = L7_7
function L7_7(A0_80)
	L3_83 = A0_80
	L1_81, L3_83, L4_84 = L1_81(L3_83)
	for L5_85, _FORV_5_ in L1_81, L3_83, L4_84 do
		if _FORV_5_ == "uitex" then
			UnloadUIImage(L5_85)
		else
			UnloadImage(_FORV_5_)
			local L7_87 = L7_87
		end
	end
end
L5_5.unInit = L7_7
function L7_7(A0_88)
	local L1_89
	L1_89 = 0
	L4_92 = pairs
	L5_93 = A0_88.aFrames
	L4_92, L5_93, L6_94 = L4_92(L5_93)
	for L7_95, _FORV_6_ in L4_92, L5_93, L6_94 do
		if _FORV_6_.interval then
			L1_89 = L1_89 + _FORV_6_.interval
		end
	end
	if L1_89 == 0 then
		L4_92 = A0_88.aFrames
		L4_92 = #L4_92
		L5_93 = A0_88.interval
		if not L5_93 then
			L5_93 = 0
		end
		L1_89 = L4_92 * L5_93
	end
	return L1_89
end
imageAni_onceGifTime = L7_7
function L7_7(A0_96)
	local L1_97
	L1_97 = A0_96.interval_self
	if not L1_97 then
		L1_97 = A0_96.interval
	end
	if not L1_97 then
		L1_97 = 0
	end
	if A0_96.rotate_delta then
		local L4_100 = L4_100
		L4_100 = L4_100(2 * math.pi / A0_96.rotate_delta)
		return L4_100 * L1_97
	end
	L4_100 = A0_96.per_delta
	if L4_100 then
		L4_100 = math
		L4_100 = L4_100.ceil
		local L4_100, L3_99 = L4_100(1 / A0_96.per_delta), L3_99
		L3_99 = L4_100 * L1_97
		return L3_99
	end
end
imageAni_onceSelfTime = L7_7
function L7_7(A0_101)
	local L1_102
	L1_102 = _ENV
	L1_102 = L1_102[A0_101]
	return L1_102
end
imageAni_isplaying = L7_7
function L7_7(A0_103, A1_104, A2_105, A3_106)
	if not A2_105 then
		return
	end
	if _ENV[A0_103] then
		imageAni_stop(A0_103)
	end
	if A3_106 == nil then
		A3_106 = A2_105.bLoop
	end
	if not A1_104 then
		A1_104 = A2_105.playtime
	end
	L5_5.updateEvent(true)
	local L4_107 = L4_107
	local L5_108 = L5_108
	local L6_109 = L6_109
	local L7_110 = L7_110
	L4_107(L5_108, L6_109, L7_110, A3_106)
	local L8_111 = L8_111
end
imageAni_play = L7_7
function L7_7(A0_112, A1_113, A2_114, A3_115)
	local L4_116 = L4_116
	L4_116 = L4_116(A1_113)
	L4_116 = clone(L4_116)
	L4_116.fnEndAction = A3_115
	local L5_117 = L5_117
	local L6_118 = L6_118
	local L7_119 = L7_119
	L5_117(L6_118, L7_119, L4_116)
	local L8_120 = L8_120
end
imageAni_playEx = L7_7
function L7_7(A0_121)
	if _ENV[A0_121] then
		if _ENV[A0_121]._rtd.loadedImage then
			local L1_122 = L1_122
			L1_122(_ENV[A0_121]._rtd.loadedImage)
		end
		L1_122 = _ENV
		L1_122 = L1_122[A0_121]
		L1_122 = L1_122.fnEndAction
		_ENV[A0_121]._used = nil
		_ENV[A0_121].delete = nil
		_ENV[A0_121]._rtd = nil
		_ENV[A0_121] = nil
		if L1_122 then
			local L2_123 = L2_123
			L2_123(A0_121)
			local L3_124 = L3_124
		end
	end
end
imageAni_stop = L7_7
function L7_7()
	L2_127 = _ENV
	L0_125, L2_127, L3_128 = L0_125(L2_127)
	for L4_129, _FORV_4_ in L0_125, L2_127, L3_128 do
		if _FORV_4_._rtd then
			L5_5.unInit(_FORV_4_._rtd.loadedImage)
			local L6_131 = L6_131
			_FORV_4_._rtd = nil
		end
	end
end
local L8_8 = L8_8
local L9_9 = L9_9
L8_8(L9_9, L7_7)
local L10_10 = L10_10
function L8_8(A0_132, A1_133, A2_134)
	local L3_135, L4_136
	L3_135 = A1_133._rtd
	L4_136 = L3_135.starttime
	L4_136 = A2_134 - L4_136
	L4_136 = L4_136 / A1_133.interval
	local L5_137 = L5_137
	local L6_138 = L6_138
	L5_137 = L5_137(L6_138, 1)
	L4_136 = L5_137
	L5_137 = A1_133.startPer
	L6_138 = A1_133.endPer
	L6_138 = L6_138 - A1_133.startPer
	L6_138 = L6_138 * L4_136
	L5_137 = L5_137 + L6_138
	L6_138 = math
	L6_138 = L6_138.floor
	L6_138 = L6_138(math.abs(L5_137))
	if 0 <= L5_137 then
		L5_137 = L5_137 - L6_138
	else
		L5_137 = L5_137 + L6_138
	end
	if L5_137 == 0 and 1 < L6_138 then
		L5_137 = 1
	elseif L5_137 == 0 and L6_138 < -1 then
		L5_137 = -1
	end
	local L7_139 = L7_139
	local L8_140 = L8_140
	local L9_141 = L9_141
	local L10_142 = L10_142
	L7_139(L8_140, L9_141, L10_142, A1_133.is_ver)
	local L11_143 = L11_143
	if L4_136 == 1 then
		L7_139 = true
		return L7_139
	end
	return
end
L5_5.rotate3D = L8_8
L8_8 = nil
function L9_9(A0_144, A1_145, A2_146, A3_147, A4_148, A5_149, A6_150)
	if not A0_144:IsValid() then
		return
	end
	if _ENV[A0_144] then
		local L7_151 = L7_151
		L7_151(A0_144)
	end
	L7_151 = L8_8
	if not L7_151 then
		L7_151 = {}
		L7_151._used = false
	end
	L8_8 = L7_151
	L7_151 = L8_8
	if L7_151._used then
		L7_151 = {}
	else
		L7_151._used = true
	end
	_ENV[A0_144] = L7_151
	L7_151.startPer = A1_145
	L7_151.endPer = A2_146
	L7_151.interval = A3_147
	L7_151.is_ver = A4_148
	L7_151.axisPos = A5_149
	L7_151.end_func = A6_150
	L7_151._rtd = {}
	L7_151._rtd.starttime = GetTickCount()
	L7_151._rtd.state_3DRotate = true
	local L10_154 = L10_154
	local L11_155 = L11_155
	L10_154(L11_155, A1_145, A5_149, A4_148)
	local L12_156 = L12_156
	L10_154 = L5_5
	L10_154 = L10_154.updateEvent
	L11_155 = true
	L10_154(L11_155)
end
L5_5.playRotate3D = L9_9
function L9_9(A0_157, A1_158, A2_159, A3_160)
	local L4_161, L5_162
	if A3_160 then
		L4_161 = A1_158
		L5_162 = L4_161 * A2_159
	else
		L4_161 = A0_157
		L5_162 = L4_161 * A2_159
	end
	local L6_163 = L6_163
	local L8_165 = L4_161 * L4_161 - L5_162 * L5_162
	return L6_163(L8_165)
end
function L10_10(A0_166, A1_167, A2_168, A3_169)
	repeat
		local L4_170, L5_171 = A0_166:GetSize()
		local L6_172 = L6_172
		L6_172 = L6_172(L4_170, L5_171, 1 - math.abs(A1_167), A3_169)
		L6_172 = L6_172 / L5_171
		L6_172 = L6_172 / 5
		if A3_169 then
			local L10_176 = L10_176
			if 0 <= A1_167 then
				A0_166:SetPaintOffset(0, L6_172 * A2_168, L10_176)
				A0_166:SetPaintOffset(1, 1 - L6_172 * A2_168, L10_176)
				A0_166:SetPaintOffset(2, -(L6_172 * (1 - A2_168)), 1 - math.abs(A1_167) * (1 - A2_168))
				A0_166:SetPaintOffset(3, 1 + L6_172 * (1 - A2_168), 1 - math.abs(A1_167) * (1 - A2_168))
				break -- pseudo-goto
			end
			A0_166:SetPaintOffset(0, -(L6_172 * A2_168), L10_176)
			A0_166:SetPaintOffset(1, 1 + L6_172 * A2_168, L10_176)
			A0_166:SetPaintOffset(2, L6_172 * (1 - A2_168), 1 - math.abs(A1_167) * (1 - A2_168))
			A0_166:SetPaintOffset(3, 1 - L6_172 * (1 - A2_168), 1 - math.abs(A1_167) * (1 - A2_168))
			break -- pseudo-goto
		end
		L10_176 = math
		L10_176 = L10_176.abs
		L10_176 = L10_176(A1_167)
		L10_176 = L10_176 * A2_168
		local L8_174 = L8_174
		local L8_174, L9_175 = L8_174(A1_167), L9_175
		L9_175 = 1 - A2_168
		L8_174 = L8_174 * L9_175
		L9_175 = L6_172 * A2_168
		if 0 <= A1_167 then
			A0_166:SetPaintOffset(0, L10_176, L9_175)
			A0_166:SetPaintOffset(2, L10_176, 1 - L9_175)
			A0_166:SetPaintOffset(1, 1 - L8_174, -(L6_172 * (1 - A2_168)))
			A0_166:SetPaintOffset(3, 1 - L8_174, 1 + L6_172 * (1 - A2_168))
		else
			A0_166:SetPaintOffset(0, L10_176, -L9_175)
			A0_166:SetPaintOffset(2, L10_176, 1 + L9_175)
			A0_166:SetPaintOffset(1, 1 - L8_174, L6_172 * (1 - A2_168))
			local L11_177, L12_178 = L11_177, L12_178
			local L13_179 = L13_179
			local L14_180 = L14_180
			L12_178(L13_179, L14_180, 1 - L8_174, 1 - L11_177)
			local L15_181 = L15_181
		end
	until true
end
image3D_Rotate = L10_10
function L10_10(A0_182)
	A0_182:SetPaintOffset(0, 0, 0)
	A0_182:SetPaintOffset(2, 0, 1)
	A0_182:SetPaintOffset(1, 1, 0)
	local L3_183 = L3_183
	local L4_184 = L4_184
	L3_183(L4_184, 3, 1, 1)
	local L5_185 = L5_185
end
image3D_restore = L10_10
function L10_10(A0_186, A1_187, A2_188, A3_189, A4_190, A5_191, A6_192)
	if not A3_189 then
		A3_189 = 200
	end
	if A4_190 == nil then
		A4_190 = 0.5
	end
	A0_186:Show()
	local L7_193 = L7_193
	local L8_194 = L8_194
	local L9_195 = L9_195
	local L10_196 = L10_196
	local L11_197 = L11_197
	local L12_198 = L12_198
	local L13_199 = L13_199
	L7_193(L8_194, L9_195, L10_196, L11_197, L12_198, L13_199, A6_192)
	local L14_200 = L14_200
end
imageAni_3DRotate = L10_10
function L10_10(A0_201, A1_202, A2_203, A3_204, A4_205, A5_206)
	local L6_207
	if type(A2_203) == "string" then
		L6_207 = _ENV(A2_203)
	else
		local L7_208 = L7_208
		local L7_208, L8_209 = L7_208(A2_203), L8_209
		if L7_208 == "table" then
			L6_207 = A2_203
		end
	end
	if not L6_207 then
		return
	end
	if not A4_205 then
		A4_205 = 1
	end
	function L7_208(A0_218)
		local L1_219, L2_220, L3_221
		L1_219 = A0_218._data
		L2_220 = A0_218._cimgNext
		L3_221 = L1_219.playCount
		L3_221 = L3_221 + 1
		L1_219.playCount = L3_221
		L3_221 = L1_219.params
		if L1_219.playCount >= L1_219.totalCount then
			A0_218._data = nil
			A0_218._cimgNext = nil
			L2_220._cimgNext = nil
			L2_220._data = nil
			if L1_219.cb_func then
				L1_219.cb_func(A0_218, L2_220)
			end
			return
		end
		A0_218:Hide()
		L2_220:Show()
		local L4_222, L5_223 = L4_222, L5_223
		L4_222 = L1_219.playCount
		L4_222 = L4_222 % 2
		L4_222 = L4_222 + 1
		L4_222 = L3_221[L4_222]
		L5_223 = L1_219.interval
		if not L5_223 then
			L5_223 = L4_222.interval
		end
		local L6_224 = L6_224
		local L7_225 = L7_225
		local L8_226 = L8_226
		local L9_227 = L9_227
		local L10_228 = L10_228
		local L11_229 = L11_229
		local L12_230 = L12_230
		L6_224(L7_225, L8_226, L9_227, L10_228, L11_229, L12_230, L1_219.end_func)
		local L13_231 = L13_231
	end
	A0_201._cimgNext = A1_202
	A1_202._cimgNext = A0_201
	L8_209 = {}
	L8_209.params = L6_207
	L8_209.interval = A3_204
	L8_209.is_ver = is_ver
	L8_209.playCount = 0
	L8_209.totalCount = A4_205 * 2
	L8_209.cb_func = A5_206
	L8_209.end_func = L7_208
	A0_201._data = L8_209
	L8_209 = A0_201._data
	A1_202._data = L8_209
	L8_209 = L6_207[1]
	if not A3_204 then
		A3_204 = L8_209.interval
	end
	local L9_210 = L9_210
	local L10_211 = L10_211
	local L11_212 = L11_212
	local L12_213 = L12_213
	local L13_214 = L13_214
	local L14_215 = L14_215
	local L15_216 = L15_216
	L9_210(L10_211, L11_212, L12_213, L13_214, L14_215, L15_216, L7_208)
	local L16_217 = L16_217
end
imageAni_turn = L10_10
function L10_10(A0_232, A1_233, A2_234, A3_235)
	local L8_240, L9_241, L10_242, L11_243, L12_244 = L8_240, L9_241, L10_242, L11_243, L12_244
	if not A0_232.nOrigX then
		L9_241 = A0_232
		L8_240 = A0_232.GetRelX
		L8_240 = L8_240(L9_241)
	end
	L9_241 = A0_232.nOrigY
	if not L9_241 then
		L10_242 = A0_232
		L9_241 = A0_232.GetRelY
		L9_241 = L9_241(L10_242)
	end
	L11_243 = A0_232
	L10_242 = A0_232.GetSize
	L10_242, L11_243 = L10_242(L11_243)
	L12_244 = A2_234 * L10_242
	local L13_245 = L13_245
	A0_232:SetRotate(A1_233)
	A0_232:SetRelX(L8_240 + (-(L12_244 - 0.5 * L10_242) * math.cos(A1_233) + (A3_235 * L11_243 - 0.5 * L11_243) * math.sin(A1_233) + (L12_244 - 0.5 * L10_242)))
	A0_232:SetRelY(L9_241 + (-(L12_244 - 0.5 * L10_242) * math.sin(A1_233) - (A3_235 * L11_243 - 0.5 * L11_243) * math.cos(A1_233) + (A3_235 * L11_243 - 0.5 * L11_243)))
	local L16_248 = L16_248
	A0_232:GetParent():FormatAllItemPos()
	local L15_247 = L15_247
end
imageAni_RotateWithCenter = L10_10
