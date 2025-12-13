using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using Tabalonia.Events;
using Tabalonia.Panels;


namespace Tabalonia.Controls;

public class TabsControl : TabControl
{
    #region Constants

    private const double DefaultTabWidth = 140;

    public const double WindowsAndLinuxDefaultLeftThumbWidth = 4d;
    public const double MacOsDefaultLeftThumbWidth = 80d;

    public const double WindowsDefaultRightThumbWidth = 160d;
    public const double MacOsDefaultRightThumbWidth = 50d;

    #endregion


    #region Private Fields

    private readonly TabsPanel _tabsPanel;

    private DragTabItem? _draggedItem;
    private bool _dragging;

    private ICommand _addItemCommand;
    private ICommand _closeItemCommand;

    #endregion

    #region Avalonia Properties

    public static readonly StyledProperty<double> AdjacentHeaderItemOffsetProperty =
        AvaloniaProperty.Register<TabsControl, double>(nameof(AdjacentHeaderItemOffset), defaultValue: 0);


    public static readonly StyledProperty<double> TabItemWidthProperty =
        AvaloniaProperty.Register<TabsControl, double>(nameof(TabItemWidth), defaultValue: DefaultTabWidth);


    public static readonly StyledProperty<bool> ShowDefaultCloseButtonProperty =
        AvaloniaProperty.Register<TabsControl, bool>(nameof(ShowDefaultCloseButton), defaultValue: true);


    public static readonly StyledProperty<bool> ShowDefaultAddButtonProperty =
        AvaloniaProperty.Register<TabsControl, bool>(nameof(ShowDefaultAddButton), defaultValue: true);


    public static readonly StyledProperty<int> FixedHeaderCountProperty =
        AvaloniaProperty.Register<TabsControl, int>(nameof(FixedHeaderCount), defaultValue: 0);


    public static readonly StyledProperty<Func<Task<object>>?> NewItemAsyncFactoryProperty =
        AvaloniaProperty.Register<TabsControl, Func<Task<object>>?>(nameof(NewItemAsyncFactory));


    public static readonly StyledProperty<Func<object>?> NewItemFactoryProperty =
        AvaloniaProperty.Register<TabsControl, Func<object>?>(nameof(NewItemFactory));


    public static readonly StyledProperty<EventHandler<DragTabDragStartedEventArgs>?> TabDragStartedProperty =
        AvaloniaProperty.Register<TabsControl, EventHandler<DragTabDragStartedEventArgs>?>(nameof(TabDragStarted));

    public static readonly StyledProperty<EventHandler<DragTabDragCompletedEventArgs>?> TabDragCompletedProperty =
        AvaloniaProperty.Register<TabsControl, EventHandler<DragTabDragCompletedEventArgs>?>(nameof(TabDragCompleted));


    public static readonly StyledProperty<EventHandler<TabClosedEventArgs>?> TabClosedProperty =
        AvaloniaProperty.Register<TabsControl, EventHandler<TabClosedEventArgs>?>(nameof(TabClosed));

    public static readonly StyledProperty<EventHandler<TabClosingEventArgs>?> TabClosingProperty =
        AvaloniaProperty.Register<TabsControl, EventHandler<TabClosingEventArgs>?>(nameof(TabClosing));


    public static readonly StyledProperty<EventHandler<CloseLastTabEventArgs>?> LastTabClosedActionProperty =
        AvaloniaProperty.Register<TabsControl, EventHandler<CloseLastTabEventArgs>?>(nameof(LastTabClosedAction));


    public static readonly StyledProperty<double> LeftThumbWidthProperty =
        AvaloniaProperty.Register<TabsControl, double>(nameof(LeftThumbWidth),
            defaultValue: OperatingSystem.IsMacOS()
                ? MacOsDefaultLeftThumbWidth
                : WindowsAndLinuxDefaultLeftThumbWidth);


    public static readonly StyledProperty<double> RightThumbWidthProperty =
        AvaloniaProperty.Register<TabsControl, double>(nameof(RightThumbWidth),
            defaultValue: OperatingSystem.IsWindows() ? WindowsDefaultRightThumbWidth : MacOsDefaultRightThumbWidth);


    public static readonly DirectProperty<TabsControl, ICommand> AddItemCommandProperty =
        AvaloniaProperty.RegisterDirect<TabsControl, ICommand>(
            nameof(AddItemCommand),
            o => o.AddItemCommand,
            (o, v) => o.AddItemCommand = v);


    public static readonly DirectProperty<TabsControl, ICommand> CloseItemCommandProperty =
        AvaloniaProperty.RegisterDirect<TabsControl, ICommand>(
            nameof(CloseItemCommand),
            o => o.CloseItemCommand,
            (o, v) => o.CloseItemCommand = v);

    public static readonly StyledProperty<object?> LeftContentProperty =
        AvaloniaProperty.Register<TabsControl, object?>(nameof(LeftContent));
    
    public static readonly StyledProperty<object?> RightContentProperty =
        AvaloniaProperty.Register<TabsControl, object?>(nameof(RightContent));
    
    #endregion


    #region Constructor

    public TabsControl()
    {
        // TODO - Unsubscribe from events
        
        AddHandler(DragTabItem.DragStarted, ItemDragStarted, handledEventsToo: true);
        AddHandler(DragTabItem.DragDelta, ItemDragDelta);
        AddHandler(DragTabItem.DragCompleted, ItemDragCompleted, handledEventsToo: true);

        _tabsPanel = new TabsPanel(this)
        {
            ItemWidth = TabItemWidth,
            ItemOffset = AdjacentHeaderItemOffset
        };

        _tabsPanel.DragCompleted += TabsPanelOnDragCompleted;

        ItemsPanel = new FuncTemplate<Panel>(() => _tabsPanel);

        LastTabClosedAction = (_, _) => GetThisWindow()?.Close();

        _addItemCommand = new SimpleActionCommand(AddItem);
        _closeItemCommand = new SimpleParamActionCommand(CloseItem);

        Items.CollectionChanged += Items_CollectionChanged;
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        /*
         * If the _draggedItem is not null and is the item that was removed, we remove the reference
         */

        if (_draggedItem is null)
        {
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems?.Count > 0)
        {
            foreach (var oldItem in e.OldItems)
            {
                if (_draggedItem?.DataContext == oldItem)
                {
                    _draggedItem = null;
                    break;
                }
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Replace && e.NewItems?.Count > 0)
        {
            foreach (var oldItem in e.NewItems)
            {
                if (_draggedItem?.DataContext == oldItem)
                {
                    _draggedItem = null;
                    break;
                }
            }
        }
    }

    #endregion


    #region Public Properties

    public double AdjacentHeaderItemOffset
    {
        get => GetValue(AdjacentHeaderItemOffsetProperty);
        set => SetValue(AdjacentHeaderItemOffsetProperty, value);
    }


    public double TabItemWidth
    {
        get => GetValue(TabItemWidthProperty);
        set => SetValue(TabItemWidthProperty, value);
    }


    public bool ShowDefaultCloseButton
    {
        get => GetValue(ShowDefaultCloseButtonProperty);
        set => SetValue(ShowDefaultCloseButtonProperty, value);
    }


    public bool ShowDefaultAddButton
    {
        get => GetValue(ShowDefaultAddButtonProperty);
        set => SetValue(ShowDefaultAddButtonProperty, value);
    }


    public Func<Task<object>>? NewItemAsyncFactory
    {
        get => GetValue(NewItemAsyncFactoryProperty);
        set => SetValue(NewItemAsyncFactoryProperty, value);
    }


    public Func<object>? NewItemFactory
    {
        get => GetValue(NewItemFactoryProperty);
        set => SetValue(NewItemFactoryProperty, value);
    }

    public EventHandler<DragTabDragStartedEventArgs>? TabDragStarted
    {
        get => GetValue(TabDragStartedProperty);
        set => SetValue(TabDragStartedProperty, value);
    }

    public EventHandler<DragTabDragCompletedEventArgs>? TabDragCompleted
    {
        get => GetValue(TabDragCompletedProperty);
        set => SetValue(TabDragCompletedProperty, value);
    }

    public EventHandler<TabClosedEventArgs>? TabClosed
    {
        get => GetValue(TabClosedProperty);
        set => SetValue(TabClosedProperty, value);
    }


    public EventHandler<TabClosingEventArgs>? TabClosing
    {
        get => GetValue(TabClosingProperty);
        set => SetValue(TabClosingProperty, value);
    }


    public EventHandler<CloseLastTabEventArgs>? LastTabClosedAction
    {
        get => GetValue(LastTabClosedActionProperty);
        set => SetValue(LastTabClosedActionProperty, value);
    }
    
    /// <summary>
    /// Allows a the first adjacent tabs to be fixed (no dragging, and default close button will not show).
    /// </summary>
    public int FixedHeaderCount
    {
        get => GetValue(FixedHeaderCountProperty);
        set => SetValue(FixedHeaderCountProperty, value);
    }


    public double LeftThumbWidth
    {
        get => GetValue(LeftThumbWidthProperty);
        set => SetValue(LeftThumbWidthProperty, value);
    }


    public double RightThumbWidth
    {
        get => GetValue(RightThumbWidthProperty);
        set => SetValue(RightThumbWidthProperty, value);
    }


    public ICommand AddItemCommand
    {
        get => _addItemCommand;
        private set => SetAndRaise(AddItemCommandProperty, ref _addItemCommand, value);
    }


    public ICommand CloseItemCommand
    {
        get => _closeItemCommand;
        private set => SetAndRaise(CloseItemCommandProperty, ref _closeItemCommand, value);
    }

    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }
    
    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }
    
    #endregion


    #region Protected Methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var leftDragWindowThumb = e.NameScope.Get<Thumb>("PART_LeftDragWindowThumb");
        leftDragWindowThumb.AddHandler(PointerPressedEvent, OnThumbBeginDrag, handledEventsToo: true);
        //leftDragWindowThumb.DragDelta += WindowDragThumbOnDragDelta;
        leftDragWindowThumb.DoubleTapped += WindowDragThumbOnDoubleTapped;

        var rightDragWindowThumb = e.NameScope.Get<Thumb>("PART_RightDragWindowThumb");
        rightDragWindowThumb.AddHandler(PointerPressedEvent, OnThumbBeginDrag, handledEventsToo: true);
        // rightDragWindowThumb.DragDelta += WindowDragThumbOnDragDelta;
        rightDragWindowThumb.DoubleTapped += WindowDragThumbOnDoubleTapped;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new DragTabItem();


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AdjacentHeaderItemOffsetProperty)
        {
            _tabsPanel.ItemOffset = AdjacentHeaderItemOffset;
        }
        else if (change.Property == TabItemWidthProperty)
        {
            _tabsPanel.ItemWidth = TabItemWidth;
        }
    }

    #endregion


    #region Private Methods

    private void RemoveItem(DragTabItem container)
    {
        object? item = ItemFromContainer(container);

        if (item == null)
            return;

        if (ItemsSource is not IList itemsList)
            return;

        int removedItemIndex = itemsList.IndexOf(item);

        if (removedItemIndex == -1)
            return;

        TabClosingEventArgs tabClosingEventArgs = new(item);
        TabClosing?.Invoke(this, tabClosingEventArgs);
        if (tabClosingEventArgs.Cancel)
            return;

        bool removedItemIsSelected = SelectedItem == item;

        itemsList.Remove(item);

        TabClosed?.Invoke(this, new TabClosedEventArgs(item));

        if (_draggedItem == container)
        {
            _draggedItem = null;
        }

        if (itemsList.Count == 0)
            LastTabClosedAction?.Invoke(this, new CloseLastTabEventArgs(GetThisWindow()));
        else if (removedItemIsSelected)
            SetSelectedNewTab(itemsList, removedItemIndex);
    }


    private void SetSelectedNewTab(IList items, int removedItemIndex) =>
        SelectedItem = removedItemIndex == items.Count ? items[^1] : items[removedItemIndex];


    private Window? GetThisWindow() => this.FindLogicalAncestorOfType<Window>();


    private IEnumerable<DragTabItem> DragTabItems()
    {
        foreach (object item in Items)
        {
            var container = ContainerFromItem(item);

            if (container is DragTabItem dragTabItem)
                yield return dragTabItem;
        }
    }


    private void ItemDragStarted(object? sender, DragTabDragStartedEventArgs e)
    {
        TabDragStarted?.Invoke(sender, e);

        _draggedItem = e.TabItem;

        e.Handled = true;

        _draggedItem.IsSelected = true;

        object? item = ItemFromContainer(_draggedItem);

        if (item != null)
        {
            if (item is TabItem tabItem)
                tabItem.IsSelected = true;

            SelectedItem = item;
        }
    }


    private void ItemDragDelta(object? sender, DragTabDragDeltaEventArgs e)
    {
        if (_draggedItem is null)
            throw new Exception($"{nameof(TabsControl)}.{nameof(ItemDragDelta)} - _draggedItem is null");

        if (_draggedItem.LogicalIndex < FixedHeaderCount)
        {
            e.Handled = true;
            return;
        }

        if (!_dragging)
        {
            _dragging = true;
            SetDraggingItem(_draggedItem);
        }

        _draggedItem.X += e.DragDeltaEventArgs.Vector.X;
        _draggedItem.Y += e.DragDeltaEventArgs.Vector.Y;

        Dispatcher.UIThread.Post(() => _tabsPanel.InvalidateMeasure(), DispatcherPriority.Loaded);

        e.Handled = true;
    }


    private void ItemDragCompleted(object? sender, DragTabDragCompletedEventArgs e)
    {
        foreach (var item in DragTabItems())
        {
            item.IsDragging = false;
            item.IsSiblingDragging = false;
        }

        Dispatcher.UIThread.Post(() => _tabsPanel.InvalidateMeasure(), DispatcherPriority.Loaded);

        _dragging = false;
        TabDragCompleted?.Invoke(sender, e);
    }


    private void SetDraggingItem(DragTabItem draggedItem)
    {
        foreach (var item in DragTabItems())
        {
            item.IsDragging = false;
            item.IsSiblingDragging = true;
        }

        draggedItem.IsDragging = true;
        draggedItem.IsSiblingDragging = false;
    }


    private void TabsPanelOnDragCompleted()
    {
        MoveTabModelsIfNeeded();
        _draggedItem = null;
    }

    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        if (_isContainerSwapping)
        {
            return;
        }

        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
    }


    private bool _isContainerSwapping = false;

    private void MoveTabModelsIfNeeded()
    {
        if (_draggedItem is null)
        {
            return;
        }

        object? item = ItemFromContainer(_draggedItem);

        if (item == null)
        {
            return;
        }

        DragTabItem container = _draggedItem;

        if (ItemsSource is not IList list)
        {
            return;
        }

        int oldIndex = list.IndexOf(item);
        int newIndex = container.LogicalIndex;

        if (newIndex == oldIndex)
        {
            return;
        }

        // We want to avoid triggering a change of DataContext on the selected container.
        // We cannot use Remove() / Insert() as it will trigger an unwanted change of
        // DataContext on the selected container (back and forth).
        // In order to do so we:

        // Save the old indexes, to later on re-process ContainerIndexChangedOverride
        Span<int> indexes = list.Count <= 32 ? stackalloc int[list.Count] : new int[list.Count];
        for (int i = 0; i < indexes.Length; i++)
        {
            indexes[i] = i;
        }

        var selectionMode = SelectionMode;
        try
        {
            // - Prevent ContainerIndexChangedOverride while re-ordering the items
            _isContainerSwapping = true;
            BeginInit();

            // - Temporarily set SelectionMode to Single
            // When SelectionMode is AlwaysSelected, removing an item will change 
            // the DataContext of the selected item to another item.
            // To avoid this, we temporarily set SelectionMode to Single.
            SelectionMode = SelectionMode.Single;
            var temp = list[newIndex];

            // - Move the item directly and set the selected index to the new index
            // The moved item is already available at newIndex position
            list[newIndex] = list[oldIndex];
            indexes[newIndex] = oldIndex;
            SelectedIndex = newIndex;

            // - Shift the necessary items
            if (oldIndex < newIndex)
            {
                for (int i = oldIndex; i < newIndex - 1; ++i)
                {
                    indexes[i] = indexes[i + 1];
                    list[i] = list[i + 1];
                }

                indexes[newIndex - 1] = newIndex;
                list[newIndex - 1] = temp;
            }
            else
            {
                for (int i = oldIndex; i > newIndex + 1; --i)
                {
                    indexes[i] = indexes[i - 1];
                    list[i] = list[i - 1];
                }

                indexes[newIndex + 1] = newIndex;
                list[newIndex + 1] = temp;
            }
        }
        finally
        {
            SelectedItem = item; // Do it first to ensure an item is selected when switching back to AlwaysSelected
            SelectionMode = selectionMode;
            EndInit();
            _isContainerSwapping = false;

            int i = 0;
            foreach (var dragTabItem in DragTabItems())
            {
                int newIndexLocal = i++;
                dragTabItem.LogicalIndex = newIndexLocal;

                // Unclear if ContainerIndexChangedOverride is actually necessary
                ContainerIndexChangedOverride(dragTabItem, indexes[newIndexLocal], newIndexLocal);
            }
        }
    }

    private void OnThumbBeginDrag(object? sender, PointerPressedEventArgs e)
    {
        var toplevel = TopLevel.GetTopLevel(this);
        if(toplevel is not Window window) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            e.GetCurrentPoint(this).Pointer.Type == PointerType.Touch)
        {
            window.BeginMoveDrag(e);
        }
    }

    private void WindowDragThumbOnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        var window = this.FindLogicalAncestorOfType<Window>();

        window?.RestoreWindow();
    }

    [Obsolete]
    private void WindowDragThumbOnDragDelta(object? sender, VectorEventArgs e)
    {
        var window = this.FindLogicalAncestorOfType<Window>();

        window?.DragWindow(e.Vector.X, e.Vector.Y);
    }


    private void AddItem()
    {
        if (NewItemAsyncFactory is not null)
        {
            NewItemAsyncFactory.Invoke().ContinueWith(t => { AddItem(t.Result); },
                scheduler: TaskScheduler.FromCurrentSynchronizationContext());

            return;
        }

        AddItem(NewItemFactory?.Invoke());
    }


    private void AddItem(object? newItem)
    {
        ArgumentNullException.ThrowIfNull(newItem);

        if (ItemsSource is IList itemsList)
            itemsList.Add(newItem);

        SelectedItem = newItem;
    }


    private void CloseItem(object? tabItemSource)
    {
        ArgumentNullException.ThrowIfNull(tabItemSource);

        if (tabItemSource is not DragTabItem tabItem)
            return;

        RemoveItem(tabItem);
    }

    #endregion
}