using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tabalonia.Controls;
using Xunit;

namespace Tabalonia.Tests;

/// <summary>
/// Horizontal tab strip scrolling: tabs stop shrinking at <see cref="TabsControl.MinTabItemWidth"/>
/// and the strip scrolls behind the overflow buttons instead.
/// </summary>
public class TabScrollingTests : TabsWindowTest
{
    /// <summary>Matches TabsPanel.ScrollStep - one click of an overflow button.</summary>
    private const double ScrollStep = 80.0;

    private (Window Window, TabsControl Tabs, ObservableCollection<string> Items) CreateTabsWindow(
        double width, int tabCount)
    {
        var items = new ObservableCollection<string>(
            Enumerable.Range(0, tabCount).Select(i => $"Tab {i}"));

        var tabs = new TabsControl { ItemsSource = items };
        var window = ShowWindow(new Window { Width = width, Height = 300, Content = tabs });

        return (window, tabs, items);
    }

    private static Button ScrollButton(TabsControl tabs, string name) =>
        tabs.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static DragTabItem Container(TabsControl tabs, int index) =>
        (DragTabItem)tabs.ContainerFromIndex(index)!;

    [AvaloniaFact]
    public void Fluent_Theme_Offers_The_Same_Scroll_Buttons_As_The_Custom_Theme()
    {
        var fluent = new Themes.Fluent.FluentTheme();
        Assert.True(fluent.TryGetResource(typeof(TabsControl), null, out object? fluentTabsTheme));

        var items = new ObservableCollection<string>(
            Enumerable.Range(0, 20).Select(i => $"Tab {i}"));

        // The test app installs the Custom theme globally, so the Fluent one is pinned explicitly
        // rather than relying on which resource scope wins.
        var tabs = new TabsControl
        {
            ItemsSource = items,
            Theme = (ControlTheme)fluentTabsTheme!
        };

        var window = new Window { Width = 400, Height = 300, Content = tabs };
        window.Styles.Add(fluent);

        ShowWindow(window);

        // The Fluent template root is a DockPanel; the Custom one is a Grid.
        Assert.Contains(tabs.GetVisualChildren().Single().GetVisualChildren(), c => c is DockPanel);

        Assert.True(tabs.IsTabStripOverflowing);

        var scrollLeft = ScrollButton(tabs, "PART_ScrollTabsLeftButton");
        var scrollRight = ScrollButton(tabs, "PART_ScrollTabsRightButton");

        Assert.True(scrollLeft.IsVisible);
        Assert.True(scrollRight.IsVisible);
        Assert.False(scrollLeft.IsEnabled);
        Assert.True(scrollRight.IsEnabled);

        // A control theme that produced no content would still be "visible" but occupy nothing.
        Assert.True(scrollLeft.Bounds.Width > 0 && scrollLeft.Bounds.Height > 0);
        Assert.True(scrollRight.Bounds.Width > 0 && scrollRight.Bounds.Height > 0);

        // The buttons are not just present, they are hooked up to the panel.
        double startX = Container(tabs, 0).X;
        Click(scrollRight);

        Assert.Equal(startX - ScrollStep, Container(tabs, 0).X, precision: 1);
        Assert.True(scrollLeft.IsEnabled);
    }

    [AvaloniaFact]
    public void Tab_Strip_Does_Not_Overflow_When_Tabs_Fit()
    {
        var (_, tabs, _) = CreateTabsWindow(width: 800, tabCount: 3);

        Assert.False(tabs.IsTabStripOverflowing);
        Assert.False(tabs.CanScrollLeft);
        Assert.False(tabs.CanScrollRight);
    }

    [AvaloniaFact]
    public void Scroll_Buttons_Are_Hidden_Until_The_Strip_Overflows()
    {
        var (_, fitting, _) = CreateTabsWindow(width: 800, tabCount: 3);

        Assert.False(ScrollButton(fitting, "PART_ScrollTabsLeftButton").IsVisible);
        Assert.False(ScrollButton(fitting, "PART_ScrollTabsRightButton").IsVisible);

        var (_, overflowing, _) = CreateTabsWindow(width: 400, tabCount: 20);

        Assert.True(ScrollButton(overflowing, "PART_ScrollTabsLeftButton").IsVisible);
        Assert.True(ScrollButton(overflowing, "PART_ScrollTabsRightButton").IsVisible);
    }

    [AvaloniaFact]
    public void Overflowing_Strip_Starts_Scrolled_To_The_Left_Edge()
    {
        var (_, tabs, _) = CreateTabsWindow(width: 400, tabCount: 20);

        Assert.True(tabs.IsTabStripOverflowing);
        Assert.False(tabs.CanScrollLeft);
        Assert.True(tabs.CanScrollRight);

        Assert.False(ScrollButton(tabs, "PART_ScrollTabsLeftButton").IsEnabled);
        Assert.True(ScrollButton(tabs, "PART_ScrollTabsRightButton").IsEnabled);
    }

    [AvaloniaFact]
    public void ScrollRight_Button_Shifts_The_Strip_And_Enables_ScrollLeft()
    {
        var (_, tabs, _) = CreateTabsWindow(width: 400, tabCount: 20);

        double startX = Container(tabs, 0).X;

        Click(ScrollButton(tabs, "PART_ScrollTabsRightButton"));

        Assert.Equal(startX - ScrollStep, Container(tabs, 0).X, precision: 1);
        Assert.True(tabs.CanScrollLeft);
        Assert.True(ScrollButton(tabs, "PART_ScrollTabsLeftButton").IsEnabled);
    }

    [AvaloniaFact]
    public void ScrollLeft_Button_Returns_The_Strip_To_Its_Left_Edge()
    {
        var (_, tabs, _) = CreateTabsWindow(width: 400, tabCount: 20);

        double startX = Container(tabs, 0).X;

        Click(ScrollButton(tabs, "PART_ScrollTabsRightButton"));
        Click(ScrollButton(tabs, "PART_ScrollTabsLeftButton"));

        Assert.Equal(startX, Container(tabs, 0).X, precision: 1);
        Assert.False(tabs.CanScrollLeft);
    }

    [AvaloniaFact]
    public void Scrolling_Past_The_End_Clamps_To_The_Last_Tab()
    {
        var (window, tabs, items) = CreateTabsWindow(width: 400, tabCount: 20);

        var scrollRight = ScrollButton(tabs, "PART_ScrollTabsRightButton");

        for (int i = 0; i < 40; i++)
            Click(scrollRight);

        Assert.False(tabs.CanScrollRight);
        Assert.True(tabs.CanScrollLeft);
        Assert.False(scrollRight.IsEnabled);

        // The last tab must be fully inside the strip, not scrolled past it.
        var last = Container(tabs, items.Count - 1);
        Assert.True(last.X + last.Bounds.Width <= window.Width);
    }

    [AvaloniaFact]
    public void Selecting_A_Tab_Scrolled_Out_Of_View_Brings_It_Into_View()
    {
        var (window, tabs, items) = CreateTabsWindow(width: 400, tabCount: 20);

        var last = Container(tabs, items.Count - 1);
        Assert.True(last.X + last.Bounds.Width > window.Width, "the last tab should start off-screen");

        tabs.SelectedIndex = items.Count - 1;
        Dispatcher.UIThread.RunJobs();

        last = Container(tabs, items.Count - 1);
        Assert.True(last.X >= 0);
        Assert.True(last.X + last.Bounds.Width <= window.Width);
    }

    [AvaloniaFact]
    public void MinTabItemWidth_Is_The_Floor_For_Tab_Width()
    {
        // 20 tabs cannot fit in 400px, so each one sits at the floor rather than shrinking further.
        var (_, tabs, _) = CreateTabsWindow(width: 400, tabCount: 20);

        Assert.Equal(60, tabs.MinTabItemWidth);
        Assert.Equal(60, Container(tabs, 0).Bounds.Width, precision: 1);

        // Raising the floor after the first layout pass has to re-measure the strip.
        tabs.MinTabItemWidth = 100;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(100, Container(tabs, 0).Bounds.Width, precision: 1);
    }

    [AvaloniaFact]
    public void Tabs_Shrink_Instead_Of_Overflowing_When_There_Is_No_Minimum_Width()
    {
        var (_, tabs, _) = CreateTabsWindow(width: 400, tabCount: 20);
        Assert.True(tabs.IsTabStripOverflowing);

        tabs.MinTabItemWidth = 0;
        Dispatcher.UIThread.RunJobs();

        Assert.False(tabs.IsTabStripOverflowing);
        Assert.False(tabs.CanScrollRight);
        Assert.True(Container(tabs, 0).Bounds.Width < 60);
    }

    [AvaloniaFact]
    public void Narrowing_TabItemWidth_At_Runtime_Removes_The_Overflow()
    {
        var (_, tabs, _) = CreateTabsWindow(width: 400, tabCount: 8);
        Assert.True(tabs.IsTabStripOverflowing);

        // TabItemWidth is the ceiling, MinTabItemWidth the floor; drop both below the share each
        // tab would get and the strip fits again.
        tabs.MinTabItemWidth = 10;
        tabs.TabItemWidth = 20;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(20, Container(tabs, 0).Bounds.Width, precision: 1);
        Assert.False(tabs.IsTabStripOverflowing);
    }
}
