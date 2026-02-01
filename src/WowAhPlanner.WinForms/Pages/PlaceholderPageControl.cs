namespace WowAhPlanner.WinForms.Pages;

internal sealed class PlaceholderPageControl(string title) : PageControlBase
{
    private readonly Label label = new();

    public override string Title => title;

    public override Task OnNavigatedToAsync()
    {
        if (Controls.Count == 0)
        {
            label.Text = $"'{title}' page not implemented yet.";
            label.AutoSize = true;
            label.Margin = new Padding(0);
            label.ForeColor = Ui.Theme.TextSecondary;
            Controls.Add(label);
        }

        return Task.CompletedTask;
    }
}

