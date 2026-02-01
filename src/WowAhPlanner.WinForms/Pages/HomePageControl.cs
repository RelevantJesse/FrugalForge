using WowAhPlanner.Core.Domain;
using Region = WowAhPlanner.Core.Domain.Region;
using WowAhPlanner.WinForms.Services;

namespace WowAhPlanner.WinForms.Pages;

internal sealed class HomePageControl(SelectionState selection, RealmCatalog realms) : PageControlBase
{
    private readonly Ui.CardPanel realmCard = new();
    private readonly Label realmTitleLabel = new();
    private readonly TableLayoutPanel realmGrid = new();
    private readonly Label versionLabel = new();
    private readonly ComboBox versionComboBox = new();
    private readonly Label regionLabel = new();
    private readonly ComboBox regionComboBox = new();
    private readonly Label realmLabel = new();
    private readonly ComboBox realmComboBox = new();
    private readonly Label realmKeyLabel = new();

    private readonly Ui.CardPanel nextCard = new();
    private readonly Label nextTitleLabel = new();
    private readonly Label nextBodyLabel = new();

    private bool loading;

    public override string Title => "Home";

    public override async Task OnNavigatedToAsync()
    {
        if (Controls.Count == 0)
        {
            BuildUi();
        }

        if (!loading)
        {
            loading = true;
            await selection.EnsureLoadedAsync();
            ApplySelectionToUi();
            RefreshRealms();
            await selection.PersistAsync();
            loading = false;
        }
    }

    public override async Task RefreshAsync()
    {
        await selection.EnsureLoadedAsync();
        ApplySelectionToUi();
        RefreshRealms();
        await selection.PersistAsync();
    }

    private void BuildUi()
    {
        Ui.Theme.ApplyCardStyle(realmCard);
        realmCard.Dock = DockStyle.Top;
        realmCard.Padding = new Padding(16);
        realmCard.Margin = new Padding(0, 0, 0, 12);
        realmCard.AutoSize = true;
        realmCard.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        realmTitleLabel.Text = "Realm";
        realmTitleLabel.AutoSize = true;
        realmTitleLabel.Dock = DockStyle.Top;
        realmTitleLabel.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyCardTitleStyle(realmTitleLabel);

        realmGrid.ColumnCount = 3;
        realmGrid.RowCount = 6;
        realmGrid.Dock = DockStyle.Top;
        realmGrid.AutoSize = true;
        realmGrid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        realmGrid.Margin = new Padding(0);
        realmGrid.Padding = new Padding(0);
        realmGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        realmGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        realmGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0F));
        for (int i = 0; i < realmGrid.RowCount; i++)
        {
            realmGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        versionLabel.Text = "Game version";
        versionLabel.AutoSize = true;
        versionLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(versionLabel);

        versionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        versionComboBox.Dock = DockStyle.Fill;
        versionComboBox.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyComboBoxStyle(versionComboBox);
        versionComboBox.Items.AddRange(Enum.GetValues<GameVersion>().Cast<object>().ToArray());
        versionComboBox.SelectedIndexChanged += async (_, _) => await OnVersionOrRegionChangedAsync();

        regionLabel.Text = "Region";
        regionLabel.AutoSize = true;
        regionLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(regionLabel);

        regionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        regionComboBox.Dock = DockStyle.Fill;
        regionComboBox.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyComboBoxStyle(regionComboBox);
        regionComboBox.Items.AddRange(Enum.GetValues<Region>().Cast<object>().ToArray());
        regionComboBox.SelectedIndexChanged += async (_, _) => await OnVersionOrRegionChangedAsync();

        realmLabel.Text = "Realm";
        realmLabel.AutoSize = true;
        realmLabel.Margin = new Padding(0, 6, 8, 6);
        Ui.Theme.ApplyInputLabelStyle(realmLabel);

        realmComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        realmComboBox.Dock = DockStyle.Fill;
        realmComboBox.Margin = new Padding(0, 0, 0, 12);
        Ui.Theme.ApplyComboBoxStyle(realmComboBox);
        realmComboBox.SelectedIndexChanged += async (_, _) => await OnRealmChangedAsync();
        realmComboBox.TextChanged += async (_, _) => await OnRealmChangedAsync();

        realmKeyLabel.AutoSize = true;
        realmKeyLabel.Margin = new Padding(0);
        realmKeyLabel.ForeColor = Ui.Theme.TextSecondary;

        realmGrid.Controls.Add(versionLabel, 0, 0);
        realmGrid.Controls.Add(versionComboBox, 1, 0);
        realmGrid.Controls.Add(regionLabel, 0, 1);
        realmGrid.Controls.Add(regionComboBox, 1, 1);
        realmGrid.Controls.Add(realmLabel, 0, 2);
        realmGrid.Controls.Add(realmComboBox, 1, 2);
        realmGrid.Controls.Add(realmKeyLabel, 0, 3);
        realmGrid.SetColumnSpan(realmKeyLabel, 2);

        realmCard.Controls.Add(realmGrid);
        realmCard.Controls.Add(realmTitleLabel);

        Ui.Theme.ApplyCardStyle(nextCard);
        nextCard.Dock = DockStyle.Top;
        nextCard.Padding = new Padding(16);
        nextCard.Margin = new Padding(0, 0, 0, 12);
        nextCard.AutoSize = true;
        nextCard.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        nextTitleLabel.Text = "Next";
        nextTitleLabel.AutoSize = true;
        nextTitleLabel.Dock = DockStyle.Top;
        nextTitleLabel.Margin = new Padding(0, 0, 0, 8);
        Ui.Theme.ApplyCardTitleStyle(nextTitleLabel);

        nextBodyLabel.Text = "Use Recipes to browse the pack, Plan to generate a leveling plan, and Upload/Owned to import data from the addon.";
        nextBodyLabel.AutoSize = true;
        nextBodyLabel.Dock = DockStyle.Top;
        nextBodyLabel.Margin = new Padding(0);
        Ui.Theme.ApplyCardBodyStyle(nextBodyLabel);

        nextCard.Controls.Add(nextBodyLabel);
        nextCard.Controls.Add(nextTitleLabel);

        Controls.Add(nextCard);
        Controls.Add(realmCard);
    }

    private void ApplySelectionToUi()
    {
        versionComboBox.SelectedItem = selection.GameVersion;
        regionComboBox.SelectedItem = selection.Region;
        realmComboBox.Text = selection.RealmSlug;
        realmKeyLabel.Text = $"Current realm key: {selection.ToRealmKey()}";
    }

    private async Task OnVersionOrRegionChangedAsync()
    {
        if (loading)
        {
            return;
        }

        selection.GameVersion = versionComboBox.SelectedItem is GameVersion v ? v : selection.GameVersion;
        selection.Region = regionComboBox.SelectedItem is Region r ? r : selection.Region;

        RefreshRealms();
        await selection.PersistAsync();

        realmKeyLabel.Text = $"Current realm key: {selection.ToRealmKey()}";
    }

    private async Task OnRealmChangedAsync()
    {
        if (loading)
        {
            return;
        }

        selection.RealmSlug = GetRealmSlugFromComboBox();
        await selection.PersistAsync();

        realmKeyLabel.Text = $"Current realm key: {selection.ToRealmKey()}";
    }

    private void RefreshRealms()
    {
        var list = realms.GetRealms(selection.Region, selection.GameVersion);

        realmComboBox.BeginUpdate();
        try
        {
            var selected = selection.RealmSlug;
            realmComboBox.DataSource = null;
            realmComboBox.DisplayMember = nameof(Realm.Name);
            realmComboBox.ValueMember = nameof(Realm.Slug);
            realmComboBox.DataSource = list.ToArray();

            if (list.Count > 0)
            {
                var match = list.FirstOrDefault(x => x.Slug == selected) ?? list[0];
                realmComboBox.SelectedItem = match;
                realmComboBox.Text = match.Slug;
                selection.RealmSlug = match.Slug;
            }
            else
            {
                realmComboBox.Text = selected;
            }
        }
        finally
        {
            realmComboBox.EndUpdate();
        }
    }

    private string GetRealmSlugFromComboBox()
    {
        if (realmComboBox.SelectedItem is Realm realm)
        {
            return realm.Slug;
        }

        return (realmComboBox.Text ?? "").Trim();
    }
}
