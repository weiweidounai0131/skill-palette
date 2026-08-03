using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using SkillPalette_Windows.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SkillPalette_Windows;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;
    private TrayIconService? _trayIcon;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var launchInBackground = Environment.GetCommandLineArgs()
            .Skip(1)
            .Any(argument => string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase));
        _window = new MainWindow(launchInBackground);
        _trayIcon = new TrayIconService(
            _window.GetWindowHandle(),
            () => DispatchOpenPage("Skills"),
            () => DispatchRescan(),
            () => DispatchOpenPage("General"),
            () => DispatchOpenPage("About"),
            () => Dispatch(ExitApplication));
        _window.Activate();
        if (launchInBackground)
        {
            _window.CompleteBackgroundStartupAsync();
        }
    }

    private void Dispatch(Action action) => _window?.DispatcherQueue.TryEnqueue(() => action());

    private void DispatchOpenPage(string page) => _window?.DispatcherQueue.TryEnqueue(async () =>
    {
        if (_window is not null)
        {
            await _window.OpenPageAsync(page);
        }
    });

    private void DispatchRescan() => _window?.DispatcherQueue.TryEnqueue(async () =>
    {
        if (_window is not null)
        {
            await _window.RescanFromTrayAsync();
        }
    });

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        _window?.ExitFromTray();
    }
}
