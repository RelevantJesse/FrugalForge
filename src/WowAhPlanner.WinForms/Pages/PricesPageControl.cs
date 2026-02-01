using System.ComponentModel;
using WowAhPlanner.Core.Domain;
using WowAhPlanner.Core.Ports;
using WowAhPlanner.Infrastructure.Pricing;
using WowAhPlanner.WinForms.Services;
using WowAhPlanner.WinForms.Ui;
using Region = WowAhPlanner.Core.Domain.Region;

namespace WowAhPlanner.WinForms.Pages;

internal sealed class PricesPageControl(
    SelectionState selection,
    IRecipeRepository recipeRepository,
    IItemRepository itemRepository,
    PriceBrowseService priceBrowse) : PageControlBase
{
    private readonly Ui.CardPanel filtersCard = new();
    private readonly Label filtersTitleLabel = new();
    private readonly TableLayoutPanel filtersLayout = new();

    private readonly Label professionLabel = new();
    private readonly ComboBox professionComboBox = new();
    private readonly Label providerLabel = new();
    private readonly ComboBox providerComboBox = new();
    private readonly Label searchLabel = new();
    private readonly TextBox searchTextBox = new();
    private readonly Label metaLabel = new();

    private readonly Ui.CardPanel resultsCard = new();
    private readonly Label resultsTitleLabel = new();
    private readonly DataGridView grid = new();

    private bool initialized;
    private bool loading;

    private IReadOnlyList<Profession> professions = [];
    private IReadOnlyList<string> providers = [];
    private Dictionary<int, string> itemNames = new();

    private IReadOnlyList<PriceBrowseService.CachedPriceRow> rows = [];
    private List<PriceBrowseService.CachedPriceRow> filtered = [];
    private HashSet<int>? professionItemIds;

    public override string Title => "Prices";

    public override async Task OnNavigatedToAsync()
    {
        if (!initialized)
        {
            initialized = true;
            BuildUi();
        }

        await LoadAsync();
    }

    public override Task RefreshAsync() => LoadAsync();

    private void BuildUi()
    {
        Ui.Theme.ApplyCardStyle(filtersCard);
        filtersCard.Dock = DockStyle.Top;
        filtersCard.Padding = new Padding(16);
        filtersCard.Margin = new Padding(0, 0, 0, 12);
        filtersCard.AutoSize = true;
        filtersCard.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        filtersTitleLabel.Text = "Filters";
        filtersTitleLabel.AutoSize = true;
        filtersTitleLabel.Dock = DockStyle.Top;
        filtersTitleLabel.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyCardTitleStyle(filtersTitleLabel);

        filtersLayout.ColumnCount = 2;
        filtersLayout.RowCount = 6;
        filtersLayout.Dock = DockStyle.Top;
        filtersLayout.AutoSize = true;
        filtersLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        filtersLayout.Margin = new Padding(0);
        filtersLayout.Padding = new Padding(0);
        filtersLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        filtersLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int i = 0; i < filtersLayout.RowCount; i++)
        {
            filtersLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        professionLabel.Text = "Profession";
        professionLabel.AutoSize = true;
        professionLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(professionLabel);

        professionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        professionComboBox.Dock = DockStyle.Fill;
        professionComboBox.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyComboBoxStyle(professionComboBox);
        professionComboBox.SelectedIndexChanged += async (_, _) => await ReloadAsync();

        providerLabel.Text = "Provider";
        providerLabel.AutoSize = true;
        providerLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(providerLabel);

        providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        providerComboBox.Dock = DockStyle.Left;
        providerComboBox.Width = 280;
        providerComboBox.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyComboBoxStyle(providerComboBox);
        providerComboBox.SelectedIndexChanged += async (_, _) => await ReloadAsync();

        searchLabel.Text = "Search";
        searchLabel.AutoSize = true;
        searchLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(searchLabel);

        searchTextBox.Dock = DockStyle.Fill;
        searchTextBox.Margin = new Padding(0, 0, 0, 12);
        searchTextBox.BackColor = Ui.Theme.AppBackground;
        searchTextBox.ForeColor = Ui.Theme.TextPrimary;
        searchTextBox.BorderStyle = BorderStyle.FixedSingle;
        searchTextBox.TextChanged += (_, _) => ApplyFilters();

        metaLabel.AutoSize = true;
        metaLabel.Margin = new Padding(0);
        metaLabel.ForeColor = Ui.Theme.TextSecondary;

        filtersLayout.Controls.Add(professionLabel, 0, 0);
        filtersLayout.Controls.Add(professionComboBox, 1, 0);
        filtersLayout.Controls.Add(providerLabel, 0, 1);
        filtersLayout.Controls.Add(providerComboBox, 1, 1);
        filtersLayout.Controls.Add(searchLabel, 0, 2);
        filtersLayout.Controls.Add(searchTextBox, 1, 2);
        filtersLayout.Controls.Add(metaLabel, 0, 3);
        filtersLayout.SetColumnSpan(metaLabel, 2);

        filtersCard.Controls.Add(filtersLayout);
        filtersCard.Controls.Add(filtersTitleLabel);

        Ui.Theme.ApplyCardStyle(resultsCard);
        resultsCard.Dock = DockStyle.Fill;
        resultsCard.Padding = new Padding(16);
        resultsCard.Margin = new Padding(0);

        resultsTitleLabel.Text = "Cached prices";
        resultsTitleLabel.AutoSize = true;
        resultsTitleLabel.Dock = DockStyle.Top;
        resultsTitleLabel.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyCardTitleStyle(resultsTitleLabel);

        Ui.Theme.ApplyDataGridViewStyle(grid);
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Item", DataPropertyName = nameof(RowView.Item), MinimumWidth = 240 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Min", DataPropertyName = nameof(RowView.Min), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Median", DataPropertyName = nameof(RowView.Median), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Provider", DataPropertyName = nameof(RowView.Provider), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Snapshot", DataPropertyName = nameof(RowView.Snapshot), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cached", DataPropertyName = nameof(RowView.Cached), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });

        resultsCard.Controls.Add(grid);
        resultsCard.Controls.Add(resultsTitleLabel);

        Controls.Add(resultsCard);
        Controls.Add(filtersCard);
    }

    private async Task LoadAsync()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        try
        {
            await selection.EnsureLoadedAsync();

            professions = await recipeRepository.GetProfessionsAsync(selection.GameVersion, CancellationToken.None);
            itemNames = (await itemRepository.GetItemsAsync(selection.GameVersion, CancellationToken.None))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var realm = selection.ToRealmKey();
            providers = await priceBrowse.GetProvidersAsync(realm, CancellationToken.None);

            var professionOptions = new List<ProfessionOption>
            {
                new ProfessionOption(0, "(all)"),
            };
            professionOptions.AddRange(professions.Select(p => new ProfessionOption(p.ProfessionId, $"{p.Name} ({p.ProfessionId})")));

            professionComboBox.BeginUpdate();
            try
            {
                professionComboBox.DataSource = null;
                professionComboBox.DisplayMember = nameof(ProfessionOption.Display);
                professionComboBox.ValueMember = nameof(ProfessionOption.ProfessionId);
                professionComboBox.DataSource = professionOptions;
                professionComboBox.SelectedItem = professionOptions.FirstOrDefault(x => x.ProfessionId == selection.ProfessionId) ?? professionOptions[0];
            }
            finally
            {
                professionComboBox.EndUpdate();
            }

            var providerOptions = new List<string> { "(all)" };
            providerOptions.AddRange(providers);

            providerComboBox.BeginUpdate();
            try
            {
                providerComboBox.DataSource = null;
                providerComboBox.DataSource = providerOptions;
                providerComboBox.SelectedIndex = 0;
            }
            finally
            {
                providerComboBox.EndUpdate();
            }

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            metaLabel.Text = $"Error: {ex.Message}";
            grid.DataSource = new BindingList<RowView>();
        }
        finally
        {
            loading = false;
        }
    }

    private async Task ReloadAsync()
    {
        if (loading)
        {
            return;
        }

        professionItemIds = null;

        try
        {
            await selection.EnsureLoadedAsync();
            var realm = selection.ToRealmKey();

            var provider = providerComboBox.SelectedItem as string;
            var providerFilter = provider is "(all)" or null ? "" : provider;

            rows = await priceBrowse.GetCachedPricesAsync(realm, providerFilter, CancellationToken.None);

            var professionId = professionComboBox.SelectedItem is ProfessionOption po ? po.ProfessionId : 0;
            if (professionId > 0)
            {
                professionItemIds = await BuildProfessionItemIdsAsync(professionId);
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            metaLabel.Text = $"Error: {ex.Message}";
            rows = [];
            filtered = [];
            grid.DataSource = new BindingList<RowView>();
        }
    }

    private async Task<HashSet<int>> BuildProfessionItemIdsAsync(int professionId)
    {
        var recipes = await recipeRepository.GetRecipesAsync(selection.GameVersion, professionId, CancellationToken.None);
        var ids = new HashSet<int>();

        foreach (var recipe in recipes)
        {
            foreach (var reagent in recipe.Reagents)
            {
                ids.Add(reagent.ItemId);
            }

            if (recipe.Output is { } output)
            {
                ids.Add(output.ItemId);
            }
        }

        return ids;
    }

    private void ApplyFilters()
    {
        var q = (searchTextBox.Text ?? "").Trim();

        IEnumerable<PriceBrowseService.CachedPriceRow> query = rows;
        if (professionItemIds is not null)
        {
            query = query.Where(r => professionItemIds.Contains(r.ItemId));
        }

        if (q.Length > 0)
        {
            query = query.Where(r =>
                r.ItemId.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (itemNames.GetValueOrDefault(r.ItemId) ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        filtered = query.ToList();

        metaLabel.Text = $"Realm: {selection.ToRealmKey()} | Showing: {filtered.Count}";

        var view = new BindingList<RowView>(filtered.Select(r =>
        {
            var name = itemNames.GetValueOrDefault(r.ItemId) ?? $"Item {r.ItemId}";
            return new RowView(
                Item: $"{name} ({r.ItemId})",
                Min: new Money(r.MinBuyoutCopper).ToGscString(),
                Median: r.MedianCopper is long med ? new Money(med).ToGscString() : "-",
                Provider: r.ProviderName,
                Snapshot: r.SnapshotTimestampUtc.ToString("u"),
                Cached: r.CachedAtUtc.ToString("u"));
        }).ToList());

        grid.DataSource = view;
    }

    private sealed record ProfessionOption(int ProfessionId, string Display);

    private sealed record RowView(
        string Item,
        string Min,
        string Median,
        string Provider,
        string Snapshot,
        string Cached);
}

