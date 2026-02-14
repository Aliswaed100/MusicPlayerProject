# ConsoleApp44 / MusicPlayerWpf

## What the app does
- Plays local music files from the list.
- Fetches song metadata from the iTunes Search API during playback.
- Caches metadata locally in JSON to avoid repeated API calls.
- Provides an Edit window to update Track Name and manage per-song images.
- Rotates user-added images during playback (every 3 seconds).

## Run on Windows
- `dotnet build ConsoleApp44.sln`
- `dotnet run --project MusicPlayerWpf`

## Notes
- WPF runs on Windows only.
- Target framework: `net9.0-windows`.

## Cache location and reset
- JSON cache: `%APPDATA%\MusicPlayerWpf\song-metadata-cache.json`
- User images: `%APPDATA%\MusicPlayerWpf\song-images\{hash}\`
- To reset: delete the JSON file and the `song-images` folder.
