using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using MusicPlayerWpf.Models;
using MusicPlayerWpf.Services;

namespace MusicPlayerWpf.ViewModels;

public sealed class PlayerViewModel : INotifyPropertyChanged
{
    private const string DefaultCoverRelativePath = "Assets/default_cover.png";

    private readonly IApiMusicMetadataService _api;
    private readonly IMetadataCache _cache;
    private readonly IAudioPlaybackService _audioPlayback;
    private readonly IEditWindowService _editWindowService;
    private readonly RelayCommand _playCommand;
    private readonly RelayCommand _editCommand;
    private readonly DispatcherTimer _imageTimer;
    private readonly List<string> _loopImages = new();
    private int _loopIndex;
    private string? _nowPlayingPath;
    private CancellationTokenSource? _metadataCts;
    private CancellationTokenSource? _selectionCts;

    public PlayerViewModel()
        : this(
            new ItunesMetadataService(),
            new JsonMetadataCacheService(),
            new WpfAudioPlaybackService(),
            new WpfEditWindowService(new WpfFileDialogService(), new ImageStorageService()))
    {
        // Demo songs: replace with your real local files.
        Songs.Add(new SongItem { FullPath = @"C:\Music\Artist - Song.mp3" });
        Songs.Add(new SongItem { FullPath = @"C:\Music\AnotherSong.mp3" });
    }

    public PlayerViewModel(
        IApiMusicMetadataService api,
        IMetadataCache cache,
        IAudioPlaybackService audioPlayback,
        IEditWindowService editWindowService)
    {
        _api = api;
        _cache = cache;
        _audioPlayback = audioPlayback;
        _editWindowService = editWindowService;
        _playCommand = new RelayCommand(_ => _ = PlaySelectedAsync(), _ => SelectedSong != null);
        _editCommand = new RelayCommand(_ => _ = OpenEditWindowAsync(), _ => SelectedSong != null);
        _imageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _imageTimer.Tick += OnImageTimerTick;
        CurrentCoverImagePath = ResolveImagePath(DefaultCoverRelativePath);
    }

    public ObservableCollection<SongItem> Songs { get; } = new();

    private SongItem? _selectedSong;
    public SongItem? SelectedSong
    {
        get => _selectedSong;
        set
        {
            _selectedSong = value;
            OnPropertyChanged();
            _playCommand.RaiseCanExecuteChanged();
            _editCommand.RaiseCanExecuteChanged();

            StopImageLoop();

            if (value == null)
                return;

            DisplayFileName = value.FileNameWithoutExt;
            DisplayFilePath = value.FullPath;
            TrackName = value.FileNameWithoutExt;
            ArtistName = "";
            AlbumName = "";
            ArtworkUrl = DefaultCoverRelativePath;
            CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
            StatusMessage = "Ready";
            NotifyMetadataChanged();

            _ = LoadCachedSelectionAsync(value);
        }
    }

    public string DisplayFileName { get; private set; } = "";
    public string DisplayFilePath { get; private set; } = "";
    public string TrackName { get; private set; } = "";
    public string ArtistName { get; private set; } = "";
    public string AlbumName { get; private set; } = "";
    public string ArtworkUrl { get; private set; } = DefaultCoverRelativePath;
    public string CurrentCoverImagePath { get; private set; } = "";
    public string StatusMessage { get; private set; } = "";

    public ICommand PlayCommand => _playCommand;
    public ICommand EditCommand => _editCommand;

    public async Task PlaySelectedAsync()
    {
        if (SelectedSong == null)
            return;

        var song = SelectedSong;

        try
        {
            _audioPlayback.Play(song.FullPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Playback error: {ex.Message}";
            OnPropertyChanged(nameof(StatusMessage));
            return;
        }

        _nowPlayingPath = song.FullPath;
        StopImageLoop();

        _metadataCts?.Cancel();
        _metadataCts?.Dispose();
        _metadataCts = new CancellationTokenSource();
        var ct = _metadataCts.Token;

        SongCacheEntry? cached;
        try
        {
            cached = await _cache.GetAsync(song.FullPath, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested || SelectedSong?.FullPath != song.FullPath)
            return;

        if (cached != null)
        {
            ApplyCachedMetadata(song, cached, "Loaded from cache", isPlayback: true);
            return;
        }

        ApplyFallbackMetadata(song, "Loading metadata...");

        try
        {
            await LoadMetadataFromApiAsync(song, ct, isPlayback: true);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation when switching songs quickly.
        }
        catch (Exception ex)
        {
            StatusMessage = $"Metadata error: {ex.Message}";
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    private async Task OpenEditWindowAsync()
    {
        if (SelectedSong == null)
            return;

        var song = SelectedSong;
        _editWindowService.ShowEditSongWindow(song, _cache);
        await RefreshAfterEditAsync(song);
    }

    private async Task RefreshAfterEditAsync(SongItem song)
    {
        try
        {
            var cached = await _cache.GetAsync(song.FullPath, CancellationToken.None);
            if (cached == null)
                return;

            var isPlayback = _nowPlayingPath == song.FullPath;
            ApplyCachedMetadata(song, cached, "Updated", isPlayback);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cache error: {ex.Message}";
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    private async Task LoadCachedSelectionAsync(SongItem song)
    {
        _selectionCts?.Cancel();
        _selectionCts?.Dispose();
        _selectionCts = new CancellationTokenSource();
        var ct = _selectionCts.Token;

        try
        {
            var cached = await _cache.GetAsync(song.FullPath, ct);
            if (cached == null || ct.IsCancellationRequested || SelectedSong?.FullPath != song.FullPath)
                return;

            ApplyCachedMetadata(song, cached, "Loaded from cache", isPlayback: false);
        }
        catch (OperationCanceledException)
        {
            // Ignore selection races.
        }
    }

    private async Task LoadMetadataFromApiAsync(SongItem song, CancellationToken ct, bool isPlayback)
    {
        var query = SongQueryParser.BuildQueryFromFileName(song.FileNameWithoutExt);
        SongMetadataResult result;
        try
        {
            result = await _api.SearchAsync(query, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
            return;

        if (SelectedSong?.FullPath != song.FullPath)
            return;

        if (!result.Success)
        {
            ApplyFallbackMetadata(song, $"API error: {result.ErrorMessage}");
            return;
        }

        var entry = new SongCacheEntry
        {
            FilePath = song.FullPath,
            TrackName = result.TrackName ?? song.FileNameWithoutExt,
            ArtistName = result.ArtistName ?? "",
            AlbumName = result.AlbumName ?? "",
            ApiArtworkUrl = string.IsNullOrWhiteSpace(result.ArtworkUrl) ? DefaultCoverRelativePath : result.ArtworkUrl,
            UserImages = new List<string>()
        };

        await _cache.UpsertAsync(entry, ct);

        if (ct.IsCancellationRequested || SelectedSong?.FullPath != song.FullPath)
            return;

        ApplyCachedMetadata(song, entry, "OK", isPlayback);
    }

    private void ApplyCachedMetadata(SongItem song, SongCacheEntry entry, string status, bool isPlayback)
    {
        DisplayFileName = song.FileNameWithoutExt;
        DisplayFilePath = song.FullPath;
        TrackName = string.IsNullOrWhiteSpace(entry.TrackName) ? song.FileNameWithoutExt : entry.TrackName;
        ArtistName = entry.ArtistName ?? "";
        AlbumName = entry.AlbumName ?? "";

        var apiArtwork = string.IsNullOrWhiteSpace(entry.ApiArtworkUrl)
            ? DefaultCoverRelativePath
            : entry.ApiArtworkUrl;
        ArtworkUrl = apiArtwork;

        var userImages = GetValidUserImages(entry);
        if (isPlayback && userImages.Count > 0)
        {
            StartImageLoop(userImages);
            CurrentCoverImagePath = ResolveImagePath(userImages[0]);
        }
        else
        {
            StopImageLoop();
            CurrentCoverImagePath = ResolveImagePath(userImages.Count > 0 ? userImages[0] : apiArtwork);
        }

        StatusMessage = status;
        NotifyMetadataChanged();
    }

    private void ApplyFallbackMetadata(SongItem song, string status)
    {
        DisplayFileName = song.FileNameWithoutExt;
        DisplayFilePath = song.FullPath;
        TrackName = song.FileNameWithoutExt;
        ArtistName = "";
        AlbumName = "";
        ArtworkUrl = DefaultCoverRelativePath;
        CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
        StatusMessage = status;
        StopImageLoop();
        NotifyMetadataChanged();
    }

    private static List<string> GetValidUserImages(SongCacheEntry entry)
    {
        return entry.UserImages
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .ToList();
    }

    private void StartImageLoop(List<string> images)
    {
        _loopImages.Clear();
        _loopImages.AddRange(images);
        _loopIndex = 0;

        _imageTimer.Stop();
        if (_loopImages.Count > 1)
            _imageTimer.Start();
    }

    private void StopImageLoop()
    {
        _imageTimer.Stop();
        _loopImages.Clear();
        _loopIndex = 0;
    }

    private void OnImageTimerTick(object? sender, EventArgs e)
    {
        if (_loopImages.Count == 0)
            return;

        _loopIndex = (_loopIndex + 1) % _loopImages.Count;
        CurrentCoverImagePath = ResolveImagePath(_loopImages[_loopIndex]);
        OnPropertyChanged(nameof(CurrentCoverImagePath));
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
        OnPropertyChanged(nameof(ArtworkUrl));
        OnPropertyChanged(nameof(CurrentCoverImagePath));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(DisplayFileName));
        OnPropertyChanged(nameof(DisplayFilePath));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
