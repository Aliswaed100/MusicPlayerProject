using System.Text.Json;

namespace MusicPlayerWpf.Services;

public sealed class JsonMetadataCacheService : IMetadataCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _ioLock = new(1, 1);
    public string CacheFilePath { get; }

    public JsonMetadataCacheService(string? cacheFilePath = null)
    {
        CacheFilePath = cacheFilePath ?? AppDataPaths.GetCacheFilePath();
    }

    public async Task<SongCacheEntry?> GetAsync(string filePath, CancellationToken ct)
    {
        await _ioLock.WaitAsync(ct);
        try
        {
            var cache = await LoadDictionaryAsync(ct);
            return cache.TryGetValue(filePath, out var entry) ? Clone(entry) : null;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task UpsertAsync(SongCacheEntry entry, CancellationToken ct)
    {
        await _ioLock.WaitAsync(ct);
        try
        {
            var cache = await LoadDictionaryAsync(ct);
            cache[entry.FilePath] = Normalize(entry);
            await SaveDictionaryAsync(cache, ct);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<Dictionary<string, SongCacheEntry>> LoadDictionaryAsync(CancellationToken ct)
    {
        if (!File.Exists(CacheFilePath))
            return new Dictionary<string, SongCacheEntry>(StringComparer.OrdinalIgnoreCase);

        await using var stream = new FileStream(CacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = await JsonSerializer.DeserializeAsync<Dictionary<string, SongCacheEntry>>(stream, JsonOptions, ct);
        return result != null
            ? new Dictionary<string, SongCacheEntry>(result, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, SongCacheEntry>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveDictionaryAsync(Dictionary<string, SongCacheEntry> cache, CancellationToken ct)
    {
        var folder = Path.GetDirectoryName(CacheFilePath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        await using var stream = new FileStream(CacheFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, cache, JsonOptions, ct);
    }

    private static SongCacheEntry Normalize(SongCacheEntry source)
    {
        return new SongCacheEntry
        {
            FilePath = source.FilePath,
            TrackName = source.TrackName,
            ArtistName = source.ArtistName,
            AlbumName = source.AlbumName,
            ApiArtworkUrl = source.ApiArtworkUrl,
            UserImages = source.UserImages
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static SongCacheEntry Clone(SongCacheEntry source)
    {
        return new SongCacheEntry
        {
            FilePath = source.FilePath,
            TrackName = source.TrackName,
            ArtistName = source.ArtistName,
            AlbumName = source.AlbumName,
            ApiArtworkUrl = source.ApiArtworkUrl,
            UserImages = source.UserImages.ToList()
        };
    }
}
