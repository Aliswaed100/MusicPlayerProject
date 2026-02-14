using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using YourApp.Models;
using YourApp.Services;

namespace YourApp.ViewModels;

public sealed class PlayerViewModel : INotifyPropertyChanged
{
    private readonly IApiMusicMetadataService _api = new ItunesMetadataService();
    private CancellationTokenSource? _cts;

    public PlayerViewModel()
    {
        // مثال: غيّر المسارات لملفات mp3 عندك
        Songs.Add(new SongItem { FullPath = @"C:\Music\Artist - Song.mp3" });
        Songs.Add(new SongItem { FullPath = @"C:\Music\AnotherSong.mp3" });
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

            // Single click: show file name + path
            if (value != null)
            {
                DisplayFileName = value.FileNameWithoutExt;
                DisplayFilePath = value.FullPath;
                OnPropertyChanged(nameof(DisplayFileName));
                OnPropertyChanged(nameof(DisplayFilePath));
            }
        }
    }

    public string DisplayFileName { get; set; } = "";
    public string DisplayFilePath { get; set; } = "";

    public string TrackName { get; set; } = "";
    public string ArtistName { get; set; } = "";
    public string AlbumName { get; set; } = "";
    public string ArtworkUrl { get; set; } = "pack://application:,,,/Assets/default_cover.png";
    public string StatusMessage { get; set; } = "";

    public ICommand PlayCommand => new RelayCommand(async _ => await PlaySelectedAsync());

    public async Task PlaySelectedAsync()
    {
        if (SelectedSong == null) return;

        // A) start audio NOW (بدّل هذه بدالة التشغيل الموجودة عندك)
        StartPlayback(SelectedSong.FullPath);

        // B) cancel previous API call
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        // C) show default while loading
        TrackName = SelectedSong.FileNameWithoutExt;
        ArtistName = "";
        AlbumName = "";
        ArtworkUrl = "pack://application:,,,/Assets/default_cover.png";
        StatusMessage = "Loading...";
        NotifyMetadataChanged();

        // D) fetch metadata async
        await LoadMetadataAsync(SelectedSong, _cts.Token);
    }

    private async Task LoadMetadataAsync(SongItem song, CancellationToken ct)
    {
        var query = SongQueryParser.BuildQueryFromFileName(song.FileNameWithoutExt);
        var result = await _api.SearchAsync(query, ct);

        if (ct.IsCancellationRequested) return;

        if (!result.Success)
        {
            // عند خطأ: اعرض اسم الملف + المسار
            TrackName = song.FileNameWithoutExt;
            DisplayFilePath = song.FullPath;
            StatusMessage = $"API error: {result.ErrorMessage}";
            NotifyMetadataChanged();
            return;
        }

        TrackName = result.TrackName ?? song.FileNameWithoutExt;
        ArtistName = result.ArtistName ?? "";
        AlbumName = result.AlbumName ?? "";
        ArtworkUrl = result.ArtworkUrl ?? "pack://application:,,,/Assets/default_cover.png";
        StatusMessage = "OK";
        NotifyMetadataChanged();
    }

    private void NotifyMetadataChanged()
    {
        OnPropertyChanged(nameof(TrackName));
        OnPropertyChanged(nameof(ArtistName));
        OnPropertyChanged(nameof(AlbumName));
        OnPropertyChanged(nameof(ArtworkUrl));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(DisplayFileName));
        OnPropertyChanged(nameof(DisplayFilePath));
    }

    private void StartPlayback(string path)
    {
        // مؤقت: خليها فاضية إذا مش جاهز.
        // لاحقاً تربط MediaPlayer/NAudio حسب مشروعك.
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
