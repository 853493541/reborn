-- UI runtime replay harness: run a compiled window script's OnFrameCreate against
-- the window's own INI tree and record every UI mutation.
-- Usage: lua32.exe harness.lua <window.lua> <ModuleName> <window.ini> <out.tsv>

local scriptPath, modName, iniPath, outPath = arg[1], arg[2], arg[3], arg[4]

--------------------------------------------------------------------- INI model
local sections = {}      -- name -> { parent=..., order=..., values={} }
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

local proxyOf -- forward
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
  -- children of sec first, then any section by name (engine Lookup is name-based)
  for _, s in ipairs(order) do
    if s.parent == sec.name and s.name == first then return s end
  end
  if sections[first] then return sections[first] end
  -- "A/B": try the full path's last segment too
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
        elseif k == "SetRelPos" or k == "SetAbsPos" or k == "SetPoint" then
          -- recorded; positions are viewer concerns
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
          -- ALL_CAPS: engine constant table (MAP_OPERATION_TYPE, ITEM_GENRE, ...)
          local v = proxy(name .. "." .. k)
          rawset(t, k, v)
          return v
        end
        if k:match("^%u") then
          -- PascalCase: engine/API function
          return function(...) return proxy(name .. "." .. k .. "()") end
        end
        -- camelCase/sz/dw/n: data field -> number (0) so numeric comparisons work
        return 0
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
setmetatable(_G, { __index = function(t, k) local v = proxy("_G." .. tostring(k)); rawset(t, k, v); return v end })

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

--------------------------------------------------------------------- run
local f = assert(loadfile(scriptPath))
local ok, err = pcall(f)
if not ok then print("chunk error: " .. tostring(err)); os.exit(3) end
local mod = _G[modName]
if type(mod) ~= "table" then print("module not found: " .. tostring(modName)); os.exit(4) end

local root = proxyOf(rootSection)
local function handler(e)
  local out = { tostring(e) }
  local i = 2
  while true do
    local info = debug.getinfo(i, "nSf")
    if not info then break end
    out[#out + 1] = string.format("  [%d] %s %s:%s", i, info.name or "?",
      tostring(info.short_src), tostring(info.currentline))
    if i == 2 and info.func then
      if os.getenv("DUMP_FUNC") then
        local df = io.open(os.getenv("DUMP_FUNC"), "wb")
        df:write(string.dump(info.func))
        df:close()
      end
      out[#out + 1] = "      func=" .. tostring(info.func)
      local n1, v1 = debug.getupvalue(info.func, 1)
      out[#out + 1] = "      up1=[" .. tostring(n1) .. "]=" .. tostring(v1)
      for u = 1, 30 do
        local n, v = debug.getupvalue(info.func, u)
        if not n then break end
        out[#out + 1] = string.format("      upvalue %s = %s (%s)", tostring(n), tostring(v), type(v))
      end
    end
    i = i + 1
  end
  return table.concat(out, "\n")
end
if type(mod.OnFrameCreate) == "function" then
  _G.this = root
  local ok2, err2 = xpcall(function() return mod.OnFrameCreate(root) end, handler)
  print("OnFrameCreate ok=" .. tostring(ok2) .. " err=" .. tostring(err2))
else
  print("no OnFrameCreate")
end

local out = assert(io.open(outPath, "w"))
out:write("section\tmethod\targs\n")
for i = 1, #log do
  out:write(log[i].sec .. "\t" .. log[i].method .. "\t" .. table.concat(log[i].args, "\t") .. "\n")
end
out:close()
print("mutations: " .. #log .. " -> " .. outPath)
