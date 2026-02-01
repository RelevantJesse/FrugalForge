namespace WowAhPlanner.WinForms.Pages;

internal class PageControlBase : UserControl
{
    public virtual string Title => "Page";

    protected PageControlBase()
    {
        BackColor = Ui.Theme.AppBackground;
        ForeColor = Ui.Theme.TextPrimary;
        Margin = new Padding(0);
        Padding = new Padding(0);
        AutoSize = false;
        AutoScroll = true;
    }

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;

    public virtual Task RefreshAsync() => Task.CompletedTask;
}
