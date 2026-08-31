using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Tabalonia.Controls;
using Tabalonia.Events;
using Xunit;

namespace Tabalonia.Tests;

/// <summary>
/// The <see cref="TabsControl.TabDragStarted"/> / <see cref="TabsControl.TabDragCompleted"/>
/// notifications, and the in-place reorder that keeps every container on the model it already had.
/// </summary>
public class TabDragEventTests : TabsWindowTest
{
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
        TrackDetachedWindowsOf(tabs);

        return (window, tabs, itemsSource);
    }

    private static DragTabItem Container(TabsControl tabs, int index) =>
        (DragTabItem)tabs.ContainerFromIndex(index)!;

    private static Point TabCenter(Window window, TabsControl tabs, int index)
    {
        var container = Container(tabs, index);
        Point topLeft = container.TranslatePoint(new Point(0, 0), window)!.Value;

        return topLeft + new Vector(container.Bounds.Width / 2, container.Bounds.Height / 2);
    }

    /// <summary>Drags a tab sideways by <paramref name="offsetX"/> and releases it.</summary>
    private static void DragTabBy(Window window, TabsControl tabs, int index, double offsetX)
    {
        Point start = TabCenter(window, tabs, index);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(5, 0), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        Point end = start + new Vector(offsetX, 0);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        window.MouseUp(end, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TabDragStarted_Reports_The_Dragged_Tab()
    {
        var (window, tabs, _) = CreateTabsWindow("A", "B", "C");

        DragTabDragStartedEventArgs? started = null;
        tabs.TabDragStarted = (_, e) => started = e;

        Point start = TabCenter(window, tabs, 1);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(5, 0), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(started);
        Assert.Same(Container(tabs, 1), started.TabItem);

        window.MouseUp(start + new Vector(5, 0), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TabDragCompleted_Is_Raised_Only_When_The_Pointer_Is_Released()
    {
        var (window, tabs, _) = CreateTabsWindow("A", "B", "C");

        DragTabDragCompletedEventArgs? completed = null;
        tabs.TabDragCompleted = (_, e) => completed = e;

        Point start = TabCenter(window, tabs, 1);
        var dragged = Container(tabs, 1);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(5, 0), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(completed);

        window.MouseUp(start + new Vector(5, 0), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(completed);
        Assert.Same(dragged, completed.TabItem);
    }

    [AvaloniaFact]
    public void Dragging_A_Tab_Selects_It()
    {
        var (window, tabs, _) = CreateTabsWindow("A", "B", "C");
        tabs.SelectedIndex = 0;

        DragTabBy(window, tabs, 2, offsetX: 5);

        Assert.Equal("C", tabs.SelectedItem);
    }

    [AvaloniaFact]
    public void Closing_The_Dragged_Tab_Mid_Drag_Does_Not_Bring_It_Back()
    {
        var (window, tabs, items) = CreateTabsWindow("A", "B", "C");

        Point start = TabCenter(window, tabs, 1);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(5, 0), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();

        // The tab is closed from under the drag, by its close button or the owning view model.
        tabs.CloseItemCommand.Execute(Container(tabs, 1));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["A", "C"], items);

        // Finishing the gesture must not reorder the strip or resurrect the closed tab.
        window.MouseUp(start + new Vector(5, 0), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["A", "C"], items);
    }

    [AvaloniaFact]
    public void Dragging_A_Tab_Past_Its_Neighbour_Reorders_The_Models()
    {
        var (window, tabs, items) = CreateTabsWindow("A", "B", "C");

        double step = Container(tabs, 1).X - Container(tabs, 0).X;

        DragTabBy(window, tabs, 0, step);

        Assert.Equal(["B", "A", "C"], items);
        Assert.Equal("A", tabs.SelectedItem);
    }

    [AvaloniaFact]
    public void Dragging_A_Tab_Less_Than_Half_Way_Leaves_The_Order_Alone()
    {
        var (window, tabs, items) = CreateTabsWindow("A", "B", "C");

        double step = Container(tabs, 1).X - Container(tabs, 0).X;

        DragTabBy(window, tabs, 0, step * 0.4);

        Assert.Equal(["A", "B", "C"], items);
    }

    [AvaloniaFact]
    public void Each_Gesture_Raises_Exactly_One_Started_And_One_Completed()
    {
        var (window, tabs, _) = CreateTabsWindow("A", "B", "C");

        double step = Container(tabs, 1).X - Container(tabs, 0).X;

        // Reordering re-applies the template on live containers; the thumb handlers must not pile up.
        DragTabBy(window, tabs, 0, step);

        int startedCount = 0;
        int completedCount = 0;
        tabs.TabDragStarted = (_, _) => startedCount++;
        tabs.TabDragCompleted = (_, _) => completedCount++;

        DragTabBy(window, tabs, 0, step);

        Assert.Equal(1, startedCount);
        Assert.Equal(1, completedCount);
    }

    [AvaloniaFact]
    public void Reordering_Tabs_Does_Not_Change_Any_Container_DataContext()
    {
        var (window, tabs, items) = CreateTabsWindow("A", "B", "C");

        var containers = Enumerable.Range(0, items.Count).Select(i => Container(tabs, i)).ToList();
        var before = containers.ToDictionary(c => c, c => c.DataContext);

        int dataContextChanges = 0;
        foreach (var container in containers)
            container.DataContextChanged += (_, _) => dataContextChanges++;

        double step = containers[1].X - containers[0].X;

        DragTabBy(window, tabs, 0, step);

        Assert.Equal(["B", "A", "C"], items);

        // The models moved around the containers, not the other way round.
        Assert.Equal(0, dataContextChanges);

        foreach (var container in containers)
            Assert.Same(before[container], container.DataContext);
    }
}
