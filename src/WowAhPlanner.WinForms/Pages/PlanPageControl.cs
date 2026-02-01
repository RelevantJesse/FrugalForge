using System.ComponentModel;
using WowAhPlanner.Core.Domain;
using WowAhPlanner.Core.Domain.Planning;
using WowAhPlanner.Core.Ports;
using WowAhPlanner.Core.Services;
using WowAhPlanner.Infrastructure.Owned;
using WowAhPlanner.WinForms.Services;
using WowAhPlanner.WinForms.Ui;
using Region = WowAhPlanner.Core.Domain.Region;

namespace WowAhPlanner.WinForms.Pages;

internal sealed class PlanPageControl(
    SelectionState selection,
    IRecipeRepository recipeRepository,
    IItemRepository itemRepository,
    IVendorPriceRepository vendorPriceRepository,
    PlannerService planner,
    OwnedMaterialsService ownedMaterialsService,
    OwnedBreakdownService ownedBreakdownService,
    PlannerPreferences plannerPreferences) : PageControlBase
{
    private const string LocalUserId = "local";

    private readonly Ui.CardPanel inputsCard = new();
    private readonly Label inputsTitleLabel = new();
    private readonly TableLayoutPanel inputsLayout = new();

    private readonly Label professionLabel = new();
    private readonly ComboBox professionComboBox = new();
    private readonly Label currentSkillLabel = new();
    private readonly NumericUpDown currentSkillUpDown = new();
    private readonly Label targetSkillLabel = new();
    private readonly NumericUpDown targetSkillUpDown = new();
    private readonly Label priceModeLabel = new();
    private readonly ComboBox priceModeComboBox = new();
    private readonly CheckBox craftIntermediatesCheckBox = new();
    private readonly CheckBox smeltIntermediatesCheckBox = new();
    private readonly CheckBox useOwnedMaterialsCheckBox = new();
    private readonly Button generateButton = new();

    private readonly Ui.CardPanel resultsCard = new();
    private readonly Label pricingLabel = new();
    private readonly Label summaryLabel = new();
    private readonly TabControl resultsTabs = new();
    private readonly TabPage stepsTab = new();
    private readonly TabPage shoppingTab = new();
    private readonly TabPage issuesTab = new();
    private readonly DataGridView stepsGrid = new();
    private readonly SplitContainer stepsSplit = new();
    private readonly Panel stepDetailsHeaderPanel = new();
    private readonly Label stepDetailsTitleLabel = new();
    private readonly DataGridView stepDetailsGrid = new();
    private readonly DataGridView shoppingGrid = new();
    private readonly TextBox issuesTextBox = new();

    private IReadOnlyList<Profession> professions = [];
    private Dictionary<int, string> itemNames = new();
    private Dictionary<int, long> vendorPricesCopper = new();

    private bool initialized;
    private bool loading;
    private PlanComputationResult? result;
    private IReadOnlyDictionary<int, long> lastOwnedByItemId = new Dictionary<int, long>();
    private IReadOnlyList<int> uiMissingPricedItemIds = [];
    private IReadOnlyDictionary<int, IReadOnlyList<(string CharacterName, long Qty)>> ownedByCharacterByItemId =
        new Dictionary<int, IReadOnlyList<(string, long)>>();
    private PriceMode lastPriceMode = PriceMode.Min;

    public override string Title => "Plan";

    public override async Task OnNavigatedToAsync()
    {
        if (!initialized)
        {
            initialized = true;
            BuildUi();
        }

        await EnsureLoadedAsync();
    }

    public override async Task RefreshAsync()
    {
        await EnsureLoadedAsync();
    }

    private void BuildUi()
    {
        Ui.Theme.ApplyCardStyle(inputsCard);
        inputsCard.Dock = DockStyle.Top;
        inputsCard.Padding = new Padding(16);
        inputsCard.Margin = new Padding(0, 0, 0, 12);
        inputsCard.AutoSize = true;
        inputsCard.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        inputsTitleLabel.Text = "Inputs";
        inputsTitleLabel.AutoSize = true;
        inputsTitleLabel.Dock = DockStyle.Top;
        inputsTitleLabel.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyCardTitleStyle(inputsTitleLabel);

        inputsLayout.ColumnCount = 2;
        inputsLayout.RowCount = 12;
        inputsLayout.Dock = DockStyle.Top;
        inputsLayout.AutoSize = true;
        inputsLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        inputsLayout.Margin = new Padding(0);
        inputsLayout.Padding = new Padding(0);
        inputsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        inputsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int i = 0; i < inputsLayout.RowCount; i++)
        {
            inputsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        professionLabel.Text = "Profession";
        professionLabel.AutoSize = true;
        professionLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(professionLabel);

        professionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        professionComboBox.Dock = DockStyle.Fill;
        professionComboBox.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyComboBoxStyle(professionComboBox);
        professionComboBox.SelectedIndexChanged += async (_, _) => await OnProfessionChangedAsync();

        currentSkillLabel.Text = "Current skill";
        currentSkillLabel.AutoSize = true;
        currentSkillLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(currentSkillLabel);

        currentSkillUpDown.Minimum = 0;
        currentSkillUpDown.Maximum = 450;
        currentSkillUpDown.Value = 1;
        currentSkillUpDown.Dock = DockStyle.Left;
        currentSkillUpDown.Width = 120;
        currentSkillUpDown.Margin = new Padding(0, 0, 0, 12);

        targetSkillLabel.Text = "Target skill";
        targetSkillLabel.AutoSize = true;
        targetSkillLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(targetSkillLabel);

        targetSkillUpDown.Minimum = 0;
        targetSkillUpDown.Maximum = 350;
        targetSkillUpDown.Value = 300;
        targetSkillUpDown.Dock = DockStyle.Left;
        targetSkillUpDown.Width = 120;
        targetSkillUpDown.Margin = new Padding(0, 0, 0, 12);

        priceModeLabel.Text = "Price mode";
        priceModeLabel.AutoSize = true;
        priceModeLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(priceModeLabel);

        priceModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        priceModeComboBox.Dock = DockStyle.Left;
        priceModeComboBox.Width = 160;
        priceModeComboBox.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyComboBoxStyle(priceModeComboBox);
        priceModeComboBox.Items.AddRange(Enum.GetValues<PriceMode>().Cast<object>().ToArray());
        priceModeComboBox.SelectedItem = PriceMode.Min;

        craftIntermediatesCheckBox.Text = "Craft other-profession intermediates (optional)";
        craftIntermediatesCheckBox.AutoSize = true;
        craftIntermediatesCheckBox.Margin = new Padding(0, 0, 0, 8);
        craftIntermediatesCheckBox.ForeColor = Ui.Theme.TextSecondary;

        smeltIntermediatesCheckBox.Text = "Smelt intermediates (bars from ore)";
        smeltIntermediatesCheckBox.AutoSize = true;
        smeltIntermediatesCheckBox.Margin = new Padding(0, 0, 0, 8);
        smeltIntermediatesCheckBox.ForeColor = Ui.Theme.TextSecondary;
        smeltIntermediatesCheckBox.Checked = true;

        useOwnedMaterialsCheckBox.Text = "Subtract owned materials";
        useOwnedMaterialsCheckBox.AutoSize = true;
        useOwnedMaterialsCheckBox.Margin = new Padding(0, 0, 0, 12);
        useOwnedMaterialsCheckBox.ForeColor = Ui.Theme.TextSecondary;
        useOwnedMaterialsCheckBox.Checked = true;

        generateButton.Text = "Generate plan";
        generateButton.AutoSize = true;
        generateButton.Margin = new Padding(0);
        Ui.Theme.ApplyPrimaryButtonStyle(generateButton);
        generateButton.Click += async (_, _) => await GenerateAsync();

        inputsLayout.Controls.Add(professionLabel, 0, 0);
        inputsLayout.Controls.Add(professionComboBox, 1, 0);
        inputsLayout.Controls.Add(currentSkillLabel, 0, 1);
        inputsLayout.Controls.Add(currentSkillUpDown, 1, 1);
        inputsLayout.Controls.Add(targetSkillLabel, 0, 2);
        inputsLayout.Controls.Add(targetSkillUpDown, 1, 2);
        inputsLayout.Controls.Add(priceModeLabel, 0, 3);
        inputsLayout.Controls.Add(priceModeComboBox, 1, 3);
        inputsLayout.Controls.Add(craftIntermediatesCheckBox, 1, 4);
        inputsLayout.Controls.Add(smeltIntermediatesCheckBox, 1, 5);
        inputsLayout.Controls.Add(useOwnedMaterialsCheckBox, 1, 6);
        inputsLayout.Controls.Add(generateButton, 1, 7);

        inputsCard.Controls.Add(inputsLayout);
        inputsCard.Controls.Add(inputsTitleLabel);

        Ui.Theme.ApplyCardStyle(resultsCard);
        resultsCard.Dock = DockStyle.Fill;
        resultsCard.Padding = new Padding(16);
        resultsCard.Margin = new Padding(0);

        pricingLabel.AutoSize = true;
        pricingLabel.Dock = DockStyle.Top;
        pricingLabel.Margin = new Padding(0, 0, 0, 8);
        pricingLabel.ForeColor = Ui.Theme.TextSecondary;
        pricingLabel.Text = "Pricing: n/a";

        summaryLabel.AutoSize = true;
        summaryLabel.Dock = DockStyle.Top;
        summaryLabel.Margin = new Padding(0, 0, 0, 12);
        summaryLabel.ForeColor = Ui.Theme.TextSecondary;
        summaryLabel.Text = "Generate a plan to see results.";

        resultsTabs.Dock = DockStyle.Fill;
        resultsTabs.Margin = new Padding(0);

        stepsTab.Text = "Steps";
        shoppingTab.Text = "Shopping";
        issuesTab.Text = "Issues";

        Ui.Theme.ApplyDataGridViewStyle(stepsGrid);
        stepsGrid.Dock = DockStyle.Fill;
        stepsGrid.AutoGenerateColumns = false;
        stepsGrid.SelectionChanged += (_, _) => UpdateStepDetails();
        stepsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Skill", DataPropertyName = nameof(StepRow.Skill), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Recipe", DataPropertyName = nameof(StepRow.Recipe), MinimumWidth = 240 });
        stepsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chance", DataPropertyName = nameof(StepRow.Chance), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Crafts", DataPropertyName = nameof(StepRow.ExpectedCrafts), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cost", DataPropertyName = nameof(StepRow.ExpectedCost), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Materials", DataPropertyName = nameof(StepRow.Materials), MinimumWidth = 260 });
        stepsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "_idx", DataPropertyName = nameof(StepRow.StepIndex), Visible = false });

        Ui.Theme.ApplyDataGridViewStyle(stepDetailsGrid);
        stepDetailsGrid.Dock = DockStyle.Fill;
        stepDetailsGrid.AutoGenerateColumns = false;
        stepDetailsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Item", DataPropertyName = nameof(StepMaterialRow.Item), MinimumWidth = 240 });
        stepDetailsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Required", DataPropertyName = nameof(StepMaterialRow.Required), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepDetailsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Owned used", DataPropertyName = nameof(StepMaterialRow.OwnedUsed), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepDetailsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Acquire/Produce", DataPropertyName = nameof(StepMaterialRow.AcquireOrProduce), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepDetailsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", DataPropertyName = nameof(StepMaterialRow.Source), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepDetailsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Unit", DataPropertyName = nameof(StepMaterialRow.UnitPrice), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepDetailsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Line", DataPropertyName = nameof(StepMaterialRow.LineCost), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        stepDetailsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Owned by", DataPropertyName = nameof(StepMaterialRow.OwnedBy), MinimumWidth = 200 });

        stepDetailsHeaderPanel.Dock = DockStyle.Top;
        stepDetailsHeaderPanel.Height = 28;
        stepDetailsHeaderPanel.Margin = new Padding(0);
        stepDetailsHeaderPanel.Padding = new Padding(0, 6, 0, 6);
        stepDetailsHeaderPanel.BackColor = Ui.Theme.AppBackground;

        stepDetailsTitleLabel.Text = "Step details";
        stepDetailsTitleLabel.AutoSize = true;
        stepDetailsTitleLabel.Margin = new Padding(0);
        stepDetailsTitleLabel.ForeColor = Ui.Theme.TextSecondary;
        stepDetailsHeaderPanel.Controls.Add(stepDetailsTitleLabel);

        stepsSplit.Dock = DockStyle.Fill;
        stepsSplit.Orientation = Orientation.Horizontal;
        stepsSplit.SplitterWidth = 6;
        stepsSplit.Panel1MinSize = 0;
        stepsSplit.Panel2MinSize = 0;
        stepsSplit.SizeChanged += (_, _) => EnsureStepsSplitterDistance();
        stepsSplit.Panel1.Controls.Add(stepsGrid);
        stepsSplit.Panel2.Controls.Add(stepDetailsGrid);
        stepsSplit.Panel2.Controls.Add(stepDetailsHeaderPanel);

        Ui.Theme.ApplyDataGridViewStyle(shoppingGrid);
        shoppingGrid.Dock = DockStyle.Fill;
        shoppingGrid.AutoGenerateColumns = false;
        shoppingGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Item", DataPropertyName = nameof(ShoppingRow.Item), MinimumWidth = 240 });
        shoppingGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Needed", DataPropertyName = nameof(ShoppingRow.Needed), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        shoppingGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Owned", DataPropertyName = nameof(ShoppingRow.Owned), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        shoppingGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Buy", DataPropertyName = nameof(ShoppingRow.Buy), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        shoppingGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Unit", DataPropertyName = nameof(ShoppingRow.Unit), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        shoppingGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cost", DataPropertyName = nameof(ShoppingRow.Cost), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });

        issuesTextBox.Multiline = true;
        issuesTextBox.ReadOnly = true;
        issuesTextBox.Dock = DockStyle.Fill;
        issuesTextBox.ScrollBars = ScrollBars.Vertical;
        issuesTextBox.BackColor = Ui.Theme.AppBackground;
        issuesTextBox.ForeColor = Ui.Theme.TextSecondary;
        issuesTextBox.BorderStyle = BorderStyle.FixedSingle;

        stepsTab.Controls.Add(stepsSplit);
        shoppingTab.Controls.Add(shoppingGrid);
        issuesTab.Controls.Add(issuesTextBox);

        resultsTabs.TabPages.Add(stepsTab);
        resultsTabs.TabPages.Add(shoppingTab);
        resultsTabs.TabPages.Add(issuesTab);

        resultsCard.Controls.Add(resultsTabs);
        resultsCard.Controls.Add(summaryLabel);
        resultsCard.Controls.Add(pricingLabel);

        Controls.Add(resultsCard);
        Controls.Add(inputsCard);

        EnsureStepsSplitterDistance();
    }

    private void EnsureStepsSplitterDistance()
    {
        if (!stepsSplit.IsHandleCreated)
        {
            return;
        }

        var dim = stepsSplit.Orientation == Orientation.Horizontal
            ? stepsSplit.Height
            : stepsSplit.Width;

        if (dim <= 0)
        {
            return;
        }

        var min = stepsSplit.Panel1MinSize;
        var max = dim - stepsSplit.Panel2MinSize;
        if (max < min)
        {
            // If the container is too small, collapse the details panel.
            stepsSplit.Panel2Collapsed = true;
            return;
        }

        stepsSplit.Panel2Collapsed = false;

        var desired = (int)(dim * 0.58);
        var clamped = Math.Clamp(desired, min, max);

        if (stepsSplit.SplitterDistance != clamped)
        {
            stepsSplit.SplitterDistance = clamped;
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        try
        {
            await selection.EnsureLoadedAsync();
            await plannerPreferences.EnsureLoadedAsync();

            professions = await recipeRepository.GetProfessionsAsync(selection.GameVersion, CancellationToken.None);
            var preferred = selection.ProfessionId;
            var selectedProfessionId = professions.Any(p => p.ProfessionId == preferred)
                ? preferred
                : professions.FirstOrDefault()?.ProfessionId ?? 0;

            professionComboBox.BeginUpdate();
            try
            {
                professionComboBox.DataSource = null;
                professionComboBox.DisplayMember = nameof(Profession.Name);
                professionComboBox.ValueMember = nameof(Profession.ProfessionId);
                professionComboBox.DataSource = professions.ToArray();
                professionComboBox.SelectedItem = professions.FirstOrDefault(p => p.ProfessionId == selectedProfessionId);
            }
            finally
            {
                professionComboBox.EndUpdate();
            }

            selection.ProfessionId = selectedProfessionId;
            await selection.PersistAsync();

            itemNames = (await itemRepository.GetItemsAsync(selection.GameVersion, CancellationToken.None))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            vendorPricesCopper = (await vendorPriceRepository.GetVendorPricesAsync(selection.GameVersion, CancellationToken.None))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            ApplyResultToUi();
        }
        catch (Exception ex)
        {
            issuesTextBox.Text = ex.ToString();
            resultsTabs.SelectedTab = issuesTab;
        }
        finally
        {
            loading = false;
        }
    }

    private async Task OnProfessionChangedAsync()
    {
        if (loading)
        {
            return;
        }

        if (professionComboBox.SelectedItem is not Profession p)
        {
            return;
        }

        selection.ProfessionId = p.ProfessionId;
        await selection.PersistAsync();
    }

    private async Task GenerateAsync()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        generateButton.Enabled = false;
        uiMissingPricedItemIds = [];

        try
        {
            await selection.EnsureLoadedAsync();
            await plannerPreferences.EnsureLoadedAsync();

            var realmKey = selection.ToRealmKey();

            IReadOnlyDictionary<int, long>? owned = null;
            if (useOwnedMaterialsCheckBox.Checked)
            {
                owned = await ownedMaterialsService.GetOwnedAsync(realmKey.ToString(), LocalUserId, CancellationToken.None);
            }
            lastOwnedByItemId = owned ?? new Dictionary<int, long>();
            ownedByCharacterByItemId = await ownedBreakdownService.LoadAsync(realmKey.ToString());

            var excludedRecipeIds = plannerPreferences.GetExcludedRecipeIds(selection.GameVersion, selection.ProfessionId);

            var priceMode = priceModeComboBox.SelectedItem is PriceMode pm ? pm : PriceMode.Min;
            lastPriceMode = priceMode;

            result = await planner.BuildPlanAsync(
                new PlanRequest(
                    realmKey,
                    selection.ProfessionId,
                    CurrentSkill: (int)currentSkillUpDown.Value,
                    TargetSkill: (int)targetSkillUpDown.Value,
                    PriceMode: priceMode,
                    UseCraftIntermediates: craftIntermediatesCheckBox.Checked,
                    UseSmeltIntermediates: smeltIntermediatesCheckBox.Checked,
                    OwnedMaterials: owned,
                    ExcludedRecipeIds: excludedRecipeIds),
                CancellationToken.None);

            if (result.Plan is not null)
            {
                // Planner currently falls back to 0c for unknown prices in the shopping list.
                // For UI purposes we fail closed: if you can't price what you need to buy (and it's not covered by owned mats),
                // we treat the plan as unavailable.
                uiMissingPricedItemIds = result.Plan.ShoppingList
                    .Where(x => x.Quantity > 0 && x.UnitPrice == Money.Zero)
                    .Select(x => x.ItemId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToArray();
            }

            ApplyResultToUi();
        }
        catch (Exception ex)
        {
            issuesTextBox.Text = ex.ToString();
            resultsTabs.SelectedTab = issuesTab;
        }
        finally
        {
            loading = false;
            generateButton.Enabled = true;
        }
    }

    private void ApplyResultToUi()
    {
        pricingLabel.Text = result is null
            ? "Pricing: n/a"
            : $"Pricing: {result.PriceSnapshot.ProviderName} | {result.PriceSnapshot.SnapshotTimestampUtc:u}"
              + (result.PriceSnapshot.IsStale ? " | stale" : "")
              + (!string.IsNullOrWhiteSpace(result.PriceSnapshot.ErrorMessage) ? $" | {result.PriceSnapshot.ErrorMessage}" : "");

        var missingIds = new HashSet<int>();
        foreach (var id in uiMissingPricedItemIds) missingIds.Add(id);
        if (result?.MissingItemIds is { Count: > 0 })
        {
            foreach (var id in result.MissingItemIds) missingIds.Add(id);
        }

        if (result?.Plan is null || missingIds.Count > 0)
        {
            stepsGrid.DataSource = new BindingList<StepRow>();
            shoppingGrid.DataSource = new BindingList<ShoppingRow>();

            if (result is null)
            {
                summaryLabel.Text = "Generate a plan to see results.";
                issuesTextBox.Text = "";
                return;
            }

            var orderedMissing = missingIds.OrderBy(x => x).ToArray();
            var missing = orderedMissing.Length == 0
                ? ""
                : "Missing prices for:\r\n" + string.Join(
                    "\r\n",
                    orderedMissing.Select(id => $"- {(itemNames.GetValueOrDefault(id) ?? $"Item {id}")} ({id})"));

            summaryLabel.Text = orderedMissing.Length > 0
                ? "Not enough AH/owned data to generate a plan."
                : (result.ErrorMessage ?? "Unable to plan.");
            issuesTextBox.Text = string.Join("\r\n\r\n", new[]
            {
                orderedMissing.Length > 0 ? "Not enough AH/owned data to generate a plan." : (result.ErrorMessage ?? ""),
                missing,
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

            resultsTabs.SelectedTab = issuesTab;
            return;
        }

        var plan = result.Plan;
        summaryLabel.Text =
            $"Total cost: {plan.TotalCost.ToGscString()} | Generated: {plan.GeneratedAtUtc:u}"
            + (plan.SkillCreditApplied > 0 ? $" | Intermediate skill-ups: {plan.SkillCreditApplied} (expected {plan.ExpectedSkillUpsFromIntermediates:0.##})" : "");

        var breakdownByIndex = plan.StepBreakdowns.ToDictionary(x => x.StepIndex);
        var stepRows = new BindingList<StepRow>(plan.Steps.Select((s, idx) =>
        {
            breakdownByIndex.TryGetValue(idx, out var breakdown);
            return new StepRow(
                Skill: $"{s.SkillFrom} \u2192 {s.SkillTo}",
                Recipe: $"{s.RecipeName} ({s.RecipeId})" + (s.LearnedByTrainer == true ? " [Trainer]" : ""),
                Chance: $"{s.SkillUpChance:0.##}",
                ExpectedCrafts: $"{s.ExpectedCrafts:0.##}",
                ExpectedCost: s.ExpectedCost.ToGscString(),
                Materials: BuildMaterialsText(breakdown),
                StepIndex: idx);
        }).ToList());

        stepsGrid.DataSource = stepRows;
        UpdateStepDetails();

        var shoppingRows = new BindingList<ShoppingRow>(BuildShoppingRows(plan, lastOwnedByItemId, result.PriceSnapshot).ToList());
        shoppingGrid.DataSource = shoppingRows;

        issuesTextBox.Text = uiMissingPricedItemIds.Count == 0
            ? ""
            : "Missing prices for:\r\n" + string.Join("\r\n", uiMissingPricedItemIds.Select(id => $"- {(itemNames.GetValueOrDefault(id) ?? $"Item {id}")} ({id})"));

        resultsTabs.SelectedTab = stepsTab;
    }

    private string BuildMaterialsText(PlanStepBreakdown? breakdown)
    {
        if (breakdown is null)
        {
            return "";
        }

        var parts = new List<string>();

        foreach (var a in breakdown.Acquisitions.Where(x => x.AcquireQuantity > 0))
        {
            var name = itemNames.GetValueOrDefault(a.ItemId) ?? $"Item {a.ItemId}";
            parts.Add($"{a.AcquireQuantity:0.##}x {name}");
        }

        foreach (var i in breakdown.Intermediates.Where(x => x.ToProduceQuantity > 0))
        {
            var name = itemNames.GetValueOrDefault(i.ItemId) ?? $"Item {i.ItemId}";
            parts.Add($"{i.ToProduceQuantity:0.##}x {name} ({i.Kind})");
        }

        return string.Join("; ", parts);
    }

    private IEnumerable<ShoppingRow> BuildShoppingRows(PlanResult plan, IReadOnlyDictionary<int, long> ownedByItemId, PriceSnapshot snapshot)
    {
        var ownedUsedByItemId = plan.OwnedMaterialsUsed
            .GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityUsed));

        var buyByItemId = plan.ShoppingList.ToDictionary(x => x.ItemId, x => x);

        var allItemIds = new HashSet<int>(ownedUsedByItemId.Keys);
        foreach (var itemId in buyByItemId.Keys) allItemIds.Add(itemId);

        foreach (var itemId in allItemIds.OrderBy(x => x))
        {
            var ownedUsedQty = ownedUsedByItemId.GetValueOrDefault(itemId);
            var ownedTotalQty = ownedByItemId.GetValueOrDefault(itemId);
            var buyQty = buyByItemId.TryGetValue(itemId, out var buyLine) ? buyLine.Quantity : 0m;

            var unit = buyByItemId.TryGetValue(itemId, out buyLine)
                ? buyLine.UnitPrice
                : GetUnitPrice(itemId, snapshot);

            var lineCost = unit * buyQty;

            var name = itemNames.GetValueOrDefault(itemId) ?? $"Item {itemId}";
            yield return new ShoppingRow(
                Item: $"{name} ({itemId})",
                Needed: (ownedUsedQty + buyQty).ToString("0.##"),
                Owned: ownedTotalQty.ToString(),
                Buy: buyQty.ToString("0.##"),
                Unit: unit.ToGscString(),
                Cost: lineCost.ToGscString());
        }
    }

    private Money GetUnitPrice(int itemId, PriceSnapshot snapshot)
    {
        if (vendorPricesCopper.TryGetValue(itemId, out var vendorCopper))
        {
            return new Money(vendorCopper);
        }

        if (snapshot.Prices.TryGetValue(itemId, out var summary))
        {
            return lastPriceMode switch
            {
                PriceMode.Median when summary.MedianCopper is long med => new Money(med),
                _ => new Money(summary.MinBuyoutCopper),
            };
        }

        return Money.Zero;
    }

    private void UpdateStepDetails()
    {
        if (result?.Plan is null)
        {
            stepDetailsTitleLabel.Text = "Step details";
            stepDetailsGrid.DataSource = new BindingList<StepMaterialRow>();
            return;
        }

        if (stepsGrid.CurrentRow?.DataBoundItem is not StepRow stepRow)
        {
            stepDetailsTitleLabel.Text = "Step details";
            stepDetailsGrid.DataSource = new BindingList<StepMaterialRow>();
            return;
        }

        var plan = result.Plan;
        var idx = stepRow.StepIndex;
        var step = plan.Steps[idx];
        var breakdown = plan.StepBreakdowns.FirstOrDefault(x => x.StepIndex == idx);

        stepDetailsTitleLabel.Text = $"Step {idx + 1}: {step.RecipeName} ({step.RecipeId})";

        var rows = new List<StepMaterialRow>();

        if (breakdown is not null)
        {
            foreach (var a in breakdown.Acquisitions.Where(x => x.RequiredQuantity > 0))
            {
                var name = itemNames.GetValueOrDefault(a.ItemId) ?? $"Item {a.ItemId}";
                var unit = GetUnitPrice(a.ItemId, result.PriceSnapshot);
                var line = unit * a.AcquireQuantity;
                rows.Add(new StepMaterialRow(
                    Item: $"{name} ({a.ItemId})",
                    Required: a.RequiredQuantity.ToString("0.##"),
                    OwnedUsed: a.OwnedUsedQuantity.ToString("0.##"),
                    AcquireOrProduce: a.AcquireQuantity.ToString("0.##"),
                    Source: a.Source.ToString(),
                    UnitPrice: unit.ToGscString(),
                    LineCost: line.ToGscString(),
                    OwnedBy: FormatOwnedBy(a.ItemId)));
            }

            foreach (var i in breakdown.Intermediates.Where(x => x.RequiredQuantity > 0))
            {
                var name = itemNames.GetValueOrDefault(i.ItemId) ?? $"Item {i.ItemId}";
                rows.Add(new StepMaterialRow(
                    Item: $"{name} ({i.ItemId})",
                    Required: i.RequiredQuantity.ToString("0.##"),
                    OwnedUsed: i.OwnedUsedQuantity.ToString("0.##"),
                    AcquireOrProduce: i.ToProduceQuantity.ToString("0.##"),
                    Source: $"{i.Kind}: {i.ProducerName}",
                    UnitPrice: "-",
                    LineCost: "-",
                    OwnedBy: FormatOwnedBy(i.ItemId)));
            }
        }

        stepDetailsGrid.DataSource = new BindingList<StepMaterialRow>(rows);
    }

    private string FormatOwnedBy(int itemId)
    {
        if (!ownedByCharacterByItemId.TryGetValue(itemId, out var list) || list.Count == 0)
        {
            return "";
        }

        return string.Join(
            ", ",
            list
                .OrderByDescending(x => x.Qty)
                .ThenBy(x => x.CharacterName, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(x => $"{x.CharacterName}: {x.Qty}"));
    }

    private sealed record StepRow(
        string Skill,
        string Recipe,
        string Chance,
        string ExpectedCrafts,
        string ExpectedCost,
        string Materials,
        int StepIndex);

    private sealed record ShoppingRow(
        string Item,
        string Needed,
        string Owned,
        string Buy,
        string Unit,
        string Cost);

    private sealed record StepMaterialRow(
        string Item,
        string Required,
        string OwnedUsed,
        string AcquireOrProduce,
        string Source,
        string UnitPrice,
        string LineCost,
        string OwnedBy);
}
