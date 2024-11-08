using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace Atamatay.Modules
{
    public class MusicModule() : ModuleBase<SocketCommandContext>
    {
        [Command("join", RunMode = RunMode.Async)]
        public async Task JoinAsync()
        {
            var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
            if (voiceChannel == null)
            {
                await ReplyAsync("You need to be in a voice channel first.");
                return;
            }

            await voiceChannel.ConnectAsync(true);
        }

        [Command("leave", RunMode = RunMode.Async)]
        public async Task LeaveAsync()
        {
            var voiceChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
            if (voiceChannel == null)
            {
                await ReplyAsync("I'm not connected to any voice channel.");
                return;
            }

            await voiceChannel.DisconnectAsync();
            await ReplyAsync($"Left {voiceChannel.Name}.");
        }

        [Command("play", RunMode = RunMode.Async)]
        public async Task PlayAsync([Remainder] string query)
        {
            try
            {
                var youtube = new YoutubeClient();

                // Get audio info
                var video = await youtube.Videos.GetAsync(query);

                var title = video.Title;
                var author = video.Author.ChannelTitle;
                var duration = video.Duration;

                await ReplyAsync($"{title} | {author} | {duration}");

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

                // Play audio
                var channel = (Context.User as IGuildUser)?.VoiceChannel;
                if (channel == null) { await Context.Channel.SendMessageAsync("User must be in a voice channel, or a voice channel must be passed as an argument."); return; }


                using var ffmpeg = CreateStream($"songs/{songName}");
                await using var output = ffmpeg.StandardOutput.BaseStream;

                var audioClient = await channel.ConnectAsync(true);

                await using var discord = audioClient.CreatePCMStream(AudioApplication.Mixed);
                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

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
    }
}
