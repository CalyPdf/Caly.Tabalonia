using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tabalonia.Panels;
using Xunit;

namespace Tabalonia.Tests;

/// <summary>
/// <see cref="TopPanel"/> lays out the tab strip: the tabs get whatever width the other parts
/// (drag thumbs, add button, scroll buttons, side content) leave.
/// </summary>
public class TopPanelTests
{
    private const double StripHeight = 30;

    /// <summary>
    /// A panel with every part the templates provide, no tabs, and a right drag thumb whose
    /// minimum width is wider than the strip (like Caly's <c>RightThumbWidth="300"</c>), so that
    /// measuring at a narrow width shrinks the other parts to fill the strip exactly.
    /// </summary>
    private static (TopPanel Panel, Control Tabs) CreatePanel()
    {
        Control Part(string name, double width) => new Border { Name = name, Width = width, Height = StripHeight };

        var tabs = Part("PART_ItemsPresenter", 0);
        var panel = new TopPanel
        {
            // The headless platform renders at 100%, where rounding would snap a fractional
            // arrange width back to the measured one and hide the shortfall that fractional
            // display scalings produce.
            UseLayoutRounding = false,
            Children =
            {
                Part("PART_LeftContent", 0),
                Part("PART_LeftDragWindowThumb", 4),
                tabs,
                Part("PART_AddItemButton", 38),
                Part("PART_ScrollTabsLeftButton", 0),
                Part("PART_ScrollTabsRightButton", 0),
                new Border { Name = "PART_RightDragWindowThumb", MinWidth = 300, Height = StripHeight },
                Part("PART_RightContent", 0),
            }
        };

        return (panel, tabs);
    }

    /// <summary>
    /// Layout rounding at some display scalings (110%, 220%, 270%...) arranges the strip a
    /// fraction of a pixel narrower than it was measured. What is left for the tabs then goes
    /// negative, and Arrange threw "Invalid Arrange rectangle" - crashing Caly on startup
    /// (https://github.com/CalyPdf/CalyPdf/issues/321).
    /// </summary>
    [AvaloniaTheory]
    [InlineData(0.01)]
    [InlineData(0.4)]
    [InlineData(10)]
    public void Arranged_Narrower_Than_Measured_When_Other_Parts_Fill_The_Strip_Gives_Tabs_No_Width(double shortfall)
    {
        var (panel, tabs) = CreatePanel();
        const double measuredWidth = 250;

        panel.Measure(new Size(measuredWidth, StripHeight));
        double otherParts = panel.Children.Where(c => c != tabs).Sum(c => c.DesiredSize.Width);
        Assert.Equal(measuredWidth, otherParts, 6); // The other parts fill the strip exactly

        panel.Arrange(new Rect(0, 0, measuredWidth - shortfall, StripHeight));

        Assert.Equal(0, tabs.Bounds.Width);
    }

    [AvaloniaFact]
    public void Arranged_At_The_Measured_Width_Gives_Tabs_What_The_Other_Parts_Leave()
    {
        var (panel, tabs) = CreatePanel();

        panel.Measure(new Size(500, StripHeight));
        panel.Arrange(new Rect(0, 0, 500, StripHeight));

        // 500 - (4 + 38 + 300): the tabs fit, so they keep their own (zero) width, placed after the left thumb.
        Assert.Equal(0, tabs.Bounds.Width);
        Assert.Equal(4, tabs.Bounds.X);
    }
}
