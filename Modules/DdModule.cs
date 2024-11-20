using Discord.Commands;

namespace Atamatay.Modules
{
    public class DdModule : ModuleBase<SocketCommandContext>
    {
        [Command("dd-start", RunMode = RunMode.Async)]
        [Summary("Start a new D&D game.")]
        public async Task StartGameAsync([Remainder] string worldName)
        {
            if (string.IsNullOrEmpty(worldName))
            {
                await Context.Channel.SendMessageAsync("Please choose a name for your world!");
                return;
            }
            await Context.Channel.SendMessageAsync($"Welcome to {worldName} ...\n\nDeveloping...");
        }
    }
}
