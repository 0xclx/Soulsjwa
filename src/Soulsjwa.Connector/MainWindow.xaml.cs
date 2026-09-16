using System.Windows;
using Soulsjwa.Connector.ViewModels;

namespace Soulsjwa.Connector;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}