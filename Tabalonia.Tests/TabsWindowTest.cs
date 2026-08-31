using Avalonia.Controls;
using Avalonia.Threading;
using Tabalonia.Controls;

namespace Tabalonia.Tests;

/// <summary>
/// Closes every window a test opened.
/// <para>
/// The headless platform maps all windows onto the same screen space, and a
/// <see cref="Controls.TabsControl"/> stays in the static drag-target registry until it leaves the
/// visual tree. A window left open by one test therefore keeps offering itself as a drop target to
/// the next one, and dragged tabs silently land in the wrong strip.
/// </para>
/// </summary>
public abstract class TabsWindowTest : IDisposable
{
    private readonly List<Window> _windows = [];

    /// <summary>Shows <paramref name="window"/>, runs a layout pass, and tracks it for cleanup.</summary>
    protected Window ShowWindow(Window window)
    {
        _windows.Add(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>Tracks a window the control opened by itself, such as a torn-off tab host.</summary>
    protected T Track<T>(T window) where T : Window
    {
        _windows.Add(window);

        return window;
    }

    /// <summary>
    /// Routes tabs torn off <paramref name="tabs"/> into tracked windows, so the floating hosts a
    /// drag creates get closed along with the rest.
    /// </summary>
    protected void TrackDetachedWindowsOf(TabsControl tabs) =>
        tabs.DetachedWindowFactory = host => Track(new Window
        {
            Width = 600,
            Height = 500,
            Content = host,
            DataContext = host.DataContext,
            Title = "Detached Tab"
        });

    public void Dispose()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            foreach (var window in _windows)
                window.Close();

            Dispatcher.UIThread.RunJobs();
        });

        _windows.Clear();
        GC.SuppressFinalize(this);
    }
}
