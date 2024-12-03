using Atamatay.Models.DnD;
using Atamatay.Utilities;
using Discord;
using Discord.Commands;

namespace Atamatay.Services
{
    public interface IDdService
    {
        Task StartGame(SocketCommandContext context, string worldName, string playerDetail, List<DdPlayer> players);
        Task SendDialog(SocketCommandContext context, string dialog);
        Task AcceptSession(SocketCommandContext context);
    }

    public class DdService(IGptService gpt) : IDdService
    {
        private readonly IGptService _gpt = gpt;

        private Dictionary<ulong, List<DdDialog>> _sessionsDialogs = new Dictionary<ulong, List<DdDialog>>();

        public async Task StartGame(SocketCommandContext context, string worldName, string playerDetail, List<DdPlayer> players)
        {
            try
            {
                var worldPath = $"dnd/{context.Channel.Id}.json";
                if (File.Exists(worldPath))
                {
                    await Message.SendEmbedAsync(context, "World Already Exists!",
                        "The story continues in this channel.", Color.Blue);
                    return;
                }

                var message = await context.Channel.SendMessageAsync($"Creating the '{worldName}'...");

                var story = await _gpt.CreateWorld(worldName, playerDetail);

                await message.DeleteAsync();

                var db = new DdDatabase(worldPath);

                db.AddSession(new DdSession
                {
                    WorldName = worldName,
                    ChannelId = context.Channel.Id,
                    Players = players,
                    Timelines =
                    [
                        new DdTimeline
                            { Role = "system", Content = "You are a DM (Dungeon Master).", Round = 1, CreatedAt = DateTime.Now },
                        new DdTimeline
                        {
                            Role = "user",
                            Content =
                                $"You are the Dungeon Master for an RPG/FRP game set in the world of '{worldName}'. Players: {playerDetail}. Your task is to guide the players through the adventure. First, choose a name for each player, define their abilities, select attributes, and pick starter items based on their race. As they progress, narrate the story, provide options for the players to interact with, and roll the dice when necessary to determine the outcomes of key actions. Wait for player input, and continue the adventure based on their choices.",
                            Round = 1,
                            CreatedAt = DateTime.Now
                        },
                        new DdTimeline
                        {
                            Role = "assistant",
                            Content = story,
                            Round = 1,
                            CreatedAt = DateTime.Now
                        }
                    ],
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

        public async Task SendDialog(SocketCommandContext context, string dialog)
        {
            try
            {
                var worldPath = $"dnd/{context.Channel.Id}.json";
                if (!File.Exists(worldPath))
                {
                    await Message.SendEmbedAsync(context, "World Not Exists!",
                        "There is no DnD session in this channel.", Color.Red);
                    return;
                }

                var db = new DdDatabase(worldPath);
                var currentSession = db.GetSession(context.Channel.Id);

                if (currentSession == null)
                {
                    await Message.SendEmbedAsync(context, "World Not Exists!",
                        "There is no DnD session in this channel.", Color.Red);
                    return;
                }

                if (currentSession.Players.Any(p => !p.IsAccepted))
                {
                    await Message.SendEmbedAsync(context, "Players Not Ready!",
                        "Every player must accept this session.", Color.Orange);
                    return;
                }

                if (!_sessionsDialogs.Any(s => s.Key == currentSession.ChannelId))
                {
                    _sessionsDialogs.Add(currentSession.ChannelId, []);
                }

                var currentSessionDialogs = _sessionsDialogs.FirstOrDefault(s => s.Key == currentSession.ChannelId);

                var currentPlayer = currentSession.Players.FirstOrDefault(p => p.PlayerId == context.User.Id);
                if (currentPlayer == null)
                {
                    await Message.SendEmbedAsync(context, "Who Are You?!",
                        "You are not in this session.", Color.DarkOrange);
                    return;
                }

                var newDialog = new DdDialog
                {
                    Dialog = dialog,
                    MessageId = context.Message.Id,
                    PlayerId = currentPlayer.PlayerId
                };

                db.AddDialogToSession(currentSession.ChannelId, newDialog);
                currentSessionDialogs.Value.Add(newDialog);

                if (currentSessionDialogs.Value.Count == currentSession.Players.Count)
                {
                    var timeLineContent = "";
                    foreach (var ddDialog in currentSessionDialogs.Value)
                    {
                        var player = currentSession.Players.FirstOrDefault(p => p.PlayerId == ddDialog.PlayerId);
                        timeLineContent +=
                            $"{player?.Username} ({player?.Race} {player?.Gender}): {ddDialog.Dialog}\n\n";
                    }

                    var newTimeline = new DdTimeline
                    {
                        Role = "user",
                        Content = timeLineContent,
                        Round = currentSession.Round,
                        CreatedAt = DateTime.Now
                    };
                    db.AddTimelineToSession(currentSession.ChannelId, newTimeline);

                    await Task.Delay(500);

                    var story = await _gpt.ContinueStory(currentSession.Timelines);

                    var newRound = currentSession.Round + 1;

                    var newStory = new DdTimeline
                    {
                        Role = "assistant",
                        Content = story,
                        Round = newRound,
                        CreatedAt = DateTime.Now
                    };
                    db.AddTimelineToSession(currentSession.ChannelId, newStory);

                    db.UpdateSessionRound(currentSession.ChannelId, newRound);

                    await Message.SendEmbedAsync(context, currentSession.WorldName, story, Color.Gold);

                    _sessionsDialogs.Remove(currentSession.ChannelId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task AcceptSession(SocketCommandContext context)
        {
            try
            {
                var worldPath = $"dnd/{context.Channel.Id}.json";
                if (!File.Exists(worldPath))
                {
                    await Message.SendEmbedAsync(context, "World Not Exists!",
                        "There is no DnD session in this channel.", Color.Red);
                    return;
                }

                var db = new DdDatabase(worldPath);
                var currentSession = db.GetSession(context.Channel.Id);

                if (currentSession == null)
                {
                    await Message.SendEmbedAsync(context, "World Not Exists!",
                        "There is no DnD session in this channel.", Color.Red);
                    return;
                }

                var currentPlayer = currentSession.Players.FirstOrDefault(p => p.PlayerId == context.User.Id);
                if (currentPlayer == null)
                {
                    await Message.SendEmbedAsync(context, "Who Are You?!",
                        "You are not in this session.", Color.DarkOrange);
                    return;
                }

                if (!currentPlayer.IsAccepted)
                {
                    db.UpdatePlayerIsAccepted(currentSession.ChannelId, currentPlayer.PlayerId, true);
                    await Message.SendEmbedAsync(context, "You In The World!",
                        $"World accepted by {currentPlayer.Username}.", Color.Green);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
