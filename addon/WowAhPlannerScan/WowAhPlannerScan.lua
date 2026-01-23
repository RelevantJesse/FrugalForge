local ADDON_NAME = ...

WowAhPlannerScanDB = WowAhPlannerScanDB or {}

local function Print(msg)
  DEFAULT_CHAT_FRAME:AddMessage("|cff7dd3fcWowAhPlannerScan|r: " .. tostring(msg))
end

local state = {
  running = false,
  queue = {},
  currentItemId = nil,
  page = 0,
  maxPages = 20,
  delaySeconds = 0.7,
  awaiting = false,
  prices = {},
  startedAt = nil,
}

local function IsAtAuctionHouse()
  return AuctionFrame and AuctionFrame:IsShown()
end

local function ParseItemIdFromLink(link)
  if not link then return nil end
  local id = string.match(link, "item:(%d+):")
  return id and tonumber(id) or nil
end

local function CanQuery()
  if not CanSendAuctionQuery then
    return true
  end
  return CanSendAuctionQuery()
end

local function EnsureItemName(itemId)
  local name = GetItemInfo(itemId)
  if name then return name end
  if GameTooltip then
    GameTooltip:SetOwner(UIParent, "ANCHOR_NONE")
    GameTooltip:SetHyperlink("item:" .. itemId)
    GameTooltip:Hide()
  end
  return GetItemInfo(itemId)
end

local function StartSnapshot()
  state.prices = {}
  state.startedAt = time()
end

local function FinishSnapshot()
  local snapshot = {
    schema = "wowahplanner-scan-v1",
    snapshotTimestampUtc = date("!%Y-%m-%dT%H:%M:%SZ", time()),
    realmName = GetRealmName(),
    faction = UnitFactionGroup("player"),
    generatedAtEpochUtc = time(),
    prices = {},
  }

  for itemId, entry in pairs(state.prices) do
    table.insert(snapshot.prices, {
      itemId = itemId,
      minUnitBuyoutCopper = entry.minUnitBuyoutCopper,
      totalQuantity = entry.totalQuantity,
    })
  end

  table.sort(snapshot.prices, function(a, b) return a.itemId < b.itemId end)

  WowAhPlannerScanDB.lastSnapshot = snapshot
  WowAhPlannerScanDB.lastGeneratedAtEpochUtc = snapshot.generatedAtEpochUtc

  state.running = false
  state.currentItemId = nil
  state.queue = {}
  state.awaiting = false

  Print("Scan complete. Use /wahpscan export to copy JSON.")
end

local function BuildExportJson()
  local snap = WowAhPlannerScanDB.lastSnapshot
  if not snap then return nil end

  local parts = {}
  table.insert(parts, '{"schema":"wowahplanner-scan-v1"')
  table.insert(parts, ',"snapshotTimestampUtc":"' .. snap.snapshotTimestampUtc .. '"')
  table.insert(parts, ',"realmName":"' .. (snap.realmName or "") .. '"')
  table.insert(parts, ',"faction":"' .. (snap.faction or "") .. '"')
  table.insert(parts, ',"prices":[')

  for i, p in ipairs(snap.prices or {}) do
    if i > 1 then table.insert(parts, ",") end
    table.insert(parts, string.format('{"itemId":%d,"minUnitBuyoutCopper":%d,"totalQuantity":%d}', p.itemId, p.minUnitBuyoutCopper or 0, p.totalQuantity or 0))
  end

  table.insert(parts, "]}")
  return table.concat(parts)
end

local exportFrame
local function ShowExportFrame()
  local json = BuildExportJson()
  if not json then
    Print("No snapshot found yet. Run /wahpscan start first.")
    return
  end

  if not exportFrame then
    exportFrame = CreateFrame("Frame", "WowAhPlannerScanExportFrame", UIParent, "BackdropTemplate")
    exportFrame:SetSize(700, 450)
    exportFrame:SetPoint("CENTER")
    exportFrame:SetMovable(true)
    exportFrame:EnableMouse(true)
    exportFrame:RegisterForDrag("LeftButton")
    exportFrame:SetScript("OnDragStart", exportFrame.StartMoving)
    exportFrame:SetScript("OnDragStop", exportFrame.StopMovingOrSizing)

    exportFrame:SetBackdrop({
      bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background",
      edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
      tile = true, tileSize = 32, edgeSize = 32,
      insets = { left = 8, right = 8, top = 8, bottom = 8 }
    })

    local title = exportFrame:CreateFontString(nil, "OVERLAY", "GameFontNormalLarge")
    title:SetPoint("TOP", 0, -16)
    title:SetText("WowAhPlannerScan Export")

    local close = CreateFrame("Button", nil, exportFrame, "UIPanelCloseButton")
    close:SetPoint("TOPRIGHT", -8, -8)

    local scroll = CreateFrame("ScrollFrame", nil, exportFrame, "UIPanelScrollFrameTemplate")
    scroll:SetPoint("TOPLEFT", 16, -48)
    scroll:SetPoint("BOTTOMRIGHT", -36, 16)

    local editBox = CreateFrame("EditBox", nil, scroll)
    editBox:SetMultiLine(true)
    editBox:SetFontObject(ChatFontNormal)
    editBox:SetWidth(640)
    editBox:SetAutoFocus(false)
    editBox:SetScript("OnEscapePressed", function() exportFrame:Hide() end)
    scroll:SetScrollChild(editBox)

    exportFrame.editBox = editBox
  end

  exportFrame.editBox:SetText(json)
  exportFrame.editBox:HighlightText()
  exportFrame:Show()
end

local function QueueItems()
  local targets = WowAhPlannerScan_TargetItemIds or {}
  if type(targets) ~= "table" then targets = {} end

  state.queue = {}
  for _, itemId in ipairs(targets) do
    if type(itemId) == "number" and itemId > 0 then
      table.insert(state.queue, itemId)
    end
  end

  Print("Queued " .. tostring(#state.queue) .. " items.")
end

local function QueryCurrentPage()
  if not state.currentItemId then return end
  if not IsAtAuctionHouse() then
    Print("Auction House is not open.")
    state.running = false
    return
  end
  if not CanQuery() then
    C_Timer.After(state.delaySeconds, QueryCurrentPage)
    return
  end

  local name = EnsureItemName(state.currentItemId)
  if not name then
    table.insert(state.queue, state.currentItemId)
    state.currentItemId = nil
    C_Timer.After(state.delaySeconds, NextItem)
    return
  end

  state.awaiting = true
  QueryAuctionItems(name, nil, nil, 0, 0, 0, state.page, false, nil, false, true)
end

local function NextItem()
  if not state.running then return end

  if #state.queue == 0 then
    FinishSnapshot()
    return
  end

  state.currentItemId = table.remove(state.queue, 1)
  state.page = 0
  state.awaiting = false
  QueryCurrentPage()
end

local function ProcessCurrentPage()
  if not state.currentItemId then return end

  local shown, total = GetNumAuctionItems("list")
  local itemId = state.currentItemId

  for i = 1, shown do
    local _, _, count, _, _, _, _, _, buyoutPrice = GetAuctionItemInfo("list", i)
    local link = GetAuctionItemLink("list", i)
    local id = ParseItemIdFromLink(link)

    if id == itemId and buyoutPrice and buyoutPrice > 0 and count and count > 0 then
      local unit = math.floor(buyoutPrice / count)
      local entry = state.prices[itemId]
      if not entry then
        entry = { minUnitBuyoutCopper = unit, totalQuantity = 0 }
        state.prices[itemId] = entry
      end
      if unit < entry.minUnitBuyoutCopper then
        entry.minUnitBuyoutCopper = unit
      end
      entry.totalQuantity = entry.totalQuantity + count
    end
  end

  local nextPageExists = total and total > (state.page + 1) * 50
  if nextPageExists and state.page < state.maxPages then
    state.page = state.page + 1
    C_Timer.After(state.delaySeconds, QueryCurrentPage)
  else
    state.currentItemId = nil
    C_Timer.After(state.delaySeconds, NextItem)
  end
end

local frame = CreateFrame("Frame")
frame:RegisterEvent("AUCTION_ITEM_LIST_UPDATE")
frame:SetScript("OnEvent", function(_, event)
  if event == "AUCTION_ITEM_LIST_UPDATE" and state.running and state.awaiting then
    state.awaiting = false
    ProcessCurrentPage()
  end
end)

SLASH_WOWAHPLANNERSCAN1 = "/wahpscan"
SlashCmdList["WOWAHPLANNERSCAN"] = function(msg)
  msg = string.lower(msg or "")

  if msg == "start" then
    if not IsAtAuctionHouse() then
      Print("Open the Auction House first.")
      return
    end
    QueueItems()
    if #state.queue == 0 then
      Print("No targets loaded. Generate targets.lua from the web app and load it as SavedVariables WowAhPlannerScan_TargetItemIds.")
      return
    end
    StartSnapshot()
    state.running = true
    Print("Starting scan...")
    NextItem()
    return
  end

  if msg == "stop" then
    state.running = false
    state.awaiting = false
    state.queue = {}
    state.currentItemId = nil
    Print("Stopped.")
    return
  end

  if msg == "status" then
    Print("running=" .. tostring(state.running) .. ", remaining=" .. tostring(#state.queue) .. ", current=" .. tostring(state.currentItemId))
    return
  end

  if msg == "export" then
    ShowExportFrame()
    return
  end

  Print("Commands: /wahpscan start | stop | status | export")
end
