local L0_0
L0_0 = true
RegisterEvent("FIRST_LOADING_END", function()
	if not GetClientPlayer() then
		return
	else
		repeat
			local L2_8 = L2_8
			if _ENV and Kungfu_GetPlayerMountType() == FORCE_TYPE.BA_DAO then
				BadaoPosture.Open()
				break -- pseudo-goto
			end
			BadaoPosture.Close()
		until true
	end
end)
RegisterEvent("SKILL_MOUNT_KUNG_FU", function()
	if not GetClientPlayer() then
		return
	else
		repeat
			local L2_10 = L2_10
			if _ENV and Kungfu_GetPlayerMountType() == FORCE_TYPE.BA_DAO then
				BadaoPosture.Open()
				break -- pseudo-goto
			end
			BadaoPosture.Close()
		until true
	end
end)
function EnableBadaoPosture(A0_11)
	_ENV = A0_11
	_UPVALUE1_()
	local L1_12 = L1_12
end
RegisterEvent("SKILL_MOUNT_KUNG_FU", function()
	if Kungfu_GetPlayerMountType() == FORCE_TYPE.BA_DAO then
		if StorageServer.GetData("BarBind_Pose1") == 0 then
			StorageServer.SetData("BarBind_Pose1", 3)
		end
		if StorageServer.GetData("BarBind_Pose2") == 0 then
			StorageServer.SetData("BarBind_Pose2", 1)
		end
		if StorageServer.GetData("BarBind_Pose3") == 0 then
			local L1_13 = L1_13
			L1_13("BarBind_Pose3", 2)
			local L2_14 = L2_14
		end
	end
end)
local L5_5 = L5_5
RegisterEvent("SYNC_USER_PREFERENCES_END", function()
	if StorageServer.IsNull() then
		_ENV()
	end
end)
local L6_6 = L6_6
L0_0 = true
function L5_5()
	if not GetClientPlayer() then
		return
	else
		repeat
			local L2_16 = L2_16
			if _ENV and Kungfu_GetPlayerMountType() == FORCE_TYPE.QI_XIU then
				QiXiuPosture.Open()
				break -- pseudo-goto
			end
			QiXiuPosture.Close()
		until true
	end
end
L6_6 = RegisterEvent
L6_6("FIRST_LOADING_END", L5_5)
L6_6 = RegisterEvent
L6_6("SKILL_MOUNT_KUNG_FU", L5_5)
L0_0 = true
function L5_5()
	if not GetClientPlayer() then
		return
	else
		repeat
			local L2_18 = L2_18
			if _ENV and Kungfu_GetPlayerMountType() == FORCE_TYPE.CHANG_GE then
				ChangGePosture.Open()
				break -- pseudo-goto
			end
			ChangGePosture.Close()
		until true
	end
end
L6_6 = RegisterEvent
L6_6("FIRST_LOADING_END", L5_5)
L6_6 = RegisterEvent
L6_6("SKILL_MOUNT_KUNG_FU", L5_5)
function L0_0()
	local L0_19 = GetClientPlayer()
	if not L0_19 then
		return
	elseif Kungfu_GetPlayerMountType() ~= FORCE_TYPE.CANG_JIAN then
		CJPosture.Close()
		return
	end
	if L0_19 and L0_19.bCanUseBigSword then
		CJPosture.Open()
	else
		local L2_21 = IsModuleLoaded("SwitchSword")
		if L2_21 then
			L2_21 = CJPosture
			L2_21 = L2_21.Close
			L2_21()
		end
	end
end
L5_5 = RegisterEvent
L6_6 = "FIRST_LOADING_END"
L5_5(L6_6, L0_0)
L5_5 = RegisterEvent
L6_6 = "SKILL_MOUNT_KUNG_FU"
L5_5(L6_6, L0_0)
L5_5 = RegisterEvent
L6_6 = "CURRENT_PLAYER_FORCE_CHANGED"
L5_5(L6_6, L0_0)
L5_5 = RegisterEvent
L6_6 = "SKILL_UNMOUNT_KUNG_FU"
L5_5(L6_6, L0_0)
local L3_3 = L3_3
