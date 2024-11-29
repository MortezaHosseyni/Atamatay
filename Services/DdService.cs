using Atamatay.Models.DnD;
using Atamatay.Utilities;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Atamatay.Services
{
    public interface IDdService
    {
        Task StartGame(SocketCommandContext context, string worldName, string playerDetail, List<SocketUser> players);
    }

    public class DdService(IGptService gpt) : IDdService
    {
        private readonly IGptService _gpt = gpt;

        private Dictionary<ulong, List<DdDialog>> _sessionsDialogs = new Dictionary<ulong, List<DdDialog>>();

        public async Task StartGame(SocketCommandContext context, string worldName, string playerDetail, List<SocketUser> players)
        {
            try
            {
                var message = await context.Channel.SendMessageAsync($"Creating the '{worldName}'...");

                var story = await _gpt.Post(worldName, playerDetail);

                await message.DeleteAsync();

                var db = new DdDatabase($"dnd/{context.Channel.Id}.json");

                var sessionPlayers = players.Select(player => new DdPlayer { PlayerId = player.Id, Username = player.Username, IsAccepted = false, CreatedAt = DateTime.Now }).ToList();

                db.AddSession(new DdSession
                {
                    WorldName = worldName,
                    ChannelId = context.Channel.Id,
                    Players = sessionPlayers,
                    Round = 1,
                    CreatedAt = DateTime.Now
                });

                await Message.SendEmbedAsync(context, worldName, story, Color.DarkGreen, "And so, the world took shape from the void...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task SendDialog()
        {
            // TODO: Send dialog logic.
        }

        public async Task AcceptSession()
        {
            // TODO: Accept session by player.
        }
    }
}
