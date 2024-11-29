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
        Task EnqueuePlaylist(SocketCommandContext context, SongModel song);
        Task StopAsync(SocketCommandContext context);
    }

    public class MusicService(IYoutubeService youtube) : IMusicService
    {
        private readonly IYoutubeService _youtube = youtube;

        public List<PlaylistModel>? Playlist { get; set; } = new List<PlaylistModel>();

        public async Task PlayAsync(SocketCommandContext context)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null) { return; }

                var playList = Playlist?.FirstOrDefault(p => p.ChannelId == channel.Id);
                if (playList == null)
                {
                    await Message.SendEmbedAsync(context, "Playlist!", "Channel playlist is empty.", Color.Red);
                    return;
                }

                if (playList.Songs == null || playList.Songs.IsEmpty)
                {
                    await Message.SendEmbedAsync(context, channel.Name, "Channel playlist is empty.", Color.Purple);
                    return;
                }

                if (channel.Id != playList.ChannelId)
                {
                    await Message.SendEmbedAsync(context, context.Channel.Name, "Please join to my voice channel.", Color.Blue);
                    return;
                }

                while (!playList.SkipRequested && playList.Songs.TryDequeue(out var song))
                {
                    if (song == null) continue;

                    if (!playList.IsPlaying)
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
                            await Message.SendEmbedAsync(context, "ERROR!", "An error occured while playing the song.", Color.Red);
                            continue;
                        }
                    }

                    using var ffmpeg = CreateStream($"songs/{playList.ChannelId}/{song.Name}");
                    await using var output = ffmpeg.StandardOutput.BaseStream;
                    await Message.SendEmbedAsync(context, "Playing...", $"{song.Title} | {song.Author} | {song.Duration}", Color.Green);

                    playList.IsPlaying = true;
                    playList.CurrentSong = new CancellationTokenSource();

                    playList.AudioClient ??= await channel.ConnectAsync(true);
                    await using var discord = playList.AudioClient.CreatePCMStream(AudioApplication.Music);
                    try
                    {
                        var buffer = new byte[81920];
                        int bytesRead;

                        while ((bytesRead = await output.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            if (playList.CurrentSong.Token.IsCancellationRequested)
                            {
                                break;
                            }

                            await discord.WriteAsync(buffer, 0, bytesRead);
                        }
                    }
                    finally
                    {
                        await discord.FlushAsync();
                        playList.IsPlaying = false;

                        ffmpeg.Kill();

                        await Task.Delay(1000);

                        var channelPath = $"songs/{playList.ChannelId}";
                        if (Directory.Exists(channelPath))
                            Directory.Delete(channelPath, true);

                        await Task.Delay(1000);

                        await OnAudioFinished(context, playList.ChannelId);
                    }

                    if (!playList.SkipRequested) continue;
                    playList.SkipRequested = false;
                    continue;
                }

                if (!playList.SkipRequested && playList.Songs.Count == 0)
                {
                    await Message.SendEmbedAsync(context, "Empty playlist!", "All the songs were played.", Color.Purple);
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
            if (channel == null)
            {
                await Message.SendEmbedAsync(context, "Join to voice!", "You must be in a voice channel.", Color.Blue);
                return;
            }

            var playList = Playlist?.FirstOrDefault(p => p.ChannelId == channel.Id);
            if (playList == null)
            {
                await Message.SendEmbedAsync(context, "Playlist!", "Channel playlist is empty.", Color.Red);
                return;
            }

            if (playList.Songs == null || playList.Songs.IsEmpty || playList.AudioClient == null)
            {
                await Message.SendEmbedAsync(context, channel.Name, "Channel playlist is empty.", Color.Purple);
                return;
            }

            if (!playList.IsPlaying)
            {
                await Message.SendEmbedAsync(context, "Empty playlist!", "No song is currently playing", Color.Red);
                return;
            }

            playList.SkipRequested = true;
            if (playList.CurrentSong != null)
                await playList.CurrentSong.CancelAsync();
            await Message.SendEmbedAsync(context, "NEXT!", "Skipping to next song...", Color.Green);

            if (playList.Songs.Count == 0)
            {
                await Message.SendEmbedAsync(context, "Empty playlist!", "All the songs were played.", Color.Purple);
            }
        }

        public async Task<bool> GetPlayerStatus(SocketCommandContext context)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { return false; }

            var playList = Playlist?.FirstOrDefault(p => p.ChannelId == channel.Id);
            if (playList == null)
            {
                await Message.SendEmbedAsync(context, "Playlist!", "Channel playlist is empty.", Color.Red);
                return false;
            }

            if (channel.Id == playList.ChannelId)
                return playList is { Songs: { IsEmpty: false }, AudioClient: not null, IsPlaying: true };
            return true;
        }

        public async Task AddPlaylistAsync(SocketCommandContext context, string query)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null)
                {
                    await Message.SendEmbedAsync(context, "Join to voice!", "You must be in a voice channel.", Color.Blue);
                    return;
                }

                var playList = new PlaylistModel
                {
                    ChannelId = channel.Id,
                    Songs = new ConcurrentQueue<SongModel>(),
                    CreatedAt = DateTime.Now
                };

                var existsPlaylist = Playlist?.FirstOrDefault(p => p.ChannelId == channel.Id);
                if (existsPlaylist != null)
                    playList = existsPlaylist;
                else
                    Playlist?.Add(playList);

                if (Validation.IsUrl(query) && query.Contains("youtube.com"))
                {
                    var song = await _youtube.Get(context, query, channel.Id);

                    playList.Songs?.Enqueue(song);
                    await Message.SendEmbedAsync(context, "Add to playlist", $"\ud83c\udfb5 {song.Title}\n\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}", Color.Green);
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
                        await Message.SendEmbedAsync(context, "Invalid query", "Song name or URL is invalid", Color.Red);
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

                    playList.Songs?.Enqueue(song);
                    await Message.SendEmbedAsync(context, "Add to playlist", $"\ud83c\udfb5 {song.Title}\n\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}", Color.Green);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task EnqueuePlaylist(SocketCommandContext context, SongModel song)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { return; }

            var playList = Playlist?.FirstOrDefault(p => p.ChannelId == channel.Id);
            if (playList == null)
            {
                await Message.SendEmbedAsync(context, "Playlist!", "Channel playlist is empty.", Color.Red);
                return;
            }

            playList.Songs?.Enqueue(song);
        }

        public async Task StopAsync(SocketCommandContext context)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null)
                {
                    await Message.SendEmbedAsync(context, "Join to voice!", "You must be in a voice channel.", Color.Blue);
                    return;
                }

                var playList = Playlist?.FirstOrDefault(p => p.ChannelId == channel.Id);
                if (playList == null)
                {
                    await Message.SendEmbedAsync(context, "Playlist!", "Channel playlist is empty.", Color.Red);
                    return;
                }

                if (Playlist != null)
                {
                    if (playList.ChannelId != channel.Id)
                    {
                        await Message.SendEmbedAsync(context, "Join to voice!", "Please join to my voice channel.", Color.Blue);
                        return;
                    }

                    var voiceChannel = context.Guild.GetVoiceChannel(channel.Id);
                    if (voiceChannel != null)
                        await voiceChannel.DisconnectAsync();

                    var audioClient = playList.AudioClient;
                    if (audioClient != null)
                        await audioClient.StopAsync();
                    playList.AudioClient = null;

                    playList.IsPlaying = false;
                    if (playList.CurrentSong != null)
                        await playList.CurrentSong.CancelAsync();
                    playList.Songs?.Clear();
                    playList = null;
                }

                await Message.SendEmbedAsync(context, "STOP!", "Stopped playing and cleared the playlist.", Color.Orange, "Bot disconnected from channel.");
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

        private async Task OnAudioFinished(SocketCommandContext context, ulong channelId)
        {
            var voiceChannel = context.Guild.GetVoiceChannel(channelId);

            var usersInChannel = voiceChannel?.Users
                .Count(x => x.Id != context.Client.CurrentUser.Id);

            if (usersInChannel <= 0 && voiceChannel != null)
            {
                await voiceChannel.DisconnectAsync();

                var playList = Playlist?.FirstOrDefault(p => p.ChannelId == channelId);
                if (playList != null)
                {
                    playList.IsPlaying = false;
                    if (playList.CurrentSong != null)
                        await playList.CurrentSong.CancelAsync();

                    playList.Songs?.Clear();

                    var audioClient = (context.Guild as IGuild)?.AudioClient;
                    if (audioClient != null)
                        await audioClient.StopAsync();
                    playList.AudioClient = null;
                }
            }
        }
        #endregion
    }
}
