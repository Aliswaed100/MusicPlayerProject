namespace MusicPlayerWpf.Services;

public interface IImageStorageService
{
    Task<string> CopyToSongFolderAsync(string songFilePath, string sourceImagePath, CancellationToken ct);
    Task DeleteImageIfExistsAsync(string imagePath, CancellationToken ct);
}
