using Atamatay.Models;
using Discord.Commands;
using YoutubeExplode.Search;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using Discord;
using Atamatay.Utilities;

namespace Atamatay.Services
{
    public interface IYoutubeService
    {
        Task<bool> Download(SocketCommandContext context, string? query, ulong channelId);
        Task<SongModel> Search(SocketCommandContext context, string searchParameter);
        Task<SongModel> Get(SocketCommandContext context, string query, ulong channelId);
    }

    public class YoutubeService : IYoutubeService
    {
        public async Task<bool> Download(SocketCommandContext context, string? query, ulong channelId)
        {
            var youtube = new YoutubeClient();

            var downloadSongMessage = await context.Channel.SendMessageAsync("\ud83c\udfb6 Preparing the song for playback...");

            var video = await youtube.Videos.GetAsync(query);

            var channelPath = $"songs/{channelId}";

            if (!Directory.Exists(channelPath))
                Directory.CreateDirectory(channelPath);

            var songName = $"{video.Id}.mp3";
            if (!File.Exists($"{channelPath}/{songName}"))
            {
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(query);
                var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                await youtube.Videos.Streams.DownloadAsync(streamInfo, $"{channelPath}/{songName}");
            }

            await downloadSongMessage.DeleteAsync();

            return true;
        }

        public async Task<SongModel> Search(SocketCommandContext context, string searchParameter)
        {
            var channel = (context.User as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Message.SendEmbedAsync(context, "Join to voice!", "You must be in a voice channel.", Color.Blue); return null!; }

            var searchMessage = await context.Channel.SendMessageAsync($"\ud83d\udd0d Searching for |{searchParameter}| ...");

            var youtube = new YoutubeClient();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));

            try
            {
                var results = youtube.Search.GetResultsAsync(searchParameter, cts.Token);

                await foreach (var result in results.WithCancellation(cts.Token))
                {
                    if (result is not VideoSearchResult video) continue;
                    await searchMessage.DeleteAsync();
                    return new SongModel
                    {
                        ChannelId = channel.Id,
                        Name = $"{video.Id}.mp3",
                        Platform = "Youtube",
                        Title = video.Title,
                        Author = video.Author.ChannelTitle,
                        Duration = video.Duration,
                        Url = video.Url,
                        Id = video.Id,
                        CreatedAt = DateTime.Now
                    };
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Search timed out.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            return null!;
        }

        public async Task<SongModel> Get(SocketCommandContext context, string query, ulong channelId)
        {
            var youtube = new YoutubeClient();
            var video = await youtube.Videos.GetAsync(query);

            return new SongModel
            {
                ChannelId = channelId,
                Name = $"{video.Id}.mp3",
                Platform = "Youtube",
                Title = video.Title,
                Author = video.Author.ChannelTitle,
                Duration = video.Duration,
                Url = video.Url,
                Id = video.Id,
                CreatedAt = DateTime.Now
            };
        }
    }
}
