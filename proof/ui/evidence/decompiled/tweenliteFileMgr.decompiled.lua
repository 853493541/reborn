local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7
L0_0 = {}
function L1_1(A0_8, A1_9)
	if A0_8 == "linear" then
		return A0_8
	end
	local L2_10 = L2_10
	L2_10 = L2_10(A1_9, 5)
	local L3_11 = L3_11
	local L3_11, L4_12 = L3_11 .. A0_8, L4_12
	return L3_11
end
function L2_2(A0_13, A1_14, A2_15, A3_16)
	local L4_17 = L4_17
	local L6_18 = L6_18
	local L7_19 = L7_19
	local L4_17, L6_18, L8_20 = L4_17(L6_18, L7_19, A2_15, A3_16)
	if L6_18 then
		return L4_17
	end
end
function L3_3(A0_21, A1_22, A2_23, A3_24)
	local L4_25 = L4_25
	local L6_26 = L6_26
	local L7_27 = L7_27
	local L4_25, L6_26, L8_28 = L4_25(L6_26, L7_27, A2_23, A3_24)
	if L6_26 then
		return L4_25
	end
end
function L4_4(A0_29, A1_30, A2_31, A3_32)
	local L4_33 = L4_33
	local L6_34 = L6_34
	local L7_35 = L7_35
	local L4_33, L6_34, L8_36 = L4_33(L6_34, L7_35, A2_31, A3_32)
	L4_33 = L4_33 == 1
	if L6_34 then
		return L4_33
	end
end
function L5_5(A0_37, A1_38, A2_39)
	repeat
		local L8_45 = L8_45
		L8_45 = L8_45(A0_37, A1_38, "NewTween", 0)
		if L8_45 == 0 then
			L8_45 = A0_37.ReadInteger
			L8_45 = L8_45(A0_37, A1_38, "StepCount", 0)
			_FOR_ = 1
			local L7_44 = L7_44
			for _FORV_8_ = _FOR_, _FOR_, _FOR_ do
				({}).fun = A0_37:ReadString(A1_38 .. "_Step" .. _FORV_8_, "Tweenfun", "to")
				;({}).easing = _ENV(A0_37:ReadString(A1_38 .. "_Step" .. _FORV_8_, "TweenType", "linear"), (A0_37:ReadString(A1_38 .. "_Step" .. _FORV_8_, "EaseType", "easein")))
				;({}).time = A0_37:ReadInteger(A1_38 .. "_Step" .. _FORV_8_, "TweenTime", 200)
				;({}).relX = L2_2(A0_37, A1_38 .. "_Step" .. _FORV_8_, "relX", 0)
				;({}).relY = L2_2(A0_37, A1_38 .. "_Step" .. _FORV_8_, "relY", 0)
				;({}).alpha = L2_2(A0_37, A1_38 .. "_Step" .. _FORV_8_, "alpha", 0)
				;({}).delay = L2_2(A0_37, A1_38 .. "_Step" .. _FORV_8_, "delay", 0)
				;({}).scale = L3_3(A0_37, A1_38 .. "_Step" .. _FORV_8_, "scale", 1.0E-4)
				;({}).scaleX = L3_3(A0_37, A1_38 .. "_Step" .. _FORV_8_, "scaleX", 1.0E-5)
				;({}).scaleY = L3_3(A0_37, A1_38 .. "_Step" .. _FORV_8_, "scaleY", 1.0E-5)
				;({}).rotate = L3_3(A0_37, A1_38 .. "_Step" .. _FORV_8_, "rotate", 0)
				if ({}).scale then
					({}).scale = math.max(({}).scale, 1.0E-4)
				end
				if ({}).scaleX then
					({}).scaleX = math.max(({}).scaleX, 1.0E-4)
				end
				if ({}).scaleY then
					({}).scaleY = math.max(({}).scaleY, 1.0E-4)
				end
				;({}).waiting = true
				local L18_55 = L18_55
				L18_55.args, ({}).usemap = {}, L4_4(A0_37, A1_38 .. "_Step" .. _FORV_8_, "usemap", 0)
				local L15_52 = L15_52
				table.insert(L7_44, L18_55)
				local L16_53 = L16_53
			end
			A2_39[A1_38] = L7_44
			break -- pseudo-goto
		end
		L8_45 = TweenNew
		L8_45 = L8_45.LoadTween
		L7_44 = A0_37
		L10_47 = A1_38
		L8_45 = L8_45(L7_44, L10_47)
		A2_39[A1_38] = L8_45
	until true
end
function L6_6(A0_56)
	local L1_57 = L1_57
	local L1_57, L2_58 = L1_57(A0_56), L2_58
	if not L1_57 then
		return
	end
	L2_58 = ""
	while true do
		L2_58 = L1_57:GetNextSection(L2_58)
		if not L2_58 then
			break
		end
		if L1_57:ReadString(L2_58, "Type", "") == "Tween" then
			local L5_61 = L5_61
			local L6_62 = L6_62
			L6_62(L1_57, L2_58, L0_0[A0_56])
			local L7_63 = L7_63
		end
	end
	L6_62 = L1_57
	L5_61 = L1_57.Close
	L5_61(L6_62)
end
function L7_7(A0_64, A1_65, A2_66)
	if not _ENV[A0_64] or A2_66 then
		_ENV[A0_64] = {}
		local L3_67 = L3_67
		L3_67(A0_64)
		local L4_68 = L4_68
	end
	L3_67 = _ENV
	L3_67 = L3_67[A0_64]
	L3_67 = L3_67[A1_65]
	return L3_67
end
GetTweenParam = L7_7
function L7_7(A0_69, A1_70, A2_71, A3_72)
	repeat
		local L4_73 = A0_69:GetTweenFile()
		if not L4_73 then
			return
		end
		if not A3_72 then
			local L5_74 = A0_69:GetTweenName()
			A3_72 = L5_74
		end
		L5_74 = clone
		L5_74 = L5_74(GetTweenParam(L4_73, A3_72, A2_71))
		if not L5_74 then
			return
		end
		if L5_74.bNewTween then
			L8_77(L9_78, L10_79, L5_74, L4_73, A3_72)
			break -- pseudo-goto
		end
		L8_77 = ipairs
		L9_78 = L5_74
		L8_77, L9_78, L10_79 = L8_77(L9_78)
		for _FORV_9_, _FORV_10_ in L8_77, L9_78, L10_79 do
			if _FORV_9_ == #L5_74 then
				clone(_FORV_10_.args).complete = A1_70
			end
			local L13_82 = L13_82
			local L14_83 = L14_83
			local L15_84 = L15_84
			L14_83(L15_84, A0_69, L13_82, _FORV_10_.easing)
			local L16_85 = L16_85
		end
	until true
end
TweenPlay = L7_7
