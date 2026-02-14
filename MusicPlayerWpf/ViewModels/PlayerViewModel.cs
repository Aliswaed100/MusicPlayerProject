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
    private readonly IAudioPlaybackService _audioPlayback;
    private readonly RelayCommand _playCommand;
    private CancellationTokenSource? _metadataCts;

    public PlayerViewModel()
        : this(new ItunesMetadataService(), new WpfAudioPlaybackService())
    {
        // Demo songs: replace with your real local files.
        Songs.Add(new SongItem { FullPath = @"C:\Music\Artist - Song.mp3" });
        Songs.Add(new SongItem { FullPath = @"C:\Music\AnotherSong.mp3" });
    }

    public PlayerViewModel(IApiMusicMetadataService api, IAudioPlaybackService audioPlayback)
    {
        _api = api;
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

        TrackName = song.FileNameWithoutExt;
        ArtistName = "";
        AlbumName = "";
        ArtworkUrl = DefaultCoverRelativePath;
        CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
        StatusMessage = "Loading metadata...";
        NotifyMetadataChanged();

        await LoadMetadataAsync(song, _metadataCts.Token);
    }

    private async Task LoadMetadataAsync(SongItem song, CancellationToken ct)
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
            TrackName = song.FileNameWithoutExt;
            ArtistName = "";
            AlbumName = "";
            DisplayFileName = song.FileNameWithoutExt;
            DisplayFilePath = song.FullPath;
            ArtworkUrl = DefaultCoverRelativePath;
            CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
            StatusMessage = $"API error: {result.ErrorMessage}";
            NotifyMetadataChanged();
            return;
        }

        TrackName = result.TrackName ?? song.FileNameWithoutExt;
        ArtistName = result.ArtistName ?? "";
        AlbumName = result.AlbumName ?? "";
        ArtworkUrl = string.IsNullOrWhiteSpace(result.ArtworkUrl) ? DefaultCoverRelativePath : result.ArtworkUrl;
        CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
        StatusMessage = "OK";
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
