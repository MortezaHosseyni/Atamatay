using Atamatay.Services;
using Discord.Commands;

namespace Atamatay.Modules
{
    public class ManagerModule(IManagerService manager) : ModuleBase<SocketCommandContext>
    {
        private readonly IManagerService _manager = manager;

        [Command("del-messages")]
        [Summary("Deletes a specified number of messages from the current text channel.")]
        public async Task DeleteMessagesAsync(int count)
        {
            await _manager.DeleteMessagesAsync(Context, count);
        }
    }
}
