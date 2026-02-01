using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace WowAhPlanner.WinForms.Ui;

internal sealed class CardPanel : Panel
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = SystemColors.ControlDark;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderThickness { get; set; } = 1;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 12;

    public CardPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = ClientRectangle;
        rect.Inflate(-1, -1);

        using var path = Theme.CreateRoundedRectPath(rect, CornerRadius);
        using var brush = new SolidBrush(BackColor);
        using var pen = new Pen(BorderColor, BorderThickness);

        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
        Invalidate();
    }

    private void UpdateRegion()
    {
        var rect = ClientRectangle;
        rect.Inflate(-1, -1);

        using var path = Theme.CreateRoundedRectPath(rect, CornerRadius);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }
}
