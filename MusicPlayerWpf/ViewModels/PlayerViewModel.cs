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
  private readonly IApiMusicMetadataService _api = new ItunesMetadataService();
  private CancellationTokenSource? _cts;

  public PlayerViewModel()
  {
    // Demo paths (replace on Windows):
    Songs.Add(new SongItem { FullPath = @"C:\Music\Artist - Song.mp3" });
    Songs.Add(new SongItem { FullPath = @"C:\Music\AnotherSong.mp3" });

    _playCommand = new RelayCommand(async _ => await PlaySelectedAsync(), _ => SelectedSong != null);
    _editCommand = new RelayCommand(_ => { }, _ => SelectedSong != null); // placeholder, Step32 later
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

      if (value != null)
      {
        DisplayFileName = value.FileNameWithoutExt;
        DisplayFilePath = value.FullPath;
        TrackName = value.FileNameWithoutExt;
        ArtistName = "";
        AlbumName = "";
        ArtworkUrl = DefaultCoverRelativePath;
        CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
        StatusMessage = "Ready";
        NotifyAll();
      }
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

  private readonly RelayCommand _playCommand;
  private readonly RelayCommand _editCommand;
  public ICommand PlayCommand => _playCommand;
  public ICommand EditCommand => _editCommand;

  public async Task PlaySelectedAsync()
  {
    if (SelectedSong == null) return;
    var song = SelectedSong;

    // start playback later (placeholder)
    // audio must not block UI

    _cts?.Cancel();
    _cts?.Dispose();
    _cts = new CancellationTokenSource();
    var ct = _cts.Token;

    StatusMessage = "Loading...";
    ArtworkUrl = DefaultCoverRelativePath;
    CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
    NotifyAll();

    var query = SongQueryParser.BuildQueryFromFileName(song.FileNameWithoutExt);
    var result = await _api.SearchAsync(query, ct);

    if (ct.IsCancellationRequested) return;
    if (SelectedSong?.FullPath != song.FullPath) return;

    if (!result.Success)
    {
      TrackName = song.FileNameWithoutExt;
      DisplayFilePath = song.FullPath;
      StatusMessage = $"API error: {result.ErrorMessage}";
      NotifyAll();
      return;
    }

    TrackName = result.TrackName ?? song.FileNameWithoutExt;
    ArtistName = result.ArtistName ?? "";
    AlbumName = result.AlbumName ?? "";
    ArtworkUrl = string.IsNullOrWhiteSpace(result.ArtworkUrl) ? DefaultCoverRelativePath : result.ArtworkUrl!;
    CurrentCoverImagePath = ResolveImagePath(ArtworkUrl);
    StatusMessage = "OK";
    NotifyAll();
  }

  private static string ResolveImagePath(string value)
  {
    if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
      return absoluteUri.ToString();
    if (System.IO.Path.IsPathRooted(value))
      return value;
    return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, value));
  }

  private void NotifyAll()
  {
    OnPropertyChanged(nameof(DisplayFileName));
    OnPropertyChanged(nameof(DisplayFilePath));
    OnPropertyChanged(nameof(TrackName));
    OnPropertyChanged(nameof(ArtistName));
    OnPropertyChanged(nameof(AlbumName));
    OnPropertyChanged(nameof(ArtworkUrl));
    OnPropertyChanged(nameof(CurrentCoverImagePath));
    OnPropertyChanged(nameof(StatusMessage));
  }

  public event PropertyChangedEventHandler? PropertyChanged;
  private void OnPropertyChanged([CallerMemberName] string? name = null)
    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
