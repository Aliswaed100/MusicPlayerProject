namespace MusicPlayerWpf.Services;

public sealed class SongCacheEntry
{
    public string FilePath { get; set; } = "";
    public string? TrackName { get; set; }
    public string? ArtistName { get; set; }
    public string? AlbumName { get; set; }
    public string? ApiArtworkUrl { get; set; }
    public List<string> UserImages { get; set; } = new();
}
