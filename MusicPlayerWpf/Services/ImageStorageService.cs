namespace MusicPlayerWpf.Services;

public sealed class ImageStorageService : IImageStorageService
{
    public async Task<string> CopyToSongFolderAsync(string songFilePath, string sourceImagePath, CancellationToken ct)
    {
        var folder = AppDataPaths.GetSongImagesFolderPath(songFilePath);
        Directory.CreateDirectory(folder);

        var fileName = Path.GetFileName(sourceImagePath);
        var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var destPath = Path.Combine(folder, uniqueName);

        await Task.Run(() => File.Copy(sourceImagePath, destPath, overwrite: true), ct);
        return destPath;
    }

    public async Task DeleteImageIfExistsAsync(string imagePath, CancellationToken ct)
    {
        if (!File.Exists(imagePath))
            return;

        await Task.Run(() => File.Delete(imagePath), ct);
    }
}
