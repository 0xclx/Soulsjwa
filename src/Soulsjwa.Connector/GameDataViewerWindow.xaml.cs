using System.Windows;
using Soulsjwa.Connector.ViewModels;

namespace Soulsjwa.Connector;

public partial class GameDataViewerWindow : Window
{
    private readonly GameDataViewerViewModel _viewModel;

    public GameDataViewerWindow(GameDataViewerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>
    /// The view model subscribes to the shared value store, which outlives this
    /// window — and the viewer is modeless, so any number of these can be opened
    /// and closed during one session. Dropping the subscription here is what
    /// stops closed windows from re-filtering on every poll tick forever.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
