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
  -- Anchor getters return an anchor table (s/r/x/y) in the engine; the generic
  -- Get* fallback returned 0, so `ComboPanel.tAnchor = self:GetDefaultAnchor()`
  -- stored a number and UpdateAnchor died indexing it (six windows).
  methods.GetDefaultAnchor = function() return proxy(sec.name .. ".GetDefaultAnchor") end
  methods.GetFrameAnchor = function() return proxy(sec.name .. ".GetFrameAnchor") end
  -- Page/child navigation: the engine returns controls, not numbers. GetActivePage
  -- resolves the INI's authored page= (the scripts compare page:GetName() to a
  -- known page id); GetFirstChild/GetNext terminate the child walk with nil.
  methods.GetActivePage = function()
    local page = sec.values.page or sec.values.Page
    if page and page ~= "" then return proxyOf(resolvePath(sec, page)) end
    return proxyOf(sec)
  end
  methods.GetFirstChild = function() return proxy(sec.name .. ".GetFirstChild") end
  methods.GetNext = function() return nil end
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
        -- property read (camelCase/sz/dw/n): numeric authored value or 0 (Lua 5.1
        -- mixed-type comparisons cannot use metatables; 0 keeps them numeric).
        if sec.values[k] ~= nil then return num(sec.values[k]) end
        -- Engine control properties: hXxx is the child control handle (the scripts
        -- index it: self.hBtnProperty:...), tXxx/pXxx are tables/pointers. Returning
        -- 0 aborted the replay ("attempt to index a number value"). Resolve the child
        -- section by its authored name (hBtnProperty -> BtnProperty); fall back to a
        -- permissive proxy when the INI has no such child.
        if k:match("^h") or k:match("^t") or k:match("^p") then
          local bare = k:sub(2)
          local child = nil
          for _, s in ipairs(order) do
            if s.parent == sec.name and s.name == bare then child = s break end
          end
          if child == nil and sections[bare] then child = sections[bare] end
          if child == nil and bare ~= "" then
            local cap = bare:sub(1, 1):upper() .. bare:sub(2)
            for _, s in ipairs(order) do
              if s.parent == sec.name and s.name == cap then child = s break end
            end
            if child == nil and sections[cap] then child = sections[cap] end
          end
          if child ~= nil then return proxyOf(child) end
          local v = proxy(sec.name .. "." .. k)
          rawset(t, k, v)
          return v
        end
        return 0
      end
      local fn = methods[k]
      if fn then return fn end
      if type(k) == "string" and k:match("^_") then
        -- engine-assigned data fields (_AutoPosInfo, ...): tables, not methods;
        -- the generic fallback returned a function and the real base libs
        -- (InitFrameAutoPosInfo) indexed it.
        local v = proxy(sec.name .. "." .. k)
        rawset(t, k, v)
        return v
      end
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

-- Scalar engine getters: names that carry a "Get" and end in a numeric suffix
-- (counts/ids/indices/levels/scores/screens/rates). The scripts use them as
-- numeric loop bounds and in comparisons; returning a proxy/table errors in Lua
-- 5.1 ("'for' limit must be a number", "compare number with table"). Excluded on
-- purpose: Size (bag scripts continue through GetBoxSize arithmetic — returning 0
-- costs BigBagPanel ~200 mutations) and Time/Frame (GetTodayTime returns a
-- month/day table, GetMgFrame/GetGameFrame return frame objects; returning 0 broke
-- EditBox and LuckyMeeting).
local function numericGetter(k)
  if type(k) ~= "string" or not k:find("Get") then return false end
  return k:match("Count$") or k:match("Num$") or k:match("ID$") or k:match("Id$")
      or k:match("Index$") or k:match("Level$") or k:match("Score$") or k:match("Screen$")
      or k:match("Rate$") or k:match("Percent$") or k:match("Msg$")
end

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
          if k == "GetNext" or k == "GetNextSibling" then return function() return nil end end
          -- Scalar getters (counts/ids/indices/levels/scores/screens/times) are used
          -- as numeric loop bounds and comparisons; returning a proxy aborted the
          -- replay ("'for' limit must be a number" / "compare number with table").
          -- Size getters stay proxies: their results feed arithmetic the bag scripts
          -- continue through (BigBagPanel loses ~200 mutations if GetBoxSize returns
          -- 0). Other Get*/PascalCase names keep the callable proxy (they may return
          -- tables the scripts index).
          if numericGetter(k) then
            return function() return 0 end
          end
          -- Unknown PascalCase global: some are module tables the scripts index
          -- (Craft.Foo, BattleField.Bar) and some are functions (Craft.Foo()).
          -- A callable proxy serves both; a plain function aborted the scripts
          -- that indexed it ("attempt to index field 'Craft' (a function value)").
          local v = proxy(name .. "." .. k)
          rawset(t, k, v)
          return v
        end
        -- camelCase data field: the engine's Hungarian prefixes tell the type.
        if k:match("^is_") then
          -- API-table predicates (sns_sina.is_bind): boolean functions, not ints.
          return function() return false end
        end
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
      -- Numeric/other key: array indexing into a stubbed object (t[n] then pairs/
      -- index). Returning 0 aborted `pairs(t[n])`; a permissive element proxy keeps
      -- the walk alive.
      local v = proxy(name .. "[" .. tostring(k) .. "]")
      rawset(t, k, v)
      return v
    end,
    -- Multi-value returns: the engine's list getters return several lists
    -- (GetDungeonList -> 4) and scripts sort each; one proxy left the rest nil.
    __call = function(t, ...)
      return proxy(name .. "()"), proxy(name .. "()[2]"), proxy(name .. "()[3]"), proxy(name .. "()[4]")
    end,
    __add = function() return 0 end, __sub = function() return 0 end, __mul = function() return 0 end,
    __div = function() return 0 end, __mod = function() return 0 end, __pow = function() return 0 end,
    __unm = function() return 0 end, __lt = function() return false end, __le = function() return false end,
    __concat = function() return "" end, __len = function() return 0 end,
    __tostring = function() return name end,
  })
  return p
end
-- Window-chain calls: the scripts open/close other windows by module name via
-- Wnd/Station (Wnd.OpenWindow("BigBankPanel")) or the bare globals/helpers
-- (OpenBankPanel); record all forms so the viewer can surface/navigate "opens X".
local openedWindows = {}
local recordWindow = function(name, closing)
  if type(name) == "string" and name ~= "" then
    openedWindows[#openedWindows + 1] = (closing and "-" or "") .. name
  end
end
local function windowCall(a, b, closing)
  -- supports both Wnd.OpenWindow("X") and Wnd:OpenWindow("X")
  recordWindow(type(a) == "string" and a or b, closing)
end

setmetatable(_G, { __index = function(t, k)
  if type(k) == "string" and (k:match("Is%u") or k:match("^Has") or k:match("^Can")) then
    local f = function() return false end
    rawset(t, k, f)
    return f
  end
  if type(k) == "string" and (k:match("^Open") or k:match("^Close")) then
    -- engine window helpers (OpenBankPanel, CloseXxx...): record the intent
    local closing = k:match("^Close") ~= nil
    local f = function(...) recordWindow(k, closing) end
    rawset(t, k, f)
    return f
  end
  if numericGetter(k) then
    -- engine global scalar getters (GetAddTrainSkillCount, GVoiceBase_GetRequiredPlayerLevel):
    -- numeric results, used as loop bounds/comparisons.
    local f = function() return 0 end
    rawset(t, k, f)
    return f
  end
  if type(k) == "string" and k:match("^CAN_") then
    -- numeric threshold constants (CAN_HOT_POINT_SHOW): compared against levels.
    rawset(t, k, 0)
    return 0
  end
  local v = proxy("_G." .. tostring(k))
  rawset(t, k, v)
  return v
end })

INVENTORY_INDEX = { PACKAGE = 1, EQUIP = 2 }
EQUIPMENT_INVENTORY = { PACKAGE1 = 1, PACKAGE_MIBAO = 6 }
g_tStrings = setmetatable({}, { __index = function(t, k)
  -- The string table also carries authored data tables (Hungarian t* names,
  -- *_MENU/*_LIST entries): return a table proxy for those, "" for the rest.
  if type(k) == "string" and (k:match("^t") or k:match("_MENU$") or k:match("_LIST$") or k:match("_TAGS$")) then
    local v = proxy("g_tStrings." .. k)
    rawset(t, k, v)
    return v
  end
  return ""
end })
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

_G.Wnd = _G.Wnd or {}
_G.Wnd.OpenWindow = function(a, b) windowCall(a, b, false) end
_G.Wnd.CloseWindow = function(a, b) windowCall(a, b, true) end
_G.Station = _G.Station or {}
_G.Station.OpenWindow = function(a, b) windowCall(a, b, false) end
_G.Station.CloseWindow = function(a, b) windowCall(a, b, true) end
_G.OpenWindow = function(path, ...) recordWindow(path, false) end
_G.CloseWindow = function(path, ...) recordWindow(path, true) end
-- String helpers: the engine's wide-string utilities return the transformed string
-- (StringReplaceW) / a separator position or nil (StringFindW); proxies aborted
-- string.sub and spun the search loop.
_G.StringReplaceW = function(s, ...) return tostring(s or "") end
_G.StringFindW = function() return nil end
-- Date helpers: DateToTime returns the formatted time string the scripts gmatch
-- (a proxy aborted gmatch); GetCurrentTime is a number.
_G.DateToTime = function(...) return "0" end
_G.GetCurrentTime = function() return 0 end
-- String stdlib coercion: engine APIs return strings, our stubs are proxies; coerce
-- non-string first args so string.sub/find/gmatch/gsub keep working in replays.
do
  local wrap = function(name)
    local orig = string[name]
    if type(orig) == "function" then
      string[name] = function(s, ...)
        if type(s) ~= "string" and type(s) ~= "number" then s = tostring(s) end
        return orig(s, ...)
      end
    end
  end
  wrap("sub"); wrap("find"); wrap("gmatch"); wrap("gsub"); wrap("len"); wrap("byte")
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
local _getfenv, _setfenv, _setmetatable, _getmetatable, _GLOBAL = getfenv, setfenv, setmetatable, getmetatable, _G
local _type = type
if type(realModule) == "function" then
  module = function(name, ...)
    realModule(name, ...)
    -- Lua 5.1's module() setfenv's ITS caller (this wrapper) to the module table;
    -- the script chunk keeps its old env, so without the setfenv below every
    -- module global leaked into _G and the module table stayed empty - scripts
    -- reading their own module table (ArenaOpponent.Anchor, Craft, ...) then hit
    -- nil. Target the caller (the script chunk) with the wrapper's env (the
    -- module table) and chain it to _G so the engine stubs stay visible.
    local env = _getfenv(1)
    if _type(env) == "table" then
      local mt = _getmetatable(env)
      if mt == nil then
        _setmetatable(env, { __index = _GLOBAL })
      elseif mt.__index == nil then
        mt.__index = _GLOBAL
      end
      _setfenv(2, env)
    end
    return env
  end
end

local f = assert(loadfile(scriptPath))
-- Engine base scripts (module_info.xml load="true" data/lib modules, listed in
-- ui/engine_base.txt): load them before the window script so its globals are the
-- real ones - g_tTable/Table_* (table_defs.lua + table.lua), g_tStrings
-- (string.lua), VideoBase (video_base.lua). During this load module() keeps the
-- chunk in _G so the data globals stay global (the engine's ExportExternalLib net
-- effect). Missing manifest/files = stub-only mode (older checkouts).
do
  local src = debug.getinfo(1, "S").source
  local dir = src:match("^@(.+)[/\\][^/\\]+$") or "."
  local assets = dir .. "/../../ui-process-app/assets"
  -- KG_Table stub: the engine's C++ table loader. Serve the real UI table files
  -- (ui/Scheme/Case/*.txt|.tab, TSV with a GBK header + a Title descriptor from
  -- g_tTableFile) from the assets, so table.lua's loader path
  -- (g_tTable[key] = KG_Table.Load(path, title, mode)) yields real rows.
  local function splitTabs(line)
    local out = {}
    for field in (line .. "\t"):gmatch("([^\t]*)\t") do out[#out + 1] = field end
    return out
  end
  local function loadTableFile(logicalPath, title)
    local rel = logicalPath:gsub("\\", "/"):gsub("^/", "")
    local file = io.open(assets .. "/" .. rel, "rb")
    if not file then return nil end
    local rows, header, lineNo = {}, nil, 0
    for line in file:lines() do
      lineNo = lineNo + 1
      line = line:gsub("\r$", "")
      if lineNo == 1 then
        header = splitTabs(line)
      elseif line ~= "" then
        local cols = splitTabs(line)
        local row = {}
        local n = (type(title) == "table" and #title) or #header
        for i = 1, n do
          local desc = type(title) == "table" and title[i] or nil
          local name = (type(desc) == "table" and desc.t) or header[i]
          local ftype = type(desc) == "table" and desc.f or nil
          local raw = cols[i] or ""
          local val
          if ftype == "s" then val = raw
          elseif ftype == "b" then val = (raw ~= "" and raw ~= "0")
          else val = tonumber(raw) or 0 end  -- engine typed columns default to 0
          if name then row[name] = val end
        end
        rows[#rows + 1] = row
      end
    end
    file:close()
    return rows
  end
  local function makeTable(desc)
    local real = nil
    local keyField = (type(desc.Title) == "table" and type(desc.Title[1]) == "table" and desc.Title[1].t) or nil
    local function ensure()
      if real == nil then real = { __rows = loadTableFile(desc.Path, desc.Title) or {} } end
      return real
    end
    local obj = {}
    obj.GetRowCount = function() return #ensure().__rows end
    obj.GetRow = function(_, i) return ensure().__rows[i] end
    obj.GetRowByIndex = function(_, i) return ensure().__rows[i] end
    -- The engine table's key lookup (Table_Get* wrappers call g_tTable.X:Search(key)).
    obj.Search = function(_, key)
      local rows = ensure().__rows
      if keyField then
        for i = 1, #rows do
          if rows[i][keyField] == key then return rows[i] end
        end
      end
      return nil
    end
    obj.GetRowByKey = obj.Search
    return setmetatable(obj, { __index = function(t, k)
      local v = ensure()[k]
      if v == nil and type(k) == "string" and k:match("^%u") then
        v = function() return nil end  -- unknown engine table method: neutral
      end
      rawset(t, k, v)
      return v
    end })
  end
  if type(rawget(_G, "KG_Table")) ~= "table" then
    _G.FILE_OPEN_MODE = _G.FILE_OPEN_MODE or { NORMAL = 0, CACHE = 1 }
    _G.KG_Table = {
      Load = function(path, title, mode) return makeTable({ Path = path, Title = title }) end,
    }
  end
  local mf = io.open(assets .. "/ui/engine_base.txt", "r")
  if mf then
    local savedModule = module
    module = function(name, ...)
      _setfenv(2, _GLOBAL)
      return _GLOBAL
    end
    local loaded, failed = 0, 0
    local failedNames = {}
    for line in mf:lines() do
      line = line:gsub("^%s+", ""):gsub("%s+$", "")
      if line ~= "" then
        local f2 = loadfile(assets .. "/" .. line:gsub("\\", "/"))
        if f2 then
          if pcall(f2) then loaded = loaded + 1 else failed = failed + 1; failedNames[#failedNames + 1] = line end
        else
          failed = failed + 1
          failedNames[#failedNames + 1] = line
        end
      end
    end
    mf:close()
    module = savedModule
    -- Replace file-backed descriptor entries with lazy table objects (the engine
    -- loads all of g_tTableFile at startup; here each table parses on first use).
    local gtf, gt = rawget(_G, "g_tTableFile"), rawget(_G, "g_tTable")
    local wrapped = 0
    if type(gtf) == "table" and type(gt) == "table" then
      for k, desc in pairs(gtf) do
        if type(desc) == "table" and type(desc.Path) == "string" and desc.Path ~= "" then
          rawset(gt, k, makeTable(desc))
          wrapped = wrapped + 1
        end
      end
    end
    if os.getenv("RC_ENGINE_BASE_DEBUG") == "1" then
      io.stderr:write(string.format("engine_base loaded=%d failed=%d tables=%d\n", loaded, failed, wrapped))
      for i = 1, math.min(#failedNames, 12) do
        io.stderr:write("engine_base FAIL " .. failedNames[i] .. "\n")
      end
      io.stderr:write("engine_base raw g_tTable=" .. type(rawget(_G, "g_tTable"))
        .. " g_tStrings=" .. type(rawget(_G, "g_tStrings"))
        .. " VideoBase=" .. type(rawget(_G, "VideoBase")) .. "\n")
    end
  end
end

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
if os.getenv("RC_ENGINE_BASE_DEBUG") == "1" then
  io.stderr:write("pre-entry raw g_tTable=" .. type(rawget(_G, "g_tTable"))
    .. " Table_GetNewDungeonList=" .. type(rawget(_G, "Table_GetNewDungeonList"))
    .. " VideoBase=" .. type(rawget(_G, "VideoBase")) .. "\n")
end

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
