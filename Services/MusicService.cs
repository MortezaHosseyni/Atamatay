using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.Commands;
using System.Diagnostics;
using Atamatay.Models;
using Atamatay.Utilities;

namespace Atamatay.Services
{
    public interface IMusicService
    {
        Task PlayAsync(SocketCommandContext context);
        Task NextAsync(SocketCommandContext context);
        Task<bool> GetPlayerStatus(SocketCommandContext context);
        Task AddPlaylistAsync(SocketCommandContext context, string query);
        void EnqueuePlaylist(SongModel song);
        Task StopAsync(SocketCommandContext context);
    }

    public class MusicService(IYoutubeService youtube) : IMusicService
    {
        private readonly IYoutubeService _youtube = youtube;

        public PlaylistModel? Playlist { get; set; }

        public async Task PlayAsync(SocketCommandContext context)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null) { await context.Channel.SendMessageAsync("\ud83d\udce3 You must be in a voice channel."); return; }

                if (Playlist?.Songs == null || Playlist.Songs.IsEmpty)
                {
                    await context.Channel.SendMessageAsync($"\ud83d\udd07 |{channel.Name}| channel playlist is empty.");
                    return;
                }

                Playlist.AudioClient = await channel.ConnectAsync();

                while ((!Playlist.SkipRequested || !Playlist.StopRequested) && Playlist.Songs.TryDequeue(out var song))
                {
                    if (song == null) continue;

                    if (!Playlist.IsPlaying)
                    {
                        if (string.IsNullOrEmpty(song.Url))
                        {
                            var searchSong = await _youtube.Search(context, song.Title);
                            song.Url = searchSong.Url;
                            song.Name = searchSong.Name;
                        }

                        var downloadSong = await _youtube.Download(context, song.Url!, channel.Id);
                        if (!downloadSong)
                        {
                            await context.Channel.SendMessageAsync($"\u274c An error occured while playing the song.");
                            return;
                        }
                    }

                    using var ffmpeg = CreateStream($"songs/{Playlist.ChannelId}/{song.Name}");
                    await using var output = ffmpeg.StandardOutput.BaseStream;
                    await context.Channel.SendMessageAsync($"\ud83d\udc3a Playing: {song.Title} | {song.Author} | {song.Duration}");

                    Playlist.IsPlaying = true;
                    Playlist.CurrentSong = new CancellationTokenSource();

                    await using var discord = Playlist.AudioClient.CreatePCMStream(AudioApplication.Music);
                    try
                    {
                        var buffer = new byte[81920];
                        int bytesRead;

                        while ((bytesRead = await output.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            if (Playlist.CurrentSong.Token.IsCancellationRequested)
                            {
                                break;
                            }

                            await discord.WriteAsync(buffer, 0, bytesRead);
                        }
                    }
                    finally
                    {
                        await discord.FlushAsync();
                        Playlist.IsPlaying = false;
                        var channelPath = $"songs/{Playlist.ChannelId}";
                        if (Directory.Exists(channelPath))
                            Directory.Delete(channelPath, true);
                    }

                    if (!Playlist.SkipRequested) continue;
                    Playlist.SkipRequested = false;
                    continue;
                }

                if (!Playlist.SkipRequested && Playlist.Songs.Count == 0)
                {
                    await context.Channel.SendMessageAsync($"\ud83d\udc3a Playlist is empty!");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public async Task NextAsync(SocketCommandContext context)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { await context.Channel.SendMessageAsync("\ud83d\udce3 You must be in a voice channel."); return; }

            if (Playlist?.Songs == null || Playlist.Songs.IsEmpty || Playlist.AudioClient == null)
            {
                await context.Channel.SendMessageAsync($"\ud83d\udd07 |{channel.Name}| channel playlist is empty.");
                return;
            }

            if (!Playlist.IsPlaying)
            {
                await context.Channel.SendMessageAsync("\u2666\ufe0f No song is currently playing.");
                return;
            }

            Playlist.SkipRequested = true;
            if (Playlist.CurrentSong != null)
                await Playlist.CurrentSong.CancelAsync();
            await context.Channel.SendMessageAsync("⏭️ Skipping to next song...");

            if (Playlist.Songs.Count == 0)
            {
                await context.Channel.SendMessageAsync("Playlist is now empty.");
            }
        }

        public async Task<bool> GetPlayerStatus(SocketCommandContext context)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { await context.Channel.SendMessageAsync("\ud83d\udce3 You must be in a voice channel."); return false; }

            if (channel.Id == Playlist?.ChannelId)
                return Playlist is { Songs: { IsEmpty: false }, AudioClient: not null, IsPlaying: true };
            await context.Channel.SendMessageAsync("\ud83d\udc3a Please join to my channel.");
            return true;
        }

        public async Task AddPlaylistAsync(SocketCommandContext context, string query)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null) { await context.Channel.SendMessageAsync("\ud83d\udce3 You must be in a voice channel."); return; }

                Playlist ??= new PlaylistModel
                {
                    ChannelId = channel.Id,
                    Songs = new ConcurrentQueue<SongModel>(),
                    CreatedAt = DateTime.Now
                };

                if (Validation.IsUrl(query) && query.Contains("youtube.com"))
                {
                    var song = await _youtube.Get(context, query, channel.Id);

                    Playlist.Songs?.Enqueue(song);
                    await context.Channel.SendMessageAsync($"\ud83d\udc3a Add to playlist:\n\ud83c\udfb5 {song.Title}\n\ud83d\udc68\u200d\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}");
                }
                else if (Validation.IsUrl(query) && query.Contains("spotify.com"))
                {
                    var spotify = new SpotifyService(this, _youtube);

                    if (query.Contains("/playlist/"))
                    {
                        await spotify.AddPlaylistSongsAsync(context, query, channel.Id);
                    }
                    else if (query.Contains("/track/"))
                    {
                        await spotify.AddSongAsync(context, query);
                    }
                    else
                    {
                        await context.Channel.SendMessageAsync("\u2639\ufe0f Song name or URL is invalid!.");
                        return;
                    }
                }
                else if (Validation.IsUrl(query) && query.Contains("soundcloud.com"))
                {
                    var soundcloud = new SoundcloudService(this, _youtube);
                    await soundcloud.AddSongAsync(context, query);
                }
                else
                {
                    var song = await _youtube.Search(context, query);

                    Playlist.Songs?.Enqueue(song);
                    await context.Channel.SendMessageAsync($"\ud83d\udc3a Add to playlist:\n\ud83c\udfb5 {song.Title}\n\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void EnqueuePlaylist(SongModel song)
        {
            Playlist?.Songs?.Enqueue(song);
        }

        public async Task StopAsync(SocketCommandContext context)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null)
                {
                    await context.Channel.SendMessageAsync("\ud83d\udce3 You must be in a voice channel.");
                    return;
                }

                if (Playlist != null)
                {
                    if (Playlist.ChannelId != channel.Id)
                    {
                        await context.Channel.SendMessageAsync("\ud83d\udc3a Please join to my channel.");
                        return;
                    }

                    Playlist.StopRequested = true;
                    Playlist.IsPlaying = false;
                    if (Playlist.CurrentSong != null)
                        await Playlist.CurrentSong.CancelAsync();

                    Playlist.Songs?.Clear();

                    var audioClient = (context.Guild as IGuild)?.AudioClient;
                    if (audioClient != null)
                        await audioClient.StopAsync();

                    var channelPath = $"songs/{Playlist.ChannelId}";
                    if (Directory.Exists(channelPath))
                        Directory.Delete(channelPath, true);

                    Playlist = null;
                }

                await context.Channel.SendMessageAsync("\u2666\ufe0f Stopped playing and cleared the playlist.");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Playlist stopped.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        #region Statics
        private static Process CreateStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }
        private static void KillStream(Process process)
        {
            if (process is { HasExited: false })
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();
                    Console.WriteLine("Process terminated successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to terminate process: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Process is already exited or null.");
            }
        }

        private static void DeleteSong(string songName, ulong channelId)
        {
            var path = $"songs/{channelId}/{songName}";
            if (File.Exists(path))
                File.Delete(path);
        }
        #endregion
    }
}
