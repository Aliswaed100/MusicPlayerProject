using System.Net.Http;
using System.Net.Http.Json;

namespace MusicPlayerWpf.Services;

public sealed class ItunesMetadataService : IApiMusicMetadataService
{
    private static readonly HttpClient Http = new();

    public async Task<SongMetadataResult> SearchAsync(string query, CancellationToken ct)
    {
        try
        {
            var url =
                "https://itunes.apple.com/search?media=music&entity=song&limit=1&term=" +
                Uri.EscapeDataString(query);

            var resp = await Http.GetFromJsonAsync<ItunesSearchResponse>(url, ct);
            var first = resp?.Results?.FirstOrDefault();

            if (first == null)
                return new SongMetadataResult { Success = false, ErrorMessage = "No results" };

            return new SongMetadataResult
            {
                Success = true,
                TrackName = first.TrackName,
                ArtistName = first.ArtistName,
                AlbumName = first.CollectionName,
                ArtworkUrl = first.ArtworkUrl100
            };
        }
        catch (Exception ex)
        {
            return new SongMetadataResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
