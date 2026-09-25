
--==== ui recording ===============================================================

local _ModifyObjFuns 	= ModifyObjFuns
local _GetMTIndexFun 	= GetMTIndexFun
local _GetMTNewIndexFun = GetMTNewIndexFun

function MT_GetOptFunc()
	return _ModifyObjFuns, _GetMTIndexFun, _GetMTNewIndexFun
end


local function obj_func(...)
	return recording.obj_call(...)
end

--* be call by c++
function __UIObj_MTIndex(obj, key)
	local res = rawget(obj, key)
	if res ~= nil then
		return res
	end

	local mt = getmetatable(obj)
	res = rawget(mt, key)
	if ( not recording ) 				or 
	   ( not recording.is_runing() ) 	or 
	   ( not res ) 						or 
	   ( type(res) ~= "function" ) 		or 
	   ( not recording.is_hook(key) ) then
		return res
	end

	obj.__c_call = obj.__c_call or {}
	table.insert(obj.__c_call, res)
	table.insert(obj.__c_call, key)
	return obj_func
end

--==== end ===============================================================