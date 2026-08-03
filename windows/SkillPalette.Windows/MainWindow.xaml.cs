using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SkillPalette_Windows;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool _isExitRequested;
    private readonly bool _launchInBackground;

    public MainWindow(bool launchInBackground = false)
    {
        InitializeComponent();
        _launchInBackground = launchInBackground;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.Resize(new SizeInt32(1040, 720));
        AppWindow.Closing += (_, args) =>
        {
            if (_isExitRequested)
            {
                return;
            }

            args.Cancel = true;
            HideMainWindow();
        };

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    internal async Task OpenPageAsync(string page)
    {
        AppWindow.Show();
        Activate();
        if (RootFrame.Content is MainPage mainPage)
        {
            mainPage.NavigateTo(page);
            await mainPage.CheckListeningStatusAsync();
        }
    }

    internal async Task RescanFromTrayAsync()
    {
        await OpenPageAsync("Skills");
        if (RootFrame.Content is MainPage mainPage)
        {
            await mainPage.RescanFromTrayAsync();
        }
    }

    internal void ExitFromTray()
    {
        _isExitRequested = true;
        Close();
    }

    internal async void CompleteBackgroundStartupAsync()
    {
        if (!_launchInBackground) return;
        await Task.Delay(250);
        HideMainWindow();
    }

    internal IntPtr GetWindowHandle() => WinRT.Interop.WindowNative.GetWindowHandle(this);

    private void HideMainWindow()
    {
        AppWindow.Hide();
        _ = ShowWindow(GetWindowHandle(), 0);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
}
