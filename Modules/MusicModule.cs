using Discord.Commands;
using Discord.WebSocket;

namespace Atamatay.Modules
{
    public class MusicModule() : ModuleBase<SocketCommandContext>
    {
        [Command("join")]
        public async Task JoinAsync()
        {
            var voiceChannel = (Context.User as SocketGuildUser)?.VoiceChannel;

            if (voiceChannel == null)
            {
                await ReplyAsync("You need to be in a voice channel first.");
                return;
            }

            var audioClient = await voiceChannel.ConnectAsync();

            await ReplyAsync($"Joined {voiceChannel.Name}.");
        }

        [Command("leave")]
        public async Task LeaveAsync()
        {
            var voiceChannel = (Context.User as SocketGuildUser)?.VoiceChannel;
            if (voiceChannel == null)
            {
                await ReplyAsync("I'm not connected to any voice channel.");
                return;
            }

            await voiceChannel.DisconnectAsync();
            await ReplyAsync($"Left {voiceChannel.Name}.");
        }

        [Command("play")]
        public async Task PlayAsync([Remainder] string query)
        {
            // TODO: Music player logic.
        }
    }
}
