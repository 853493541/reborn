local L0_0, L1_1
L0_0 = {}
function L1_1(A0_2)
	local L1_3, L2_4
	L1_3 = {}
	local L1_3.ctor, L6_8 = false, L6_8
	L1_3.super = A0_2
	function L2_4(...)
		local L1_9
		L1_9 = {}
		;(function(A0_15, ...)
			local L2_16, L3_17, L4_18
			L2_16 = A0_15.super
			if L2_16 then
				L2_16 = _ENV
				L3_17 = A0_15.super
				L4_18 = ...
				L2_16(L3_17, L4_18)
			end
			L2_16 = A0_15.ctor
			if L2_16 then
				L2_16 = A0_15.ctor
				L3_17 = L1_9
				L4_18 = ...
				L2_16(L3_17, L4_18)
			end
		end)(_ENV, ...)
		local L2_10 = L2_10
		local L3_11 = L3_11
		local L6_14 = L6_14
		L6_14.__index = _ENV[_ENV]
		L2_10(L3_11, L6_14)
		return L1_9
	end
	L1_3.new = L2_4
	L2_4 = {}
	L6_8 = _ENV
	L6_8[L1_3] = L2_4
	L6_8 = setmetatable
	;({}).__newindex = function(A0_19, A1_20, A2_21)
		local L3_22
		L3_22 = _ENV
		L3_22[A1_20] = A2_21
	end
	L6_8(L1_3, {})
	if A0_2 then
		L6_8 = setmetatable
		local L4_6 = L4_6
		;({}).__index = function(A0_23, A1_24)
			local L2_25, L3_26
			L2_25 = _ENV
			L3_26 = A0_2
			L2_25 = L2_25[L3_26]
			L2_25 = L2_25[A1_24]
			L3_26 = L2_4
			L3_26[A1_24] = L2_25
			return L2_25
		end
		L6_8(L4_6, {})
		local L5_7 = L5_7
	end
	return L1_3
end
class = L1_1
function L1_1(A0_27, A1_28)
	local L2_29
	if not A0_27 or not A1_28 then
		return
	end
	L2_29 = A0_27.___id
	L5_32 = pairs
	L5_32, L4_31, _FOR_ = L5_32(A1_28)
	for _FORV_6_, _FORV_7_ in L5_32, L4_31, _FOR_ do
		A0_27.k = _FORV_7_
	end
	A0_27.___id = L2_29
end
copyWndUserInfo = L1_1
