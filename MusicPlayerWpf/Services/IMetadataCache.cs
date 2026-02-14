namespace MusicPlayerWpf.Services;

public interface IMetadataCache
{
    string CacheFilePath { get; }
    Task<SongCacheEntry?> GetAsync(string filePath, CancellationToken ct);
    Task UpsertAsync(SongCacheEntry entry, CancellationToken ct);
}
