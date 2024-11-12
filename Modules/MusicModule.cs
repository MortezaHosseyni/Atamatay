using Atamatay.Services;
using Discord.Commands;

namespace Atamatay.Modules
{
    public class MusicModule(IMusicService music) : ModuleBase<SocketCommandContext>
    {
        private readonly IMusicService _music = music;

        [Command("play", RunMode = RunMode.Async)]
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
        public async Task NextAsync()
        {
            await _music.NextAsync(Context);
        }

        [Command("stop", RunMode = RunMode.Async)]
        public async Task StopAsync()
        {
            await _music.StopAsync(Context);
        }
    }
}
