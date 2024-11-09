using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using System.Diagnostics;
using Atamatay.Models;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Atamatay.Services
{
    public interface IMusicService
    {
        Task JoinAsync(SocketCommandContext context);
        Task LeaveAsync(SocketCommandContext context);
        Task PlayAsync(SocketCommandContext context);
        Task NextAsync(SocketCommandContext context);
        bool GetPlayerStatus();
        Task AddPlaylistAsync(SocketCommandContext context, string query);
        Task StopAsync(SocketCommandContext context);
    }

    public class MusicService : IMusicService
    {
        private IAudioClient? _audioClient = null;
        private CancellationTokenSource _currentSong = new CancellationTokenSource();
        
        private readonly ConcurrentQueue<SongModel> _playList = [];
        private bool IsPlaying = false;
        private bool _skipRequested;

        public async Task JoinAsync(SocketCommandContext context)
        {
            var voiceChannel = (context.User as IGuildUser)?.VoiceChannel;
            if (voiceChannel == null)
            {
                await context.Channel.SendMessageAsync("You need to be in a voice channel first.");
                return;
            }

            await voiceChannel.ConnectAsync(true, false, false, false);
        }

        public async Task LeaveAsync(SocketCommandContext context)
        {
            var voiceChannel = (context.User as IGuildUser)?.VoiceChannel;
            if (voiceChannel == null)
            {
                await context.Channel.SendMessageAsync("I'm not connected to any voice channel.");
                return;
            }

            await voiceChannel.DisconnectAsync();
            await context.Channel.SendMessageAsync($"Left {voiceChannel.Name}.");
        }

        public async Task PlayAsync(SocketCommandContext context)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null) { await context.Channel.SendMessageAsync("User must be in a voice channel."); return; }

                _audioClient ??= await channel.ConnectAsync(true, false, false, false);

                while (!_skipRequested && _playList.TryDequeue(out var song))
                {
                    if (song == null) continue;

                    using var ffmpeg = CreateStream($"songs/{song.Name}");
                    await using var output = ffmpeg.StandardOutput.BaseStream;
                    await context.Channel.SendMessageAsync($"\ud83d\udc3a Playing: {song.Title} | {song.Author} | {song.Duration}");

                    IsPlaying = true;
                    _currentSong = new CancellationTokenSource();

                    await using var discord = _audioClient.CreatePCMStream(AudioApplication.Music);
                    try
                    {
                        var buffer = new byte[81920];
                        int bytesRead;

                        while ((bytesRead = await output.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            if (_currentSong.Token.IsCancellationRequested)
                            {
                                break;
                            }
                            await discord.WriteAsync(buffer, 0, bytesRead);
                        }
                    }
                    finally
                    {
                        await discord.FlushAsync();
                        IsPlaying = false;
                    }

                    if (!_skipRequested) continue;
                    _skipRequested = false;
                    continue;
                }

                if (!_skipRequested && _playList.Count == 0)
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
            if (!IsPlaying)
            {
                await context.Channel.SendMessageAsync("No song is currently playing.");
                return;
            }

            _skipRequested = true;
            await _currentSong.CancelAsync();
            await context.Channel.SendMessageAsync("⏭️ Skipping to next song...");

            if (_playList.Count == 0)
            {
                await context.Channel.SendMessageAsync("Playlist is now empty.");
            }
        }

        public bool GetPlayerStatus()
        {
            return IsPlaying;
        }

        public async Task AddPlaylistAsync(SocketCommandContext context, string query)
        {
            try
            {
                var channel = (context.User as IGuildUser)?.VoiceChannel;
                if (channel == null) { await context.Channel.SendMessageAsync("User must be in a voice channel."); return; }

                var youtube = new YoutubeClient();

                // Get audio info
                var video = await GetYoutubeVideoInfo(youtube, query);

                var title = video.Title;
                var author = video.Author.ChannelTitle;
                var duration = video.Duration;

                // Download audio
                if (!Directory.Exists("songs"))
                    Directory.CreateDirectory("songs");

                var songName = $"{video.Id}.mp3";
                if (!File.Exists($"songs/{songName}"))
                {
                    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(query);
                    var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    await youtube.Videos.Streams.DownloadAsync(streamInfo, $"songs/{songName}");
                }

                _playList.Enqueue(new SongModel()
                {
                    Title = title,
                    Author = author,
                    Name = songName,
                    Duration = duration,
                    Id = video.Id,
                    Url = video.Url,
                    Platform = "Youtube", // TODO: Make this dynamic.
                    ChannelId = channel.Id,
                    CreatedAt = DateTime.Now
                });

                await context.Channel.SendMessageAsync($"\ud83d\udc3a Add to playlist:\n+ {title}\n+ {author}\n+ {duration}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task StopAsync(SocketCommandContext context)
        {
            _playList.Clear();
            IsPlaying = false;

            var audioClient = (context.Guild as IGuild)?.AudioClient;
            if (audioClient != null)
            {
                await audioClient.StopAsync();
            }

            await context.Channel.SendMessageAsync("Stopped playing and cleared the playlist.");
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

        private static async Task<Video> GetYoutubeVideoInfo(YoutubeClient youtube, string query)
        {
            return await youtube.Videos.GetAsync(query);
        }
        #endregion
    }
}
