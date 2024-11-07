using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace Atamatay
{
    public class Program
    {
        private DiscordSocketClient _client = null!;

        private static async Task Main(string[] args) => await new Program().RunBotAsync();

        public async Task RunBotAsync()
        {
            _client = new DiscordSocketClient();
            _client.Log += LogAsync;

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var token = config["BotToken"];
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private static Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }
    }
}