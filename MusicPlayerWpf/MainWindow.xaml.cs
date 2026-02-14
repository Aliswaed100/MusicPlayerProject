using System.Windows;
using MusicPlayerWpf.ViewModels;
namespace MusicPlayerWpf;
public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
    DataContext = new PlayerViewModel();
  }
  private async void SongsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
  {
    if (DataContext is PlayerViewModel vm)
      await vm.PlaySelectedAsync();
  }
}
