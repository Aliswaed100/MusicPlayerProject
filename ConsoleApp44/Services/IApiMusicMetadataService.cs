namespace YourApp.Services;

public interface IApiMusicMetadataService
{
    Task<SongMetadataResult> SearchAsync(string query, CancellationToken ct);
}
