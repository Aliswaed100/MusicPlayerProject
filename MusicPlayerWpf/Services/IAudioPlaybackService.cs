namespace MusicPlayerWpf.Services;

public interface IAudioPlaybackService
{
    void Play(string filePath);
    void Stop();
}
