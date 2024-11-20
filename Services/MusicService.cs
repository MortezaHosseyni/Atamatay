using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.Commands;
using System.Diagnostics;
using Atamatay.Models;
using Atamatay.Utilities;
using System.Timers;
using Timer = System.Timers.Timer;

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
        void SetContext(SocketCommandContext context);
    }

    public class MusicService(IYoutubeService youtube) : IMusicService
    {
        private readonly IYoutubeService _youtube = youtube;

        public PlaylistModel? Playlist { get; set; }

        private Timer _timer;
        private SocketCommandContext? _context;

        public async Task PlayAsync(SocketCommandContext context)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null)
                {
                    await Message.SendEmbedAsync(context, "Join to voice!", "You must be in a voice channel.", Color.Blue);
                    return;
                }

                if (Playlist?.Songs == null || Playlist.Songs.IsEmpty)
                {
                    await Message.SendEmbedAsync(context, channel.Name, "Channel playlist is empty.", Color.Purple);
                    return;
                }

                if (channel.Id != Playlist.ChannelId)
                {
                    await Message.SendEmbedAsync(context, context.Channel.Name, "Please join to my voice channel.", Color.Blue);
                    return;
                }

                _timer = new Timer();
                _timer.Interval = 120000;
                _timer.Elapsed += async (sender, e) => await TimerElapsed(sender, e);
                _timer.Start();

                while (!Playlist.SkipRequested && Playlist.Songs.TryDequeue(out var song))
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
                            await Message.SendEmbedAsync(context, "ERROR!", "An error occured while playing the song.", Color.Red);
                            continue;
                        }
                    }

                    using var ffmpeg = CreateStream($"songs/{Playlist.ChannelId}/{song.Name}");
                    await using var output = ffmpeg.StandardOutput.BaseStream;
                    await Message.SendEmbedAsync(context, "Playing...", $"{song.Title} | {song.Author} | {song.Duration}", Color.Green);

                    Playlist.IsPlaying = true;
                    Playlist.CurrentSong = new CancellationTokenSource();

                    Playlist.AudioClient ??= await channel.ConnectAsync();
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

                        ffmpeg.Kill();

                        await Task.Delay(1000);

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

            if (Playlist?.Songs == null || Playlist.Songs.IsEmpty || Playlist.AudioClient == null)
            {
                await Message.SendEmbedAsync(context, channel.Name, "Channel playlist is empty.", Color.Purple);
                return;
            }

            if (!Playlist.IsPlaying)
            {
                await Message.SendEmbedAsync(context, "Empty playlist!", "No song is currently playing", Color.Red);
                return;
            }

            Playlist.SkipRequested = true;
            if (Playlist.CurrentSong != null)
                await Playlist.CurrentSong.CancelAsync();
            await Message.SendEmbedAsync(context, "NEXT!", "Skipping to next song...", Color.Green);

            if (Playlist.Songs.Count == 0)
            {
                await Message.SendEmbedAsync(context, "Empty playlist!", "All the songs were played.", Color.Purple);
            }
        }

        public async Task<bool> GetPlayerStatus(SocketCommandContext context)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { return false; }

            if (channel.Id == Playlist?.ChannelId)
                return Playlist is { Songs: { IsEmpty: false }, AudioClient: not null, IsPlaying: true };
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

                    Playlist.Songs?.Enqueue(song);
                    await Message.SendEmbedAsync(context, "Add to playlist", $"\ud83c\udfb5 {song.Title}\n\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}", Color.Green);
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
                    await Message.SendEmbedAsync(context, "Join to voice!", "You must be in a voice channel.", Color.Blue);
                    return;
                }

                if (Playlist != null)
                {
                    if (Playlist.ChannelId != channel.Id)
                    {
                        await Message.SendEmbedAsync(context, "Join to voice!", "Please join to my voice channel.", Color.Blue);
                        return;
                    }

                    Playlist.IsPlaying = false;
                    if (Playlist.CurrentSong != null)
                        await Playlist.CurrentSong.CancelAsync();

                    Playlist.Songs?.Clear();

                    var audioClient = (context.Guild as IGuild)?.AudioClient;
                    if (audioClient != null)
                        await audioClient.StopAsync();

                    Playlist = null;
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

        public void SetContext(SocketCommandContext context)
        {
            _context = context;
        }

        private async Task TimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (Playlist != null && _context?.Guild.GetVoiceChannel(Playlist.ChannelId) is { } voiceChannel)
            {
                var usersInChannel = voiceChannel.Users;
                var nonBotUsers = usersInChannel.Where(user => !user.IsBot);

                if (_context != null)
                {
                    if (Playlist is { IsPlaying: false })
                    {
                        await Message.SendEmbedAsync(_context, "No songs have been playing!", "No songs have been playing for the past 2 minutes", Color.Red, "Bot disconnected from channel.");
                        Playlist.IsPlaying = false;
                        if (Playlist.CurrentSong != null)
                            await Playlist.CurrentSong.CancelAsync();

                        Playlist.Songs?.Clear();

                        var audioClient = (_context.Guild as IGuild)?.AudioClient;
                        if (audioClient != null)
                            await audioClient.StopAsync();

                        Playlist = null;
                    }

                    if (!nonBotUsers.Any())
                    {
                        await Message.SendEmbedAsync(_context, "No one is listening!", "No one is listening for the past 2 minutes", Color.Red, "Bot disconnected from channel.");
                        Playlist.IsPlaying = false;
                        if (Playlist.CurrentSong != null)
                            await Playlist.CurrentSong.CancelAsync();

                        Playlist.Songs?.Clear();

                        var audioClient = (_context.Guild as IGuild)?.AudioClient;
                        if (audioClient != null)
                            await audioClient.StopAsync();

                        Playlist = null;
                    }
                }
            }
        }
        #endregion
    }
}
