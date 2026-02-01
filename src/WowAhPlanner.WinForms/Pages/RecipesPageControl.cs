using System.ComponentModel;
using WowAhPlanner.Core.Domain;
using WowAhPlanner.Core.Ports;
using WowAhPlanner.WinForms.Services;

namespace WowAhPlanner.WinForms.Pages;

internal sealed class RecipesPageControl(
    SelectionState selection,
    IRecipeRepository recipeRepository,
    PlannerPreferences plannerPreferences) : PageControlBase
{
    private readonly TableLayoutPanel pageLayout = new();
    private readonly Ui.CardPanel filterCard = new();
    private readonly TableLayoutPanel filterLayout = new();
    private readonly Label professionLabel = new();
    private readonly ComboBox professionComboBox = new();

    private readonly Ui.CardPanel resultsCard = new();
    private readonly Label packLabel = new();
    private readonly DataGridView grid = new();

    private IReadOnlyList<Profession> professions = [];
    private BindingList<RecipeRow> rows = new();
    private HashSet<string> excluded = new(StringComparer.OrdinalIgnoreCase);

    private bool initialized;
    private bool loading;

    public override string Title => "Recipes";

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
        pageLayout.ColumnCount = 1;
        pageLayout.RowCount = 2;
        pageLayout.Dock = DockStyle.Fill;
        pageLayout.Margin = new Padding(0);
        pageLayout.Padding = new Padding(0);
        pageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Ui.Theme.ApplyCardStyle(filterCard);
        filterCard.Dock = DockStyle.Top;
        filterCard.Padding = new Padding(16);
        filterCard.Margin = new Padding(0, 0, 0, 12);
        filterCard.AutoSize = true;
        filterCard.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        filterLayout.ColumnCount = 2;
        filterLayout.RowCount = 2;
        filterLayout.Dock = DockStyle.Top;
        filterLayout.AutoSize = true;
        filterLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        filterLayout.Margin = new Padding(0);
        filterLayout.Padding = new Padding(0);
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        filterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        filterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        professionLabel.Text = "Profession";
        professionLabel.AutoSize = true;
        professionLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(professionLabel);

        professionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        professionComboBox.Dock = DockStyle.Fill;
        professionComboBox.Margin = new Padding(0);
        Ui.Theme.ApplyComboBoxStyle(professionComboBox);
        professionComboBox.SelectedIndexChanged += async (_, _) => await OnProfessionChangedAsync();

        filterLayout.Controls.Add(professionLabel, 0, 0);
        filterLayout.Controls.Add(professionComboBox, 1, 0);

        filterCard.Controls.Add(filterLayout);

        Ui.Theme.ApplyCardStyle(resultsCard);
        resultsCard.Dock = DockStyle.Fill;
        resultsCard.Padding = new Padding(16);
        resultsCard.Margin = new Padding(0);

        packLabel.AutoSize = true;
        packLabel.Dock = DockStyle.Top;
        packLabel.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyCardTitleStyle(packLabel);

        grid.Dock = DockStyle.Fill;
        grid.AutoSize = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowTemplate.Height = 28;
        Ui.Theme.ApplyDataGridViewStyle(grid);

        var excludeColumn = new DataGridViewCheckBoxColumn
        {
            Name = "Exclude",
            HeaderText = "Exclude",
            DataPropertyName = nameof(RecipeRow.Excluded),
            Width = 70,
            ReadOnly = false,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Recipe",
            HeaderText = "Recipe",
            DataPropertyName = nameof(RecipeRow.RecipeDisplay),
            MinimumWidth = 220,
        });
        grid.Columns.Add(excludeColumn);
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Min", HeaderText = "Min", DataPropertyName = nameof(RecipeRow.MinSkill), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Orange", HeaderText = "Orange", DataPropertyName = nameof(RecipeRow.OrangeUntil), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Yellow", HeaderText = "Yellow", DataPropertyName = nameof(RecipeRow.YellowUntil), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Green", HeaderText = "Green", DataPropertyName = nameof(RecipeRow.GreenUntil), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Gray", HeaderText = "Gray", DataPropertyName = nameof(RecipeRow.GrayAt), AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Reagents",
            HeaderText = "Reagents",
            DataPropertyName = nameof(RecipeRow.ReagentsDisplay),
            MinimumWidth = 200,
        });

        grid.ReadOnly = false;
        grid.CellContentClick += async (_, e) =>
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (grid.Columns[e.ColumnIndex].Name != "Exclude")
            {
                return;
            }

            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);

            if (grid.Rows[e.RowIndex].DataBoundItem is RecipeRow row)
            {
                await ToggleExcludedAsync(row);
            }
        };

        grid.DataError += (_, _) => { };

        resultsCard.Controls.Add(grid);
        resultsCard.Controls.Add(packLabel);

        pageLayout.Controls.Add(filterCard, 0, 0);
        pageLayout.Controls.Add(resultsCard, 0, 1);

        Controls.Add(pageLayout);
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

            await LoadRecipesAsync(selectedProfessionId);
        }
        finally
        {
            loading = false;
        }
    }

    private async Task LoadRecipesAsync(int professionId)
    {
        packLabel.Text = $"Pack: {selection.GameVersion}";

        excluded = new HashSet<string>(
            plannerPreferences.GetExcludedRecipeIds(selection.GameVersion, professionId),
            StringComparer.OrdinalIgnoreCase);

        var recipes = professionId == 0
            ? []
            : await recipeRepository.GetRecipesAsync(selection.GameVersion, professionId, CancellationToken.None);

        rows = new BindingList<RecipeRow>(recipes
            .Select(r => new RecipeRow(
                RecipeId: r.RecipeId,
                RecipeDisplay: $"{r.Name} ({r.RecipeId})",
                Excluded: excluded.Contains(r.RecipeId),
                MinSkill: r.MinSkill,
                OrangeUntil: r.OrangeUntil,
                YellowUntil: r.YellowUntil,
                GreenUntil: r.GreenUntil,
                GrayAt: r.GrayAt,
                ReagentsDisplay: string.Join(", ", r.Reagents.Select(x => $"{x.Quantity}x {x.ItemId}"))))
            .ToList());

        grid.DataSource = rows;
    }

    private async Task OnProfessionChangedAsync()
    {
        if (loading)
        {
            return;
        }

        if (professionComboBox.SelectedItem is not Profession profession)
        {
            return;
        }

        selection.ProfessionId = profession.ProfessionId;
        await selection.PersistAsync();
        await LoadRecipesAsync(profession.ProfessionId);
    }

    private async Task ToggleExcludedAsync(RecipeRow row)
    {
        row.Excluded = !row.Excluded;
        rows.ResetItem(rows.IndexOf(row));

        await plannerPreferences.SetRecipeExcludedAsync(selection.GameVersion, selection.ProfessionId, row.RecipeId, row.Excluded);
    }

    private sealed class RecipeRow : INotifyPropertyChanged
    {
        public string RecipeId { get; }
        public string RecipeDisplay { get; }
        public int MinSkill { get; }
        public int OrangeUntil { get; }
        public int YellowUntil { get; }
        public int GreenUntil { get; }
        public int GrayAt { get; }
        public string ReagentsDisplay { get; }

        private bool excludedValue;
        public bool Excluded
        {
            get => excludedValue;
            set
            {
                if (excludedValue == value)
                {
                    return;
                }

                excludedValue = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Excluded)));
            }
        }

        public RecipeRow(
            string RecipeId,
            string RecipeDisplay,
            bool Excluded,
            int MinSkill,
            int OrangeUntil,
            int YellowUntil,
            int GreenUntil,
            int GrayAt,
            string ReagentsDisplay)
        {
            this.RecipeId = RecipeId;
            this.RecipeDisplay = RecipeDisplay;
            excludedValue = Excluded;
            this.MinSkill = MinSkill;
            this.OrangeUntil = OrangeUntil;
            this.YellowUntil = YellowUntil;
            this.GreenUntil = GreenUntil;
            this.GrayAt = GrayAt;
            this.ReagentsDisplay = ReagentsDisplay;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
