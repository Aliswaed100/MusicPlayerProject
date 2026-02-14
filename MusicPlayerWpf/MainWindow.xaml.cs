using System.Windows;
using System.Windows.Input;
using MusicPlayerWpf.ViewModels;

namespace MusicPlayerWpf;

public partial class MainWindow : Window
{
    private readonly PlayerViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new PlayerViewModel();
        DataContext = _viewModel;
    }

    private async void SongsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        await _viewModel.PlaySelectedAsync();
    }
}
