using System.Diagnostics;
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
                .AddScoped<Timer>()
                .AddScoped<HttpClient>()
                .AddScoped<IGptService, GptService>()
                .AddScoped<IDdService, DdService>()
                .BuildServiceProvider();

            _client.Log += LogAsync;
            _client.UserVoiceStateUpdated += HandleVoiceStateUpdated;

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

        private async Task HandleVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            try
            {
                if (user.Id == _client.CurrentUser.Id)
                {
                    if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
                    {
                        var guild = (oldState.VoiceChannel as SocketGuildChannel)?.Guild;
                        if (guild != null)
                        {
                            await HandleBotKicked(guild, oldState.VoiceChannel);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling voice state update: {ex.Message}");
            }
        }

        private static async Task HandleBotKicked(SocketGuild guild, SocketVoiceChannel channel)
        {
            try
            {
                Console.WriteLine($"Bot was kicked from {channel.Name} in {guild.Name}");

                var ffmpeg = Process.GetProcessesByName("ffmpeg");
                foreach (var process in ffmpeg)
                {
                    process.Kill();
                }

                await Task.Delay(1000);

                var path = $"songs/{channel.Id}";
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling bot kick: {ex.Message}");
            }
        }

        private static Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log);
            return Task.CompletedTask;
        }
    }
}