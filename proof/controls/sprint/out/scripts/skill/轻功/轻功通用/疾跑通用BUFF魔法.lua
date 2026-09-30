---------------------------------------------------------------------->
-- 脚本名称:	scripts/skill/轻功/轻功通用/疾跑通用BUFF魔法.lua
-- 更新时间:	2017/7/15 23:34:22
-- 更新用户:	ZHANGYAN0-PC
-- 脚本说明:
----------------------------------------------------------------------<
function Apply(dwCharacterID)

end

function UnApply(dwCharacterID)
	--print(44444)
	local player = GetPlayer(dwCharacterID)
	if not player then
		return
	end
	if player.IsHaveBuff(12190, 1) then
		player.DelBuff(12190, 1)
	end
end
