using System.Drawing.Drawing2D;

namespace WowAhPlanner.WinForms.Ui;

internal static class Theme
{
    // Align with src/WowAhPlanner.Web/wwwroot/css/site.css
    public static readonly Color AppBackground = Color.FromArgb(0x0F, 0x17, 0x2A);
    public static readonly Color SidebarBackground = Color.FromArgb(0x11, 0x18, 0x27);
    public static readonly Color CardBackground = Color.FromArgb(0x0B, 0x12, 0x20);

    public static readonly Color TextPrimary = Color.FromArgb(0xE2, 0xE8, 0xF0);
    public static readonly Color TextSecondary = Color.FromArgb(0xCB, 0xD5, 0xE1);

    public static readonly Color Border = Color.FromArgb(0x1F, 0x29, 0x37);
    public static readonly Color BorderStrong = Color.FromArgb(0x33, 0x41, 0x55);

    public static readonly Color Accent = Color.FromArgb(0x25, 0x63, 0xEB);
    public static readonly Color AccentHover = Color.FromArgb(0x1D, 0x4E, 0xD8);

    public static readonly Color NavItemBackground = CardBackground;
    public static readonly Color NavItemActiveBackground = Border;

    public static void ApplySidebarBrandLabelStyle(Label label)
    {
        label.ForeColor = TextPrimary;
        label.Font = new Font(label.Font.FontFamily, 11F, FontStyle.Bold);
    }

    public static void ApplyHeaderTitleStyle(Label label)
    {
        label.ForeColor = TextPrimary;
        label.Font = new Font(label.Font.FontFamily, 12F, FontStyle.Bold);
    }

    public static void ApplyHeaderIconButtonStyle(Button button)
    {
        ApplySecondaryButtonStyle(button);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.FlatAppearance.BorderColor = Border;
    }

    public static void ApplyHeaderButtonStyle(Button button)
    {
        ApplySecondaryButtonStyle(button);
        button.TextAlign = ContentAlignment.MiddleCenter;
    }

    public static void ApplyNavButtonStyle(NavButton button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
        button.BackColor = NavItemBackground;
        button.ForeColor = TextPrimary;
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(12, 0, 12, 0);
        button.Height = 36;
        button.ActiveBackColor = NavItemActiveBackground;
        button.HoverBackColor = Border;
        button.InactiveBackColor = NavItemBackground;
    }

    public static void ApplyCardStyle(CardPanel panel)
    {
        panel.BackColor = CardBackground;
        panel.BorderColor = Border;
        panel.BorderThickness = 1;
        panel.CornerRadius = 12;
    }

    public static void ApplyCardTitleStyle(Label label)
    {
        label.ForeColor = TextPrimary;
        label.Font = new Font(label.Font.FontFamily, 10.5F, FontStyle.Bold);
    }

    public static void ApplyCardBodyStyle(Label label)
    {
        label.ForeColor = TextSecondary;
        label.Font = new Font(label.Font.FontFamily, 9.5F, FontStyle.Regular);
    }

    public static void ApplyInputLabelStyle(Label label)
    {
        label.ForeColor = TextSecondary;
        label.Font = new Font(label.Font.FontFamily, 9F, FontStyle.Regular);
    }

    public static void ApplyComboBoxStyle(ComboBox comboBox)
    {
        comboBox.BackColor = AppBackground;
        comboBox.ForeColor = TextPrimary;
        comboBox.FlatStyle = FlatStyle.Flat;
    }

    public static void ApplyDataGridViewStyle(DataGridView grid)
    {
        grid.BackgroundColor = AppBackground;
        grid.GridColor = Border;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;

        grid.ColumnHeadersDefaultCellStyle.BackColor = SidebarBackground;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SidebarBackground;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;

        grid.DefaultCellStyle.BackColor = CardBackground;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = Border;
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;

        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
    }

    public static void ApplyPrimaryButtonStyle(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = BorderStrong;
        button.BackColor = Accent;
        button.ForeColor = Color.White;

        button.MouseEnter += (_, _) => button.BackColor = AccentHover;
        button.MouseLeave += (_, _) => button.BackColor = Accent;
    }

    public static void ApplySecondaryButtonStyle(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = BorderStrong;
        button.BackColor = AppBackground;
        button.ForeColor = TextPrimary;
    }

    internal static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
    {
        if (radius <= 0)
        {
            var path = new GraphicsPath();
            path.AddRectangle(bounds);
            return path;
        }

        int diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path2 = new GraphicsPath();

        // top-left
        path2.AddArc(arc, 180, 90);
        // top-right
        arc.X = bounds.Right - diameter;
        path2.AddArc(arc, 270, 90);
        // bottom-right
        arc.Y = bounds.Bottom - diameter;
        path2.AddArc(arc, 0, 90);
        // bottom-left
        arc.X = bounds.Left;
        path2.AddArc(arc, 90, 90);

        path2.CloseFigure();
        return path2;
    }
}
