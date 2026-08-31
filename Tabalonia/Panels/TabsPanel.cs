using Tabalonia.Controls;
using static System.Math;


namespace Tabalonia.Panels;


public class TabsPanel : Panel
{
    #region Private Fields

    private readonly TabsControl _tabsControl;

    private readonly Dictionary<DragTabItem, LocationInfo> _itemsLocations = new();
    private double _itemWidth;
    private readonly Dictionary<DragTabItem, double> _activeStoryboardTargetLocations = new();
    private DragTabItem? _dragItem;
    private bool _isOverflowing;
    private double _scrollOffset;
    private double _maxScrollOffset;
    private bool _canScrollLeft;
    private bool _canScrollRight;
    private double _viewportWidth;
    private int _pendingScrollToIndex = -1;

    private const double ScrollStep = 80.0;

    #endregion


    public event Action? DragCompleted;
    public event Action<bool>? OverflowChanged;
    public event Action<bool>? CanScrollLeftChanged;
    public event Action<bool>? CanScrollRightChanged;


    public TabsPanel(TabsControl tabsControl) => _tabsControl = tabsControl;


    #region Public Properties

    public double MinItemWidth { get; internal set; }

    public double ItemWidth { get; internal set; }

    public double ItemOffset { get; internal set; }

    #endregion


    #region Public Methods

    public void ScrollLeft() => SetScrollOffset(_scrollOffset - ScrollStep);

    public void ScrollRight() => SetScrollOffset(_scrollOffset + ScrollStep);

    public void ScrollToTab(int logicalIndex)
    {
        if (logicalIndex < 0)
        {
            return;
        }

        // Layout not yet happened: store and let ArrangeImpl apply it with fresh values.
        if (_viewportWidth <= 0 || _itemWidth <= 0)
        {
            _pendingScrollToIndex = logicalIndex;
            return;
        }

        double tabX = logicalIndex * (_itemWidth + ItemOffset);
        double tabRight = tabX + _itemWidth;

        // Tab is fully visible: do nothing, no layout pass, no events.
        if (tabX >= _scrollOffset && tabRight <= _scrollOffset + _viewportWidth)
        {
            return;
        }

        // Tab is partially or not visible: scroll the minimum distance.
        // Left of viewport → make it the first visible tab.
        // Right of viewport → make it the last visible tab.
        if (tabX < _scrollOffset)
        {
            SetScrollOffset(tabX);
        }
        else
        {
            SetScrollOffset(tabRight - _viewportWidth);
        }
    }

    #endregion

    
    #region Protected Methods
    
    protected override Size MeasureOverride(Size availableSize)
    {
        var draggedItem = GetDragItem();

        return draggedItem is not null 
            ? DragMeasureImpl(draggedItem, availableSize) 
            : MeasureImpl(availableSize);
    }
        

    protected override Size ArrangeOverride(Size finalSize)
    {
        var draggedItem = GetDragItem();
            
        if (_dragItem is not null && draggedItem is null)
        {
            var oldDragItem = _dragItem;
            _dragItem = null;
                
            return DragCompletedArrangeImpl(oldDragItem, finalSize);
        }
            
        _dragItem = draggedItem;
            
        return draggedItem is not null
            ? DragArrangeImpl(draggedItem, finalSize) 
            : ArrangeImpl(finalSize);
    }
        
    #endregion
    
    private void SetScrollOffset(double newOffset)
    {
        double clamped = Max(0, Min(newOffset, _maxScrollOffset));
        if (Abs(clamped - _scrollOffset) > 0.5)
        {
            _scrollOffset = clamped;
            NotifyScrollButtonStates();
            InvalidateArrange();
        }
    }

    private void NotifyScrollButtonStates()
    {
        bool canLeft = _scrollOffset > 0.5;
        bool canRight = _scrollOffset < _maxScrollOffset - 0.5;

        if (canLeft != _canScrollLeft)
        {
            _canScrollLeft = canLeft;
            Dispatcher.UIThread.Post(() => CanScrollLeftChanged?.Invoke(_canScrollLeft), DispatcherPriority.Loaded);
        }

        if (canRight != _canScrollRight)
        {
            _canScrollRight = canRight;
            Dispatcher.UIThread.Post(() => CanScrollRightChanged?.Invoke(_canScrollRight), DispatcherPriority.Loaded);
        }
    }


    private Size MeasureImpl(Size availableSize)
    {
        bool previousOverflow = _isOverflowing;
        _itemWidth = GetAvailableWidth(availableSize);

        double height = 0;
        double width = 0;

        bool isFirst = true;

        foreach (var tabItem in Children)
        {
            tabItem.Measure(new Size(_itemWidth, availableSize.Height));

            width += _itemWidth;
            height = Max(tabItem.DesiredSize.Height, height);

            if (!isFirst)
                width += ItemOffset;

            isFirst = false;
        }

        _isOverflowing = Children.Count > 0 && width > availableSize.Width + 0.5;

        if (previousOverflow != _isOverflowing)
        {
            Dispatcher.UIThread.Post(() => OverflowChanged?.Invoke(_isOverflowing), DispatcherPriority.Loaded);
        }

        return new Size(width, height);
    }


    private Size DragMeasureImpl(DragTabItem draggedItem, Size availableSize)
    {
        double height = 0;
        double width = 0;

        bool isFirst = true;

        foreach (var tabItem in Children)
        {
            tabItem.Measure(new Size(_itemWidth, availableSize.Height));
                
            width += _itemWidth;
            height = Max(tabItem.DesiredSize.Height, height);

            if (!isFirst)
                width += ItemOffset;

            isFirst = false;
        }
            
        if (draggedItem.X + _itemWidth > width)
            return new Size(draggedItem.X + _itemWidth, height);
            
        return new Size(width, height);
    }
        
        
    private Size ArrangeImpl(Size finalSize)
    {
        int tabsCount = Children.Count;
        if (tabsCount > 0)
        {
            double totalTabsWidth = tabsCount * _itemWidth + (tabsCount - 1) * ItemOffset;
            _maxScrollOffset = Max(0, totalTabsWidth - finalSize.Width);
            _scrollOffset = Min(_scrollOffset, _maxScrollOffset);
        }
        else
        {
            _maxScrollOffset = 0;
            _scrollOffset = 0;
        }

        _viewportWidth = finalSize.Width;

        if (_pendingScrollToIndex >= 0)
        {
            int idx = _pendingScrollToIndex;
            _pendingScrollToIndex = -1;
            double tabX = idx * (_itemWidth + ItemOffset);
            double tabRight = tabX + _itemWidth;
            if (tabX < _scrollOffset)
            {
                _scrollOffset = Max(0, tabX);
            }
            else if (tabRight > _scrollOffset + _viewportWidth)
            {
                _scrollOffset = Min(_maxScrollOffset, tabRight - _viewportWidth);
            }
        }

        double x = -_scrollOffset;
        int z = ZIndexes.NonSelected;
        int logicalIndex = 0;

        _itemsLocations.Clear();
            
        foreach (Control? child in Children)
        {
            if (child is not DragTabItem tabItem)
                continue;

            tabItem.ZIndex = tabItem.IsSelected ? int.MaxValue : --z;
            tabItem.LogicalIndex = logicalIndex++;
            
            SetLocation(tabItem, x, _itemWidth);
                
            _itemsLocations.Add(tabItem, GetLocationInfo(tabItem));

            x += _itemWidth + ItemOffset;
        }

        NotifyScrollButtonStates();

        return finalSize;
    }


    private Size DragArrangeImpl(DragTabItem dragItem, Size finalSize)
    {
        var dragItemsLocations = GetLocations(Children.OfType<DragTabItem>(), dragItem);

        double currentCoord = -_scrollOffset;

        foreach (var location in dragItemsLocations)
        {
            var item = location.Item;

            if (!Equals(item, dragItem) && item.LogicalIndex >= _tabsControl.FixedHeaderCount)
            {
                Dispatcher.UIThread.Invoke(() => SetLocation(item, currentCoord, _itemWidth), DispatcherPriority.Loaded);
            }
            else
            {
                double maxX = finalSize.Width - _itemWidth;

                if (dragItem.X > maxX) dragItem.X = maxX;

                double minX = CalculateMinX() - _scrollOffset;

                if (dragItem.X < minX) dragItem.X = minX;

                SetLocation(dragItem, dragItem.X, _itemWidth);
            }

            currentCoord += _itemWidth + ItemOffset;
        }

        return finalSize;
    }
    

    private double CalculateMinX()
    {
        if (_tabsControl.FixedHeaderCount < 1)
            return 0;
        
        double x = 0;

        for (int index = 0; index < _tabsControl.FixedHeaderCount; index++)
        {
            x += _itemWidth + ItemOffset;
        }

        return x;
    }


    private Size DragCompletedArrangeImpl(DragTabItem dragItem, Size finalSize)
    {
        var dragItemsLocations = GetLocations(Children.OfType<DragTabItem>(), dragItem);

        double currentCoord = -_scrollOffset;
        int z = ZIndexes.NonSelected;
        int logicalIndex = 0;

        foreach (var location in dragItemsLocations)
        {
            var item = location.Item;

            SetLocation(item, currentCoord, _itemWidth);
            currentCoord += _itemWidth + ItemOffset;
            item.ZIndex = --z;
            item.LogicalIndex = logicalIndex++;
        }

        dragItem.ZIndex = ZIndexes.Selected;
        
        DragCompleted?.Invoke();
            
        return finalSize;
    }
        

    private double GetAvailableWidth(Size availableSize)
    {
        int tabsCount = Children.Count;

        if (tabsCount == 0)
            return 0;

        double itemWidth = availableSize.Width / tabsCount - ItemOffset * (tabsCount - 1) / tabsCount;
        double effectiveWidth = Min(ItemWidth, itemWidth);

        if (MinItemWidth > 0)
        {
            return Max(MinItemWidth, effectiveWidth);
        }

        return effectiveWidth;
    }

        
    private IEnumerable<LocationInfo> GetLocations(IEnumerable<DragTabItem> allItems, DragTabItem dragItem)
    {
        // _itemsLocations is a snapshot from the last non-drag ArrangeImpl. When the children
        // change while the panel is mid-drag (e.g. a tab is transferred in/out during a
        // cross-window drag session) an item may be missing from it, so fall back to its live
        // position (loc) instead of indexing the stale cache and throwing.
        double DragItemStart()
        {
            if (_itemsLocations.TryGetValue(dragItem, out var dragItemInfo))
                return dragItemInfo.Start;

            return GetLocationInfo(dragItem).Start;
        }

        double dragItemStart = DragItemStart();

        double OrderSelector(LocationInfo loc)
        {
            if (Equals(loc.Item, dragItem))
                return loc.Start > dragItemStart ? loc.End : loc.Start;

            return _itemsLocations.TryGetValue(loc.Item, out var info) ? info.Mid : loc.Mid;
        }

        var currentLocations = allItems
            .Select(GetLocationInfo)
            .OrderBy(OrderSelector);

        return currentLocations;
    }
        
        /*
    private async Task SendToLocation(DragTabItem item, double location, double width)
    {
        bool itemIsAnimating = _activeStoryboardTargetLocations.TryGetValue(item, out double activeTarget);
        
        if (itemIsAnimating)
        {
            SetLocation(item, item.X, width);
            return;
        }
        
        if (Abs(item.X - location) < 1.0 || itemIsAnimating && Abs(activeTarget - location) < 1.0)
        {
            return;
        }
        
        _activeStoryboardTargetLocations[item] = location;

        const int animDuration = 200;

        var animation = new Animation
        {
            Easing = new CubicEaseOut(),
            Duration = TimeSpan.FromMilliseconds(animDuration),
            PlaybackDirection = PlaybackDirection.Normal,
            FillMode = FillMode.None,
            Children =
            {
                new KeyFrame
                {
                    KeyTime = TimeSpan.FromMilliseconds(animDuration),
                    Setters =
                    {
                        new Setter(DragTabItem.XProperty, location),
                    }
                }
            }
        };
            
        await animation.RunAsync(item);

        SetLocation(item, location, width);
            
        _activeStoryboardTargetLocations.Remove(item);
    }
    */


    private static void SetLocation(DragTabItem dragTabItem, double x, double width)
    {
        const double y = 0;

        dragTabItem.X = x;
        dragTabItem.Y = y;

        dragTabItem.Arrange(new Rect(new Point(x, y), new Size(width, dragTabItem.DesiredSize.Height)));
    }
        
        
    private LocationInfo GetLocationInfo(DragTabItem item)
    {
        double size = item.Bounds.Width;
            
        if (!_activeStoryboardTargetLocations.TryGetValue(item, out double startLocation))
            startLocation = item.X;
            
        double midLocation = startLocation + size / 2;
        double endLocation = startLocation + size;

        return new LocationInfo(item, startLocation, midLocation, endLocation);
    }


    private DragTabItem? GetDragItem() => (DragTabItem?)Children.FirstOrDefault(c => c is DragTabItem
    {
        IsDragging: true
    });


    #region Private Structs

    private readonly record struct LocationInfo(DragTabItem Item, double Start, double Mid, double End);

    #endregion
}