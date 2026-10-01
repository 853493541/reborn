---------------------------------------------------------------------->
-- 脚本名称:	scripts/skill/轻功/轻功状态结束处理.lua
-- 更新时间:	2018/8/11 17:16:15
-- 更新用户:	XIAXIANBO1
-- 脚本说明:
----------------------------------------------------------------------<
function OnRemove(nCharacterID, BuffID, nBuffLevel, nLeftFrame, nCustomValue, dwSkillSrcID, nStackNum, nBuffIndex, dwCasterID, dwCasterSkillID)
	local player = GetPlayer(dwSkillSrcID)
	if not player then
		return
	end
	
	player.StopBirdFly()
	player.UnlockBirdMoveZ()
	--空战索敌镜头关闭
	RemoteCallToClient(player.dwID, "CallUIGlobalFunction", "rlcmd", string.format("enable aircombat camera %d", 0))
end

function OnDetach(nCharacterID, BuffID, nBuffLevel, nLeftFrame, nCustomValue, dwSkillSrcID, nStackNum)
end