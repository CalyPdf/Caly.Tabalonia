using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;


namespace Tabalonia.Demo.ViewModels;


public class MainViewModel : ObservableObject
{
    private const string NoDragYet = "Drag a tab to see TabDragStarted / TabDragCompleted.";

    private int _i;

    private string _dragStatus = NoDragYet;
    private string _tabOrder = string.Empty;
    private int _dataContextChangesSinceDragStart;


    public Func<object> NewItemFactory => AddItem;


    public ObservableCollection<TabItemViewModel> TabItems { get; } = new();


    /// <summary>The last TabsControl.TabDragStarted / TabDragCompleted notification.</summary>
    public string DragStatus
    {
        get => _dragStatus;
        private set => SetProperty(ref _dragStatus, value);
    }


    /// <summary>The models in strip order, so a reorder can be read off the status bar.</summary>
    public string TabOrder
    {
        get => _tabOrder;
        private set => SetProperty(ref _tabOrder, value);
    }


    /// <summary>
    /// How often a tab container swapped models since the current drag started. Reordering shifts
    /// the models in place instead of removing and re-inserting them, so this has to stay at 0.
    /// </summary>
    public int DataContextChangesSinceDragStart
    {
        get => _dataContextChangesSinceDragStart;
        private set => SetProperty(ref _dataContextChangesSinceDragStart, value);
    }


    public MainViewModel()
    {
        TabItems.Add(new TabItemViewModel
        {
            Header = "Fixed Tab",
            SimpleContent = "Fixed Tab content"
        });

        const int count = 10;

        for (int i = 0; i < count; i++)
        {
            TabItems.Add((TabItemViewModel)AddItem());
        }

        TabItems.CollectionChanged += (_, _) => UpdateTabOrder();
        UpdateTabOrder();
    }


    public void OnTabDragStarted(TabItemViewModel? tab)
    {
        DataContextChangesSinceDragStart = 0;
        DragStatus = $"TabDragStarted: {tab?.Header ?? "?"}";
    }


    public void OnTabDragCompleted(TabItemViewModel? tab)
    {
        DragStatus = $"TabDragCompleted: {tab?.Header ?? "?"}";
        UpdateTabOrder();
    }


    public void OnContainerDataContextChanged() => DataContextChangesSinceDragStart++;


    private void UpdateTabOrder() => TabOrder = string.Join(" | ", TabItems.Select(t => t.Header));


    private object AddItem()
    {
        return new TabItemViewModel
        {
            Header = $"Tab {++_i}",
            SimpleContent = $"Tab {_i} content"
        };
    }
}
