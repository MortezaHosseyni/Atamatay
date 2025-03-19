using Atamatay.Handlers;
using Atamatay.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Timer = System.Timers.Timer;

namespace Atamatay
{
    public class Program
    {
        private DiscordSocketClient _client = null!;
        private CommandService _commands = null!;
        private IServiceProvider _services = null!;
        private IMusicService _music = null!;
        private static async Task Main() => await new Program().RunBotAsync();

        public async Task RunBotAsync()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.GuildVoiceStates
            });

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            _commands = new CommandService();
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton<CommandHandler>()
                .AddScoped<IMusicService, MusicService>()
                .AddSingleton<IYoutubeService>(new YoutubeService(config["YoutubeApiKey"]!))
                .AddScoped<IManagerService, ManagerService>()
                .AddScoped<Timer>()
                .AddScoped<HttpClient>()
                .AddScoped<IGptService, GptService>()
                .AddScoped<IDdService, DdService>()
                .BuildServiceProvider();

            _music = _services.GetRequiredService<IMusicService>();

            _client.Log += LogAsync;
            _client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;

            await _client.SetActivityAsync(new Game("$help commands", ActivityType.Listening));

            var token = config["BotToken"];
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            var commandHandler = _services.GetRequiredService<CommandHandler>();
            await commandHandler.InitializeAsync();

            await Task.Delay(-1);
        }

        private async Task OnUserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState beforeState, SocketVoiceState afterState)
        {
            if (user.Id == _client.CurrentUser.Id)
            {
                if (beforeState.VoiceChannel != null && afterState.VoiceChannel == null)
                {
                    await _music.HandleForcedDisconnect(beforeState.VoiceChannel);
                }
            }
        }
        private static Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }
    }
}