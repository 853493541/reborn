---------------------------------------------------------------------->
-- 脚本名称:	scripts/skill/轻功/通用持续冲刺结束.lua
-- 更新时间:	2018/10/31 11:45:41
-- 更新用户:	XIAXIANBO1
-- 脚本说明:
----------------------------------------------------------------------<
function OnRemove(nCharacterID, BuffID, nBuffLevel, nLeftFrame, nCustomValue, dwSkillSrcID, nStackNum, nBuffIndex, dwCasterID, dwCasterSkillID)
	local player = GetPlayer(nCharacterID)
	if not player then
		return
	end
	player.Stop()
	RemoteCallToClient(player.dwID, "CallUIGlobalFunction", "rlcmd", string.format("disable skill move camera 1 %d", 0))
	RemoteCallToClient(player.dwID, "CallUIGlobalFunction", "HideFullScreenSFX")
end

function OnDetach(nCharacterID, BuffID, nBuffLevel, nLeftFrame, nCustomValue, dwSkillSrcID, nStackNum)
end