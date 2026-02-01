using System.ComponentModel;

namespace WowAhPlanner.WinForms.Ui;

internal sealed class NavButton : Button
{
    private bool isActive;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsActive
    {
        get => isActive;
        set
        {
            if (isActive == value)
            {
                return;
            }

            isActive = value;
            ApplyStateColors();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ActiveBackColor { get; set; } = SystemColors.ControlDark;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color InactiveBackColor { get; set; } = SystemColors.Control;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverBackColor { get; set; } = SystemColors.ControlLight;

    public NavButton()
    {
        TabStop = false;

        MouseEnter += (_, _) =>
        {
            if (IsActive)
            {
                return;
            }
            BackColor = HoverBackColor;
        };

        MouseLeave += (_, _) =>
        {
            if (IsActive)
            {
                return;
            }
            BackColor = InactiveBackColor;
        };
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        ApplyStateColors();
    }

    private void ApplyStateColors()
    {
        BackColor = IsActive ? ActiveBackColor : InactiveBackColor;
    }
}
