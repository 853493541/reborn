local L0_0, L1_1, L2_2, L3_3, L4_4, L5_5, L6_6, L7_7, L8_8, L9_9, L10_10, L11_11, L12_12, L13_13, L14_14, L15_15, L16_16, L17_17, L18_18, L19_19, L20_20, L21_21, L22_22, L23_23, L24_24, L25_25, L26_26, L27_27, L28_28, L29_29, L30_30, L31_31, L32_32, L33_33, L34_34, L35_35, L36_36, L37_37, L38_38, L39_39, L40_40, L41_41, L42_42, L43_43, L44_44, L45_45, L46_46, L47_47, L48_48, L49_49, L50_50, L51_51, L52_52, L53_53, L54_54, L55_55, L56_56, L57_57, L58_58, L59_59
L0_0 = {}
L0_0._VERSION = "tween 2.0.0"
L0_0._DESCRIPTION = "tweening for lua"
L0_0._URL = "https://github.com/kikito/tween.lua"
L0_0._LICENSE = "    MIT LICENSE\n\n    Copyright (c) 2014 Enrique Garc\195\173a Cota, Yuichi Tateno, Emmanuel Oga\n\n    Permission is hereby granted, free of charge, to any person obtaining a\n    copy of this software and associated documentation files (the\n    \"Software\"), to deal in the Software without restriction, including\n    without limitation the rights to use, copy, modify, merge, publish,\n    distribute, sublicense, and/or sell copies of the Software, and to\n    permit persons to whom the Software is furnished to do so, subject to\n    the following conditions:\n\n    The above copyright notice and this permission notice shall be included\n    in all copies or substantial portions of the Software.\n\n    THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS\n    OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF\n    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.\n    IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY\n    CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,\n    TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE\n    SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.\n  "
tween = L0_0
L0_0 = math
L0_0 = L0_0.pow
L1_1 = math
L1_1 = L1_1.sin
L2_2 = math
L2_2 = L2_2.cos
L3_3 = math
L3_3 = L3_3.pi
L4_4 = math
L4_4 = L4_4.sqrt
L5_5 = math
L5_5 = L5_5.abs
L6_6 = math
L6_6 = L6_6.asin
function L7_7(A0_60, A1_61, A2_62, A3_63)
	local L4_64
	L4_64 = A2_62 * A0_60
	L4_64 = L4_64 / A3_63
	L4_64 = L4_64 + A1_61
	return L4_64
end
function L8_8(A0_65, A1_66, A2_67, A3_68)
	local L4_69 = L4_69
	local L5_70 = L5_70
	local L4_69, L6_71 = L4_69(L5_70, 2), L6_71
	L4_69 = A2_67 * L4_69
	L4_69 = L4_69 + A1_66
	return L4_69
end
function L9_9(A0_72, A1_73, A2_74, A3_75)
	local L4_76, L5_77
	A0_72 = A0_72 / A3_75
	L4_76 = -A2_74
	L4_76 = L4_76 * A0_72
	L5_77 = A0_72 - 2
	L4_76 = L4_76 * L5_77
	L4_76 = L4_76 + A1_73
	return L4_76
end
function L10_10(A0_78, A1_79, A2_80, A3_81)
	local L4_82
	L4_82 = A0_78 / A3_81
	A0_78 = L4_82 * 2
	if A0_78 < 1 then
		L4_82 = A2_80 / 2
		local L5_83 = L5_83
		local L6_84 = L6_84
		local L5_83, L7_85 = L5_83(L6_84, 2), L7_85
		L4_82 = L4_82 * L5_83
		L4_82 = L4_82 + A1_79
		return L4_82
	end
	L4_82 = -A2_80
	L4_82 = L4_82 / 2
	L5_83 = A0_78 - 1
	L6_84 = A0_78 - 3
	L5_83 = L5_83 * L6_84
	L5_83 = L5_83 - 1
	L4_82 = L4_82 * L5_83
	L4_82 = L4_82 + A1_79
	return L4_82
end
function L11_11(A0_86, A1_87, A2_88, A3_89)
	if A0_86 < A3_89 / 2 then
		return _ENV(A0_86 * 2, A1_87, A2_88 / 2, A3_89)
	end
	local L5_90 = L5_90
	local L6_91 = L6_91
	local L7_92 = L7_92
	do return L5_90(L6_91, L7_92, A2_88 / 2, A3_89) end
	local L8_93 = L8_93
end
function L12_12(A0_94, A1_95, A2_96, A3_97)
	local L4_98 = L4_98
	local L5_99 = L5_99
	local L4_98, L6_100 = L4_98(L5_99, 3), L6_100
	L4_98 = A2_96 * L4_98
	L4_98 = L4_98 + A1_95
	return L4_98
end
function L13_13(A0_101, A1_102, A2_103, A3_104)
	local L4_105 = L4_105
	local L5_106 = L5_106
	local L4_105, L6_107 = L4_105(L5_106, 3), L6_107
	L4_105 = L4_105 + 1
	L4_105 = A2_103 * L4_105
	L4_105 = L4_105 + A1_102
	return L4_105
end
function L14_14(A0_108, A1_109, A2_110, A3_111)
	local L4_112, L5_113
	L4_112 = A0_108 / A3_111
	A0_108 = L4_112 * 2
	if A0_108 < 1 then
		L4_112 = A2_110 / 2
		L4_112 = L4_112 * A0_108
		L4_112 = L4_112 * A0_108
		L4_112 = L4_112 * A0_108
		L4_112 = L4_112 + A1_109
		return L4_112
	end
	A0_108 = A0_108 - 2
	L4_112 = A2_110 / 2
	L5_113 = A0_108 * A0_108
	L5_113 = L5_113 * A0_108
	L5_113 = L5_113 + 2
	L4_112 = L4_112 * L5_113
	L4_112 = L4_112 + A1_109
	return L4_112
end
function L15_15(A0_114, A1_115, A2_116, A3_117)
	if A0_114 < A3_117 / 2 then
		return _ENV(A0_114 * 2, A1_115, A2_116 / 2, A3_117)
	end
	local L5_118 = L5_118
	local L6_119 = L6_119
	local L7_120 = L7_120
	do return L5_118(L6_119, L7_120, A2_116 / 2, A3_117) end
	local L8_121 = L8_121
end
function L16_16(A0_122, A1_123, A2_124, A3_125)
	local L4_126 = L4_126
	local L5_127 = L5_127
	local L4_126, L6_128 = L4_126(L5_127, 4), L6_128
	L4_126 = A2_124 * L4_126
	L4_126 = L4_126 + A1_123
	return L4_126
end
function L17_17(A0_129, A1_130, A2_131, A3_132)
	local L4_133
	L4_133 = -A2_131
	local L5_134 = L5_134
	local L6_135 = L6_135
	local L5_134, L7_136 = L5_134(L6_135, 4), L7_136
	L5_134 = L5_134 - 1
	L4_133 = L4_133 * L5_134
	L4_133 = L4_133 + A1_130
	return L4_133
end
function L18_18(A0_137, A1_138, A2_139, A3_140)
	local L4_141
	L4_141 = A0_137 / A3_140
	A0_137 = L4_141 * 2
	if A0_137 < 1 then
		L4_141 = A2_139 / 2
		L4_141 = L4_141 * _ENV(A0_137, 4)
		L4_141 = L4_141 + A1_138
		return L4_141
	end
	L4_141 = -A2_139
	L4_141 = L4_141 / 2
	local L5_142 = L5_142
	local L6_143 = L6_143
	local L5_142, L7_144 = L5_142(L6_143, 4), L7_144
	L5_142 = L5_142 - 2
	L4_141 = L4_141 * L5_142
	L4_141 = L4_141 + A1_138
	return L4_141
end
function L19_19(A0_145, A1_146, A2_147, A3_148)
	if A0_145 < A3_148 / 2 then
		return _ENV(A0_145 * 2, A1_146, A2_147 / 2, A3_148)
	end
	local L5_149 = L5_149
	local L6_150 = L6_150
	local L7_151 = L7_151
	do return L5_149(L6_150, L7_151, A2_147 / 2, A3_148) end
	local L8_152 = L8_152
end
function L20_20(A0_153, A1_154, A2_155, A3_156)
	local L4_157 = L4_157
	local L5_158 = L5_158
	local L4_157, L6_159 = L4_157(L5_158, 5), L6_159
	L4_157 = A2_155 * L4_157
	L4_157 = L4_157 + A1_154
	return L4_157
end
function L21_21(A0_160, A1_161, A2_162, A3_163)
	local L4_164 = L4_164
	local L5_165 = L5_165
	local L4_164, L6_166 = L4_164(L5_165, 5), L6_166
	L4_164 = L4_164 + 1
	L4_164 = A2_162 * L4_164
	L4_164 = L4_164 + A1_161
	return L4_164
end
function L22_22(A0_167, A1_168, A2_169, A3_170)
	local L4_171
	L4_171 = A0_167 / A3_170
	A0_167 = L4_171 * 2
	if A0_167 < 1 then
		L4_171 = A2_169 / 2
		L4_171 = L4_171 * _ENV(A0_167, 5)
		L4_171 = L4_171 + A1_168
		return L4_171
	end
	L4_171 = A2_169 / 2
	local L5_172 = L5_172
	local L6_173 = L6_173
	local L5_172, L7_174 = L5_172(L6_173, 5), L7_174
	L5_172 = L5_172 + 2
	L4_171 = L4_171 * L5_172
	L4_171 = L4_171 + A1_168
	return L4_171
end
function L23_23(A0_175, A1_176, A2_177, A3_178)
	if A0_175 < A3_178 / 2 then
		return _ENV(A0_175 * 2, A1_176, A2_177 / 2, A3_178)
	end
	local L5_179 = L5_179
	local L6_180 = L6_180
	local L7_181 = L7_181
	do return L5_179(L6_180, L7_181, A2_177 / 2, A3_178) end
	local L8_182 = L8_182
end
function L24_24(A0_183, A1_184, A2_185, A3_186)
	local L4_187
	L4_187 = -A2_185
	local L5_188 = L5_188
	local L7_190 = A0_183 / A3_186 * (L3_3 / 2)
	L5_188 = L5_188(L7_190)
	L4_187 = L4_187 * L5_188
	L4_187 = L4_187 + A2_185
	L4_187 = L4_187 + A1_184
	return L4_187
end
function L25_25(A0_191, A1_192, A2_193, A3_194)
	local L4_195 = L4_195
	local L6_197 = A0_191 / A3_194 * (L3_3 / 2)
	L4_195 = L4_195(L6_197)
	L4_195 = A2_193 * L4_195
	L4_195 = L4_195 + A1_192
	return L4_195
end
function L26_26(A0_198, A1_199, A2_200, A3_201)
	local L4_202
	L4_202 = -A2_200
	L4_202 = L4_202 / 2
	local L5_203 = L5_203
	local L5_203, L6_204 = L5_203(L3_3 * A0_198 / A3_201), L6_204
	L5_203 = L5_203 - 1
	L4_202 = L4_202 * L5_203
	L4_202 = L4_202 + A1_199
	return L4_202
end
function L27_27(A0_205, A1_206, A2_207, A3_208)
	if A0_205 < A3_208 / 2 then
		return _ENV(A0_205 * 2, A1_206, A2_207 / 2, A3_208)
	end
	local L5_209 = L5_209
	local L6_210 = L6_210
	local L7_211 = L7_211
	do return L5_209(L6_210, L7_211, A2_207 / 2, A3_208) end
	local L8_212 = L8_212
end
function L28_28(A0_213, A1_214, A2_215, A3_216)
	if A0_213 == 0 then
		return A1_214
	end
	local L4_217 = L4_217
	local L5_218 = L5_218
	local L4_217, L6_219 = L4_217(L5_218, 10 * (A0_213 / A3_216 - 1)), L6_219
	L4_217 = A2_215 * L4_217
	L4_217 = L4_217 + A1_214
	L5_218 = A2_215 * 0.001
	L4_217 = L4_217 - L5_218
	return L4_217
end
function L29_29(A0_220, A1_221, A2_222, A3_223)
	local L4_224
	if A0_220 == A3_223 then
		L4_224 = A1_221 + A2_222
		return L4_224
	end
	L4_224 = A2_222 * 1.001
	local L5_225 = L5_225
	local L6_226 = L6_226
	local L5_225, L7_227 = L5_225(L6_226, -10 * A0_220 / A3_223), L7_227
	L5_225 = -L5_225
	L5_225 = L5_225 + 1
	L4_224 = L4_224 * L5_225
	L4_224 = L4_224 + A1_221
	return L4_224
end
function L30_30(A0_228, A1_229, A2_230, A3_231)
	local L4_232
	if A0_228 == 0 then
		return A1_229
	end
	if A0_228 == A3_231 then
		L4_232 = A1_229 + A2_230
		return L4_232
	end
	L4_232 = A0_228 / A3_231
	A0_228 = L4_232 * 2
	if A0_228 < 1 then
		L4_232 = A2_230 / 2
		L4_232 = L4_232 * _ENV(2, 10 * (A0_228 - 1))
		L4_232 = L4_232 + A1_229
		L4_232 = L4_232 - A2_230 * 5.0E-4
		return L4_232
	end
	L4_232 = A2_230 / 2
	L4_232 = L4_232 * 1.0005
	local L5_233 = L5_233
	local L6_234 = L6_234
	local L5_233, L7_235 = L5_233(L6_234, -10 * (A0_228 - 1)), L7_235
	L5_233 = -L5_233
	L5_233 = L5_233 + 2
	L4_232 = L4_232 * L5_233
	L4_232 = L4_232 + A1_229
	return L4_232
end
function L31_31(A0_236, A1_237, A2_238, A3_239)
	if A0_236 < A3_239 / 2 then
		return _ENV(A0_236 * 2, A1_237, A2_238 / 2, A3_239)
	end
	local L5_240 = L5_240
	local L6_241 = L6_241
	local L7_242 = L7_242
	do return L5_240(L6_241, L7_242, A2_238 / 2, A3_239) end
	local L8_243 = L8_243
end
function L32_32(A0_244, A1_245, A2_246, A3_247)
	local L4_248
	L4_248 = -A2_246
	local L5_249 = L5_249
	local L7_251 = L7_251
	local L7_251, L8_252 = L7_251(A0_244 / A3_247, 2), L8_252
	L7_251 = 1 - L7_251
	L5_249 = L5_249(L7_251)
	L5_249 = L5_249 - 1
	L4_248 = L4_248 * L5_249
	L4_248 = L4_248 + A1_245
	return L4_248
end
function L33_33(A0_253, A1_254, A2_255, A3_256)
	local L4_257 = L4_257
	local L6_259 = L6_259
	local L6_259, L7_260 = L6_259(A0_253 / A3_256 - 1, 2), L7_260
	L6_259 = 1 - L6_259
	L4_257 = L4_257(L6_259)
	L4_257 = A2_255 * L4_257
	L4_257 = L4_257 + A1_254
	return L4_257
end
function L34_34(A0_261, A1_262, A2_263, A3_264)
	local L4_265
	L4_265 = A0_261 / A3_264
	A0_261 = L4_265 * 2
	if A0_261 < 1 then
		L4_265 = -A2_263
		L4_265 = L4_265 / 2
		L4_265 = L4_265 * (_ENV(1 - A0_261 * A0_261) - 1)
		L4_265 = L4_265 + A1_262
		return L4_265
	end
	A0_261 = A0_261 - 2
	L4_265 = A2_263 / 2
	local L5_266 = L5_266
	local L5_266, L6_267 = L5_266(1 - A0_261 * A0_261), L6_267
	L5_266 = L5_266 + 1
	L4_265 = L4_265 * L5_266
	L4_265 = L4_265 + A1_262
	return L4_265
end
function L35_35(A0_268, A1_269, A2_270, A3_271)
	if A0_268 < A3_271 / 2 then
		return _ENV(A0_268 * 2, A1_269, A2_270 / 2, A3_271)
	end
	local L5_272 = L5_272
	local L6_273 = L6_273
	local L7_274 = L7_274
	do return L5_272(L6_273, L7_274, A2_270 / 2, A3_271) end
	local L8_275 = L8_275
end
function L36_36(A0_276, A1_277, A2_278, A3_279)
	local L6_282 = L6_282
	if not A0_276 then
		L6_282 = A3_279 * 0.3
	end
	if not A1_277 then
		A1_277 = 0
	end
	A0_276 = L6_282
	L6_282 = _ENV
	local L6_282, L5_281 = L6_282(A2_278), L5_281
	if A1_277 < L6_282 then
		L6_282 = A0_276
		L5_281 = A2_278
		return L6_282, L5_281, A0_276 / 4
	end
	L6_282 = A0_276
	L5_281 = A1_277
	local L7_283 = L7_283
	local L8_284 = L6_6(A2_278 / A1_277)
	L7_283 = L7_283 * L8_284
	return L6_282, L5_281, L7_283
end
function L37_37(A0_285, A1_286, A2_287, A3_288, A4_289, A5_290)
	local L6_291
	if A0_285 == 0 then
		return A1_286
	end
	A0_285 = A0_285 / A3_288
	if A0_285 == 1 then
		return A1_286 + A2_287
	end
	local L11_296 = _ENV(A5_290, A4_289, A2_287, A3_288)
	A4_289, L6_291 = _ENV(A5_290, A4_289, A2_287, A3_288)
	A5_290 = L11_296
	A0_285 = A0_285 - 1
	L11_296 = L0_0
	L11_296 = L11_296(2, 10 * A0_285)
	L11_296 = A4_289 * L11_296
	local L8_293 = L8_293
	local L10_295 = (A0_285 * A3_288 - L6_291) * (2 * L3_3)
	L10_295 = L10_295 / A5_290
	L8_293 = L8_293(L10_295)
	L11_296 = L11_296 * L8_293
	L11_296 = -L11_296
	L11_296 = L11_296 + A1_286
	return L11_296
end
function L38_38(A0_297, A1_298, A2_299, A3_300, A4_301, A5_302)
	local L6_303
	if A0_297 == 0 then
		return A1_298
	end
	A0_297 = A0_297 / A3_300
	if A0_297 == 1 then
		return A1_298 + A2_299
	end
	local L11_308 = _ENV(A5_302, A4_301, A2_299, A3_300)
	A4_301, L6_303 = _ENV(A5_302, A4_301, A2_299, A3_300)
	A5_302 = L11_308
	L11_308 = L0_0
	L11_308 = L11_308(2, -10 * A0_297)
	L11_308 = A4_301 * L11_308
	local L8_305 = L8_305
	local L10_307 = (A0_297 * A3_300 - L6_303) * (2 * L3_3)
	L10_307 = L10_307 / A5_302
	L8_305 = L8_305(L10_307)
	L11_308 = L11_308 * L8_305
	L11_308 = L11_308 + A2_299
	L11_308 = L11_308 + A1_298
	return L11_308
end
function L39_39(A0_309, A1_310, A2_311, A3_312, A4_313, A5_314)
	local L6_315
	if A0_309 == 0 then
		return A1_310
	end
	A0_309 = A0_309 / A3_312 * 2
	if A0_309 == 2 then
		return A1_310 + A2_311
	end
	local L11_320 = _ENV(A5_314, A4_313, A2_311, A3_312)
	A4_313, L6_315 = _ENV(A5_314, A4_313, A2_311, A3_312)
	A5_314 = L11_320
	A0_309 = A0_309 - 1
	if A0_309 < 0 then
		L11_320 = L0_0
		L11_320 = L11_320(2, 10 * A0_309)
		L11_320 = A4_313 * L11_320
		L11_320 = L11_320 * L1_1((A0_309 * A3_312 - L6_315) * (2 * L3_3) / A5_314)
		L11_320 = -0.5 * L11_320
		L11_320 = L11_320 + A1_310
		return L11_320
	end
	L11_320 = L0_0
	L11_320 = L11_320(2, -10 * A0_309)
	L11_320 = A4_313 * L11_320
	local L8_317 = L8_317
	local L10_319 = (A0_309 * A3_312 - L6_315) * (2 * L3_3)
	L10_319 = L10_319 / A5_314
	L8_317 = L8_317(L10_319)
	L11_320 = L11_320 * L8_317
	L11_320 = L11_320 * 0.5
	L11_320 = L11_320 + A2_311
	L11_320 = L11_320 + A1_310
	return L11_320
end
function L40_40(A0_321, A1_322, A2_323, A3_324, A4_325, A5_326)
	if A0_321 < A3_324 / 2 then
		return _ENV(A0_321 * 2, A1_322, A2_323 / 2, A3_324, A4_325, A5_326)
	end
	local L7_327 = L7_327
	local L8_328 = L8_328
	local L9_329 = L9_329
	local L10_330 = L10_330
	local L11_331 = L11_331
	do return L7_327(L8_328, L9_329, L10_330, L11_331, A4_325, A5_326) end
	local L12_332 = L12_332
end
function L41_41(A0_333, A1_334, A2_335, A3_336, A4_337)
	local L5_338, L6_339
	if not A4_337 then
		A4_337 = 1.70158
	end
	A0_333 = A0_333 / A3_336
	L5_338 = A2_335 * A0_333
	L5_338 = L5_338 * A0_333
	L6_339 = A4_337 + 1
	L6_339 = L6_339 * A0_333
	L6_339 = L6_339 - A4_337
	L5_338 = L5_338 * L6_339
	L5_338 = L5_338 + A1_334
	return L5_338
end
function L42_42(A0_340, A1_341, A2_342, A3_343, A4_344)
	local L5_345, L6_346
	if not A4_344 then
		A4_344 = 1.70158
	end
	L5_345 = A0_340 / A3_343
	A0_340 = L5_345 - 1
	L5_345 = A0_340 * A0_340
	L6_346 = A4_344 + 1
	L6_346 = L6_346 * A0_340
	L6_346 = L6_346 + A4_344
	L5_345 = L5_345 * L6_346
	L5_345 = L5_345 + 1
	L5_345 = A2_342 * L5_345
	L5_345 = L5_345 + A1_341
	return L5_345
end
function L43_43(A0_347, A1_348, A2_349, A3_350, A4_351)
	local L5_352, L6_353, L7_354
	L5_352 = A4_351 or nil
	if not A4_351 then
		L5_352 = 1.70158
	end
	A4_351 = L5_352 * 1.525
	L5_352 = A0_347 / A3_350
	A0_347 = L5_352 * 2
	if A0_347 < 1 then
		L5_352 = A2_349 / 2
		L6_353 = A0_347 * A0_347
		L7_354 = A4_351 + 1
		L7_354 = L7_354 * A0_347
		L7_354 = L7_354 - A4_351
		L6_353 = L6_353 * L7_354
		L5_352 = L5_352 * L6_353
		L5_352 = L5_352 + A1_348
		return L5_352
	end
	A0_347 = A0_347 - 2
	L5_352 = A2_349 / 2
	L6_353 = A0_347 * A0_347
	L7_354 = A4_351 + 1
	L7_354 = L7_354 * A0_347
	L7_354 = L7_354 + A4_351
	L6_353 = L6_353 * L7_354
	L6_353 = L6_353 + 2
	L5_352 = L5_352 * L6_353
	L5_352 = L5_352 + A1_348
	return L5_352
end
function L44_44(A0_355, A1_356, A2_357, A3_358, A4_359)
	if A0_355 < A3_358 / 2 then
		return _ENV(A0_355 * 2, A1_356, A2_357 / 2, A3_358, A4_359)
	end
	local L6_360 = L6_360
	local L7_361 = L7_361
	local L8_362 = L8_362
	local L9_363 = L9_363
	do return L6_360(L7_361, L8_362, L9_363, A3_358, A4_359) end
	local L10_364 = L10_364
end
function L45_45(A0_365, A1_366, A2_367, A3_368)
	local L4_369
	A0_365 = A0_365 / A3_368
	if A0_365 < 0.36363636363636365 then
		L4_369 = 7.5625 * A0_365
		L4_369 = L4_369 * A0_365
		L4_369 = A2_367 * L4_369
		L4_369 = L4_369 + A1_366
		return L4_369
	end
	if A0_365 < 0.7272727272727273 then
		A0_365 = A0_365 - 0.5454545454545454
		L4_369 = 7.5625 * A0_365
		L4_369 = L4_369 * A0_365
		L4_369 = L4_369 + 0.75
		L4_369 = A2_367 * L4_369
		L4_369 = L4_369 + A1_366
		return L4_369
	elseif A0_365 < 0.9090909090909091 then
		A0_365 = A0_365 - 0.8181818181818182
		L4_369 = 7.5625 * A0_365
		L4_369 = L4_369 * A0_365
		L4_369 = L4_369 + 0.9375
		L4_369 = A2_367 * L4_369
		L4_369 = L4_369 + A1_366
		return L4_369
	end
	A0_365 = A0_365 - 0.9545454545454546
	L4_369 = 7.5625 * A0_365
	L4_369 = L4_369 * A0_365
	L4_369 = L4_369 + 0.984375
	L4_369 = A2_367 * L4_369
	L4_369 = L4_369 + A1_366
	return L4_369
end
function L46_46(A0_370, A1_371, A2_372, A3_373)
	local L4_374 = L4_374
	local L5_375 = L5_375
	local L6_376 = L6_376
	local L7_377 = L7_377
	local L4_374, L8_378 = L4_374(L5_375, L6_376, L7_377, A3_373), L8_378
	L4_374 = A2_372 - L4_374
	L4_374 = L4_374 + A1_371
	return L4_374
end
function L47_47(A0_379, A1_380, A2_381, A3_382)
	if A0_379 < A3_382 / 2 then
		return _ENV(A0_379 * 2, 0, A2_381, A3_382) * 0.5 + A1_380
	end
	local L4_383 = L4_383
	local L5_384 = L5_384
	local L6_385 = L6_385
	local L7_386 = L7_386
	local L4_383, L8_387 = L4_383(L5_384, L6_385, L7_386, A3_382), L8_387
	L4_383 = L4_383 * 0.5
	L5_384 = A2_381 * 0.5
	L4_383 = L4_383 + L5_384
	L4_383 = L4_383 + A1_380
	return L4_383
end
function L48_48(A0_388, A1_389, A2_390, A3_391)
	if A0_388 < A3_391 / 2 then
		return _ENV(A0_388 * 2, A1_389, A2_390 / 2, A3_391)
	end
	local L5_392 = L5_392
	local L6_393 = L6_393
	local L7_394 = L7_394
	do return L5_392(L6_393, L7_394, A2_390 / 2, A3_391) end
	local L8_395 = L8_395
end
L49_49 = tween
L50_50 = {}
L50_50.linear = L7_7
L50_50.inQuad = L8_8
L50_50.outQuad = L9_9
L50_50.inOutQuad = L10_10
L50_50.outInQuad = L11_11
L50_50.inCubic = L12_12
L50_50.outCubic = L13_13
L50_50.inOutCubic = L14_14
L50_50.outInCubic = L15_15
L50_50.inQuart = L16_16
L50_50.outQuart = L17_17
L50_50.inOutQuart = L18_18
L50_50.outInQuart = L19_19
L50_50.inQuint = L20_20
L50_50.outQuint = L21_21
L50_50.inOutQuint = L22_22
L50_50.outInQuint = L23_23
L50_50.inSine = L24_24
L50_50.outSine = L25_25
L50_50.inOutSine = L26_26
L50_50.outInSine = L27_27
L50_50.inExpo = L28_28
L50_50.outExpo = L29_29
L50_50.inOutExpo = L30_30
L50_50.outInExpo = L31_31
L50_50.inCirc = L32_32
L50_50.outCirc = L33_33
L50_50.inOutCirc = L34_34
L50_50.outInCirc = L35_35
L50_50.inElastic = L37_37
L50_50.outElastic = L38_38
L50_50.inOutElastic = L39_39
L50_50.outInElastic = L40_40
L50_50.inBack = L41_41
L50_50.outBack = L42_42
L50_50.inOutBack = L43_43
L50_50.outInBack = L44_44
L50_50.inBounce = L46_46
L50_50.outBounce = L45_45
L50_50.inOutBounce = L47_47
L50_50.outInBounce = L48_48
L49_49.easing = L50_50
L49_49 = {}
L49_49.delay = true
L49_49.complete = true
L49_49.args = true
function L50_50(A0_396)
	local L1_397
	L1_397 = _ENV
	L1_397 = L1_397[A0_396]
	return L1_397
end
function L51_51(A0_398, A1_399, A2_400)
	local L7_405 = L7_405
	if not A2_400 then
		A2_400 = A1_399
	end
	L7_405 = getmetatable
	L8_406 = A1_399
	L7_405 = L7_405(L8_406)
	if L7_405 then
		L8_406 = getmetatable
		L8_406 = L8_406(A0_398)
		if L8_406 == nil then
			L8_406 = setmetatable
			L8_406(A0_398, L7_405)
		end
	end
	L8_406 = pairs
	L8_406, L5_403, _FOR_ = L8_406(A1_399)
	for _FORV_7_, _FORV_8_ in L8_406, L5_403, _FOR_ do
		if _FORV_7_ and _FORV_8_ and not _ENV(_FORV_7_) then
			if type(_FORV_8_) == "table" then
				A0_398[_FORV_7_] = L51_51({}, _FORV_8_, A2_400[_FORV_7_])
				break -- pseudo-goto
			end
			repeat
				if type(_FORV_8_) == "number" then
					A0_398[_FORV_7_] = A2_400[_FORV_7_]
				end
			until true
		end
	end
	return A0_398
end
function L52_52(A0_411, A1_412, A2_413)
	local L3_414, L4_415
	if not A2_413 then
		L3_414 = {}
		A2_413 = L3_414
	end
	L3_414 = nil
	L4_415 = nil
	L7_418 = pairs
	L8_419 = A1_412
	L7_418, L8_419, L9_420 = L7_418(L8_419)
	for _FORV_8_, _FORV_9_ in L7_418, L8_419, L9_420 do
		L3_414, L4_415 = type(_FORV_9_), _ENV({}, A2_413)
		table.insert(L4_415, tostring(_FORV_8_))
		if L3_414 == "number" then
			assert(type(A0_411[_FORV_8_]) == "number", "Parameter '" .. table.concat(L4_415, "/") .. "' is missing from subject or isn't a number")
		elseif L3_414 == "table" then
			L52_52(A0_411[_FORV_8_], _FORV_9_, L4_415)
		else
			local L15_426 = L15_426
			local L14_425 = L14_425
			L15_426(L14_425, "Parameter '" .. table.concat(L4_415, "/") .. "' must be a number or table of numbers")
		end
	end
end
function L53_53(A0_427, A1_428, A2_429, A3_430)
	assert(type(A0_427) == "number" and 0 < A0_427, "duration must be a positive number. Was " .. tostring(A0_427))
	local L4_431 = L4_431
	L4_431 = L4_431(A1_428)
	assert(L4_431 == "table" or L4_431 == "userdata", "subject must be a table or userdata. Was " .. tostring(A1_428))
	assert(type(A2_429) == "table", "target must be a table. Was " .. tostring(A2_429))
	local L5_432 = L5_432
	local L6_433 = L6_433
	local L9_436 = L9_436
	L9_436 = L9_436 .. tostring(A3_430)
	L5_432(L6_433, L9_436)
end
function L54_54(A0_437)
	if not A0_437 then
		A0_437 = "linear"
	end
	local L1_438 = L1_438
	L1_438 = L1_438(A0_437)
	if L1_438 == "string" then
		L1_438 = A0_437
		A0_437 = tween.easing[L1_438]
		if type(A0_437) ~= "function" then
			local L2_439 = L2_439
			local L4_441 = L4_441
			local L4_441, L5_442 = L4_441 .. L1_438 .. "' is invalid", L5_442
			L2_439(L4_441)
		end
	end
	return A0_437
end
function L55_55(A0_443, A1_444, A2_445, A3_446, A4_447, A5_448)
	local L6_449, L7_450, L8_451, L9_452
	L12_455 = A1_444
	L10_453, L12_455, L13_456 = L10_453(L12_455)
	for L14_457, _FORV_14_ in L10_453, L12_455, L13_456 do
		if not _ENV(L14_457) then
			if type(_FORV_14_) == "table" then
				L55_55(A0_443[L14_457], _FORV_14_, A2_445[L14_457], A3_446, A4_447, A5_448)
				local L21_464 = L21_464
				break -- pseudo-goto
			end
			L21_464 = type
			L21_464 = L21_464(_FORV_14_)
			if L21_464 == "number" then
				L21_464 = A3_446
				L7_450, L8_451, L9_452 = A2_445[L14_457], _FORV_14_ - A2_445[L14_457], A4_447
				L6_449 = L21_464
				L21_464 = A5_448
				local L17_460 = L17_460
				local L18_461 = L18_461
				local L21_464, L19_462 = L21_464(L17_460, L18_461, L8_451, L9_452), L19_462
				repeat
					A0_443[L14_457] = L21_464
				until true
			end
		end
	end
end
L56_56 = {}
L57_57 = {}
L57_57.__index = L56_56
function L58_58(A0_465, A1_466)
	assert(type(A1_466) == "number", "clock must be a positive number or 0")
	A0_465.clock = A1_466
	if A0_465.clock <= 0 then
		A0_465.clock = 0
		_ENV(A0_465.subject, A0_465.initial)
	elseif A0_465.clock >= A0_465.duration then
		A0_465.clock = A0_465.duration
		_ENV(A0_465.subject, A0_465.target)
	else
		local L2_467 = L2_467
		local L3_468 = L3_468
		local L4_469 = L4_469
		local L5_470 = L5_470
		local L6_471 = L6_471
		local L7_472 = L7_472
		L2_467(L3_468, L4_469, L5_470, L6_471, L7_472, A0_465.easing)
		local L8_473 = L8_473
	end
	L2_467 = A0_465.clock
	L3_468 = A0_465.duration
	L2_467 = L2_467 >= L3_468
	return L2_467
end
L56_56.set = L58_58
function L58_58(A0_474)
	do return A0_474:set(0) end
	local L3_475 = L3_475
end
L56_56.reset = L58_58
function L58_58(A0_476, A1_477)
	assert(type(A1_477) == "number", "dt must be a number")
	local L2_478, L3_479 = L2_478, L3_479
	do return L2_478(L3_479, A0_476.clock + A1_477) end
	local L4_480 = L4_480
end
L56_56.update = L58_58
L58_58 = tween
function L59_59(A0_481, A1_482, A2_483, A3_484)
	A3_484 = _ENV(A3_484)
	L53_53(A0_481, A1_482, A2_483, A3_484)
	local L4_485 = L4_485
	;({}).duration = A0_481
	;({}).subject = A1_482
	;({}).target = A2_483
	;({}).easing = A3_484
	local L7_488 = L7_488
	local L8_489 = L8_489
	local L8_489, L9_490 = L8_489({}, A2_483, A1_482), L9_490
	L7_488.initial = L8_489
	L7_488.clock = 0
	L8_489 = L57_57
	return L4_485(L7_488, L8_489)
end
L58_58.new = L59_59
