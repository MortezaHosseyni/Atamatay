using Atamatay.Services;
using Atamatay.Utilities;
using Discord;
using Discord.Commands;

namespace Atamatay.Modules
{
    public class MusicModule(IMusicService music) : ModuleBase<SocketCommandContext>
    {
        private readonly IMusicService _music = music;

        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("Plays music from a specified query or URL.")]
        public async Task PlayAsync([Remainder] string query)
        {
            await _music.AddPlaylistAsync(Context, query);

            var isPlaying = await _music.GetPlayerStatus(Context);

            if (!isPlaying)
            {
                await _music.PlayAsync(Context);
            }
        }

        [Command("next", RunMode = RunMode.Async)]
        [Alias("skip")]
        [Summary("Skips to the next track.")]
        public async Task NextAsync()
        {
            await _music.NextAsync(Context);
        }

        [Command("stop", RunMode = RunMode.Async)]
        [Alias("leave")]
        [Summary("Stops the bot and clear playlist.")]
        public async Task StopAsync()
        {
            await _music.StopAsync(Context);
        }

        [Command("help")]
        [Summary("Shows a list of available commands.")]
        public async Task HelpAsync()
        {
            const string helpMessage = "\ud83d\udc3a Here are the commands you can use:\n" +
                                       "`$play <query> ($p)` - Plays music from a specified query.\n" +
                                       "`$stop ($leave)` - Stops the current track.\n" +
                                       "`$next ($skip)` - Skips to the next track.\n";

            await Message.SendEmbedAsync(Context, "Music commands!", helpMessage, Color.DarkBlue);
        }
    }
}
