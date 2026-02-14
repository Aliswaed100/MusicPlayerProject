using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MusicPlayerWpf.Models;
using MusicPlayerWpf.Services;

namespace MusicPlayerWpf.ViewModels;

public sealed class PlayerViewModel : INotifyPropertyChanged
{
    private const string DefaultCoverRelativePath = "Assets/default_cover.png";

    private readonly IApiMusicMetadataService _api;
    private readonly IMetadataCache _cache;
    private readonly IAudioPlaybackService _audioPlayback;
    private readonly RelayCommand _playCommand;
    private CancellationTokenSource? _metadataCts;
    private CancellationTokenSource? _selectionCts;

    public PlayerViewModel()
        : this(new ItunesMetadataService(), new JsonMetadataCacheService(), new WpfAudioPlaybackService())
    {
        // Demo songs: replace with your real local files.
        Songs.Add(new SongItem { FullPath = @"C:\Music\Artist - Song.mp3" });
        Songs.Add(new SongItem { FullPath = @"C:\Music\AnotherSong.mp3" });
    }

    public PlayerViewModel(IApiMusicMetadataService api, IMetadataCache cache, IAudioPlaybackService audioPlayback)
    {
        _api = api;
        _cache = cache;
        _audioPlayback = audioPlayback;
        _playCommand = new RelayCommand(_ => _ = PlaySelectedAsync(), _ => SelectedSong != null);
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
            ApplyCachedMetadata(song, cached, "Loaded from cache");
            return;
        }

        TrackName = song.FileNameWithoutExt;
        ArtistName = "";
        AlbumName = "";
        ArtworkUrl = DefaultCoverRelativePath;
        CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
        StatusMessage = "Loading metadata...";
        NotifyMetadataChanged();

        try
        {
            await LoadMetadataFromApiAsync(song, ct);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation when switching songs quickly.
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

            ApplyCachedMetadata(song, cached, "Loaded from cache");
        }
        catch (OperationCanceledException)
        {
            // Ignore selection races.
        }
    }

    private async Task LoadMetadataFromApiAsync(SongItem song, CancellationToken ct)
    {
        var query = SongQueryParser.BuildQueryFromFileName(song.FileNameWithoutExt);
        var result = await _api.SearchAsync(query, ct);

        if (ct.IsCancellationRequested)
            return;

        if (SelectedSong?.FullPath != song.FullPath)
            return;

        if (!result.Success)
        {
            TrackName = song.FileNameWithoutExt;
            DisplayFileName = song.FileNameWithoutExt;
            DisplayFilePath = song.FullPath;
            ArtworkUrl = DefaultCoverRelativePath;
            CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
            StatusMessage = $"API error: {result.ErrorMessage}";
            NotifyMetadataChanged();
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

        ApplyCachedMetadata(song, entry, "OK");
    }

    private void ApplyCachedMetadata(SongItem song, SongCacheEntry entry, string status)
    {
        TrackName = string.IsNullOrWhiteSpace(entry.TrackName) ? song.FileNameWithoutExt : entry.TrackName;
        ArtistName = entry.ArtistName ?? "";
        AlbumName = entry.AlbumName ?? "";
        ArtworkUrl = string.IsNullOrWhiteSpace(entry.ApiArtworkUrl) ? DefaultCoverRelativePath : entry.ApiArtworkUrl;
        CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
        StatusMessage = status;
        NotifyMetadataChanged();
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
