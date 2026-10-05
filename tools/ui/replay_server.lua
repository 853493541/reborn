-- UI runtime replay harness: run a compiled window script's OnFrameCreate against
-- the window's own INI tree and record every UI mutation.
--
-- Usage: lua32.exe replay_harness.lua <window.lua> <ModuleName|auto> <window.ini> <out.tsv>
--
-- The extracted scripts are standard Lua 5.1 bytecode and must run on a 32-bit PUC
-- Lua 5.1 (size_t=4 in the header); see docs/ui/UI_RUNTIME_REPLAY.md for the build.
-- The module chunk assigns its module table to a global (usually the file stem).

local scriptPath, modName, iniPath, outPath = arg[1], arg[2], arg[3], arg[4]

--------------------------------------------------------------------- INI model
local sections = {}
local order = {}
local function loadIni(path)
  local f = assert(io.open(path, "rb"))
  local cur = nil
  for line in f:lines() do
    local name = line:match("^%[([^%]]+)%]%s*$")
    if name then
      cur = { name = name, parent = nil, values = {} }
      sections[name] = cur
      order[#order + 1] = cur
    else
      local k, v = line:match("^([^=]+)=(.*)$")
      if k and cur then
        k = k:gsub("%s+$", "")
        if k == "._Parent" then cur.parent = v end
        cur.values[k] = v
      end
    end
  end
  f:close()
end
loadIni(iniPath)
local rootSection = order[1]
assert(rootSection, "no sections in " .. tostring(iniPath))

--------------------------------------------------------------------- UI proxies
local log = {}
local function num(v)
  if type(v) == "number" then return v end
  if type(v) == "string" then return tonumber(v) or 0 end
  return 0
end

local proxyOf
local function record(sec, method, args)
  local flat = {}
  for i = 1, math.min(#args, 4) do
    local a = args[i]
    if type(a) == "table" and a.__sectionName then a = "[" .. a.__sectionName .. "]"
    elseif type(a) == "table" then a = "{table}"
    else a = tostring(a) end
    flat[#flat + 1] = a
  end
  log[#log + 1] = { sec = sec and sec.name or "?", method = method, args = flat }
end

local function resolvePath(sec, path)
  if path == nil or path == "" then return sec end
  local first = tostring(path):match("^([^/]+)")
  if first == nil or first == "" then return sec end
  for _, s in ipairs(order) do
    if s.parent == sec.name and s.name == first then return s end
  end
  if sections[first] then return sections[first] end
  local last = tostring(path):match("([^/]+)$")
  if last and sections[last] then return sections[last] end
  return sec
end

proxyOf = function(sec)
  local methods = {}
  local proxy
  methods.Lookup = function(self, a, b)
    record(sec, "Lookup", { a, b })
    return proxyOf(resolvePath(sec, a))
  end
  methods.GetName = function() return sec.name end
  methods.GetRoot = function() return proxyOf(rootSection) end
  methods.GetParent = function()
    return sec.parent and sections[sec.parent] and proxyOf(sections[sec.parent]) or proxyOf(rootSection)
  end
  methods.GetSize = function()
    record(sec, "GetSize", {})
    return num(sec.values.Width), num(sec.values.Height)
  end
  methods.GetW = function() return num(sec.values.Width) end
  methods.GetH = function() return num(sec.values.Height) end
  methods.GetAbsPos = function() return num(sec.values.Left), num(sec.values.Top) end
  methods.GetRelPos = function() return num(sec.values.Left), num(sec.values.Top) end
  methods.IsVisible = function()
    if sec.visible == nil then return true end
    return sec.visible
  end
  methods.GetFrame = function() return num(sec.values.Frame) end
  methods.IsCheckBoxChecked = function() return sec.checked == true end
  methods.IsOpened = function() return true end
  methods.GetText = function() return sec.values["$Text"] or "" end
  methods.GetItemCount = function() return 0 end
  methods.GetData = function() return 0 end

  proxy = setmetatable({ __sectionName = sec.name }, {
    __lt = function() return false end,
    __le = function() return false end,
    __add = function() return 0 end,
    __sub = function() return 0 end,
    __mul = function() return 0 end,
    __div = function() return 0 end,
    __mod = function() return 0 end,
    __pow = function() return 0 end,
    __unm = function() return 0 end,
    __concat = function() return "" end,
    __len = function() return 0 end,
    __tostring = function() return sec.name end,
    __index = function(t, k)
      if type(k) == "string" and k:match("^%l") then
        -- property read (camelCase/sz/dw/n): authored value or 0 so numeric
        -- comparisons in the script behave; rawset writes still win.
        if sec.values[k] ~= nil then return num(sec.values[k]) end
        return 0
      end
      local fn = methods[k]
      if fn then return fn end
      return function(self, ...)
        local args = { ... }
        record(sec, k, args)
        if k == "Show" or k == "SetVisible" then
          sec.visible = (k == "Show") and true or (args[1] ~= false)
        elseif k == "Hide" then sec.visible = false
        elseif k == "SetSize" then
          if args[1] then sec.values.Width = tostring(args[1]) end
          if args[2] then sec.values.Height = tostring(args[2]) end
        elseif k == "Check" then sec.checked = true
        elseif k == "UnCheck" then sec.checked = false
        end
        if k == "Lookup" then return proxyOf(resolvePath(sec, args[1])) end
        if k:match("^Get") or k:match("^Is") then return 0 end
        return self
      end
    end,
  })
  return proxy
end

--------------------------------------------------------------------- engine stubs
local function clone(t)
  if type(t) ~= "table" then return t end
  local r = {}
  for k, v in pairs(t) do r[k] = clone(v) end
  return r
end
_G.clone = clone

local function proxy(name)
  local p = {}
  setmetatable(p, {
    __index = function(t, k)
      if type(k) == "string" then
        if k:match("^[A-Z][A-Z0-9_]*$") then
          local v = proxy(name .. "." .. k)
          rawset(t, k, v)
          return v
        end
        if k:match("^%u") then
          -- PascalCase: engine/API function. Predicates are false (a normal
          -- session is not on a limited/MOBA map, has no extended package, ...).
          if k:match("Is%u") or k:match("^Has") or k:match("^Can") then
            return function() return false end
          end
          if k == "GetSize" then return function() return 0, 0 end end
          if k == "GetW" or k == "GetH" then return function() return 0 end end
          if k == "GetAbsPos" or k == "GetRelPos" then return function() return 0, 0 end end
          if k == "IsVisible" or k == "IsOpened" then return function() return false end end
          return function(...) return proxy(name .. "." .. k .. "()") end
        end
        -- camelCase data field: the engine's Hungarian prefixes tell the type.
        if k:match("^b") then return false end
        if k:match("^s") then return "" end
        if k:match("^t") or k:match("^h") or k:match("^p") then
          local v = proxy(name .. "." .. k)
          rawset(t, k, v)
          return v
        end
        if k:match("^n") or k:match("^d") or k:match("^i") or k:match("^f")
           or k == "x" or k == "y" or k == "u" then
          return 0
        end
        -- unknown field: a permissive proxy (indexable/callable) rather than 0, so
        -- container-ish fields the scripts index (player.attribute.*) keep working.
        local v = proxy(name .. "." .. k)
        rawset(t, k, v)
        return v
      end
      return 0
    end,
    __call = function(t, ...) return proxy(name .. "()") end,
    __add = function() return 0 end, __sub = function() return 0 end, __mul = function() return 0 end,
    __div = function() return 0 end, __mod = function() return 0 end, __pow = function() return 0 end,
    __unm = function() return 0 end, __lt = function() return false end, __le = function() return false end,
    __concat = function() return "" end, __len = function() return 0 end,
    __tostring = function() return name end,
  })
  return p
end
setmetatable(_G, { __index = function(t, k)
  if type(k) == "string" and (k:match("Is%u") or k:match("^Has") or k:match("^Can")) then
    local f = function() return false end
    rawset(t, k, f)
    return f
  end
  local v = proxy("_G." .. tostring(k))
  rawset(t, k, v)
  return v
end })

INVENTORY_INDEX = { PACKAGE = 1, EQUIP = 2 }
EQUIPMENT_INVENTORY = { PACKAGE1 = 1, PACKAGE_MIBAO = 6 }
g_tStrings = setmetatable({}, { __index = function() return "" end })
local permissiveMt = {
  __index = function(t, k) local v = proxy("tbl." .. tostring(k)); rawset(t, k, v); return v end,
  __lt = function() return false end,
  __le = function() return false end,
  __add = function() return 0 end,
  __sub = function() return 0 end,
  __concat = function() return "" end,
  __call = function() return 0 end,
}
setmetatable(INVENTORY_INDEX, permissiveMt)
setmetatable(EQUIPMENT_INVENTORY, permissiveMt)

-- Lua 5.1 resolves comparison/arith metamethods on the LEFT operand only; give the
-- number type a metatable so a stubbed proxy on the right never aborts a replay
-- (mixed number/table comparisons and arithmetic return neutral values).
pcall(function()
  debug.setmetatable(0, {
    __lt = function() return false end,
    __le = function() return false end,
    __add = function() return 0 end,
    __sub = function() return 0 end,
    __mul = function() return 0 end,
    __div = function() return 0 end,
    __mod = function() return 0 end,
    __pow = function() return 0 end,
  })
end)

--------------------------------------------------------------------- run
-- Interactive state server: same shim as replay_harness.lua, but keeps the module
-- loaded and dispatches UI events from stdin. The viewer starts one server per
-- window and sends EVENT lines on clicks; the mutation delta comes back as TSV.
--
-- Usage: lua32.exe replay_server.lua <window.lua> <ModuleName|auto> <window.ini>
-- Commands:
--   EVENT <section> <handler> [arg1] [arg2]   dispatch a script handler
--   STATE                                     dump the full mutation log
--   QUIT
-- Output: "READY handlers=..." then per command "RESULT <ok> <err>", MUT lines, "END".

local realModule = module
local _getfenv, _setmetatable, _getmetatable, _GLOBAL = getfenv, setmetatable, getmetatable, _G
if type(realModule) == "function" then
  module = function(name, ...)
    realModule(name, ...)
    local env = _getfenv(2)
    local mt = _getmetatable(env)
    if mt == nil then
      _setmetatable(env, { __index = _GLOBAL })
    elseif mt.__index == nil then
      mt.__index = _GLOBAL
    end
    return env
  end
end

local f = assert(loadfile(scriptPath))
local ok, err = pcall(f)
if not ok then
  print("READY error=" .. tostring(err))
  os.exit(3)
end

local mod = nil
if modName ~= "auto" and _G[modName] ~= nil and type(_G[modName]) == "table" then
  mod = _G[modName]
else
  local stem = tostring(scriptPath):match("([^/\\]+)%.lua$") or ""
  local best, bestCount = nil, 0
  for k, v in pairs(_G) do
    if type(v) == "table" and type(k) == "string" then
      local nf = 0
      for _, fv in pairs(v) do if type(fv) == "function" then nf = nf + 1 end end
      if nf >= 3 then
        if k:lower() == stem:lower() then best, bestCount = v, nf; break end
        if nf > bestCount then best, bestCount = v, nf end
      end
    end
  end
  mod = best
end
if type(mod) ~= "table" then
  print("READY error=no-module")
  os.exit(4)
end

local root = proxyOf(rootSection)
_G.GetBigBagFrame = function() return root end
_G.this = root

-- initial state (same entry chain as the batch harness)
local handlers = {}
for k, v in pairs(mod) do
  if type(v) == "function" and k:match("^On") then handlers[#handlers + 1] = k end
end
table.sort(handlers)
print("READY handlers=" .. table.concat(handlers, ","))
io.flush()

local initialEntry = nil
for _, name in ipairs({ "OnFrameCreate", "OnLoad", "OnCreate", "Init", "OnOpen" }) do
  if type(mod[name]) == "function" then initialEntry = name; break end
end
if initialEntry then pcall(function() return mod[initialEntry](root) end) end

local function dumpFrom(fromIndex)
  for i = fromIndex + 1, #log do
    io.write(log[i].sec .. "\t" .. log[i].method .. "\t" .. table.concat(log[i].args, "\t") .. "\n")
  end
end

for line in io.lines() do
  local cmd, rest = line:match("^(%S+)%s*(.*)$")
  if cmd == "EVENT" then
    local secName, handler, a1, a2 = rest:match("^(%S+)%s+(%S+)%s*(%S*)%s*(%S*)$")
    local before = #log
    local target = proxyOf(resolvePath(rootSection, secName))
    _G.this = root
    _G.arg1 = target
    _G.arg2 = (a1 ~= "" and a1 ~= nil) and a1 or nil
    _G.arg3 = (a2 ~= "" and a2 ~= nil) and a2 or nil
    local fn = mod[handler]
    if type(fn) ~= "function" then
      print("RESULT ERR no-handler " .. tostring(handler))
    else
      if handler == "OnCheckBoxCheck" then
        -- the engine toggles the checkbox before firing the event
        local sname = target.__sectionName
        local s = sname and sections[sname]
        if s then s.checked = not (s.checked == true) end
      end
      local ok2, err2 = pcall(function() return fn(root, target, _G.arg2, _G.arg3) end)
      print("RESULT " .. (ok2 and "OK" or "ERR") .. " " .. tostring(err2))
    end
    dumpFrom(before)
    print("END")
  elseif cmd == "STATE" then
    dumpFrom(0)
    print("END")
  elseif cmd == "QUIT" then
    break
  end
  io.flush()
end
