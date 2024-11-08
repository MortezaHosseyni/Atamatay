using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Atamatay.Handlers
{
    public class CommandHandler(IServiceProvider services)
    {
        private readonly DiscordSocketClient _client = services.GetRequiredService<DiscordSocketClient>();
        private readonly CommandService _commands = services.GetRequiredService<CommandService>();

        public async Task InitializeAsync()
        {
            _client.MessageReceived += HandleCommandAsync;

            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), services);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            if (messageParam is not SocketUserMessage message || message.Author.IsBot)
                return;

            var argPos = 0;
            if (message.HasCharPrefix('$', ref argPos))
            {
                var context = new SocketCommandContext(_client, message);
                await _commands.ExecuteAsync(context, argPos, services);
            }
        }
    }
}
