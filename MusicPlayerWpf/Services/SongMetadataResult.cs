namespace MusicPlayerWpf.Services;

public sealed class SongMetadataResult
{
    public bool Success { get; init; }
    public string? TrackName { get; init; }
    public string? ArtistName { get; init; }
    public string? AlbumName { get; init; }
    public string? ArtworkUrl { get; init; }
    public string? ErrorMessage { get; init; }
}
