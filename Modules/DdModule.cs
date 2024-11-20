using Atamatay.Utilities;
using Discord;
using Discord.Commands;

namespace Atamatay.Modules
{
    public class DdModule : ModuleBase<SocketCommandContext>
    {
        [Command("dd-start")]
        [Alias("dd")]
        [Summary("Start a new D&D game.")]
        public async Task StartGameAsync([Remainder] string worldName)
        {
            if (string.IsNullOrEmpty(worldName))
            {
                await Message.SendEmbedAsync(Context, "World name!", "Please choose a name for your world.", Color.Red);
                return;
            }
            await Message.SendEmbedAsync(Context, worldName, $"Welcome to {worldName}", Color.DarkGreen, "Developing...");
        }
    }
}
