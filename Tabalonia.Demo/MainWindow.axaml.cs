using Avalonia.Controls;
using Tabalonia.Demo.ViewModels;


namespace Tabalonia.Demo
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Tabs.TabDragStarted = (_, e) => ViewModel?.OnTabDragStarted(TabOf(e.TabItem));
            Tabs.TabDragCompleted = (_, e) => ViewModel?.OnTabDragCompleted(TabOf(e.TabItem));

            // Reordering shifts the models in place, so a container never swaps DataContext. These
            // handlers put a number on that claim in the status bar.
            Tabs.ContainerPrepared += (_, e) => e.Container.DataContextChanged += OnContainerDataContextChanged;
            Tabs.ContainerClearing += (_, e) => e.Container.DataContextChanged -= OnContainerDataContextChanged;
        }


        private MainViewModel? ViewModel => DataContext as MainViewModel;


        private static TabItemViewModel? TabOf(Control? container) => container?.DataContext as TabItemViewModel;


        private void OnContainerDataContextChanged(object? sender, System.EventArgs e) =>
            ViewModel?.OnContainerDataContextChanged();
    }
}
