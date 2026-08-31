using System.Collections;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Tabalonia.Controls;
using Xunit;

namespace Tabalonia.Tests;

/// <summary>
/// Where a torn-off tab ends up: the host supplied by
/// <see cref="TabsControl.DetachedHostFactory"/>, and the guard that keeps the last tab of a window
/// attached instead of tearing it into an identical, empty-behind-it window.
/// </summary>
public class TabDetachHostTests : TabsWindowTest
{
    /// <summary>Well clear of every tab strip, so a tab dragged here is torn off.</summary>
    private static readonly Point EmptySpace = new(300, 350);

    private (Window Window, TabsControl Tabs, ObservableCollection<string> Items) CreateTabsWindow(
        params string[] items)
    {
        var itemsSource = new ObservableCollection<string>(items);

        var tabs = new TabsControl
        {
            ItemsSource = itemsSource,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };

        var window = ShowWindow(new Window { Width = 600, Height = 400, Content = tabs });

        return (window, tabs, itemsSource);
    }

    private static Point TabCenter(Window window, TabsControl tabs, int index)
    {
        var container = (DragTabItem)tabs.ContainerFromIndex(index)!;
        Point topLeft = container.TranslatePoint(new Point(0, 0), window)!.Value;

        return topLeft + new Vector(container.Bounds.Width / 2, container.Bounds.Height / 2);
    }

    private static void DragTabTo(Window window, TabsControl tabs, int index, Point target)
    {
        Point start = TabCenter(window, tabs, index);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(5, 0), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        window.MouseMove(target, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        window.MouseUp(target, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static IList Models(TabsControl tabs) => (IList)tabs.ItemsSource!;

    [AvaloniaFact]
    public void DetachedHostFactory_Receives_The_Torn_Off_Tab()
    {
        var (window, tabs, items) = CreateTabsWindow("A", "B");

        // The factory window is not shown yet, so its strip is not a drop target during the drag.
        var host = new TabsControl { ItemsSource = new ObservableCollection<object?>() };
        var hostWindow = Track(new Window { Width = 400, Height = 300, Content = host });

        TabsControl? factoryArgument = null;
        tabs.DetachedHostFactory = source =>
        {
            factoryArgument = source;

            return (host, hostWindow);
        };

        DragTabTo(window, tabs, 1, EmptySpace);

        Assert.Same(tabs, factoryArgument);
        Assert.Equal(["A"], items);
        Assert.Equal(["B"], Models(host).Cast<object>());
        Assert.Equal("B", host.SelectedItem);
        Assert.True(hostWindow.IsVisible);
    }

    [AvaloniaFact]
    public void Detaching_Falls_Back_To_A_Built_In_Host_When_No_Factory_Is_Set()
    {
        var (window, tabs, items) = CreateTabsWindow("A", "B");

        TabsControl? detachedHost = null;
        tabs.DetachedWindowFactory = host =>
        {
            detachedHost = host;

            return Track(new Window { Width = 400, Height = 300, Content = host });
        };

        DragTabTo(window, tabs, 1, EmptySpace);

        Assert.Equal(["A"], items);
        Assert.NotNull(detachedHost);
        Assert.NotSame(tabs, detachedHost);
        Assert.Equal(["B"], Models(detachedHost).Cast<object>());
    }

    [AvaloniaFact]
    public void Dragging_The_Only_Tab_Away_Does_Not_Tear_It_Off()
    {
        var (window, tabs, items) = CreateTabsWindow("A");

        bool hostFactoryCalled = false;
        tabs.DetachedHostFactory = _ =>
        {
            hostFactoryCalled = true;

            return null;
        };

        DragTabTo(window, tabs, 0, EmptySpace);

        // Tearing the last tab off would only swap one window for another identical one.
        Assert.False(hostFactoryCalled);
        Assert.Equal(["A"], items);
        Assert.Equal("A", tabs.SelectedItem);
    }

    [AvaloniaFact]
    public void The_Last_Tab_Can_Still_Be_Dropped_Into_Another_Strip()
    {
        var (sourceWindow, sourceTabs, sourceItems) = CreateTabsWindow("A");

        var targetItems = new ObservableCollection<string> { "B" };
        var targetTabs = new TabsControl
        {
            ItemsSource = targetItems,
            Margin = new Thickness(0, 250, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        var targetWindow = ShowWindow(new Window { Width = 600, Height = 500, Content = targetTabs });

        // The single-tab guard only blocks tearing off, not docking into an existing strip.
        Point overTargetStrip = TabCenter(targetWindow, targetTabs, 0) + new Vector(160, 0);
        DragTabTo(sourceWindow, sourceTabs, 0, overTargetStrip);

        Assert.Empty(sourceItems);
        Assert.Equal(["B", "A"], targetItems);
    }
}
