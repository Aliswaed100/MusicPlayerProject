using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MusicPlayerWpf.Models;
using MusicPlayerWpf.Services;

namespace MusicPlayerWpf.ViewModels;

public sealed class EditSongViewModel : INotifyPropertyChanged
{
    private const string DefaultCoverRelativePath = "Assets/default_cover.png";

    private readonly SongItem _song;
    private readonly IMetadataCache _cache;
    private readonly IFileDialogService _fileDialog;
    private readonly IImageStorageService _imageStorage;
    private readonly RelayCommand _removeImageCommand;
    private readonly RelayCommand _saveCommand;
    private SongCacheEntry _entry = new();

    public EditSongViewModel(
        SongItem song,
        IMetadataCache cache,
        IFileDialogService fileDialog,
        IImageStorageService imageStorage)
    {
        _song = song;
        _cache = cache;
        _fileDialog = fileDialog;
        _imageStorage = imageStorage;

        FilePath = song.FullPath;
        _removeImageCommand = new RelayCommand(_ => _ = RemoveImageAsync(), _ => SelectedUserImage != null);
        _saveCommand = new RelayCommand(_ => _ = SaveAsync());
        AddImageCommand = new RelayCommand(_ => _ = AddImageAsync());
        RemoveImageCommand = _removeImageCommand;
        SaveCommand = _saveCommand;

        _ = LoadAsync();
    }

    public string FilePath { get; }

    private string _trackName = "";
    public string TrackName
    {
        get => _trackName;
        set
        {
            if (_trackName == value)
                return;
            _trackName = value;
            OnPropertyChanged();
        }
    }

    public string ArtistName { get; private set; } = "";
    public string AlbumName { get; private set; } = "";
    public string ApiArtworkUrl { get; private set; } = "";

    private string _currentCoverImagePath = "";
    public string CurrentCoverImagePath
    {
        get => _currentCoverImagePath;
        private set
        {
            if (_currentCoverImagePath == value)
                return;
            _currentCoverImagePath = value;
            OnPropertyChanged();
        }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> UserImages { get; } = new();

    private string? _selectedUserImage;
    public string? SelectedUserImage
    {
        get => _selectedUserImage;
        set
        {
            if (_selectedUserImage == value)
                return;
            _selectedUserImage = value;
            OnPropertyChanged();
            _removeImageCommand.RaiseCanExecuteChanged();
        }
    }

    public ICommand AddImageCommand { get; }
    public ICommand RemoveImageCommand { get; }
    public ICommand SaveCommand { get; }

    private async Task LoadAsync()
    {
        StatusMessage = "Loading...";

        SongCacheEntry? entry;
        try
        {
            entry = await _cache.GetAsync(_song.FullPath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cache error: {ex.Message}";
            return;
        }

        _entry = entry ?? new SongCacheEntry
        {
            FilePath = _song.FullPath,
            TrackName = _song.FileNameWithoutExt,
            ApiArtworkUrl = DefaultCoverRelativePath,
            UserImages = new List<string>()
        };

        TrackName = string.IsNullOrWhiteSpace(_entry.TrackName) ? _song.FileNameWithoutExt : _entry.TrackName;
        ArtistName = _entry.ArtistName ?? "";
        AlbumName = _entry.AlbumName ?? "";
        ApiArtworkUrl = string.IsNullOrWhiteSpace(_entry.ApiArtworkUrl) ? DefaultCoverRelativePath : _entry.ApiArtworkUrl;

        UserImages.Clear();
        foreach (var path in _entry.UserImages.Where(File.Exists))
            UserImages.Add(path);

        UpdateCoverImage();
        StatusMessage = "Ready";
        NotifyMetadataChanged();
    }

    private async Task AddImageAsync()
    {
        var sourcePath = _fileDialog.PickImageFile();
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;

        try
        {
            var copiedPath = await _imageStorage.CopyToSongFolderAsync(_song.FullPath, sourcePath, CancellationToken.None);
            UserImages.Add(copiedPath);
            SelectedUserImage = copiedPath;
            UpdateCoverImage();
            StatusMessage = "Image added";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Image error: {ex.Message}";
        }
    }

    private async Task RemoveImageAsync()
    {
        if (SelectedUserImage == null)
            return;

        var toRemove = SelectedUserImage;
        UserImages.Remove(toRemove);
        SelectedUserImage = UserImages.FirstOrDefault();
        UpdateCoverImage();

        try
        {
            await _imageStorage.DeleteImageIfExistsAsync(toRemove, CancellationToken.None);
            StatusMessage = "Image removed";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Image error: {ex.Message}";
        }
    }

    private async Task SaveAsync()
    {
        var entry = new SongCacheEntry
        {
            FilePath = _song.FullPath,
            TrackName = TrackName,
            ArtistName = ArtistName,
            AlbumName = AlbumName,
            ApiArtworkUrl = ApiArtworkUrl,
            UserImages = UserImages.ToList()
        };

        try
        {
            await _cache.UpsertAsync(entry, CancellationToken.None);
            StatusMessage = "Saved";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cache error: {ex.Message}";
        }
    }

    private void UpdateCoverImage()
    {
        if (UserImages.Count > 0)
        {
            CurrentCoverImagePath = ResolveImagePath(UserImages[0]);
            return;
        }

        CurrentCoverImagePath = ResolveImagePath(ApiArtworkUrl);
    }

    private static string ResolveImagePath(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        if (Path.IsPathRooted(value))
            return value;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, value));
    }

    private void NotifyMetadataChanged()
    {
        OnPropertyChanged(nameof(TrackName));
        OnPropertyChanged(nameof(ArtistName));
        OnPropertyChanged(nameof(AlbumName));
        OnPropertyChanged(nameof(ApiArtworkUrl));
        OnPropertyChanged(nameof(CurrentCoverImagePath));
        OnPropertyChanged(nameof(StatusMessage));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
