using System.Security.Cryptography;
using System.Text;

namespace MusicPlayerWpf.Services;

public static class AppDataPaths
{
    public static string GetAppRootPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "MusicPlayerWpf");
    }

    public static string GetCacheFilePath()
    {
        return Path.Combine(GetAppRootPath(), "song-metadata-cache.json");
    }

    public static string GetSongImagesFolderPath(string filePath)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(filePath));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return Path.Combine(GetAppRootPath(), "song-images", hash);
    }
}
