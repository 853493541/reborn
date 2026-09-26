local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18
L0_0 = 2
L1_1 = table
L1_1 = L1_1.insert
L2_2 = table
L2_2 = L2_2.concat
L3_3 = {}
function L4_4(A0_29)
	if A0_29:find("${account}", nil, true) then
		if not GetUserAccount() then
		end
		A0_29 = A0_29:gsub("%${account}", "")
	end
	if A0_29:find("${region}", nil, true) or A0_29:find("${server}", nil, true) then
		A0_29 = A0_29:gsub("%${region}", (select(5, GetUserServer()):gsub("[/\\]", "")))
		local L7_36 = L7_36
		local L8_37 = L8_37
		local L9_38 = L9_38
		L9_38 = L9_38(A0_29, "%${server}", (select(5, GetUserServer()):gsub("[/\\]", "")))
		A0_29 = L9_38
	end
	L8_37 = A0_29
	L7_36 = A0_29.find
	L9_38 = "${name}"
	L7_36 = L7_36(L8_37, L9_38, nil, true)
	if L7_36 then
		L8_37 = A0_29
		L7_36 = A0_29.gsub
		L9_38 = "%${name}"
		L7_36 = L7_36(L8_37, L9_38, GetUserRoleName())
		A0_29 = L7_36
	end
	L8_37 = A0_29
	L7_36 = A0_29.find
	L9_38 = "${uid}"
	L7_36 = L7_36(L8_37, L9_38, nil, true)
	if L7_36 then
		L7_36 = GetClientPlayer
		L7_36 = L7_36()
		L8_37 = assert
		L9_38 = L7_36
		L8_37(L9_38, "Client player not exist!")
		local L4_33 = L4_33
		L9_38 = A0_29
		L8_37 = A0_29.gsub
		L4_33 = "%${uid}"
		local L5_34 = L7_36.GetGlobalID()
		L8_37 = L8_37(L9_38, L4_33, L5_34, L7_36.GetGlobalID())
		A0_29 = L8_37
	end
	return A0_29
end
L5_5 = {}
L5_5.pak = false
L5_5.crc = false
L5_5.retenv = false
L6_6 = {}
L6_6.__index = L5_5
function L7_7(A0_39, A1_40, A2_41, A3_42, A4_43)
	A0_39 = _ENV(A0_39)
	if type(A1_40) ~= "table" then
		({}).pak = A1_40
		;({}).crc = A2_41
		A1_40, ({}).retenv = {}, A3_42
	end
	local L5_44 = L5_44
	L5_44(A1_40, L6_6)
	L5_44 = nil
	if A1_40.passphrase then
		L5_44 = LoadDataFromFile(A0_39, A1_40.pak, A1_40.passphrase)
	else
		L5_44 = LoadDataFromFile(A0_39, A1_40.pak)
	end
	if L5_44 then
		if A1_40.crc or IsEncodedData(L5_44) then
			L5_44 = DecodeData(L5_44)
		end
		if L5_44 then
			if not A1_40.env then
			end
			if A4_43 then
				L5_44 = LoadSaveFile(L5_44)
			else
				local L9_48 = L9_48
				local L10_49 = str2var(L5_44, {}, true)
				L5_44 = L10_49
			end
			L10_49 = A1_40.retenv
			if L10_49 then
				L5_44 = L9_48
			elseif L5_44 == nil then
				L5_44 = L9_48.data
			end
		end
	end
	L9_48 = setmetatable
	L10_49 = A1_40
	L9_48(L10_49, nil)
	local L8_47 = L8_47
	return L5_44
end
LoadLUAData = L7_7
function L7_7(A0_50, A1_51, A2_52, A3_53, A4_54)
	A0_50 = _ENV(A0_50)
	if type(A2_52) ~= "table" then
		({}).pak = A2_52
		;({}).crc = A3_53
		A2_52, ({}).retenv = {}, A4_54
	end
	setmetatable(A2_52, L6_6)
	local L8_58 = L8_58
	L8_58(A0_50, function(A0_60)
		local L1_61
		L1_61 = _ENV
		L1_61 = L1_61.env
		if not L1_61 then
			L1_61 = {}
		end
		if A0_60 then
			local L4_64 = L4_64
			local L4_64, L5_65 = L4_64(A0_60, L1_61, true), L5_65
			A0_60 = L4_64
			L4_64 = _ENV
			L4_64 = L4_64.retenv
			if L4_64 then
				A0_60 = L1_61
			elseif A0_60 == nil then
				A0_60 = L1_61.data
			end
		end
		L4_64 = A1_51
		L5_65 = A0_60
		L4_64(L5_65)
	end, A2_52.pak, A2_52.passphrase)
	local L9_59 = L9_59
	L8_58 = setmetatable
	L9_59 = A2_52
	L8_58(L9_59, nil)
	local L7_57 = L7_57
end
AsyncLoadLUAData = L7_7
L5_5 = {}
L5_5.indent = nil
L5_5.crc = true
L5_5.compress = false
L5_5.passphrase = nil
L6_6 = {}
L6_6.__index = L5_5
function L7_7(A0_66, A1_67, A2_68, A3_69)
	A0_66 = _ENV(A0_66)
	if string.char(string.byte(A0_66, 2, 2)) == ":" then
		return
	end
	if type(A2_68) ~= "table" then
		({}).indent = A2_68
		A2_68, ({}).crc = {}, A3_69
	end
	setmetatable(A2_68, L6_6)
	local L8_74 = L8_74
	A1_67 = L8_74 .. var2str(A1_67, A2_68.indent, nil)
	L8_74 = A2_68.crc
	if not L8_74 then
		L8_74 = A2_68.compress
		if not L8_74 then
			goto lbl_49
		end
	end
	L8_74 = EncodeData
	L8_74 = L8_74(A1_67, A2_68.crc, A2_68.compress)
	A1_67 = L8_74
	::lbl_49::
	L8_74 = A2_68.passphrase
	if L8_74 then
		L8_74 = SaveDataToFile
		L8_74 = L8_74(A1_67, A0_66, A2_68.passphrase)
		repeat
			A1_67 = L8_74
			do break end -- pseudo-goto
			L8_74 = SaveDataToFile
			L8_74 = L8_74(A1_67, A0_66)
			A1_67 = L8_74
		until true
	end
	L8_74 = setmetatable
	local L5_71 = L5_71
	L8_74(L5_71, nil)
	local L6_72 = L6_72
	return A1_67
end
SaveLUAData = L7_7
function L7_7(A0_75, A1_76, A2_77, A3_78)
	A0_75 = _ENV(A0_75)
	if string.char(string.byte(A0_75, 2, 2)) == ":" then
		return
	end
	if type(A2_77) ~= "table" then
		({}).indent = A2_77
		A2_77, ({}).crc = {}, A3_78
	end
	setmetatable(A2_77, L6_6)
	A1_76 = "return " .. var2str(A1_76, A2_77.indent, nil)
	local L7_82 = L7_82
	local L8_83 = L8_83
	local L9_84 = L9_84
	L7_82(L8_83, L9_84, nil, A2_77.passphrase, A2_77.crc, A2_77.compress)
	local L10_85 = L10_85
	L7_82 = setmetatable
	L8_83 = A2_77
	L9_84 = nil
	L7_82(L8_83, L9_84)
end
AsyncSaveLuaData = L7_7
function L5_5(A0_86, A1_87)
	local L2_88
	L2_88 = A0_86
	L3_89, L4_90, L5_91 = L3_89(L4_90, "[^%.]+")
	for _FORV_6_ in L3_89, L4_90, L5_91 do
		if L2_88 then
			local L8_93 = type(L2_88)
			if L8_93 == "table" then
				L2_88 = L2_88[_FORV_6_]
		end
		else
			L2_88 = nil
			break
		end
	end
	return L2_88
end
L6_6 = nil
L7_7 = nil
L8_8 = nil
L9_9 = nil
function L10_10()
	local L0_94
	L4_98 = string.gmatch
	L5_99 = _ENV
	L6_100 = "[^%.]+"
	L4_98, L5_99, L6_100 = L4_98(L5_99, L6_100)
	for _FORV_4_ in L4_98, L5_99, L6_100 do
		if L0_94 then
			if not L7_7[L0_94] then
				L7_7[L0_94] = {}
			end
			L7_7 = L7_7[L0_94]
		end
		L0_94 = _FORV_4_
	end
	L4_98 = L7_7
	L5_99 = L9_9
	L4_98[L0_94] = L5_99
	L4_98 = true
	return L4_98
end
function L11_11(A0_101)
	local L1_102, L2_103, L3_104
	L1_102 = "SetVariable"
	L2_103 = _ENV
	L3_104 = L7_7
	L3_104 = L3_104 == GetAddonEnv()
	local L4_105 = L4_105
	local L5_106 = L5_106
	local L7_108 = L7_108
	local L9_110 = L9_110
	if not xpAddon or not " @addon" then
	end
	local L10_111 = L10_111
	local L11_112 = L11_112
	local L12_113 = L12_113
	local L13_114 = L13_114
	local L14_115 = L14_115
	local L7_108, L15_116 = L7_108 .. L9_110 .. L10_111 .. L11_112 .. L12_113 .. L13_114 .. L14_115 .. "\n" .. debug.traceback() .. "\n", L15_116
	L4_105(L5_106, L7_108)
	L4_105 = LogError
	L5_106 = string
	L5_106 = L5_106.format
	L7_108 = [[
%s
%s]]
	L9_110 = A0_101
	L10_111 = debug
	L10_111 = L10_111.traceback
	L10_111, L11_112, L12_113, L13_114, L14_115, L15_116 = L10_111()
	L5_106, L7_108, L9_110, L10_111, L11_112, L12_113, L13_114, L14_115, L15_116 = L5_106(L7_108, L9_110, L10_111, L11_112, L12_113, L13_114, L14_115, L15_116, L10_111())
	L4_105(L5_106, L7_108, L9_110, L10_111, L11_112, L12_113, L13_114, L14_115, L15_116, L5_106(L7_108, L9_110, L10_111, L11_112, L12_113, L13_114, L14_115, L15_116, L10_111()))
end
function L6_6(A0_117, A1_118, A2_119)
	_ENV, L8_8, L9_9 = A0_117, A1_118, A2_119
	local L4_120 = L4_120
	do return L4_120(L10_10, L11_11) end
	local L5_121 = L5_121
end
function L7_7(A0_122, A1_123, A2_124)
	local L4_125 = L4_125
	local L5_126 = L5_126
	L4_125(L5_126, A1_123, A2_124)
	local L6_127 = L6_127
end
L8_8 = nil
L9_9 = nil
L10_10 = nil
L11_11 = nil
L12_12 = nil
function L13_13()
	local L0_128, L1_129
	L0_128 = _ENV
	if not L0_128 then
		return
	end
	L0_128 = nil
	L1_129 = true
	L5_133 = string
	L5_133 = L5_133.gmatch
	L6_134 = L10_10
	L7_135 = "[^%.]+"
	L5_133, L6_134, L7_135 = L5_133(L6_134, L7_135)
	for L8_136 in L5_133, L6_134, L7_135 do
		if L0_128 then
			if not L9_9[L0_128] then
				L9_9[L0_128] = {}
			end
			L9_9 = L9_9[L0_128]
		end
		if not L0_128 and not L9_9[L8_136] then
			if not _ENV[L8_136] then
				_ENV[L8_136] = {}
			end
			_ENV[L10_10] = L11_11
			return
		end
		L0_128 = L8_136
	end
	L5_133 = L9_9
	L6_134 = L11_11
	L5_133[L0_128] = L6_134
	L5_133 = true
	return L5_133
end
function L14_14(A0_137)
	local L1_138, L2_139, L3_140
	L1_138 = "Module_SetVariable"
	L2_139 = _ENV
	L3_140 = L9_9
	L3_140 = L3_140 == GetAddonEnv()
	local L4_141 = L4_141
	local L5_142 = L5_142
	local L7_144 = L7_144
	local L9_146 = L9_146
	if not xpAddon or not " @addon" then
	end
	local L10_147 = L10_147
	local L11_148 = L11_148
	local L12_149 = L12_149
	local L13_150 = L13_150
	local L14_151 = L14_151
	local L7_144, L15_152 = L7_144 .. L9_146 .. L10_147 .. L11_148 .. L12_149 .. L13_150 .. L14_151 .. "\n" .. debug.traceback() .. "\n", L15_152
	L4_141(L5_142, L7_144)
	L4_141 = LogError
	L5_142 = string
	L5_142 = L5_142.format
	L7_144 = [[
%s
%s]]
	L9_146 = A0_137
	L10_147 = debug
	L10_147 = L10_147.traceback
	L10_147, L11_148, L12_149, L13_150, L14_151, L15_152 = L10_147()
	L5_142, L7_144, L9_146, L10_147, L11_148, L12_149, L13_150, L14_151, L15_152 = L5_142(L7_144, L9_146, L10_147, L11_148, L12_149, L13_150, L14_151, L15_152, L10_147())
	L4_141(L5_142, L7_144, L9_146, L10_147, L11_148, L12_149, L13_150, L14_151, L15_152, L5_142(L7_144, L9_146, L10_147, L11_148, L12_149, L13_150, L14_151, L15_152, L10_147()))
end
function L8_8(A0_153, A1_154, A2_155, A3_156)
	_ENV, L10_10, L11_11, L12_12 = A0_153, A1_154, A2_155, A3_156
	local L5_157 = L5_157
	do return L5_157(L13_13, L14_14) end
	local L6_158 = L6_158
end
function L9_9(A0_159, A1_160, A2_161, A3_162)
	local L5_163 = L5_163
	local L6_164 = L6_164
	local L7_165 = L7_165
	L5_163(L6_164, L7_165, A2_161, A3_162)
	local L8_166 = L8_166
end
function L10_10(A0_167, A1_168)
	local L2_169 = L2_169
	local L3_170 = L3_170
	L2_169 = L2_169(L3_170, "%.")
	if not L2_169 then
		L2_169 = true
		return L2_169
	else
		L2_169 = _ENV
		L3_170 = A0_167
		local L4_171, L5_172 = L4_171, L5_172
		local L6_173 = L6_173
		local L4_171, L5_172, L6_173, L7_174 = L4_171(L5_172, L6_173, "")
		L2_169 = L2_169(L3_170, L4_171, L5_172, L6_173, L7_174)
		L2_169 = L2_169 ~= nil
		return L2_169
	end
end
function L11_11()
	local L0_175, L1_176
	L0_175 = select
	L1_176 = 3
	L0_175, L1_176 = L0_175(L1_176, GetUserServer())
	local L2_177 = L2_177
	local L3_178 = L3_178
	local L2_177, L4_179 = L2_177 .. L3_178 .. L1_176, L4_179
	return L2_177
end
function L12_12()
	local L0_180, L1_181
	L0_180 = select
	L1_181 = 3
	local L2_182 = GetUserServer()
	L0_180 = L0_180(L1_181, L2_182)
	return L0_180
end
function L13_13()
	local L0_183, L1_184
	L0_183 = select
	L1_184 = 5
	L0_183, L1_184 = L0_183(L1_184, GetUserServer())
	local L2_185 = L2_185
	local L3_186 = L3_186
	local L2_185, L4_187 = L2_185 .. L3_186 .. L1_184, L4_187
	return L2_185
end
function L14_14()
	local L0_188, L1_189
	L0_188 = select
	L1_189 = 5
	local L2_190 = GetUserServer()
	L0_188 = L0_188(L1_189, L2_190)
	return L0_188
end
function L15_15(A0_191, A1_192)
	if IsLocalFileExist(A0_191) then
		CPath.DelFile(A1_192)
		CPath.Move(A0_191, A1_192)
	end
	if IsLocalFileExist(A0_191 .. ".addon") then
		CPath.DelFile(A1_192 .. ".addon")
		local L3_193 = L3_193
		local L5_195 = L5_195
		L3_193(L5_195, A1_192 .. ".addon")
	end
end
L16_16 = {}
function L17_17()
	local L0_196 = L0_196
	local L0_196, L1_197 = L0_196 .. "\\custom.dat", L1_197
	return L0_196
end
L16_16.Global = L17_17
function L17_17()
	local L0_198 = L0_198
	local L0_198, L1_199 = L0_198 .. "\\config.dat", L1_199
	return L0_198
end
L16_16.LoginGlobal = L17_17
function L17_17()
	local L0_200 = L0_200
	local L0_200, L1_201 = L0_200 .. "\\customEnter.dat", L1_201
	return L0_200
end
L16_16.EnterGlobal = L17_17
function L17_17()
	local L0_202 = L0_202
	local L1_203 = L1_203
	local L2_204 = L2_204
	local L0_202, L3_205 = L0_202 .. L1_203 .. L2_204 .. "\\config.dat", L3_205
	return L0_202
end
L16_16.LoginAccount = L17_17
function L17_17()
	if SM_IsEnable() then
		return GetUserDataFolder() .. "\\" .. SM_GetAccountName() .. "\\custom.dat"
	else
		local L0_206 = L0_206
		local L1_207 = L1_207
		local L2_208 = L2_208
		local L0_206, L3_209 = L0_206 .. L1_207 .. L2_208 .. "\\custom.dat", L3_209
		return L0_206
	end
end
L16_16.Account = L17_17
function L17_17()
	local L0_210 = L0_210
	L0_210 = L0_210 .. "\\" .. GetUserAccount() .. "\\" .. _ENV() .. "\\config.dat"
	local L1_211 = L1_211
	local L5_215 = L5_215
	local L1_211, L6_216 = L1_211 .. L5_215 .. GetUserAccount() .. "\\" .. L14_14() .. "\\custom.dat", L6_216
	L5_215 = L15_15
	L6_216 = L0_210
	L5_215(L6_216, L1_211)
	local L4_214 = L4_214
	return L1_211
end
L16_16.Region = L17_17
function L17_17()
	local L0_217 = L0_217
	L0_217 = L0_217 .. "\\" .. GetUserAccount() .. "\\" .. _ENV() .. "\\config.dat"
	local L1_218 = L1_218
	local L5_222 = L5_222
	local L1_218, L6_223 = L1_218 .. L5_222 .. GetUserAccount() .. "\\" .. L13_13() .. "\\custom.dat", L6_223
	L5_222 = L15_15
	L6_223 = L0_217
	L5_222(L6_223, L1_218)
	local L4_221 = L4_221
	return L1_218
end
L16_16.Server = L17_17
function L17_17()
	local L0_224 = L0_224
	L0_224 = L0_224 .. "\\" .. GetUserAccount() .. "\\" .. _ENV() .. "_" .. GetUserRoleName() .. ".dat"
	local L1_225 = L1_225
	local L5_229 = L5_229
	local L6_230 = L6_230
	local L7_231 = L7_231
	local L1_225, L8_232 = L1_225 .. L5_229 .. L6_230 .. L7_231 .. L13_13() .. "\\" .. GetUserRoleName() .. "\\custom.dat", L8_232
	L5_229 = L15_15
	L6_230 = L0_224
	L7_231 = L1_225
	L5_229(L6_230, L7_231)
	return L1_225
end
L16_16.Role = L17_17
function L17_17(A0_233)
	if _ENV[A0_233] then
		return _ENV[A0_233]()
	end
end
L18_18 = {}
L21_21 = pairs
L22_22 = L16_16
L21_21, L22_22, _FOR_ = L21_21(L22_22)
for _FORV_22_, _FORV_23_ in L21_21, L22_22, _FOR_ do
	({}).addons = {}
	;({}).addons_status = "initialized"
	;({}).inside = {}
	L18_18[_FORV_22_], ({}).inside_status = {}, "initialized"
end
function L21_21(A0_234, A1_235, A2_236)
	local L6_240 = string.find
	local L4_238 = L4_238
	local L6_240, L4_238, L5_239 = L6_240(L4_238, "(%a+)[/\\](.+)")
	if not L5_239 or not L6_240(L4_238, "(%a+)[/\\](.+)") then
		L5_239 = "Role"
	end
	if not _ENV[L5_239] then
		L5_239 = "Role"
	end
	local L7_241 = L7_241
	A1_235 = tonumber(A1_235)
	local L8_242 = L8_242
	if AddonMgr.GetLoadingAddon() then
		({}).version = A1_235
		if not AddonMgr.GetLoadingAddon().szID then
		end
		L8_242.addons[L7_241], ({}).addonid = {}, g_tStrings.tDataSave.UNKOWN_ADDON
	elseif A2_236 then
		({}).version = A1_235
		L8_242.addons[L7_241], ({}).addonid = {}, g_tStrings.tDataSave.UNKOWN_ADDON
	else
		if L3_3[L7_241] then
			local L9_243 = L9_243
			local L10_244 = L10_244
			local L11_245 = L11_245
			local L12_246 = L12_246
			L10_244(L11_245, L12_246, L3_3[L7_241], L3_3)
			local L13_247 = L13_247
			L10_244 = L3_3
			L10_244[L7_241] = nil
		end
		L10_244 = L8_242.inside
		L11_245 = {}
		L11_245.version = A1_235
		L10_244[L7_241] = L11_245
	end
end
RegisterCustomData = L21_21
function L21_21(A0_248, A1_249)
	repeat
		if A1_249 then
			L5_253 = pairs
			L6_254 = _ENV
			L5_253, L6_254, _FOR_ = L5_253(L6_254)
			for _FORV_5_, _FORV_6_ in L5_253, L6_254, _FOR_ do
				if _FORV_6_.addons[A0_248] then
					return true
				end
			end
			break -- pseudo-goto
		end
		L5_253 = pairs
		L6_254 = _ENV
		L5_253, L6_254, L7_255 = L5_253(L6_254)
		for _FORV_5_, _FORV_6_ in L5_253, L6_254, L7_255 do
			if _FORV_6_.inside[A0_248] then
				return true
			end
		end
	until true
end
IsRegisterCustomData = L21_21
function L21_21(A0_256, A1_257, A2_258)
	local L3_259 = L3_259
	L3_259 = L3_259(A2_258)
	if L3_259 then
		return
	end
	L3_259 = {}
	L3_259.version = _ENV
	if A0_256 == GetAddonEnv() then
		if not LoadLUAData(A1_257, false, true) then
		end
		_FOR_, _FOR_, _FOR_ = ipairs({})
		for _FORV_7_, _FORV_8_ in _FOR_, _FOR_, _FOR_ do
			if _FORV_8_.k and not A2_258[_FORV_8_.k] and not L10_10(A0_256, _FORV_8_.k) then
				L1_1(L3_259, _FORV_8_)
			end
		end
	else
		if not LoadLUAData(A1_257, false, true) then
		end
		L8_264, _FOR_, _FOR_ = L8_264({})
		for _FORV_7_, _FORV_8_ in L8_264, _FOR_, _FOR_ do
			if _FORV_8_.k and not A2_258[_FORV_8_.k] then
				L1_1(L3_259, _FORV_8_)
			end
		end
	end
	L8_264 = pairs
	L8_264, _FOR_, _FOR_ = L8_264(A2_258)
	for _FORV_7_, _FORV_8_ in L8_264, _FOR_, _FOR_ do
		if not _FORV_8_.version or _FORV_8_.version >= 0 then
			({}).k = _FORV_7_
			;({}).v = L5_5(A0_256, _FORV_7_)
			;({}).n = _FORV_8_.version
			L1_1(L3_259, {})
		end
	end
	L8_264 = AsyncSaveLuaData
	L9_265 = A1_257
	L10_266 = L3_259
	L13_269 = IsDebugClient
	L13_269 = L13_269()
	if L13_269 then
		L13_269 = "\t"
	end
	L8_264(L9_265, L10_266, L13_269)
end
function L22_22(A0_271, A1_272, A2_273)
	local L3_274 = L3_274
	local L3_274, L4_275 = L3_274(A0_271), L4_275
	L4_275 = L18_18
	L4_275 = L4_275[A0_271]
	if not L3_274 then
		Log("[Custom Data]" .. A0_271 .. " file path can't be empty!")
	elseif not L4_275 then
		Log("[Custom Data]" .. A0_271 .. " data is empty, save skipped!")
	else
		if A1_272 ~= false then
			if L4_275.inside_status == "initialized" then
				Log(("[Custom Data]%-12s(inside) hasn't been loaded yet, save skipped!"):format(A0_271))
			elseif empty(L4_275.inside) then
				Log(("[Custom Data]%-12s(inside) variable list is empty, save skipped!"):format(A0_271))
			else
				L19_19(_G, L3_274, L4_275.inside)
				Log(("[Custom Data]%-12s(inside) saved in %dms: %s"):format(A0_271, GetTickCount() - GetTickCount(), L3_274))
				L4_275.inside_status = "saved"
			end
		end
		if A2_273 ~= false then
			if L4_275.addons_status == "initialized" then
				Log(("[Custom Data]%-12s(addons) hasn't been loaded yet, save skipped!"):format(A0_271))
			elseif empty(L4_275.addons) then
				Log(("[Custom Data]%-12s(addons) variable list is empty, save skipped!"):format(A0_271))
			else
				local L5_276 = GetTickCount()
				local L6_277 = L6_277
				L6_277(GetAddonEnv(), L3_274 .. ".addon", L4_275.addons)
				L6_277 = Log
				local L7_278, L8_279 = L7_278, L8_279
				local L9_280 = L9_280
				local L10_281 = L10_281
				local L7_278, L8_279, L9_280, L10_281, L11_282 = L7_278(L8_279, L9_280, L10_281, L3_274)
				L6_277(L7_278, L8_279, L9_280, L10_281, L11_282)
				L4_275.addons_status = "saved"
			end
		end
	end
end
RegisterEvent("GAME_START", function()
	local L1_283 = L1_283
	local L2_284 = L2_284
	L1_283(L2_284, true, false)
	local L3_285 = L3_285
end)
RegisterEvent("GAME_START", function()
	local L1_286 = L1_286
	local L2_287 = L2_287
	L1_286(L2_287, true, false)
	local L3_288 = L3_288
end)
RegisterEvent("ACCOUNT_LOGIN", function()
	local L1_289 = L1_289
	local L2_290 = L2_290
	L1_289(L2_290, true, false)
	local L3_291 = L3_291
end)
RegisterEvent("ACCOUNT_LOGIN", function()
	local L1_292 = L1_292
	local L2_293 = L2_293
	L1_292(L2_293, true, false)
	local L3_294 = L3_294
end)
RegisterEvent("ACCOUNT_LOGOUT", function()
	local L1_295 = L1_295
	local L2_296 = L2_296
	L1_295(L2_296, true, false)
	local L3_297 = L3_297
end)
RegisterEvent("PLAYER_ENTER_GAME", function()
	local L1_298 = L1_298
	local L2_299 = L2_299
	L1_298(L2_299, true, false)
	local L3_300 = L3_300
end)
RegisterEvent("PLAYER_ENTER_GAME", function()
	local L1_301 = L1_301
	local L2_302 = L2_302
	L1_301(L2_302, true, false)
	local L3_303 = L3_303
end)
if SM_IsEnable() then
	RegisterEvent("LOAD_ACCOUNT_CUSTOM_DATA", function()
		local L1_304 = L1_304
		local L2_305 = L2_305
		L1_304(L2_305, true, false)
		local L3_306 = L3_306
	end)
else
	RegisterEvent("PLAYER_ENTER_GAME", function()
		local L1_307 = L1_307
		local L2_308 = L2_308
		L1_307(L2_308, true, false)
		local L3_309 = L3_309
	end)
end
RegisterEvent("PLAYER_ENTER_GAME", function()
	local L1_310 = L1_310
	local L2_311 = L2_311
	L1_310(L2_311, true, false)
	local L3_312 = L3_312
end)
RegisterEvent("PLAYER_ENTER_GAME", function()
	local L1_313 = L1_313
	local L2_314 = L2_314
	L1_313(L2_314, true, false)
	local L3_315 = L3_315
end)
local L23_23 = L23_23
RegisterEvent("PLAYER_ENTER_GAME", function()
	local L1_316 = L1_316
	local L2_317 = L2_317
	L1_316(L2_317, true, false)
	local L3_318 = L3_318
end)
RegisterEvent("GAME_EXIT", function()
	L3_322 = "LEAVE_GAME"
	L2_321(L3_322)
	L2_321 = pairs
	L3_322 = _ENV
	L2_321, L3_322, _FOR_ = L2_321(L3_322)
	for _FORV_3_, _FORV_4_ in L2_321, L3_322, _FOR_ do
		L20_20(_FORV_3_, true, false)
		local L8_326 = L8_326
	end
end)
local L24_24 = L24_24
RegisterEvent("UI_LUA_RESET", function()
	L3_330 = "LEAVE_GAME"
	L2_329(L3_330)
	L2_329 = pairs
	L3_330 = _ENV
	L2_329, L3_330, _FOR_ = L2_329(L3_330)
	for _FORV_3_, _FORV_4_ in L2_329, L3_330, _FOR_ do
		L20_20(_FORV_3_, true, false)
		local L8_334 = L8_334
	end
end)
RegisterEvent("PLAYER_ENTER_GAME", function()
	L2_337 = _ENV
	L0_335, L2_337, L3_338 = L0_335(L2_337)
	for _FORV_3_, _FORV_4_ in L0_335, L2_337, L3_338 do
		local L7_341 = L7_341
		L7_341(_FORV_3_, false, true)
		local L8_342 = L8_342
	end
end)
local L25_25 = L25_25
RegisterEvent("RELOAD_UI_ADDON_PLAYER_ENTER_GAME", function()
	L2_345 = _ENV
	L0_343, L2_345, L3_346 = L0_343(L2_345)
	for _FORV_3_, _FORV_4_ in L0_343, L2_345, L3_346 do
		local L7_349 = L7_349
		L7_349(_FORV_3_, false, true)
		local L8_350 = L8_350
	end
end)
RegisterEvent("GAME_EXIT", function()
	L2_353 = _ENV
	L0_351, L2_353, L3_354 = L0_351(L2_353)
	for L4_355, _FORV_4_ in L0_351, L2_353, L3_354 do
		local L6_357 = L6_357
		local L7_358 = L7_358
		L6_357(L7_358, false, true)
		local L8_359 = L8_359
		L6_357 = {}
		_FORV_4_.addons = L6_357
		_FORV_4_.addons_status = "initialized"
	end
end)
RegisterEvent("PLAYER_EXIT_GAME", function()
	L2_362 = _ENV
	L0_360, L2_362, L3_363 = L0_360(L2_362)
	for L4_364, _FORV_4_ in L0_360, L2_362, L3_363 do
		local L6_366 = L6_366
		local L7_367 = L7_367
		L6_366(L7_367, false, true)
		local L8_368 = L8_368
		L6_366 = {}
		_FORV_4_.addons = L6_366
		_FORV_4_.addons_status = "initialized"
	end
end)
local L26_26 = L26_26
local L27_27 = L27_27
RegisterEvent("RELOAD_UI_ADDON_BEGIN", function()
	L2_371 = _ENV
	L0_369, L2_371, L3_372 = L0_369(L2_371)
	for L4_373, _FORV_4_ in L0_369, L2_371, L3_372 do
		local L6_375 = L6_375
		local L7_376 = L7_376
		L6_375(L7_376, false, true)
		local L8_377 = L8_377
		L6_375 = {}
		_FORV_4_.addons = L6_375
		_FORV_4_.addons_status = "initialized"
	end
end)
local L28_28 = L28_28
