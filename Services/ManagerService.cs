using Atamatay.Utilities;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Atamatay.Services
{
    public interface IManagerService
    {
        Task DeleteMessagesAsync(SocketCommandContext context, int messageCount);
    }

    public class ManagerService : IManagerService
    {
        public async Task DeleteMessagesAsync(SocketCommandContext context, int messageCount)
        {
            if (context.User is not SocketGuildUser user) return;
            if (context.Channel is not SocketTextChannel channel) return;

            if (!user.GuildPermissions.ManageMessages && !user.GetPermissions(channel).ManageMessages)
            {
                await Message.SendEmbedAsync(context, "Access Denied!", "You do not have permission to manage messages in this channel.", Color.Red);
                return;
            }

            if (messageCount is < 1 or > 100)
                return;

            var messages = await channel.GetMessagesAsync(messageCount + 1).FlattenAsync();

            var listedMessages = messages.ToList();

            await channel.DeleteMessagesAsync(listedMessages);
            await Message.SendEmbedAsync(context, "Delete Messages!", $"{listedMessages.Count} messages have been deleted.", Color.Teal);
        }
    }
}
