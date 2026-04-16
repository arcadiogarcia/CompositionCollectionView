using Microsoft.UI.Xaml;

namespace CompositionCollectionView.Sample.WinUI3;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();

#if DEBUG
        try
        {
            await zRover.WinUI.DebugHost.StartAsync(_window, "CCV Sample", port: 5101, managerUrl: "http://localhost:5200");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"zRover startup failed: {ex}");
        }
#endif
    }
}
