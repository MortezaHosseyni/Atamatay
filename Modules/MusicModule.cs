using Atamatay.Services;
using Discord.Commands;

namespace Atamatay.Modules
{
    public class MusicModule(IMusicService music) : ModuleBase<SocketCommandContext>
    {
        private readonly IMusicService _music = music;

        [Command("join", RunMode = RunMode.Async)]
        public async Task JoinAsync()
        {
            await _music.JoinAsync(Context);
        }

        [Command("play", RunMode = RunMode.Async)]
        public async Task PlayAsync([Remainder] string query)
        {
            var isPlaying = _music.GetPlayerStatus();

            await _music.AddPlaylistAsync(Context, query);

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
