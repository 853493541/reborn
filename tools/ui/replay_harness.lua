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
-- forward declaration: the permissive data proxy is defined below but is needed
-- by the UI section proxies (property sub-objects).
local proxy
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
  local selfProxy
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

  selfProxy = setmetatable({ __sectionName = sec.name }, {
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
        -- property read (camelCase/sz/dw/n): numeric authored value or 0. Lua 5.1
        -- order comparisons between different types error regardless of
        -- metatables, so UI numeric properties must stay numbers.
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
  return selfProxy
end

--------------------------------------------------------------------- engine stubs
local function clone(t)
  if type(t) ~= "table" then return t end
  local r = {}
  for k, v in pairs(t) do r[k] = clone(v) end
  return r
end
_G.clone = clone

proxy = function(name)
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

-- Window-chain calls: the scripts open/close other windows by path; record them
-- so the viewer can surface "opens X" (the engine would open the window).
local openedWindows = {}
_G.OpenWindow = function(path, ...)
  if type(path) == "string" then openedWindows[#openedWindows + 1] = path end
end
_G.CloseWindow = function(path, ...)
  if type(path) == "string" then openedWindows[#openedWindows + 1] = "-" .. path end
end

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
-- The scripts call module(name, ExportExternalLib) (Lua 5.1 loadlib 'module'),
-- which sets the chunk's environment to a fresh plain table. The engine's
-- option function wires the module's globals; we chain the environment to _G so
-- the engine stubs above stay visible.
local realModule = module
local _getfenv, _setmetatable, _getmetatable, _GLOBAL = getfenv, setmetatable, getmetatable, _G
if type(realModule) == "function" then
  module = function(name, ...)
    realModule(name, ...)
    -- realModule re-setfenv's THIS wrapper to the new module table, so use the
    -- captured builtins and target the caller (the script chunk).
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
  print("RESULT ERR chunk " .. tostring(err))
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
  print("RESULT ERR no-module")
  os.exit(4)
end

local root = proxyOf(rootSection)
_G.GetBigBagFrame = function() return root end
_G.this = root

local handler = function(e)
  return tostring(e) .. "\n" .. debug.traceback("", 2)
end
local ok2, err2 = true, nil
local entry = nil
for _, name in ipairs({ "OnFrameCreate", "OnLoad", "OnCreate", "Init", "OnOpen" }) do
  if type(mod[name]) == "function" then
    entry = name
    break
  end
end
if entry == nil then
  print("RESULT ERR no-entry")
  os.exit(5)
end
ok2, err2 = xpcall(function() return mod[entry](root) end, handler)

local out = assert(io.open(outPath, "w"))
out:write("section\tmethod\targs\n")
for i = 1, #log do
  out:write(log[i].sec .. "\t" .. log[i].method .. "\t" .. table.concat(log[i].args, "\t") .. "\n")
end
out:close()
print(string.format("RESULT %s mutations=%d opens=%d %s", ok2 and "OK" or "ERR", #log, #openedWindows,
  ok2 and "" or tostring(err2):gsub("[\r\n]+", " ")))
