using Discord.Commands;

namespace Atamatay.Modules
{
    public class DdModule : ModuleBase<SocketCommandContext>
    {
        [Command("dd$start", RunMode = RunMode.Async)]
        [Summary("Start a new D&D game.")]
        public async Task StartGameAsync([Remainder] string worldName)
        {
            if (string.IsNullOrEmpty(worldName))
            {
                await ReplyAsync("Please choose a name for your world!");
                return;
            }
            await ReplyAsync($"Welcome to {worldName} ...\n\nDeveloping...");
        }
    }
}
