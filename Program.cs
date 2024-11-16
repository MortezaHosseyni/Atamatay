using Atamatay.Handlers;
using Atamatay.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atamatay
{
    public class Program
    {
        private DiscordSocketClient _client = null!;
        private CommandService _commands = null!;
        private IServiceProvider _services = null!;
        private static async Task Main() => await new Program().RunBotAsync();

        public async Task RunBotAsync()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.GuildVoiceStates
            });

            _commands = new CommandService();
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton<CommandHandler>()
                .AddScoped<IMusicService, MusicService>()
                .AddScoped<IYoutubeService, YoutubeService>()
                .BuildServiceProvider();

            _client.Log += LogAsync;

            await _client.SetActivityAsync(new Game("$help commands", ActivityType.Listening));

            #region Config
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var token = config["BotToken"];
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            #endregion

            var commandHandler = _services.GetRequiredService<CommandHandler>();
            await commandHandler.InitializeAsync();

            await Task.Delay(-1);
        }

        private static Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }
    }
}