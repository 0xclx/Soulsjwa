using System.Windows;
using Soulsjwa.Connector.Services;
using Soulsjwa.Connector.ViewModels;

namespace Soulsjwa.Connector;

public partial class App : Application
{
    private ApiService? _apiService;
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configService = new ConfigurationService();
        _apiService = new ApiService();
        _viewModel = new MainViewModel(configService, _apiService);

        var mainWindow = new MainWindow(_viewModel);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _apiService?.Dispose();
        base.OnExit(e);
    }
}
