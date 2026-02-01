namespace WowAhPlanner.WinForms;

using Microsoft.Extensions.DependencyInjection;
using WowAhPlanner.WinForms.Pages;

public partial class MainForm : Form
{
    private const int MaxContentWidth = 1000;
    private const int SidebarCollapseBreakpointWidth = 900;
    private const int SidebarWidth = 240;

    private bool sidebarCollapsedPreference;
    private bool isSidebarForcedCollapsed;

    private readonly IServiceProvider services;
    private readonly Dictionary<PageId, PageControlBase> pages = new();
    private PageId currentPageId;

    public MainForm(IServiceProvider services)
    {
        this.services = services;

        InitializeComponent();

        ApplyTheme();
        WireShellInteractions();
        ApplyResponsiveLayout();

        Resize += (_, _) => ApplyResponsiveLayout();
        sidebarPanel.Resize += (_, _) => ApplyNavButtonWidths();
        contentOuterPanel.Resize += (_, _) => ApplyResponsiveLayout();
        Shown += (_, _) => ApplyResponsiveLayout();
        Shown += async (_, _) => await NavigateToAsync(PageId.Home);
    }

    private void ApplyTheme()
    {
        BackColor = Ui.Theme.AppBackground;
        ForeColor = Ui.Theme.TextPrimary;

        rootSplit.BackColor = Ui.Theme.Border;
        sidebarPanel.BackColor = Ui.Theme.SidebarBackground;
        mainPanel.BackColor = Ui.Theme.AppBackground;
        headerPanel.BackColor = Ui.Theme.AppBackground;
        headerDivider.BackColor = Ui.Theme.Border;
        contentOuterPanel.BackColor = Ui.Theme.AppBackground;
        pageHostPanel.BackColor = Ui.Theme.AppBackground;

        Ui.Theme.ApplySidebarBrandLabelStyle(brandLabel);

        Ui.Theme.ApplyNavButtonStyle(navHomeButton);
        Ui.Theme.ApplyNavButtonStyle(navRecipesButton);
        Ui.Theme.ApplyNavButtonStyle(navPlanButton);
        Ui.Theme.ApplyNavButtonStyle(navPricesButton);
        Ui.Theme.ApplyNavButtonStyle(navTargetsButton);
        Ui.Theme.ApplyNavButtonStyle(navUploadButton);
        Ui.Theme.ApplyNavButtonStyle(navOwnedButton);

        Ui.Theme.ApplyHeaderTitleStyle(headerTitleLabel);
        Ui.Theme.ApplyHeaderIconButtonStyle(headerMenuButton);
        Ui.Theme.ApplyHeaderButtonStyle(headerActionButton);
    }

    private void WireShellInteractions()
    {
        navHomeButton.Click += async (_, _) => await NavigateToAsync(PageId.Home);
        navRecipesButton.Click += async (_, _) => await NavigateToAsync(PageId.Recipes);
        navPlanButton.Click += async (_, _) => await NavigateToAsync(PageId.Plan);
        navPricesButton.Click += async (_, _) => await NavigateToAsync(PageId.Prices);
        navTargetsButton.Click += async (_, _) => await NavigateToAsync(PageId.Targets);
        navUploadButton.Click += async (_, _) => await NavigateToAsync(PageId.Upload);
        navOwnedButton.Click += async (_, _) => await NavigateToAsync(PageId.Owned);

        navHomeMenuItem.Click += async (_, _) => await NavigateToAsync(PageId.Home);
        navRecipesMenuItem.Click += async (_, _) => await NavigateToAsync(PageId.Recipes);
        navPlanMenuItem.Click += async (_, _) => await NavigateToAsync(PageId.Plan);
        navPricesMenuItem.Click += async (_, _) => await NavigateToAsync(PageId.Prices);
        navTargetsMenuItem.Click += async (_, _) => await NavigateToAsync(PageId.Targets);
        navUploadMenuItem.Click += async (_, _) => await NavigateToAsync(PageId.Upload);
        navOwnedMenuItem.Click += async (_, _) => await NavigateToAsync(PageId.Owned);

        headerMenuButton.Click += (_, _) =>
        {
            if (isSidebarForcedCollapsed)
            {
                navMenuStrip.Show(headerMenuButton, new Point(0, headerMenuButton.Height));
                return;
            }

            sidebarCollapsedPreference = !sidebarCollapsedPreference;
            ApplyResponsiveLayout();
        };

        headerActionButton.Click += async (_, _) =>
        {
            var page = GetOrCreatePage(currentPageId);
            await page.RefreshAsync();
        };
    }

    private async Task NavigateToAsync(PageId pageId)
    {
        currentPageId = pageId;
        ApplyNavActiveState(pageId);

        var page = GetOrCreatePage(pageId);
        headerTitleLabel.Text = page.Title;

        pageHostPanel.SuspendLayout();
        try
        {
            pageHostPanel.Controls.Clear();
            page.Dock = DockStyle.Fill;
            pageHostPanel.Controls.Add(page);
        }
        finally
        {
            pageHostPanel.ResumeLayout(true);
        }

        await page.OnNavigatedToAsync();
    }

    private PageControlBase GetOrCreatePage(PageId pageId)
    {
        if (pages.TryGetValue(pageId, out var existing))
        {
            return existing;
        }

        PageControlBase page = pageId switch
        {
            PageId.Home => ActivatorUtilities.CreateInstance<HomePageControl>(services),
            PageId.Recipes => ActivatorUtilities.CreateInstance<RecipesPageControl>(services),
            PageId.Plan => ActivatorUtilities.CreateInstance<PlanPageControl>(services),
            PageId.Prices => ActivatorUtilities.CreateInstance<PricesPageControl>(services),
            PageId.Targets => ActivatorUtilities.CreateInstance<TargetsPageControl>(services),
            PageId.Upload => ActivatorUtilities.CreateInstance<UploadPageControl>(services),
            PageId.Owned => ActivatorUtilities.CreateInstance<OwnedPageControl>(services),
            _ => new PlaceholderPageControl(pageId.ToString()),
        };

        pages[pageId] = page;
        return page;
    }

    private void ApplyNavActiveState(PageId pageId)
    {
        navHomeButton.IsActive = pageId == PageId.Home;
        navRecipesButton.IsActive = pageId == PageId.Recipes;
        navPlanButton.IsActive = pageId == PageId.Plan;
        navPricesButton.IsActive = pageId == PageId.Prices;
        navTargetsButton.IsActive = pageId == PageId.Targets;
        navUploadButton.IsActive = pageId == PageId.Upload;
        navOwnedButton.IsActive = pageId == PageId.Owned;
    }

    private void ApplyResponsiveLayout()
    {
        if (!IsHandleCreated || rootSplit.Width <= 0)
        {
            return;
        }

        isSidebarForcedCollapsed = ClientSize.Width < SidebarCollapseBreakpointWidth;

        bool canShowSidebar = rootSplit.Width - rootSplit.Panel2MinSize >= rootSplit.Panel1MinSize;
        bool shouldCollapse = isSidebarForcedCollapsed || sidebarCollapsedPreference || !canShowSidebar;

        rootSplit.Panel1Collapsed = shouldCollapse;

        if (!rootSplit.Panel1Collapsed)
        {
            int min = rootSplit.Panel1MinSize;
            int max = Math.Max(min, rootSplit.Width - rootSplit.Panel2MinSize);
            rootSplit.SplitterDistance = Math.Clamp(SidebarWidth, min, max);
        }

        mainPanel.Padding = ClientSize.Width < 980 ? new Padding(16) : new Padding(24);

        int availableWidth = contentOuterPanel.ClientSize.Width;
        int horizontalPadding = Math.Max(0, (availableWidth - MaxContentWidth) / 2);
        contentOuterPanel.Padding = new Padding(horizontalPadding, 0, horizontalPadding, 0);

        ApplyNavButtonWidths();
    }

    private void ApplyNavButtonWidths()
    {
        if (rootSplit.Panel1Collapsed)
        {
            return;
        }

        int targetWidth = sidebarPanel.ClientSize.Width - sidebarPanel.Padding.Left - sidebarPanel.Padding.Right;
        targetWidth = Math.Max(0, targetWidth);

        navHomeButton.Width = targetWidth;
        navRecipesButton.Width = targetWidth;
        navPlanButton.Width = targetWidth;
        navPricesButton.Width = targetWidth;
        navTargetsButton.Width = targetWidth;
        navUploadButton.Width = targetWidth;
        navOwnedButton.Width = targetWidth;
    }
}
