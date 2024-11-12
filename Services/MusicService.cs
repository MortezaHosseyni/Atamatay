using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.Commands;
using System.Diagnostics;
using Atamatay.Models;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using YoutubeExplode.Common;
using SpotifyAPI.Web;

namespace Atamatay.Services
{
    public interface IMusicService
    {
        Task PlayAsync(SocketCommandContext context);
        Task NextAsync(SocketCommandContext context);
        Task<bool> GetPlayerStatus(SocketCommandContext context);
        Task AddPlaylistAsync(SocketCommandContext context, string query);
        Task StopAsync(SocketCommandContext context);
    }

    public class MusicService : IMusicService
    {
        public List<PlaylistModel> Playlists { get; set; } = [];

        public async Task PlayAsync(SocketCommandContext context)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null) { await context.Channel.SendMessageAsync("\ud83d\udce3 User must be in a voice channel."); return; }

                var playList = Playlists.FirstOrDefault(p => p.ChannelId == channel.Id);
                if (playList == null)
                {
                    await context.Channel.SendMessageAsync("\u2666\ufe0f No playlist found for this channel.");
                    return;
                }

                if (playList?.Songs == null || playList.Songs.IsEmpty)
                {
                    await context.Channel.SendMessageAsync($"\ud83d\udd07 |{channel.Name}| channel playlist is empty.");
                    return;
                }

                if (playList.AudioClient == null || playList.AudioClient.ConnectionState == ConnectionState.Disconnected)
                    playList.AudioClient = await channel.ConnectAsync();
                else
                {
                    await context.Channel.SendMessageAsync("\ud83d\udc3a Please join to my voice channel.");
                    return;
                }

                while (!playList.SkipRequested && playList.Songs.TryDequeue(out var song))
                {
                    if (song == null) continue;

                    using var ffmpeg = CreateStream($"songs/{song.Name}");
                    await using var output = ffmpeg.StandardOutput.BaseStream;
                    await context.Channel.SendMessageAsync($"\ud83d\udc3a Playing: {song.Title} | {song.Author} | {song.Duration}");

                    playList.IsPlaying = true;
                    playList.CurrentSong = new CancellationTokenSource();

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
                        DeleteSong(song.Name);
                        playList.IsPlaying = false;
                    }

                    if (!playList.SkipRequested) continue;
                    playList.SkipRequested = false;
                    continue;
                }

                if (!playList.SkipRequested && playList.Songs.Count == 0)
                {
                    await context.Channel.SendMessageAsync($"\ud83d\udc3a Playlist is empty!");

                    foreach (var song in playList.Songs)
                    {
                        var songPath = $"songs/{song.Name}";
                        if (File.Exists(songPath))
                            File.Delete(songPath);
                    }
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
            if (channel == null) { await context.Channel.SendMessageAsync("\ud83d\udce3 User must be in a voice channel."); return; }

            var playList = Playlists?.Where(p => p.ChannelId == channel.Id).FirstOrDefault();
            if (playList?.Songs == null || playList.Songs.IsEmpty || playList.AudioClient == null)
            {
                await context.Channel.SendMessageAsync($"\ud83d\udd07 |{channel.Name}| channel playlist is empty.");
                return;
            }

            if (!playList.IsPlaying)
            {
                await context.Channel.SendMessageAsync("\u2666\ufe0f No song is currently playing.");
                return;
            }

            playList.SkipRequested = true;
            await playList.CurrentSong.CancelAsync();
            await context.Channel.SendMessageAsync("⏭️ Skipping to next song...");

            if (playList.Songs.Count == 0)
            {
                await context.Channel.SendMessageAsync("Playlist is now empty.");
            }
        }

        public async Task<bool> GetPlayerStatus(SocketCommandContext context)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { await context.Channel.SendMessageAsync("\ud83d\udce3 User must be in a voice channel."); return false; }

            var playList = Playlists?.Where(p => p.ChannelId == channel.Id).FirstOrDefault();
            return playList is { Songs: { IsEmpty: false }, AudioClient: not null, IsPlaying: true };
        }

        public async Task AddPlaylistAsync(SocketCommandContext context, string query)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null) { await context.Channel.SendMessageAsync("\ud83d\udce3 User must be in a voice channel."); return; }

                var playList = Playlists.FirstOrDefault(p => p.ChannelId == channel.Id);

                if (playList == null)
                {
                    playList = new PlaylistModel
                    {
                        ChannelId = channel.Id,
                        Songs = new ConcurrentQueue<SongModel>(),
                        CreatedAt = DateTime.Now
                    };
                    Playlists.Add(playList);
                }

                if (IsValidUrl(query) && query.Contains("youtube.com"))
                {
                    var song = await DownloadFromYoutube(query, channel.Id);

                    playList.Songs.Enqueue(song);
                    await context.Channel.SendMessageAsync($"\ud83d\udc3a Add to playlist:\n\ud83c\udfb5 {song.Title}\n\ud83d\udc68\u200d\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}");
                }
                else if (IsValidUrl(query) && query.Contains("spotify.com"))
                {
                    var config = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .Build();

                    var clientId = config["Spotify:ClientId"]!;
                    var clientSecret = config["Spotify:ClientSecret"]!;

                    var spotifyConfig = SpotifyClientConfig.CreateDefault();

                    var request = new ClientCredentialsRequest(clientId, clientSecret);
                    var response = await new OAuthClient(spotifyConfig).RequestToken(request);

                    var spotify = new SpotifyClient(spotifyConfig.WithToken(response.AccessToken));

                    var match = Regex.Match(query, @"open\.spotify\.com/track/(?<trackId>[a-zA-Z0-9]+)");
                    var trackId = match.Success ? match.Groups["trackId"].Value : null;

                    if (string.IsNullOrEmpty(trackId))
                    {
                        await context.Channel.SendMessageAsync("\u2639\ufe0f I can't find song!.");
                        return;
                    }

                    var track = await spotify.Tracks.Get(trackId);

                    var searchSong = await SearchSong($"{track.Name} by {track.Artists[0].Name}");
                    if (!string.IsNullOrEmpty(searchSong) && IsValidUrl(searchSong))
                    {
                        var song = await DownloadFromYoutube(searchSong, channel.Id);

                        playList.Songs.Enqueue(song);
                        await context.Channel.SendMessageAsync($"\ud83d\udc3a Add to playlist:\n\ud83c\udfb5 {song.Title}\n\ud83d\udc68\u200d\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}");
                    }
                    else
                    {
                        await context.Channel.SendMessageAsync("\u2639\ufe0f I can't find song!.");
                    }
                }
                else if (IsValidUrl(query) && query.Contains("soundcloud.com"))
                {
                    var artistName = query.Split('/')[1];
                    var songName = query.Split('/')[2];

                    var searchSong = await SearchSong($"{artistName} by {songName}");
                    if (!string.IsNullOrEmpty(searchSong) && IsValidUrl(searchSong))
                    {
                        var song = await DownloadFromYoutube(searchSong, channel.Id);

                        playList.Songs.Enqueue(song);
                        await context.Channel.SendMessageAsync($"\ud83d\udc3a Add to playlist:\n\ud83c\udfb5 {song.Title}\n\ud83d\udc68\u200d\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}");
                    }
                    else
                    {
                        await context.Channel.SendMessageAsync("\u2639\ufe0f I can't find song!.");
                    }
                }
                else
                {
                    var searchSong = await SearchSong(query);
                    if (!string.IsNullOrEmpty(searchSong) && IsValidUrl(searchSong))
                    {
                        var song = await DownloadFromYoutube(searchSong, channel.Id);

                        playList.Songs.Enqueue(song);
                        await context.Channel.SendMessageAsync($"\ud83d\udc3a Add to playlist:\n\ud83c\udfb5 {song.Title}\n\ud83d\udc68\u200d\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}");
                    }
                    else
                    {
                        await context.Channel.SendMessageAsync("\u2639\ufe0f I can't find song!.");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task StopAsync(SocketCommandContext context)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { await context.Channel.SendMessageAsync("\ud83d\udce3 User must be in a voice channel."); return; }

            var playlist = Playlists?.Where(p => p.ChannelId == channel.Id).FirstOrDefault();
            if (playlist != null)
                Playlists?.Remove(playlist);

            var audioClient = (context.Guild as IGuild)?.AudioClient;
            if (audioClient != null)
            {
                await audioClient.StopAsync();
            }

            if (Directory.Exists("songs"))
                Directory.Delete("songs");
            await context.Channel.SendMessageAsync("\u2666\ufe0f Stopped playing and cleared the playlist.");
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

        private static void DeleteSong(string songName)
        {
            var path = $"songs/{songName}";
            if (File.Exists(path))
                File.Delete(path);
        }

        public static bool IsValidUrl(string url)
        {
            const string pattern = @"^(https?|ftp):\/\/[^\s/$.?#].[^\s]*$";
            return Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase);
        }
        #endregion

        #region Downloads
        private static async Task<SongModel> DownloadFromYoutube(string query, ulong channelId)
        {
            var youtube = new YoutubeClient();

            var video = await youtube.Videos.GetAsync(query);

            if (!Directory.Exists("songs"))
                Directory.CreateDirectory("songs");

            var songName = $"{video.Id}.mp3";
            if (!File.Exists($"songs/{songName}"))
            {
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(query);
                var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                await youtube.Videos.Streams.DownloadAsync(streamInfo, $"songs/{songName}");
            }

            var title = video.Title;
            var author = video.Author.ChannelTitle;
            var duration = video.Duration;

            return new SongModel()
            {
                Title = title,
                Author = author,
                Name = songName,
                Duration = duration,
                Id = video.Id,
                Url = video.Url,
                Platform = "Youtube",
                ChannelId = channelId,
                CreatedAt = DateTime.Now
            };
        }

        private static async Task<string?> SearchSong(string searchParameter)
        {
            var youtube = new YoutubeClient();
            var results = await youtube.Search.GetResultsAsync(searchParameter);

            return !results.Any() ? null : results.First().Url;
        }
        #endregion
    }
}
