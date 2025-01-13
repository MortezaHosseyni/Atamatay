using Atamatay.Utilities;
using Discord;
using Discord.Commands;

namespace Atamatay.Modules
{
    public class GeneralModule : ModuleBase<SocketCommandContext>
    {
        [Command("help")]
        [Summary("Shows a list of available commands.")]
        public async Task HelpAsync()
        {
            const string musicHelp = "\ud83d\udc3a Here are the Music commands you can use:\n" +
                                     "`$play <query> ($p)` - Plays music from a specified query.\n" +
                                     "`$stop ($leave)` - Stops the current track.\n" +
                                     "`$next ($skip)` - Skips to the next track.\n";
            await Message.SendEmbedAsync(Context, "Music Commands!", musicHelp, Color.DarkBlue);

            const string dndHelp = "\ud83c\udfa2 Here are the DnD commands you can use:\n" +
                                   "`$dd-start <args> ($dd-create)` - Starts a new D&D game with the specified arguments (example argument: $dd-start FantasticWorld @Player1 Orc Male @Player2 Wizard Male).\n" +
                                   "`$dd-dialog <dialog> ($dd)` - Saves and sends a player's dialog during the session.\n" +
                                   "`$dd-accept ($dd-play)` - Accepts and starts the game session.\n";
            await Message.SendEmbedAsync(Context, "DnD Commands!", dndHelp, Color.DarkGreen);


            const string managerHelp = "\ud83d\udd27 Here are the Manager commands you can use:\n" +
                                       "`$del-messages <count>` - Deletes the specified number of messages from the current text channel.\n";
            await Message.SendEmbedAsync(Context, "Manager Commands!", managerHelp, Color.Gold);
        }
    }
}
