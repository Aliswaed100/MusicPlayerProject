namespace YourApp.Models;

public sealed class SongItem
{
    public string FullPath { get; set; } = "";
    public string FileNameWithoutExt => System.IO.Path.GetFileNameWithoutExtension(FullPath);
}
