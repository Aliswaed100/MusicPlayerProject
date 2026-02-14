using System.Windows;
using YourApp.ViewModels;

namespace YourApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new PlayerViewModel();
    }
}
