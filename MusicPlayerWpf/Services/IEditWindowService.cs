using MusicPlayerWpf.Models;

namespace MusicPlayerWpf.Services;

public interface IEditWindowService
{
    void ShowEditSongWindow(SongItem song, IMetadataCache cache);
}
