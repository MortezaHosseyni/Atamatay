using Atamatay.Utilities;
using Discord;
using Discord.Commands;

namespace Atamatay.Services
{
    public interface IDdService
    {
        Task StartGame(SocketCommandContext context, string worldName, string playerDetail);
    }

    public class DdService(IGptService gpt) : IDdService
    {
        private readonly IGptService _gpt = gpt;

        public async Task StartGame(SocketCommandContext context, string worldName, string playerDetail)
        {
            var message = await context.Channel.SendMessageAsync($"Creating the '{worldName}'...");

            var story = await _gpt.Post(worldName, playerDetail);

            await message.DeleteAsync();

            await Message.SendEmbedAsync(context, worldName, story, Color.DarkGreen, "Summary of the story");
        }
    }
}
