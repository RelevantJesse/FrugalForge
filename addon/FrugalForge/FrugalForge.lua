local ADDON_NAME = ...

local function ts()
  return date("%Y-%m-%d %H:%M:%S", time())
end

local function ensureDb()
  FrugalForgeDB = FrugalForgeDB or {}
  FrugalForgeDB.settings = FrugalForgeDB.settings or {
    warnStaleHours = 12,
    priceRank = 3, -- 1=min, 2=median, 3=most recent
    showPanelOnAuctionHouse = true,
    maxSkillDelta = 100,
    selectedProfessionId = nil,
  }
  FrugalForgeDB.lastPlan = FrugalForgeDB.lastPlan or nil
end

local function latestSnapshot()
  local db = _G.ProfessionLevelerScanDB or _G.WowAhPlannerScanDB
  if type(db) ~= "table" then return nil end
  local snap = db.lastSnapshot
  if type(snap) ~= "table" then return nil end
  return snap
end

local function latestOwned()
  local db = _G.ProfessionLevelerScanDB or _G.WowAhPlannerScanDB
  if type(db) ~= "table" then return nil end
  local owned = db.lastOwnedSnapshot
  if type(owned) ~= "table" then return nil end
  return owned
end

local function parseIsoTimestampToEpoch(ts)
  if type(ts) ~= "string" then return nil end
  local y, m, d, hh, mm, ss = string.match(ts, "^(%d+)%-(%d+)%-(%d+)T(%d+):(%d+):(%d+)Z$")
  if not y then return nil end
  return time({ year = tonumber(y), month = tonumber(m), day = tonumber(d), hour = tonumber(hh), min = tonumber(mm), sec = tonumber(ss) })
end

local function getSnapshotEpoch(snap)
  if type(snap) ~= "table" then return nil end
  if type(snap.generatedAtEpochUtc) == "number" then return snap.generatedAtEpochUtc end
  if type(snap.snapshotTimestampUtc) == "string" then
    return parseIsoTimestampToEpoch(snap.snapshotTimestampUtc)
  end
  return nil
end

local function fmtAge(epochUtc)
  if type(epochUtc) ~= "number" then return "unknown" end
  local delta = time() - epochUtc
  if delta < 0 then delta = 0 end
  local hours = math.floor(delta / 3600)
  local mins = math.floor((delta % 3600) / 60)
  return string.format("%dh %dm ago", hours, mins)
end

local function hoursSince(epochUtc)
  if type(epochUtc) ~= "number" then return nil end
  local delta = time() - epochUtc
  if delta < 0 then delta = 0 end
  return delta / 3600
end

local function colorize(text, color)
  return color .. text .. "|r"
end

local function normalizeProfessionName(name)
  if type(name) ~= "string" then return nil end
  local s = string.lower(name)
  local idx = string.find(s, "%(")
  if idx and idx > 1 then
    s = string.sub(s, 1, idx - 1)
  end
  s = string.gsub(s, "^%s+", "")
  s = string.gsub(s, "%s+$", "")
  return s
end

local function hasProfession(targetName)
  local targetNorm = normalizeProfessionName(targetName)
  if not targetNorm then return false end
  if not GetNumSkillLines or not GetSkillLineInfo then return false end

  local num = GetNumSkillLines()
  if not num or num <= 0 then return false end

  for i = 1, num do
    local name, isHeader, _, rank, _, _, maxRank = GetSkillLineInfo(i)
    if not isHeader then
      local norm = normalizeProfessionName(name)
      if norm and norm == targetNorm then
        return true, rank, maxRank
      end
    end
  end

  return false
end

local function getProfessionList()
  local data = FrugalForgeData_Anniversary
  if not data or type(data.professions) ~= "table" then return {} end
  return data.professions
end

local function getProfessionById(profId)
  for _, p in ipairs(getProfessionList()) do
    if p.professionId == profId then return p end
  end
  return nil
end

local function getProfessionByName(name)
  local target = normalizeProfessionName(name or "")
  for _, p in ipairs(getProfessionList()) do
    if normalizeProfessionName(p.name) == target then return p end
  end
  return nil
end

local function currentSkillForProfession(professionName)
  local ok, rank, maxRank = hasProfession(professionName)
  if ok then return rank, maxRank end
  return nil, nil
end

local function buildTargetsForProfession(professionId, maxSkillDelta)
  local prof = getProfessionById(professionId)
  if not prof then return nil, "Unknown profession" end

  local currentSkill, _ = currentSkillForProfession(prof.name)
  if not currentSkill then currentSkill = 1 end
  local maxSkill = currentSkill + (maxSkillDelta or 100)

  local targets = {}
  local reagentIds = {}
  local seen = {}

  for _, r in ipairs(prof.recipes or {}) do
    if r.minSkill and r.minSkill >= currentSkill and r.minSkill <= maxSkill then
      table.insert(targets, r)
      for _, reg in ipairs(r.reagents or {}) do
        local itemId = reg.itemId
        if itemId and not seen[itemId] then
          seen[itemId] = true
          table.insert(reagentIds, itemId)
        end
      end
    end
  end

  table.sort(targets, function(a, b)
    if a.minSkill == b.minSkill then return tostring(a.recipeId) < tostring(b.recipeId) end
    return a.minSkill < b.minSkill
  end)
  table.sort(reagentIds)

  return {
    profession = prof,
    targets = targets,
    reagentIds = reagentIds,
    currentSkill = currentSkill,
    maxSkill = maxSkill,
  }, nil
end

local function applyTargets(targets)
  ProfessionLevelerScan_TargetProfessionId = targets.profession.professionId
  ProfessionLevelerScan_TargetProfessionName = targets.profession.name
  ProfessionLevelerScan_TargetGameVersion = "Anniversary"
  ProfessionLevelerScan_RecipeTargets = targets.targets
  ProfessionLevelerScan_TargetItemIds = targets.reagentIds

  WowAhPlannerScan_TargetProfessionId = ProfessionLevelerScan_TargetProfessionId
  WowAhPlannerScan_TargetProfessionName = ProfessionLevelerScan_TargetProfessionName
  WowAhPlannerScan_TargetGameVersion = ProfessionLevelerScan_TargetGameVersion
  WowAhPlannerScan_RecipeTargets = ProfessionLevelerScan_RecipeTargets
  WowAhPlannerScan_TargetItemIds = ProfessionLevelerScan_TargetItemIds

  FrugalForgeDB.targets = {
    professionId = targets.profession.professionId,
    professionName = targets.profession.name,
    targets = targets.targets,
    reagentIds = targets.reagentIds,
  }
end

local ui = {}

local function updateUi()
  if not ui.frame then return end
  ensureDb()

  local snap = latestSnapshot()
  if snap and (snap.snapshotTimestampUtc or snap.generatedAtEpochUtc) then
    local epoch = getSnapshotEpoch(snap)
    local tsText = snap.snapshotTimestampUtc or "unknown"
    ui.snapshotValue:SetText(string.format("Snapshot: %s (%s)", tsText, fmtAge(epoch)))
  else
    ui.snapshotValue:SetText("Snapshot: none found (run scan)")
  end

  local owned = latestOwned()
  if owned and owned.snapshotTimestampUtc then
    ui.ownedValue:SetText(string.format("Owned: %s (%s)", owned.snapshotTimestampUtc, fmtAge(owned.snapshotTimestampUtc)))
  else
    ui.ownedValue:SetText("Owned: none found (run /wahpscan owned)")
  end

  local plan = FrugalForgeDB.lastPlan
  if plan and plan.generatedAt then
    ui.planValue:SetText(string.format("Plan: %s (%s)", plan.generatedAt, fmtAge(plan.generatedAtEpochUtc or time())))
    ui.stepsBox:SetText(plan.stepsText or "")
    ui.shoppingBox:SetText(plan.shoppingText or "")
    ui.summaryBox:SetText(plan.summaryText or "")
    ui.coverageValue:SetText(colorize(string.format("Coverage: %d%% (%d/%d priced)", plan.coveragePercent or 0, plan.pricedKinds or 0, plan.reagentKinds or 0), "|cffc0ffc0"))
    ui.missingValue:SetText(colorize(string.format("Missing prices: %d", plan.missingPriceItemCount or 0), (plan.missingPriceItemCount or 0) > 0 and "|cffffa0a0" or "|cffc0ffc0"))
    if plan.staleWarning then
      ui.staleValue:SetText(colorize(plan.staleWarning, "|cffffa0a0"))
    else
      ui.staleValue:SetText(colorize("Snapshot fresh", "|cffc0ffc0"))
    end
  else
    ui.planValue:SetText("Plan: not generated yet")
    ui.stepsBox:SetText("")
    ui.shoppingBox:SetText("")
    ui.summaryBox:SetText("")
    ui.coverageValue:SetText("Coverage: n/a")
    ui.missingValue:SetText("")
    ui.staleValue:SetText("")
  end
end

local function buildMaps()
  local snap = latestSnapshot()
  local owned = latestOwned()
  local prices = {}
  local priceCount = 0
  if snap and type(snap.prices) == "table" then
    for _, p in ipairs(snap.prices) do
      if p.itemId and p.minUnitBuyoutCopper then
        prices[p.itemId] = p.minUnitBuyoutCopper
        priceCount = priceCount + 1
      end
    end
  end

  local ownedMap = {}
  local ownedCount = 0
  if owned and type(owned.items) == "table" then
    for _, it in ipairs(owned.items) do
      if it.itemId and it.qty and it.qty > 0 then
        ownedMap[it.itemId] = (ownedMap[it.itemId] or 0) + it.qty
        ownedCount = ownedCount + 1
      end
    end
  end

  local ownedByChar = {}
  if owned and type(owned.characters) == "table" then
    for _, c in ipairs(owned.characters) do
      if type(c) == "table" and c.name and type(c.items) == "table" then
        for _, it in ipairs(c.items) do
          if it.itemId and it.qty and it.qty > 0 then
            local charMap = ownedByChar[it.itemId] or {}
            charMap[c.name] = (charMap[c.name] or 0) + it.qty
            ownedByChar[it.itemId] = charMap
          end
        end
      end
    end
  end

  return prices, priceCount, ownedMap, ownedCount, ownedByChar, snap, owned
end

local function copperToText(c)
  if not c then return "?" end
  local gold = math.floor(c / 10000)
  local silver = math.floor((c % 10000) / 100)
  local copper = c % 100
  if gold > 0 then
    return string.format("%dg %ds %dc", gold, silver, copper)
  elseif silver > 0 then
    return string.format("%ds %dc", silver, copper)
  else
    return string.format("%dc", copper)
  end
end

local function generatePlan()
  ensureDb()
  local prices, priceCount, ownedMap, ownedCount, ownedByChar, snap, owned = buildMaps()

  local recipes = _G.ProfessionLevelerScan_RecipeTargets or _G.WowAhPlannerScan_RecipeTargets
  if type(recipes) ~= "table" or #recipes == 0 then
    local selected = FrugalForgeDB.settings.selectedProfessionId
    if selected then
      local built, err = buildTargetsForProfession(selected, FrugalForgeDB.settings.maxSkillDelta or 100)
      if not built then
        DEFAULT_CHAT_FRAME:AddMessage("|cff7dd3fcFrugalForge|r: " .. tostring(err or "Failed to build targets"))
        return
      end
      applyTargets(built)
      recipes = built.targets
    else
      print("|cff7dd3fcFrugalForge|r: No recipe targets found. Choose a profession in FrugalForge and build targets.")
      return
    end
  end

  local targetProfessionName = (snap and snap.targetProfessionName) or (_G.ProfessionLevelerScan_TargetProfessionName) or (_G.WowAhPlannerScan_TargetProfessionName)
  local known, rank, maxRank = hasProfession(targetProfessionName)
  if targetProfessionName and not known then
    local msg = string.format("|cff7dd3fcFrugalForge|r: Targets are for %s but this character does not know it. Install targets for your profession and /reload.", targetProfessionName)
    DEFAULT_CHAT_FRAME:AddMessage(msg)
    FrugalForgeDB.lastPlan = {
      generatedAt = ts(),
      generatedAtEpochUtc = time(),
      snapshotTimestampUtc = snap and snap.snapshotTimestampUtc or nil,
      ownedTimestampUtc = owned and owned.snapshotTimestampUtc or nil,
      staleWarning = msg,
      stepsText = "",
      shoppingText = "",
      summaryText = msg,
    }
    updateUi()
    return
  end

  local stepLines = {}
  local shopping = {}
  local totalCost = 0
  local missingPriceItems = {}
  local reagentKinds = {}
  local pricedKinds = {}

  for _, r in ipairs(recipes) do
    if type(r) == "table" then
      local reagents = nil
      if type(r.reagentsWithQty) == "table" and #r.reagentsWithQty > 0 then
        reagents = r.reagentsWithQty
      elseif type(r.reagents) == "table" then
        reagents = {}
        for _, itemId in ipairs(r.reagents) do
          table.insert(reagents, { itemId = itemId, qty = 1 })
        end
      end

      if reagents then
        local stepCost = 0
        local stepMissing = 0
        for _, entry in ipairs(reagents) do
          local itemId = entry.itemId or entry.id or entry[1]
          local qty = entry.qty or entry.quantity or 1
          if itemId and qty and qty > 0 then
            local ownedQty = ownedMap[itemId] or 0
            local buy = math.max(0, qty - ownedQty)
            if buy > 0 then
              local price = prices[itemId]
              if price then
                stepCost = stepCost + price * buy
                shopping[itemId] = shopping[itemId] or { need = 0, owned = 0, price = price }
                shopping[itemId].need = shopping[itemId].need + qty
                shopping[itemId].owned = shopping[itemId].owned + ownedQty
                pricedKinds[itemId] = true
              else
                stepMissing = stepMissing + 1
                missingPriceItems[itemId] = true
              end
            end
            reagentKinds[itemId] = true
          end
        end

        totalCost = totalCost + stepCost
        local skillText = ""
        if r.minSkill then
          skillText = string.format(" (skill %s-%s)", tostring(r.minSkill), tostring(r.grayAt or ""))
        end
        table.insert(stepLines, string.format("- %s%s: cost %s%s",
          r.recipeId or "recipe",
          skillText,
          copperToText(stepCost),
          stepMissing > 0 and string.format(" (missing prices x%d)", stepMissing) or ""))
      end
    end
  end

  local shoppingLines = { "Shopping list:" }
  local missingCount = 0
  for itemId, entry in pairs(shopping) do
    local buy = math.max(0, entry.need - entry.owned)
    local ownedBreakdown = ""
    local chars = ownedByChar[itemId]
    if chars then
      local parts = {}
      for name, qty in pairs(chars) do
        table.insert(parts, string.format("%s:%d", name, qty))
      end
      table.sort(parts)
      ownedBreakdown = " [" .. table.concat(parts, ", ") .. "]"
    end

    local line = string.format("  - item %d: need %d (owned %d%s), buy %d @ %s = %s",
      itemId, entry.need, entry.owned, ownedBreakdown, buy, copperToText(entry.price), copperToText(entry.price * buy))
    table.insert(shoppingLines, line)
  end
  for _ in pairs(missingPriceItems) do missingCount = missingCount + 1 end

  local summaryLines = {}
  local snapCount = 0
  if snap and type(snap.prices) == "table" then snapCount = #snap.prices end
  table.insert(summaryLines, string.format("Snapshot priced items: %d", snapCount))
  table.insert(summaryLines, string.format("Total cost (priced items): %s", copperToText(totalCost)))
  local totalKinds = 0
  for _ in pairs(reagentKinds) do totalKinds = totalKinds + 1 end
  local pricedKindCount = 0
  for _ in pairs(pricedKinds) do pricedKindCount = pricedKindCount + 1 end
  local coverage = totalKinds > 0 and math.floor((pricedKindCount / totalKinds) * 100) or 0
  table.insert(summaryLines, string.format("Price coverage: %d%% (%d/%d reagents with prices)", coverage, pricedKindCount, totalKinds))
  table.insert(summaryLines, string.format("Owned items counted: %d unique", ownedCount or 0))
  if missingCount > 0 then
    table.insert(summaryLines, string.format("Missing prices for %d item(s); those steps are marked accordingly.", missingCount))
  end
  if snapCount == 0 then
    table.insert(summaryLines, "No prices found in snapshot. Run a scan at the AH and /reload.")
  end
  if targetProfessionName then
    table.insert(summaryLines, "Targets profession: " .. tostring(targetProfessionName))
    if known and rank and maxRank then
      table.insert(summaryLines, string.format("Your skill: %d/%d", rank, maxRank))
    end
  end
  local warnStaleHours = FrugalForgeDB.settings.warnStaleHours or 12
  local ageHours = hoursSince(getSnapshotEpoch(snap))
  local staleWarn = (ageHours and warnStaleHours and ageHours > warnStaleHours) and
    string.format("Snapshot is stale: %.1f hours old (threshold %d).", ageHours, warnStaleHours) or nil
  if staleWarn then table.insert(summaryLines, staleWarn) end

  FrugalForgeDB.lastPlan = {
    generatedAt = ts(),
    generatedAtEpochUtc = time(),
    snapshotTimestampUtc = snap and snap.snapshotTimestampUtc or nil,
    ownedTimestampUtc = owned and owned.snapshotTimestampUtc or nil,
    totalCostCopper = totalCost,
    missingPriceItemCount = missingCount,
    coveragePercent = coverage,
    reagentKinds = totalKinds,
    pricedKinds = pricedKindCount,
    staleWarning = staleWarn,
    stepsText = table.concat(stepLines, "\n"),
    shoppingText = table.concat(shoppingLines, "\n"),
    summaryText = table.concat(summaryLines, "\n"),
  }

  print("|cff7dd3fcFrugalForge|r: plan generated.")
  updateUi()
end

local function createUi()
  if ui.frame then return end

  local f = CreateFrame("Frame", "FrugalForgeFrame", UIParent, "BasicFrameTemplateWithInset")
  f:SetSize(580, 520)
  f:SetPoint("CENTER")
  f:Hide()
  f:SetMovable(true)
  f:EnableMouse(true)
  f:RegisterForDrag("LeftButton")
  f:SetScript("OnDragStart", f.StartMoving)
  f:SetScript("OnDragStop", f.StopMovingOrSizing)

  f.title = f:CreateFontString(nil, "OVERLAY", "GameFontHighlight")
  f.title:SetPoint("TOP", 0, -8)
  f.title:SetText("FrugalForge (beta)")

  local y = -32
  local labels = {
    { "snapshotLabel", "Snapshot", "snapshotValue" },
    { "ownedLabel", "Owned", "ownedValue" },
    { "planLabel", "Plan", "planValue" },
  }

  for _, row in ipairs(labels) do
    local lblName, lblText, valName = row[1], row[2], row[3]
    ui[lblName] = f:CreateFontString(nil, "OVERLAY", "GameFontNormal")
    ui[lblName]:SetPoint("TOPLEFT", 16, y)
    ui[lblName]:SetText(lblText .. ":")

    ui[valName] = f:CreateFontString(nil, "OVERLAY", "GameFontHighlight")
    ui[valName]:SetPoint("TOPLEFT", 140, y)
    ui[valName]:SetText("...")

    y = y - 20
  end

  ui.profLabel = f:CreateFontString(nil, "OVERLAY", "GameFontNormal")
  ui.profLabel:SetPoint("TOPLEFT", 16, y - 4)
  ui.profLabel:SetText("Profession:")

  ui.profDrop = CreateFrame("Frame", "FrugalForgeProfessionDropDown", f, "UIDropDownMenuTemplate")
  ui.profDrop:SetPoint("TOPLEFT", 110, y - 10)

  ui.deltaLabel = f:CreateFontString(nil, "OVERLAY", "GameFontNormal")
  ui.deltaLabel:SetPoint("TOPLEFT", 320, y - 4)
  ui.deltaLabel:SetText("Skill +")

  ui.deltaBox = CreateFrame("EditBox", nil, f, "InputBoxTemplate")
  ui.deltaBox:SetSize(40, 20)
  ui.deltaBox:SetPoint("TOPLEFT", 375, y - 8)
  ui.deltaBox:SetAutoFocus(false)
  ui.deltaBox:SetNumeric(true)
  ui.deltaBox:SetText(tostring(FrugalForgeDB.settings.maxSkillDelta or 100))

  ui.buildTargetsBtn = CreateFrame("Button", nil, f, "UIPanelButtonTemplate")
  ui.buildTargetsBtn:SetSize(120, 22)
  ui.buildTargetsBtn:SetPoint("TOPLEFT", 430, y - 10)
  ui.buildTargetsBtn:SetText("Build Targets")
  ui.buildTargetsBtn:SetScript("OnClick", function()
    local selected = FrugalForgeDB.settings.selectedProfessionId
    if not selected then
      DEFAULT_CHAT_FRAME:AddMessage("|cff7dd3fcFrugalForge|r: Select a profession first.")
      return
    end
    local delta = tonumber(ui.deltaBox:GetText() or "") or 100
    FrugalForgeDB.settings.maxSkillDelta = delta
    local built, err = buildTargetsForProfession(selected, delta)
    if not built then
      DEFAULT_CHAT_FRAME:AddMessage("|cff7dd3fcFrugalForge|r: " .. tostring(err or "Failed to build targets"))
      return
    end
    applyTargets(built)
    DEFAULT_CHAT_FRAME:AddMessage("|cff7dd3fcFrugalForge|r: Targets built for " .. tostring(built.profession.name))
  end)

  ui.scanBtn = CreateFrame("Button", nil, f, "UIPanelButtonTemplate")
  ui.scanBtn:SetSize(80, 22)
  ui.scanBtn:SetPoint("TOPLEFT", 16, y - 32)
  ui.scanBtn:SetText("Scan AH")
  ui.scanBtn:SetScript("OnClick", function()
    if type(SlashCmdList) == "table" and SlashCmdList["WAHPSCAN"] then
      SlashCmdList["WAHPSCAN"]("start")
    else
      DEFAULT_CHAT_FRAME:AddMessage("|cff7dd3fcFrugalForge|r: Scanner not loaded.")
    end
  end)

  ui.generateBtn = CreateFrame("Button", nil, f, "UIPanelButtonTemplate")
  ui.generateBtn:SetSize(140, 22)
  ui.generateBtn:SetPoint("TOPLEFT", 104, y - 32)
  ui.generateBtn:SetText("Generate Plan")
  ui.generateBtn:SetScript("OnClick", generatePlan)

  ui.closeBtn = CreateFrame("Button", nil, f, "UIPanelButtonTemplate")
  ui.closeBtn:SetSize(80, 24)
  ui.closeBtn:SetPoint("TOPRIGHT", -16, y - 32)
  ui.closeBtn:SetText("Close")
  ui.closeBtn:SetScript("OnClick", function() f:Hide() end)

  y = y - 68

  ui.coverageValue = f:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall")
  ui.coverageValue:SetPoint("TOPLEFT", 16, y)
  ui.coverageValue:SetText("Coverage: n/a")

  ui.missingValue = f:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall")
  ui.missingValue:SetPoint("TOPLEFT", 16, y - 16)
  ui.missingValue:SetText("")

  ui.staleValue = f:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall")
  ui.staleValue:SetPoint("TOPLEFT", 16, y - 32)
  ui.staleValue:SetText("")

  UIDropDownMenu_Initialize(ui.profDrop, function(self, level)
    local info = UIDropDownMenu_CreateInfo()
    for _, p in ipairs(getProfessionList()) do
      info.text = p.name
      info.checked = (FrugalForgeDB.settings.selectedProfessionId == p.professionId)
      info.func = function()
        FrugalForgeDB.settings.selectedProfessionId = p.professionId
        UIDropDownMenu_SetSelectedID(ui.profDrop, nil)
        UIDropDownMenu_SetText(ui.profDrop, p.name)
      end
      UIDropDownMenu_AddButton(info, level)
    end
  end)

  local selected = getProfessionById(FrugalForgeDB.settings.selectedProfessionId)
  if selected then
    UIDropDownMenu_SetText(ui.profDrop, selected.name)
  else
    UIDropDownMenu_SetText(ui.profDrop, "Select...")
  end

  -- Steps box
  local stepsScroll = CreateFrame("ScrollFrame", nil, f, "UIPanelScrollFrameTemplate")
  stepsScroll:SetPoint("TOPLEFT", 16, y - 60)
  stepsScroll:SetPoint("RIGHT", -36, -8)
  stepsScroll:SetHeight(160)

  local stepsBox = CreateFrame("EditBox", nil, stepsScroll)
  stepsBox:SetMultiLine(true)
  stepsBox:SetFontObject(GameFontHighlightSmall)
  stepsBox:SetWidth(510)
  stepsBox:SetAutoFocus(false)
  stepsBox:SetScript("OnEscapePressed", function() stepsBox:ClearFocus() end)
  stepsScroll:SetScrollChild(stepsBox)
  ui.stepsBox = stepsBox

  -- Shopping box
  local shopScroll = CreateFrame("ScrollFrame", nil, f, "UIPanelScrollFrameTemplate")
  shopScroll:SetPoint("TOPLEFT", stepsScroll, "BOTTOMLEFT", 0, -8)
  shopScroll:SetPoint("RIGHT", -36, -8)
  shopScroll:SetHeight(120)

  local shopBox = CreateFrame("EditBox", nil, shopScroll)
  shopBox:SetMultiLine(true)
  shopBox:SetFontObject(GameFontHighlightSmall)
  shopBox:SetWidth(510)
  shopBox:SetAutoFocus(false)
  shopBox:SetScript("OnEscapePressed", function() shopBox:ClearFocus() end)
  shopScroll:SetScrollChild(shopBox)
  ui.shoppingBox = shopBox

  -- Summary box
  local summaryScroll = CreateFrame("ScrollFrame", nil, f, "UIPanelScrollFrameTemplate")
  summaryScroll:SetPoint("TOPLEFT", shopScroll, "BOTTOMLEFT", 0, -8)
  summaryScroll:SetPoint("BOTTOMRIGHT", -36, 16)

  local summaryBox = CreateFrame("EditBox", nil, summaryScroll)
  summaryBox:SetMultiLine(true)
  summaryBox:SetFontObject(GameFontHighlightSmall)
  summaryBox:SetWidth(510)
  summaryBox:SetAutoFocus(false)
  summaryBox:SetScript("OnEscapePressed", function() summaryBox:ClearFocus() end)
  summaryScroll:SetScrollChild(summaryBox)
  ui.summaryBox = summaryBox

  ui.frame = f
end

local function toggleUi()
  createUi()
  if ui.frame:IsShown() then
    ui.frame:Hide()
  else
    updateUi()
    ui.frame:Show()
  end
end

SLASH_FRUGALFORGE1 = "/frugal"
SLASH_FRUGALFORGE2 = "/frugalforge"
SlashCmdList["FRUGALFORGE"] = function()
  toggleUi()
end

local eventFrame = CreateFrame("Frame")
eventFrame:RegisterEvent("ADDON_LOADED")
eventFrame:SetScript("OnEvent", function(_, event, addon)
  if event == "ADDON_LOADED" and addon == ADDON_NAME then
    ensureDb()
    createUi()
    if FrugalForgeDB.targets then
      local t = FrugalForgeDB.targets
      applyTargets({
        profession = { professionId = t.professionId, name = t.professionName },
        targets = t.targets or {},
        reagentIds = t.reagentIds or {},
      })
    end
    updateUi()
    print("|cff7dd3fcFrugalForge|r loaded. Use /frugal to open.")
  end
end)
