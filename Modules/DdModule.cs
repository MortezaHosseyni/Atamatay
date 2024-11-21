using Atamatay.Services;
using Atamatay.Utilities;
using Discord;
using Discord.Commands;

namespace Atamatay.Modules
{
    public class DdModule(IDdService dd) : ModuleBase<SocketCommandContext>
    {
        private readonly IDdService _dd = dd;

        [Command("dd-start")]
        [Alias("dd")]
        [Summary("Start a new D&D game.")]
        public async Task StartGameAsync([Remainder] string args)
        {
            var arguments = args.Split(' ');

            var worldName = arguments[0];

            if (string.IsNullOrWhiteSpace(worldName))
            {
                await Message.SendEmbedAsync(Context, "World name!", "Please choose a name for your world.", Color.Red);
                return;
            }

            var mentionedUsers = Context.Message.MentionedUsers.ToList();

            if (mentionedUsers.Count == 0 || arguments.Length < mentionedUsers.Count * 3)
            {
                await ReplyAsync("Invalid command format! Example usage:\n" +
                                 "`$dd-start FantasticWorld @Player1 Orc Male @Player2 Wizard Male`");
                return;
            }

            var playerDetails = new List<string>();
            var userIndex = 1;

            foreach (var user in mentionedUsers)
            {
                if (userIndex + 2 >= arguments.Length)
                {
                    await ReplyAsync("Invalid format! Ensure each user has a race and gender specified.");
                    return;
                }

                var race = arguments[userIndex + 1];
                var gender = arguments[userIndex + 2];

                playerDetails.Add($"**{user.Username}** ({user.Mention}): Race = {race}, Gender = {gender}");
                userIndex += 3;
            }

            var pd = string.Join("\n", playerDetails);

            await _dd.StartGame(Context, worldName, pd);
        }
    }
}
