local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L19_19, L20_20
L0_0 = {}
local L1_1, L38_38, L39_39, L40_40, L41_41, L42_42, L43_43, L44_44, L45_45, L46_46, L47_47, L48_48, L49_49, L50_50, L51_51, L52_52, L53_53, L54_54, L55_55, L56_56, L57_57, L58_58, L59_59, L60_60, L61_61, L62_62, L63_63, L64_64 = {}, L38_38, L39_39, L40_40, L41_41, L42_42, L43_43, L44_44, L45_45, L46_46, L47_47, L48_48, L49_49, L50_50, L51_51, L52_52, L53_53, L54_54, L55_55, L56_56, L57_57, L58_58, L59_59, L60_60, L61_61, L62_62, L63_63, L64_64
L2_2 = {}
L3_3 = false
L4_4 = true
L5_5 = 2
L6_6 = true
L7_7 = true
L8_8 = 0
L9_9 = 31
L10_10 = 61
L11_11 = 101
L12_12 = 100
L13_13 = 47
L14_14 = 177
L15_15 = 49
L16_16 = 106
L17_17 = 0
L18_18 = 7.421
L19_19 = {}
L19_19.TRADITION = 0
L19_19.MOUSE = 1
L19_19.TARGET = 2
L20_20 = {}
L20_20[36274] = true
L23_23 = 1000
ALT_KEY_VALID_INTERVAL = L23_23
L23_23 = false
g_bMoreSkillInfo = L23_23
L23_23 = RegisterCustomData
L24_24 = "g_bMoreSkillInfo"
L23_23(L24_24)
L23_23 = pairs
L24_24 = g_tUIConfig
L24_24 = L24_24.SkillQiChang
L23_23, L24_24, L25_25 = L23_23(L24_24)
for L28_28, L29_29 in L23_23, L24_24, L25_25 do
	L30_30 = type
	L31_31 = L29_29.ids
	L30_30 = L30_30(L31_31)
	if L30_30 == "table" then
		L30_30 = ipairs
		L31_31 = L29_29.ids
		L30_30, L31_31, L32_32 = L30_30(L31_31)
		repeat
			for L33_33, L37_37 in L30_30, L31_31, L32_32 do
				L1_1[L37_37] = true
			end
			do break end -- pseudo-goto
			L30_30 = L29_29.ids
			L1_1[L30_30] = true
		until true
	end
end
L23_23 = {}
L23_23.SKILL = "skill"
L23_23.BUFF = "buff"
L23_23.NOUN = "noun"
DESC_CONTEXT_SOURCE = L23_23
L23_23 = {}
L23_23.PLAIN = "plain"
L23_23.GLOSSARY = "glossary"
DESC_CONTEXT_MODE = L23_23
function L23_23(A0_68)
	if not A0_68 then
		return nil
	end
	local L1_69 = L1_69
	do return L1_69(A0_68) end
	local L2_70 = L2_70
end
CloneDescContext = L23_23
function L23_23(A0_71, A1_72, A2_73, A3_74, A4_75, A5_76, A6_77, A7_78)
	local L8_79 = L8_79
	L8_79 = L8_79(A0_71)
	if not L8_79 then
		L8_79 = {}
	end
	if not L8_79.sourceType and not A1_72 then
	end
	L8_79.sourceType = DESC_CONTEXT_SOURCE.SKILL
	if not L8_79.displayMode and not A2_73 then
	end
	L8_79.displayMode = DESC_CONTEXT_MODE.GLOSSARY
	if not L8_79.player and not A3_74 then
		local L9_80 = GetClientPlayer()
	end
	L8_79.player = L9_80
	if A4_75 ~= nil then
		L9_80 = L8_79.ownerID
		if L9_80 == nil then
			L8_79.ownerID = A4_75
		end
	end
	if A5_76 ~= nil then
		L9_80 = L8_79.ownerLevel
		if L9_80 == nil then
			L8_79.ownerLevel = A5_76
		end
	end
	if A6_77 ~= nil then
		L9_80 = L8_79.skillkey
		if L9_80 == nil then
			L8_79.skillkey = A6_77
		end
	end
	if A7_78 ~= nil then
		L9_80 = L8_79.skillInfo
		if L9_80 == nil then
			L8_79.skillInfo = A7_78
		end
	end
	return L8_79
end
EnsureDescContext = L23_23
function L23_23(A0_81)
	local L1_82, L2_83
	L1_82 = A0_81 or nil
	if A0_81 then
		L1_82 = A0_81.sourceType
		L2_83 = DESC_CONTEXT_SOURCE
		L2_83 = L2_83.BUFF
		L1_82 = L1_82 == L2_83
	end
	return L1_82
end
IsBuffPlainDescContext = L23_23
function L23_23(A0_84, A1_85)
	A0_84 = tonumber(A0_84) or A0_84
	if not tonumber(A0_84) then
		A0_84 = 0
	end
	if A0_84 == 0 then
		local L2_86 = L2_86
		local L2_86, L3_87 = L2_86(A1_85), L3_87
		if not L2_86 then
			L2_86 = 0
		end
		return L2_86
	end
	return A0_84
end
ResolveDescLevel = L23_23
function L23_23(A0_88)
	local L1_89 = L1_89
	if not A0_88 then
	end
	local L2_90 = L2_90
	local L1_89, L3_91 = L1_89 .. L2_90 .. "\161\177", L3_91
	return L1_89
end
GetQuotedGlossaryName = L23_23
function L23_23(A0_92, A1_93, A2_94, A3_95, A4_96, A5_97)
	local L7_98 = L7_98
	local L8_99 = L8_99
	local L9_100 = L9_100
	local L10_101 = L10_101
	local L11_102 = L11_102
	local L12_103 = L12_103
	local L13_104 = L13_104
	do return L7_98(L8_99, L9_100, L10_101, L11_102, L12_103, L13_104, A2_94, A3_95) end
	local L14_105 = L14_105
end
CreateSkillDescContext = L23_23
function L23_23(A0_106, A1_107, A2_108, A3_109)
	local L4_110
	L4_110 = DESC_CONTEXT_MODE
	L4_110 = L4_110.PLAIN
	if A3_109 and A3_109.displayMode then
		L4_110 = A3_109.displayMode
	end
	local L5_111 = L5_111
	local L6_112 = L6_112
	local L7_113 = L7_113
	local L8_114 = L8_114
	local L9_115 = L9_115
	local L10_116 = L10_116
	do return L5_111(L6_112, L7_113, L8_114, L9_115, L10_116, A1_107) end
	local L11_117 = L11_117
end
CreateBuffDescContext = L23_23
function L23_23(A0_118, A1_119, A2_120, A3_121, A4_122)
	if not A3_121 then
		A3_121 = GetClientPlayer()
	end
	if not A3_121 then
		return ""
	end
	if A4_122 then
	end
	A1_119 = ResolveDescLevel(A1_119, A4_122.ownerLevel)
	local L5_123 = L5_123
	L5_123 = L5_123(A0_118, A1_119)
	local L6_124 = L6_124
	L6_124 = L6_124(L5_123, A3_121.dwID)
	local L7_125 = L7_125
	L7_125 = L7_125(A0_118, A1_119, L5_123, L6_124, A3_121, A4_122)
	local L8_126 = L8_126
	local L9_127 = L9_127
	local L10_128 = L10_128
	local L11_129 = L11_129
	local L12_130 = L12_130
	local L13_131 = L13_131
	local L14_132 = L14_132
	do return L8_126(L9_127, L10_128, L11_129, L12_130, L13_131, L14_132, L7_125) end
	local L15_133 = L15_133
end
GetSubSkillDesc = L23_23
function L23_23(A0_134, A1_135, A2_136, A3_137, A4_138)
	local L8_142, L9_143 = L8_142, L9_143
	if not A3_137 then
		L8_142 = GetClientPlayer
		L8_142 = L8_142()
		A3_137 = L8_142
	end
	L8_142 = string
	L8_142 = L8_142.match
	L9_143 = A0_134
	local L8_142, L9_143, L7_141 = L8_142(L9_143, "(%d+)?(%d+)_(%d+);(%d+)_(%d+)")
	L8_142 = tonumber(L8_142)
	local L10_144 = L10_144
	local L11_145 = L11_145
	local L12_146 = L12_146
	if 0 < L12_146 then
	end
	if tonumber(L7_141) == 0 then
	end
	local L13_147 = L13_147
	local L14_148 = L14_148
	local L15_149 = L15_149
	local L16_150 = L16_150
	local L17_151 = L17_151
	do return L15_149(L16_150, L17_151, A2_136, A3_137, A4_138) end
	local L18_152 = L18_152
end
GetRadioSubSkillDesc = L23_23
function L23_23(A0_153, A1_154)
	local L2_155
	if not A1_154 then
		L2_155 = {}
		A1_154 = L2_155
	end
	L2_155 = A1_154.tDescCtx
	if IsBuffPlainDescContext(L2_155) then
		return ""
	end
	A0_153 = tonumber(A0_153)
	if not A0_153 then
		return ""
	end
	local L3_156 = L3_156
	local L3_156, L4_157 = L3_156(A0_153), L4_157
	if not L3_156 then
		L3_156 = {}
	end
	L4_157 = L3_156.szDesc
	if not L4_157 then
		L4_157 = ""
	end
	if L4_157 == "" then
		return ""
	end
	if L2_155 and L2_155.sourceType == DESC_CONTEXT_SOURCE.BUFF and ParseBuffDesc then
		if not L2_155.ownerID then
		end
		if not L2_155.ownerLevel then
		end
		return ParseBuffDesc(L4_157, A1_154.dwSkillID, A1_154.dwSkillLevel, L2_155)
	end
	local L5_158 = L5_158
	local L6_159 = L6_159
	local L7_160 = L7_160
	local L8_161 = L8_161
	local L9_162 = L9_162
	return L5_158(L6_159, L7_160, L8_161, L9_162, A1_154.skillInfo, A1_154.bShort, A1_154.player, L2_155)
end
ParseNounDescText = L23_23
function L23_23(A0_163)
	local L1_164
	L1_164 = _ENV
	L1_164 = L1_164[A0_163]
	return L1_164
end
IsQiChangSkill = L23_23
function L23_23(A0_165, A1_166, A2_167)
	local L3_168
	L3_168 = 1
	if A2_167 then
		L3_168 = 0
	end
	L7_172 = A1_166.tRepresentID
	if L7_172 then
		L7_172 = ipairs
		L8_173 = A1_166.tRepresentID
		L7_172, L8_173, _FOR_ = L7_172(L8_173)
		for _FORV_7_, _FORV_8_ in L7_172, L8_173, _FOR_ do
			rlcmd("set gas field " .. A0_165 .. " " .. _FORV_8_ .. " " .. L3_168)
		end
	end
	L7_172 = A1_166.tSFXID
	if not L7_172 then
		return
	end
	L7_172 = ipairs
	L8_173 = A1_166.tSFXID
	L7_172, L8_173, _FOR_ = L7_172(L8_173)
	for _FORV_7_, _FORV_8_ in L7_172, L8_173, _FOR_ do
		local L13_178 = L13_178
		local L14_179 = L14_179
		local L14_179, L15_180 = L14_179 .. A0_165 .. " " .. _FORV_8_ .. " " .. L3_168, L15_180
		L13_178(L14_179)
	end
end
SetGasFieldRepresentVisible = L23_23
function L23_23(A0_181, A1_182)
	repeat
		L5_186 = A0_181
		L4_185 = L4_185(L5_186)
		if L4_185 == "table" then
			L4_185 = pairs
			L5_186 = A0_181
			L4_185, L5_186, L6_187 = L4_185(L5_186)
			for L7_188, _FORV_6_ in L4_185, L5_186, L6_187 do
				_ENV[_FORV_6_] = A1_182
			end
			break -- pseudo-goto
		end
		L4_185 = _ENV
		L4_185[A0_181] = A1_182
	until true
end
SetSkillCastToMe = L23_23
function L23_23(A0_189)
	local L1_190
	L1_190 = _ENV
	L1_190 = L1_190[A0_189]
	if L1_190 == 101 then
		L1_190 = true
		return L1_190
	end
end
function L24_24(A0_191)
	local L1_192 = L1_192
	local L1_192, L2_193 = L1_192(A0_191), L2_193
	if L1_192 then
		L1_192 = true
		return L1_192
	end
	L1_192 = L2_2
	L1_192 = L1_192[A0_191]
	if L1_192 then
		L1_192 = false
		return L1_192
	end
	L1_192 = L0_0
	L1_192 = L1_192[A0_191]
	return L1_192
end
IsSkillCastToMe = L24_24
function L24_24()
	local L1_194
	L1_194 = _ENV
	return L1_194
end
IsSelfCastSkill = L24_24
function L24_24(A0_195)
	local L1_196
	_ENV = A0_195
end
SetSelfCastSkill = L24_24
function L24_24()
	local L1_197
	L1_197 = _ENV
	return L1_197
end
GetPointAreaCastSetting = L24_24
function L24_24(A0_198)
	local L1_199
	_ENV = A0_198
end
SetPointAreaCastSetting = L24_24
function L24_24()
	local L1_200
	L1_200 = _ENV
	return L1_200
end
IsShowGCDBar = L24_24
function L24_24(A0_201)
	local L1_202
	_ENV = A0_201
end
SetShowGCDBar = L24_24
function L24_24()
	local L1_203
	L1_203 = _ENV
	return L1_203
end
IsAutoTarget = L24_24
function L24_24(A0_204)
	local L1_205
	_ENV = A0_204
end
SetAutoTarget = L24_24
function L24_24(A0_206, A1_207)
	if A0_206 == 0 then
		return false
	end
	if not A1_207 then
		A1_207 = 1
	end
	local L2_208 = L2_208
	local L3_209 = L3_209
	local L2_208, L4_210 = L2_208(L3_209, A1_207), L4_210
	if L2_208 then
		L3_209 = L2_208.nPlatformType
		L4_210 = SKILL_PLATFORM_TYPE
		L4_210 = L4_210.MOBILE
		if L3_209 == L4_210 then
			L3_209 = true
			return L3_209
		end
	end
end
IsMobileSkill = L24_24
function L24_24()
	local L0_211, L1_212, L2_213 = GetClientPlayer(), L1_212, L2_213
	if L0_211 then
		L1_212 = L0_211.nSkillPlatformType
		L2_213 = SKILL_PLATFORM_TYPE
		L2_213 = L2_213.MOBILE
		L1_212 = L1_212 == L2_213
		return L1_212
	end
end
IsMobileKungfu = L24_24
function L24_24(A0_214, ...)
	local L2_215, L3_216
	L2_215 = A0_214.GetTalentInfo
	L3_216, L7_220, L8_221 = ...
	L2_215 = L2_215(L3_216, L7_220, L8_221, ...)
	if L2_215 then
		L3_216 = #L2_215
		if not (L3_216 <= 0) then
			goto lbl_11
		end
	end
	L3_216 = {}
	do return L3_216 end
	::lbl_11::
	L3_216 = {}
	L7_220 = ipairs
	L8_221 = L2_215
	L7_220, L8_221, _FOR_ = L7_220(L8_221)
	for _FORV_7_, _FORV_8_ in L7_220, L8_221, _FOR_ do
		if _FORV_8_.nType == TALENT_SELECTION_TYPE.MIXED then
			table.insert(L3_216, _FORV_8_)
		end
	end
	L7_220 = table
	L7_220 = L7_220.sort
	L8_221 = L3_216
	function L9_222(A0_225, A1_226)
		local L2_227, L3_228
		L2_227 = A0_225.nSelectIndex
		L3_228 = A1_226.nSelectIndex
		L2_227 = L2_227 < L3_228
		return L2_227
	end
	L7_220(L8_221, L9_222)
	L7_220 = 1
	L8_221 = ipairs
	L9_222 = L2_215
	L8_221, L9_222, L10_223 = L8_221(L9_222)
	for L11_224, _FORV_9_ in L8_221, L9_222, L10_223 do
		if _FORV_9_.nType == TALENT_SELECTION_TYPE.MIXED then
			L2_215[L11_224] = L3_216[L7_220]
			L7_220 = L7_220 + 1
		end
	end
	return L2_215
end
GetPlayerTalentInfo = L24_24
function L24_24(A0_229, A1_230, A2_231)
	local L3_232, L4_233 = GetClientPlayer(), L4_233
	if not L3_232 then
		L4_233 = {}
		return L4_233
	end
	L4_233 = L3_232.dwForceID
	if A1_230 == 0 then
		return {}
	end
	if not A1_230 then
		if not L3_232.GetActualKungfuMount() then
			return {}
		end
		A1_230 = L3_232.GetActualKungfuMount().dwSkillID
	end
	if not A2_231 then
		A2_231 = L3_232.GetTalentCurrentSet(L4_233, A1_230)
	end
	if A2_231 == -1 then
		return {}
	end
	local L5_234 = L5_234
	local L6_235 = L6_235
	L5_234 = L5_234(L6_235, L4_233, A1_230, A2_231)
	if L5_234 then
		L6_235 = #L5_234
		if not (L6_235 <= 0) then
			goto lbl_45
		end
	end
	L6_235 = {}
	do return L6_235 end
	::lbl_45::
	if A0_229 then
		function L6_235(A0_239, A1_240)
			local L2_241, L3_242
			L2_241 = A0_239.nRequireLevel
			L3_242 = A1_240.nRequireLevel
			if L2_241 == L3_242 then
				L2_241 = A0_239.nType
				L3_242 = TALENT_SELECTION_TYPE
				L3_242 = L3_242.MIXED
				if L2_241 == L3_242 then
					L2_241 = A1_240.nType
					L3_242 = TALENT_SELECTION_TYPE
					L3_242 = L3_242.MIXED
					if L2_241 == L3_242 then
						L2_241 = A0_239.nSelectIndex
						L3_242 = A1_240.nSelectIndex
						L2_241 = L2_241 < L3_242
						return L2_241
				end
				else
					L2_241 = A0_239.dwPointID
					L3_242 = A1_240.dwPointID
					L2_241 = L2_241 < L3_242
					return L2_241
				end
			end
			L2_241 = A0_239.nRequireLevel
			L3_242 = A1_240.nRequireLevel
			L2_241 = L2_241 < L3_242
			return L2_241
		end
		local L7_236 = L7_236
		local L8_237 = L8_237
		L7_236(L8_237, L6_235)
		local L9_238 = L9_238
	end
	return L5_234
end
GetQixueList = L24_24
L24_24 = nil
L25_25 = nil
function L28_28()
	if _ENV then
		local L1_243 = L1_243
		L1_243(_ENV, L25_25)
		local L2_244 = L2_244
	end
	L1_243 = nil
	L2_244 = nil
	L25_25 = L2_244
	_ENV = L1_243
end
function L29_29(A0_245, A1_246, A2_247)
	if not A1_246 then
		A1_246 = GetClientPlayer()
	end
	local L3_248 = L3_248
	local L3_248, L4_249 = L3_248(A0_245)
	if L3_248 == 1 then
		L4_249 = nil
	end
	return L4_249
end
Skill_GetCongNengCDID = L29_29
L29_29 = nil
L30_30 = nil
function L31_31(A0_250, A1_251, A2_252)
	repeat
		if not A1_251 then
			A1_251 = 1
		end
		if A2_252 then
			local L6_256 = L6_256
			local L6_256, L7_257 = L6_256 .. "_" .. A1_251 .. "_" .. A2_252, L7_257
			do return L6_256 end
			break -- pseudo-goto
		end
		L6_256 = A0_250
		L7_257 = "_"
		local L6_256, L5_255 = L6_256 .. L7_257 .. A1_251, L5_255
		return L6_256
	until true
end
function L32_32()
	local _ENV, L3_261, L4_262, L5_263, L6_264, L9_267, L10_268, L11_269 = {}, L3_261, L4_262, L5_263, L6_264, L9_267, L10_268, L11_269
	L3_261 = GetClientPlayer
	L3_261 = L3_261()
	L4_262 = L3_261.GetAllSkillList
	L4_262 = L4_262()
	if not L4_262 then
		L4_262 = {}
	end
	L5_263 = GetClientPlayer
	L5_263 = L5_263()
	L6_264 = nil
	L9_267 = nil
	L10_268 = nil
	L11_269 = nil
	_FOR_, _FOR_, _FOR_ = pairs(L4_262)
	for _FORV_10_, _FORV_11_ in _FOR_, _FOR_, _FOR_ do
		L6_264 = Skill_GetCongNengCDID(_FORV_10_, L5_263)
		L9_267, L10_268, L11_269 = Skill_GetCDProgress(_FORV_10_, _FORV_11_, L6_264, L5_263)
		L17_275[L31_31(_FORV_10_, _FORV_11_, L6_264)], ({})[1] = {}, L9_267
		L17_275[L31_31(_FORV_10_, _FORV_11_, L6_264)], ({})[2] = {}, L10_268
		local L17_275[L31_31(_FORV_10_, _FORV_11_, L6_264)], ({})[3], L17_275 = {}, L11_269, L17_275
	end
	L12_270 = true
	L30_30 = L12_270
end
Skill_StopCDProgress = L32_32
function L32_32()
	local L0_276, L1_277
	_ENV = L0_276
	L0_276 = nil
	L30_30 = L0_276
end
Skill_RestoreCDProgress = L32_32
function L32_32(A0_278, A1_279, A2_280, A3_281)
	repeat
		if _ENV then
			local L8_286 = L8_286
			L8_286 = L8_286[L31_31(A0_278, A1_279, A2_280)]
			if L8_286 then
				return unpack(L8_286)
			end
			return
		end
		if not A3_281 then
			L8_286 = GetClientPlayer
			L8_286 = L8_286()
			A3_281 = L8_286
		end
		if A2_280 then
			L8_286 = A3_281.GetSkillCDProgress
			do return L8_286(A0_278, A1_279, A2_280) end
			break -- pseudo-goto
		end
		L8_286 = A3_281.GetSkillCDProgress
		local L5_283 = L5_283
		do return L8_286(L5_283, A1_279) end
		local L6_284 = L6_284
	until true
end
Skill_GetCDProgress = L32_32
function L32_32(A0_287, A1_288, A2_289)
	local L5_292, L6_293, L7_294 = L5_292, L6_293, L7_294
	if not A2_289 then
		L5_292 = GetClientPlayer
		L5_292 = L5_292()
		A2_289 = L5_292
	end
	if not A2_289 then
		return
	end
	if not A1_288 then
		L5_292 = A2_289.GetSkillLevel
		L6_293 = A0_287
		L5_292 = L5_292(L6_293)
		A1_288 = L5_292
	end
	L5_292 = nil
	L6_293 = nil
	L7_294 = nil
	local L8_295 = L8_295
	local L9_296 = L9_296
	local L10_297 = L10_297
	local L11_298 = L11_298
	if 1 < L10_297 then
		L5_292, L6_293, L7_294, L8_295, L9_296 = Skill_GetCDProgress(A0_287, A1_288, L11_298, A2_289)
		if A2_289.GetCDLeft(L11_298) == 0 then
			return false
		end
	else
		if A2_289.GetCDMaxOverDraftCount(A0_287) > 1 then
			L5_292, L6_293, L7_294, L8_295, L9_296 = Skill_GetCDProgress(A0_287, A1_288, A2_289.GetCDMaxOverDraftCount(A0_287))
			if A2_289.GetOverDraftCoolDown(A2_289.GetCDMaxOverDraftCount(A0_287)) ~= A2_289.GetOverDraftCoolDown(A2_289.GetCDMaxOverDraftCount(A0_287)) then
				goto lbl_84
			end
			local L5_292, L6_293, L7_294, L8_295, L9_296, L17_304, L18_305 = Skill_GetCDProgress(A0_287, A1_288, nil, A2_289)
			break -- pseudo-goto
		end
		local L14_301 = L14_301
		local L15_302 = L15_302
		local L8_295, L9_296, L14_301, L15_302, L16_303 = L14_301(L15_302, A1_288, nil, A2_289)
		repeat
			L7_294 = L16_303
			L6_293 = L15_302
			L5_292 = L14_301
		until true
	end
	::lbl_84::
	if not L5_292 or L6_293 == 0 and L7_294 == 0 then
		L14_301 = true
		return L14_301
	end
	L14_301 = false
	return L14_301
end
Skill_NotInCountDown = L32_32
function L32_32(A0_306, A1_307)
	local L2_308, L3_309, L4_310
	L2_308 = A0_306.nCostQi
	local L9_315, L10_316, L11_317 = L9_315, L10_316, L11_317
	if L2_308 <= 0 then
		return
	end
	L2_308 = A1_307.nMaxQiEnergy
	L3_309 = A1_307.nQiEnergyReplenish
	L4_310 = A1_307.nQiEnergyReplenish
	L9_315 = A1_307.nQiEnergyReplenishPercent
	L4_310 = L4_310 * L9_315
	L4_310 = L4_310 / 1024
	L3_309 = L3_309 + L4_310
	L4_310 = A0_306.nCostQi
	L9_315 = math
	L9_315 = L9_315.floor
	L10_316 = L2_308 / L4_310
	L9_315 = L9_315(L10_316)
	L10_316 = math
	L10_316 = L10_316.min
	L11_317 = math
	L11_317 = L11_317.floor
	L11_317 = L11_317(A1_307.nCurrentQiEnergy / L4_310)
	L10_316 = L10_316(L11_317, L9_315)
	L11_317 = math
	L11_317 = L11_317.ceil
	local L11_317, L8_314 = L11_317(L4_310 / L3_309), L8_314
	if L9_315 <= L10_316 then
		L8_314 = 0
		if L8_314 then
			goto lbl_39
		end
	end
	L8_314 = A1_307.nCurrentQiEnergy
	L8_314 = L8_314 % L4_310
	L8_314 = L4_310 - L8_314
	L8_314 = L8_314 / L3_309
	::lbl_39::
	return L10_316, L11_317, L8_314
end
Skill_GetQiEnergyCDProgress = L32_32
function L32_32(A0_318, A1_319)
	if A1_319 == GetControlPlayerID() then
		return GetSkillInfoByProxy(A0_318)
	else
		do return GetSkillInfo(A0_318) end
		local L3_320 = L3_320
	end
end
GetSkillInfoEx = L32_32
function L32_32(A0_321, A1_322, A2_323, A3_324)
	if Table_IsBlackListSkill(A0_321, A1_322) then
		return
	end
	local L5_325 = L5_325
	local L6_326 = L6_326
	local L7_327 = L7_327
	do return L5_325(L6_326, L7_327, A2_323, A3_324) end
	local L8_328 = L8_328
end
CheckBlackListAddOnUseSkill = L32_32
L32_32 = {}
L32_32._vir = true
function L33_33(A0_329, A1_330, A2_331, A3_332)
	repeat
		local L4_333, L6_335 = GetClientPlayer(), L6_335
		if not L4_333 then
			return
		end
		L6_335 = L4_333.GetTarget
		L6_335 = L6_335()
		if not L6_335 or not L6_335() then
			L6_335 = TARGET.NO_TARGET
		end
		if not A2_331 and L6_335 == 4 and L4_333.dwID ~= 0 then
			return
		end
		_ENV.nSkillLevel = A1_330
		if IsMobileKungfu() then
			if A0_329 and 0 < A0_329 and VkActionBar.IsSkillShow(A0_329) and MobileSkillGrandPanel.TryCastSkill(L4_333, A0_329) then
				return SKILL_RESULT_CODE.SUCCESS
			end
		else
			local L7_336 = L7_336
			if A3_332 then
				if A3_332 == "self" then
					do return OnUseSkill(A0_329, A0_329 * (A0_329 % 10 + 1), _ENV, Skill_GetOptType(A0_329, A1_330) == "hoard", nil, nil, true) end
					local L15_344 = L15_344
					break -- pseudo-goto
				end
				if A3_332 ~= "target" then
					goto lbl_103
				end
				do return OnUseSkill(A0_329, A0_329 * (A0_329 % 10 + 1), _ENV, L15_344 == "hoard", nil, true) end
				local L14_343 = L14_343
				break -- pseudo-goto
			end
			L14_343 = OnUseSkill
			local L10_339 = L10_339
			local L11_340 = L11_340
			do return L14_343(L10_339, L11_340, _ENV, L15_344 == "hoard") end
			local L12_341 = L12_341
		end
	until true
	::lbl_103::
end
OnAddOnUseSkill = L33_33
function L33_33(A0_345)
	local L1_346
	L1_346 = false
	local L3_348 = L3_348
	if A0_345 and IsSelfCastSkill() and (A0_345.nCastMode == SKILL_CAST_MODE.TARGET_SINGLE or A0_345.nCastMode == SKILL_CAST_MODE.TARGET_CHAIN or A0_345.nCastMode == SKILL_CAST_MODE.TARGET_TEAM_AREA) and A0_345.nEffectType == SKILL_CAST_EFFECT_TYPE.BENEFICIAL then
		L3_348 = Target_GetTargetData
		L3_348 = L3_348()
		local L4_349 = L4_349
		if L3_348 == TARGET.NPC or L3_348 == TARGET.PLAYER then
			local L5_350 = L5_350
			local L6_351 = L6_351
			local L6_351, L7_352 = L6_351(GetControlPlayerID(), L4_349), L7_352
			repeat
				if not L6_351 then
					goto lbl_49
				end
				L1_346 = true
				do break end -- pseudo-goto
				L1_346 = true
			until true
		end
	end
	::lbl_49::
	return L1_346
end
IsSkillCastMyself = L33_33
L33_33 = {}
function L37_37(A0_353)
	local L2_354 = L2_354
	L2_354(_ENV, A0_353)
	local L3_355 = L3_355
end
RegisterCastSkillFun = L37_37
function L37_37(A0_356)
	L3_359 = _ENV
	L1_357, L3_359, L4_360 = L1_357(L3_359)
	for _FORV_4_, _FORV_5_ in L1_357, L3_359, L4_360 do
		if _FORV_5_ == A0_356 then
			table.remove(_ENV, _FORV_4_)
			local L8_363 = L8_363
			return
		end
	end
end
UnRegisterCastSkillFun = L37_37
L37_37 = nil
L38_38 = nil
L39_39 = nil
function L40_40()
	local L1_364, L2_365
	L1_364 = arg0
	L2_365 = arg1
	L39_39 = arg2
	L38_38 = L2_365
	_ENV = L1_364
end
function L41_41()
	local L0_366, L1_367, L2_368
	L0_366 = _ENV
	L1_367 = L38_38
	L2_368 = L39_39
	return L0_366, L1_367, L2_368
end
getShootPoint = L41_41
L41_41 = RegisterEvent
L42_42 = "UPDATE_MANNEDSPACE_FRONTSIGHT"
L43_43 = L40_40
L41_41(L42_42, L43_43)
L41_41 = nil
function L42_42()
	local L1_369
	L1_369 = _ENV
	return L1_369
end
Skill_GetLastCast = L42_42
L42_42 = nil
function L43_43(A0_370, A1_371)
	local L2_372 = L2_372
	local L3_373 = L3_373
	local L2_372, L4_374 = L2_372(L3_373, A1_371), L4_374
	_ENV = L2_372
	L2_372 = _ENV
	L2_372 = L2_372.bHoardSkill
	if L2_372 then
		L2_372 = "hoard"
		return L2_372
	else
		L2_372 = _ENV
		L2_372 = L2_372.bKeyDownSkill
		if L2_372 then
			L2_372 = "keydown"
			return L2_372
		else
			L2_372 = _ENV
			L2_372 = L2_372.bKeyUpSkill
			if L2_372 then
				L2_372 = "keyup"
				return L2_372
			else
				L2_372 = _ENV
				L2_372 = L2_372.bKeyDownAndUpSkill
				if L2_372 then
					L2_372 = "onlykeyup"
					return L2_372
				end
			end
		end
	end
end
Skill_GetOptType = L43_43
function L43_43(A0_375, A1_376)
	local L2_377, L3_378
	L2_377 = KUNFU2TYPE_LIST
	if not L2_377 then
		L2_377 = false
		return L2_377
	end
	L2_377 = KUNFU2TYPE_LIST
	L2_377 = L2_377[A0_375]
	L3_378 = L2_377 == A1_376
	return L3_378
end
KungfuFit = L43_43
function L43_43(A0_379, A1_380, A2_381)
	local L3_382 = L3_382
	L3_382 = L3_382(A1_380)
	if not L3_382 then
		return
	end
	if L3_382.bForbidSelectTarget then
		return
	end
	local L4_383 = A0_379.GetActualKungfuMount()
	local L5_384 = L5_384
	local L5_384, L7_386 = L5_384(L4_383.dwSkillID, PLAYER_ARENA_TYPE.THERAPY), L7_386
	if not L5_384 then
		L7_386 = A0_379.GetTarget
		L7_386 = L7_386()
		if L7_386() == 0 then
			if L3_382.bAllyTarget then
				SearchAllies()
			else
				SearchEnemy()
				local L8_387 = L8_387
			end
		end
	end
end
function L44_44(A0_388, A1_389)
	local L2_390 = L2_390
	L2_390 = L2_390(A0_388, A1_389, GetClientPlayer().dwID)
	if not L2_390 then
		local L3_391 = L3_391
		local L4_392 = L4_392
		local L3_391, L5_393 = L3_391(L4_392, A1_389), L5_393
		L2_390 = L3_391
	end
	return L2_390
end
L45_45 = nil
L46_46 = nil
function L47_47()
	local L0_394, L1_395
	L0_394 = _ENV
	L1_395 = L46_46
	return L0_394, L1_395
end
GetCastingSkill = L47_47
function L47_47(A0_396, A1_397, A2_398)
	if A1_397 == 0 then
		return true
	end
	local L3_399 = L3_399
	local L4_400 = L4_400
	local L3_399, L5_401 = L3_399(L4_400, A1_397), L5_401
	if L3_399 then
		L3_399 = false
		return L3_399
	end
	L3_399 = A2_398.nCastMode
	L4_400 = SKILL_CAST_MODE
	L4_400 = L4_400.TARGET_SINGLE
	if L3_399 ~= L4_400 then
		L3_399 = A2_398.nCastMode
		L4_400 = SKILL_CAST_MODE
		L4_400 = L4_400.TARGET_CHAIN
		if L3_399 ~= L4_400 then
			L3_399 = A2_398.nCastMode
			L4_400 = SKILL_CAST_MODE
			L4_400 = L4_400.TARGET_AREA
		end
	end
	L3_399 = L3_399 == L4_400
	return L3_399
end
function L48_48(A0_402, A1_403, A2_404, A3_405, A4_406)
	if GetOperationMode() ~= JOYSTICK_MODE then
		return
	end
	local L5_407 = L5_407
	L5_407 = L5_407(A3_405, A4_406)
	if not L5_407 or not L5_407.bAutoSelectTarget then
		return
	end
	local L7_409 = L7_409
	local L8_410 = L8_410
	local L7_409, L9_411 = L7_409(L8_410, A1_403, A2_404), L9_411
	if L7_409 then
		L7_409 = A3_405
		L46_46 = A4_406
		L45_45 = L7_409
		L7_409 = SearchEnemy
		L7_409()
		L7_409 = nil
		L8_410 = nil
		L46_46 = L8_410
		L45_45 = L7_409
	end
end
function L49_49(A0_412, A1_413, A2_414)
	if A1_413 ~= 0 then
		return
	end
	local L4_416 = L4_416
	if A0_412.bBirdMove and 0 < A0_412.nFlyFlag and (A2_414.nCastMode == SKILL_CAST_MODE.TARGET_SINGLE or A2_414.nCastMode == SKILL_CAST_MODE.TARGET_CHAIN or A2_414.nCastMode == SKILL_CAST_MODE.TARGET_AREA) and A2_414.nEffectType ~= SKILL_CAST_EFFECT_TYPE.BENEFICIAL then
		L4_416 = A2_414.IsRelationEnemy
		L4_416 = L4_416()
		if L4_416 then
			L4_416 = SearchEnemy
			L4_416()
		end
	end
end
function L50_50(A0_417, A1_418, A2_419, A3_420)
	local L4_421, L5_422 = A0_417.GetTarget()
	_ENV(A0_417, L5_422, A1_418)
	local L6_423 = L6_423
	local L7_424 = L7_424
	local L8_425 = L8_425
	local L9_426 = L9_426
	local L10_427 = L10_427
	L6_423(L7_424, L8_425, L9_426, L10_427, A3_420)
	local L11_428 = L11_428
end
function L51_51(A0_429, A1_430, A2_431, A3_432)
	local L4_433, L5_434, L6_435, L7_436
	L4_433 = _ENV
	if A3_432 then
		L5_434 = L19_19
		L4_433 = L5_434.TARGET
	end
	L5_434 = L19_19
	L5_434 = L5_434.MOUSE
	if L4_433 == L5_434 and A2_431 then
		L5_434 = L19_19
		L4_433 = L5_434.TRADITION
	end
	L5_434 = A1_430.bRangePutOpti
	L6_435 = L2_2
	L6_435 = L6_435[A0_429]
	if L6_435 then
		L6_435 = L2_2
		L6_435 = L6_435[A0_429]
		if L6_435 == 102 then
			L5_434 = true
			L6_435 = L19_19
			L4_433 = L6_435.MOUSE
		else
			L5_434 = false
			L6_435 = L2_2
			L4_433 = L6_435[A0_429]
		end
	end
	L6_435 = L4_433
	L7_436 = L5_434
	return L6_435, L7_436
end
function L52_52(A0_437)
	local L1_438, L2_439, L4_441 = Target_GetTargetData()
	if A0_437 and (not L1_438 or not L2_439) then
		L4_441 = TARGET
		L1_438 = L4_441.PLAYER
		L4_441 = GetClientPlayer
		L4_441 = L4_441()
		L2_439 = L4_441.dwID
	end
	L4_441 = L1_438
	return L4_441, L2_439
end
function L53_53(A0_442, A1_443, A2_444)
	local L3_445 = L3_445
	L3_445 = L3_445(A1_443, A0_442)
	local L4_446 = L4_446
	local L5_447 = L5_447
	local L6_448 = L6_448
	local L7_449 = L7_449
	local L4_446, L5_447, L6_448, L7_449, L8_450 = L4_446(L5_447, L6_448, L7_449, A0_442)
	if not L4_446 or L5_447 == 0 and (L7_449 or L6_448 == 0) then
		L8_450 = true
		return L8_450
	end
end
function L54_54(A0_451, A1_452)
	if IsMobileSkill(A0_451, A1_452) then
		Selection_ShowSFX(100019, 1)
	else
		local L3_453 = L3_453
		L3_453(A0_451, A1_452)
		local L4_454 = L4_454
	end
end
function L55_55(A0_455, A1_456, A2_457, A3_458, A4_459, A5_460, A6_461, A7_462, A8_463)
	local L9_464, L14_469 = L9_464, A0_455
	L9_464 = L9_464(L14_469, A1_456)
	L14_469 = A7_462.GetSkillRecipeKey
	L14_469 = L14_469(A0_455, A1_456)
	local L11_466 = L11_466
	local L12_467 = L12_467
	local L11_466, L13_468 = L11_466(L12_467, A7_462.dwID), L13_468
	function L12_467()
		Selection_HideSFX()
	end
	L13_468 = true
	if L9_464.UITestCast(A7_462.dwID, IsSkillCastMyself(L9_464)) ~= SKILL_RESULT_CODE.SUCCESS then
		return
	end
	if IsSkillCastToMe(A0_455) or A6_461 then
		if IsQiChangSkill(A0_455) then
			A8_463(true, A2_457)
		else
			A8_463(A7_462.GetAbsoluteCoordinate())
		end
		return
	end
	if not L53_53(A7_462, A0_455, A1_456) and not A5_460 then
		return
	end
	L54_54(A0_455, A1_456)
	local L15_470 = L15_470
	local L16_471 = L16_471
	L16_471 = L16_471(A0_455, L9_464, A4_459, A5_460)
	if L16_471 == L19_19.MOUSE then
		if L16_471(A0_455, L9_464, A4_459, A5_460) then
			PostThreadCall(function(A0_493, A1_494, A2_495)
				repeat
					if _ENV(A0_493, A1_494, A2_495) then
						local L4_496 = L4_496
						local L5_497 = L5_497
						L4_496(L5_497, A1_494, A2_495)
						local L6_498 = L6_498
						break -- pseudo-goto
					end
					L4_496 = Selection_HideSFX
					L4_496()
				until true
			end, nil, "Scene_SelectRayGround", Cursor.GetPos(false))
		else
			PostThreadCall(function(A0_499, A1_500, A2_501)
				repeat
					if _ENV(A0_499, A1_500, A2_501) then
						local L4_502 = L4_502
						local L5_503 = L5_503
						L4_502(L5_503, A1_500, A2_501)
						local L6_504 = L6_504
						break -- pseudo-goto
					end
					L4_502 = Selection_HideSFX
					L4_502()
				until true
			end, nil, "Scene_SelectGround", Cursor.GetPos(false))
		end
		return
	end
	if L16_471 == L19_19.TARGET and (L52_52(A5_460) == TARGET.NPC or L52_52(A5_460) == TARGET.PLAYER) then
		if L52_52(A5_460) == TARGET.PLAYER then
		elseif L52_52(A5_460) == TARGET.NPC then
		end
		if L52_52(A5_460) == TARGET.NPC and IsMobileSkill(A0_455, A1_456) then
			local L27_482 = L27_482
			local L28_483 = L28_483
			local L29_484, L30_485, L31_486 = L29_484, L30_485, L31_486
			local L32_487 = L32_487
			if math.max(128, GetNpc(L52_52(A5_460)).nTouchRange) > math.sqrt((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8 * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8)) then
				L31_486, L32_487 = A7_462.GetAbsoluteCoordinate()
				break -- pseudo-goto
			end
			if math.sqrt((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8 * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8)) > 0.01 and math.sqrt((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8 * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8)) < L11_466.MaxRadius + math.max(128, GetNpc(L52_52(A5_460)).nTouchRange) then
				local L33_488 = L33_488
				L31_486, L32_487, L33_488 = A7_462.GetAbsoluteCoordinate() + (math.sqrt((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8 * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8)) - math.max(128, GetNpc(L52_52(A5_460)).nTouchRange)) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / math.sqrt((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8 * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8)), A7_462.GetAbsoluteCoordinate() + (math.sqrt((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8 * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8)) - math.max(128, GetNpc(L52_52(A5_460)).nTouchRange)) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / math.sqrt((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8 * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8)), A7_462.GetAbsoluteCoordinate() + (math.sqrt((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8 * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8)) - math.max(128, GetNpc(L52_52(A5_460)).nTouchRange)) * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8) / math.sqrt((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) * (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) + (GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8 * ((GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate() - A7_462.GetAbsoluteCoordinate()) / 8)) * 8
				if not A7_462.GetScene().GetFloor(L31_486, L32_487, L33_488 + 512) then
				end
				local L34_489 = L34_489
				if 128 > math.abs(A7_462.GetScene().GetFloor(GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate()) - GetNpc(L52_52(A5_460)).GetAbsoluteCoordinate()) then
					L33_488 = L33_488
				else
					local L36_491 = L36_491
					local L37_492 = L37_492
					L33_488 = math.max(L33_488, L33_488)
				end
			end
		end
		repeat
		until true
		L34_489 = L15_470
		L36_491 = L31_486
		L37_492 = L32_487
		L34_489 = L34_489(L36_491, L37_492, L33_488)
		if L34_489 then
			L34_489 = IsQiChangSkill
			L36_491 = A0_455
			L34_489 = L34_489(L36_491)
			if L34_489 then
				L34_489 = A8_463
				L36_491 = false
				L37_492 = A2_457
				L34_489(L36_491, L37_492)
			else
				L34_489 = A8_463
				L36_491 = L31_486
				L37_492 = L32_487
				L34_489(L36_491, L37_492, L33_488)
			end
			return
		end
	end
	L28_483 = UserSelect
	L28_483 = L28_483.SelectPoint
	L29_484 = A8_463
	L30_485 = L12_467
	L31_486 = L15_470
	L32_487 = A3_458
	L28_483(L29_484, L30_485, L31_486, L32_487)
end
function L56_56(A0_505, A1_506, A2_507, A3_508, A4_509, A5_510, A6_511)
	local L7_512
	local L8_513 = L8_513
	L8_513 = L8_513(A0_505, A1_506)
	UserSelect.CancelSelect()
	local L9_514 = L9_514
	L9_514 = L9_514(L8_513)
	local L10_515 = L10_515
	local L10_515, L11_516 = L10_515(A0_505)
	if L10_515 then
		L7_512 = CastCommonSkill(L11_516)
		CheckCastSkillResult(L7_512, A3_508)
		return L7_512
	end
	if L8_513.bHoardSkill then
		if A4_509 then
			if not Skill_NotInCountDown(A0_505, A1_506, A5_510) then
				return
			end
			if _ENV[A0_505] then
				local L15_520 = L15_520
				local L15_520, L16_521 = L15_520(A0_505, A1_506, TARGET.PLAYER, A5_510.dwID), L16_521
				if true then
					repeat
						L7_512 = L15_520
						do break end -- pseudo-goto
						L15_520 = A5_510.StartHoardSkill
						L16_521 = A0_505
						L15_520 = L15_520(L16_521, A1_506)
						L7_512 = L15_520
					until true
					L15_520 = SKILL_RESULT_CODE
					L15_520 = L15_520.SUCCESS
					if L7_512 == L15_520 then
						goto lbl_69
					end
					L15_520 = FireUIEvent
					L16_521 = "SYS_MSG_UI_OME_SKILL_RESPOND"
					L15_520(L16_521, nil)
			end
		end
		else
			L15_520 = A5_510.CastHoardSkill
			L15_520()
			L15_520 = SKILL_RESULT_CODE
			L7_512 = L15_520.SUCCESS
		end
		::lbl_69::
		return L7_512
	end
	L15_520 = IsSkillCastToMe
	L16_521 = A0_505
	L15_520 = L15_520(L16_521)
	if L15_520 then
		L9_514 = true
	end
	L15_520 = A6_511
	L16_521 = L9_514
	L15_520(L16_521, A2_507)
	local L14_519 = L14_519
end
function L57_57(A0_522, A1_523, A2_524, A3_525, A4_526, A5_527, A6_528, A7_529, A8_530, A9_531)
	local L10_532
	repeat
		local L11_533 = L11_533
		L11_533 = L11_533(A0_522, A1_523)
		local L12_534 = L12_534
		L12_534 = L12_534(A0_522, A1_523)
		local L13_535 = L13_535
		L13_535 = L13_535(L12_534, A8_530.dwID)
		UserSelect.CancelSelect()
		if L11_533.UITestCast(A8_530.dwID, IsSkillCastMyself(L11_533)) ~= SKILL_RESULT_CODE.SUCCESS then
			return
		end
		if not Skill_NotInCountDown(A0_522, A1_523, A8_530) then
			return
		end
		local L14_536 = L14_536
		local L15_537 = L15_537
		local L16_538 = L16_538
		local L17_539 = L17_539
		L14_536, L15_537 = L14_536(L15_537, L16_538, L17_539, A6_528)
		L16_538 = true
		function L17_539(A0_550, A1_551, A2_552, A3_553)
			local L4_554 = L4_554
			L4_554 = L4_554(A0_522, A1_523)
			local L8_558 = L8_558
			local L8_558, L9_559 = L8_558(A8_530.dwID, A0_550, A1_551, A2_552), L9_559
			L9_559 = SKILL_RESULT_CODE
			L9_559 = L9_559.SUCCESS
			if L8_558 == L9_559 then
				L8_558 = L16_538
				if not L8_558 then
					L8_558 = Selection_HideSFX
					L8_558()
				end
				if A3_553 then
					L8_558 = Target_GetTargetID
					L8_558 = L8_558()
					if L8_558 then
						goto lbl_31
					end
				end
				L8_558 = L54_54
				L9_559 = A0_522
				L8_558(L9_559, A1_523)
				::lbl_31::
				L8_558 = true
				L16_538 = L8_558
				L8_558 = true
				return L8_558
			else
				L8_558 = L16_538
				if L8_558 then
					L8_558 = Selection_HideSFX
					L8_558()
				end
				if A3_553 then
					L8_558 = Target_GetTargetID
					L8_558 = L8_558()
					if L8_558 then
						goto lbl_53
					end
				end
				L8_558 = Selection_ShowSFX
				L9_559 = SKILL_SELECT_POINT_UNNORMAL
				L9_559 = L9_559.nSkillID
				L8_558(L9_559, SKILL_SELECT_POINT_UNNORMAL.nLevel)
				local L7_557 = L7_557
				::lbl_53::
				L8_558 = false
				L16_538 = L8_558
				L8_558 = false
				return L8_558
			end
		end
		if A4_526 then
			L10_532 = A8_530.StartHoardSkill(A0_522, A1_523, TARGET.PLAYER, A8_530.dwID)
			if L10_532 ~= SKILL_RESULT_CODE.SUCCESS then
				FireUIEvent("SYS_MSG_UI_OME_SKILL_RESPOND", nil)
			else
				