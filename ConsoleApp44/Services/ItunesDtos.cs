namespace YourApp.Services;

public sealed class ItunesSearchResponse
{
    public int ResultCount { get; set; }
    public List<ItunesTrack> Results { get; set; } = new();
}

public sealed class ItunesTrack
{
    public string? TrackName { get; set; }
    public string? ArtistName { get; set; }
    public string? CollectionName { get; set; }
    public string? ArtworkUrl100 { get; set; }
}
