using System.Windows;
using MusicPlayerWpf.Models;
using MusicPlayerWpf.ViewModels;

namespace MusicPlayerWpf.Services;

public sealed class WpfEditWindowService : IEditWindowService
{
    private readonly IFileDialogService _fileDialog;
    private readonly IImageStorageService _imageStorage;

    public WpfEditWindowService(IFileDialogService fileDialog, IImageStorageService imageStorage)
    {
        _fileDialog = fileDialog;
        _imageStorage = imageStorage;
    }

    public void ShowEditSongWindow(SongItem song, IMetadataCache cache)
    {
        var viewModel = new EditSongViewModel(song, cache, _fileDialog, _imageStorage);
        var window = new EditSongWindow
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow
        };
        window.ShowDialog();
    }
}
