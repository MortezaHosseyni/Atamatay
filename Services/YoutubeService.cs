using Atamatay.Models;
using Discord.Commands;
using Discord;
using Atamatay.Utilities;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System.Collections.Concurrent;

namespace Atamatay.Services
{
    public interface IYoutubeService
    {
        Task<bool> Download(SocketCommandContext context, string? query, ulong channelId);
        Task<SongModel> Search(SocketCommandContext context, string searchParameter);
        Task<SongModel> Get(SocketCommandContext context, string query, ulong channelId);
    }

    public class YoutubeService(string apiKey) : IYoutubeService
    {
        private readonly string _apiKey = apiKey;
        private readonly YouTubeService _youtubeApiService = new(new BaseClientService.Initializer()
        {
            ApiKey = apiKey,
            ApplicationName = "Atamatay"
        });
        private readonly HttpClient _httpClient = new();
        private readonly ConcurrentDictionary<string, SongModel> _songCache = new();
        private readonly SemaphoreSlim _downloadSemaphore = new(3, 3);

        public async Task<bool> Download(SocketCommandContext context, string? query, ulong channelId)
        {
            var downloadSongMessage = await context.Channel.SendMessageAsync("\ud83c\udfb6 Preparing the song for playback...");

            try
            {
                var videoId = ExtractVideoId(query);
                if (string.IsNullOrEmpty(videoId))
                {
                    var searchResult = await Search(context, query);
                    if (searchResult == null)
                    {
                        await downloadSongMessage.DeleteAsync();
                        await context.Channel.SendMessageAsync("❌ Could not find any matching videos.");
                        return false;
                    }
                    videoId = searchResult.Id;
                }

                var channelPath = $"songs/{channelId}";
                Directory.CreateDirectory(channelPath);

                var songName = $"{videoId}.mp3";
                var filePath = $"{channelPath}/{songName}";

                if (!File.Exists(filePath))
                {
                    await _downloadSemaphore.WaitAsync();

                    try
                    {
                        if (!File.Exists(filePath))
                        {
                            if (!_songCache.ContainsKey(videoId))
                            {
                                var videoRequest = _youtubeApiService.Videos.List("snippet,contentDetails");
                                videoRequest.Id = videoId;
                                videoRequest.Fields = "items(id,snippet/title,snippet/channelTitle,contentDetails/duration)";
                                var videoResponse = await videoRequest.ExecuteAsync();

                                if (videoResponse.Items.Count == 0)
                                {
                                    throw new Exception($"Video '{videoId}' not found or is unavailable.");
                                }
                            }

                            var process = new System.Diagnostics.Process
                            {
                                StartInfo = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "yt-dlp",
                                    Arguments = $"-x --audio-format mp3 --audio-quality 5 --no-playlist --no-warnings --no-check-certificate --prefer-ffmpeg --concurrent-fragments 5 -o \"{filePath}\" https://www.youtube.com/watch?v={videoId}",
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                }
                            };

                            process.Start();
                            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
                            var processTask = process.WaitForExitAsync();

                            await Task.WhenAny(processTask, timeoutTask);

                            if (!processTask.IsCompleted)
                            {
                                try { process.Kill(); } catch { }
                                throw new Exception("Download timed out. The video might be too long or there might be connection issues.");
                            }

                            if (!File.Exists(filePath))
                            {
                                throw new Exception("Failed to download the audio. The video might be restricted.");
                            }
                        }
                    }
                    finally
                    {
                        _downloadSemaphore.Release();
                    }
                }

                await downloadSongMessage.DeleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Download error: {ex}");
                await downloadSongMessage.DeleteAsync();
                await context.Channel.SendMessageAsync($"⚠️ Error: {ex.Message}");
                return false;
            }
        }

        public async Task<SongModel> Search(SocketCommandContext context, string searchParameter)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null)
            {
                await Message.SendEmbedAsync(context, "Join to voice!", "You must be in a voice channel.", Color.Blue);
                return null!;
            }

            var searchMessage = await context.Channel.SendMessageAsync($"\ud83d\udd0d Searching for |{searchParameter}| ...");

            try
            {
                var searchListRequest = _youtubeApiService.Search.List("snippet");
                searchListRequest.Q = searchParameter;
                searchListRequest.MaxResults = 1;
                searchListRequest.Type = "video";
                searchListRequest.VideoCategoryId = "10";
                searchListRequest.Fields = "items(id/videoId,snippet/title,snippet/channelTitle)";

                var searchResponse = await searchListRequest.ExecuteAsync();

                if (searchResponse.Items.Count > 0)
                {
                    var item = searchResponse.Items[0];
                    var videoId = item.Id.VideoId;

                    if (_songCache.TryGetValue(videoId, out var cachedSong))
                    {
                        cachedSong.ChannelId = channel.Id;
                        await searchMessage.DeleteAsync();
                        return cachedSong;
                    }

                    var videoRequest = _youtubeApiService.Videos.List("contentDetails");
                    videoRequest.Id = videoId;
                    videoRequest.Fields = "items(contentDetails/duration)";
                    var videoResponse = await videoRequest.ExecuteAsync();

                    if (videoResponse.Items.Count > 0)
                    {
                        var videoItem = videoResponse.Items[0];
                        var duration = ParseDuration(videoItem.ContentDetails.Duration);

                        var songModel = new SongModel
                        {
                            ChannelId = channel.Id,
                            Name = $"{videoId}.mp3",
                            Platform = "Youtube",
                            Title = item.Snippet.Title,
                            Author = item.Snippet.ChannelTitle,
                            Duration = duration,
                            Url = $"https://www.youtube.com/watch?v={videoId}",
                            Id = videoId,
                            CreatedAt = DateTime.Now
                        };

                        _songCache.TryAdd(videoId, songModel);

                        await searchMessage.DeleteAsync();
                        return songModel;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search error: {ex}");
            }

            await searchMessage.DeleteAsync();
            await context.Channel.SendMessageAsync("❌ Could not find any matching videos.");
            return null!;
        }

        public async Task<SongModel> Get(SocketCommandContext context, string query, ulong channelId)
        {
            try
            {
                var videoId = ExtractVideoId(query);

                if (string.IsNullOrEmpty(videoId)) return await Search(context, query);
                if (_songCache.TryGetValue(videoId, out var cachedSong))
                {
                    cachedSong.ChannelId = channelId;
                    return cachedSong;
                }

                var videoRequest = _youtubeApiService.Videos.List("snippet,contentDetails");
                videoRequest.Id = videoId;
                videoRequest.Fields = "items(id,snippet/title,snippet/channelTitle,contentDetails/duration)";
                var videoResponse = await videoRequest.ExecuteAsync();

                if (videoResponse.Items.Count <= 0) return await Search(context, query);
                var videoItem = videoResponse.Items[0];
                var duration = ParseDuration(videoItem.ContentDetails.Duration);

                var songModel = new SongModel
                {
                    ChannelId = channelId,
                    Name = $"{videoId}.mp3",
                    Platform = "Youtube",
                    Title = videoItem.Snippet.Title,
                    Author = videoItem.Snippet.ChannelTitle,
                    Duration = duration,
                    Url = $"https://www.youtube.com/watch?v={videoId}",
                    Id = videoId,
                    CreatedAt = DateTime.Now
                };

                _songCache.TryAdd(videoId, songModel);

                return songModel;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get error: {ex}");
                await context.Channel.SendMessageAsync($"⚠️ Error: {ex.Message}");
                return null!;
            }
        }

        private static string ExtractVideoId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (input.Contains("youtu.be/"))
            {
                var startIndex = input.IndexOf("youtu.be/", StringComparison.Ordinal) + 9;
                var endIndex = input.IndexOf('?', startIndex);
                return endIndex > startIndex ? input.Substring(startIndex, endIndex - startIndex) : input[startIndex..];
            }

            if (input.Contains("youtube.com/watch") && input.Contains("v="))
            {
                var startIndex = input.IndexOf("v=", StringComparison.Ordinal) + 2;
                var endIndex = input.IndexOf('&', startIndex);
                return endIndex > startIndex ? input.Substring(startIndex, endIndex - startIndex) : input[startIndex..];
            }

            if (!input.Contains(" ") && input.Length == 11)
                return input;

            return null;
        }

        private static TimeSpan? ParseDuration(string isoDuration)
        {
            try
            {
                if (string.IsNullOrEmpty(isoDuration) || isoDuration == "P0D")
                    return TimeSpan.Zero;

                isoDuration = isoDuration.Replace("PT", "");

                var hours = 0;
                var minutes = 0;
                var seconds = 0;

                var hIndex = isoDuration.IndexOf('H');
                if (hIndex > 0)
                {
                    hours = int.Parse(isoDuration[..hIndex]);
                    isoDuration = isoDuration[(hIndex + 1)..];
                }

                var mIndex = isoDuration.IndexOf('M');
                if (mIndex > 0)
                {
                    minutes = int.Parse(isoDuration[..mIndex]);
                    isoDuration = isoDuration[(mIndex + 1)..];
                }

                var sIndex = isoDuration.IndexOf('S');
                if (sIndex > 0)
                {
                    seconds = int.Parse(isoDuration[..sIndex]);
                }

                return new TimeSpan(hours, minutes, seconds);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }
    }
}
