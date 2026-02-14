using System.Windows.Media;

namespace MusicPlayerWpf.Services;

public sealed class WpfAudioPlaybackService : IAudioPlaybackService
{
    private readonly MediaPlayer _player = new();

    public void Play(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio file not found.", filePath);

        _player.Open(new Uri(filePath, UriKind.Absolute));
        _player.Play();
    }

    public void Stop()
    {
        _player.Stop();
    }
}
