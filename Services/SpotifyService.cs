using Discord.Commands;
using SpotifyAPI.Web;
using System.Text.RegularExpressions;
using Atamatay.Models;
using Microsoft.Extensions.Configuration;

namespace Atamatay.Services
{
    public class SpotifyService
    {
        private readonly IMusicService _music;
        private readonly IYoutubeService _youtube;

        private readonly SpotifyClientConfig _spotifyConfig;
        private readonly ClientCredentialsRequest _request;

        public SpotifyService(IMusicService music, IYoutubeService youtube)
        {
            _music = music;
            _youtube = youtube;

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var clientId = config["Spotify:ClientId"]!;
            var clientSecret = config["Spotify:ClientSecret"]!;

            _spotifyConfig = SpotifyClientConfig.CreateDefault();
            _request = new ClientCredentialsRequest(clientId, clientSecret);
        }

        public async Task AddSongAsync(SocketCommandContext context, string query)
        {
            var match = Regex.Match(query, @"open\.spotify\.com/track/(?<trackId>[a-zA-Z0-9]+)");
            var trackId = match.Success ? match.Groups["trackId"].Value : null;
            if (string.IsNullOrEmpty(trackId))
            {
                await context.Channel.SendMessageAsync("\u2639\ufe0f I can't find song!.");
                return;
            }

            var response = await new OAuthClient(_spotifyConfig).RequestToken(_request);
            var spotify = new SpotifyClient(_spotifyConfig.WithToken(response.AccessToken));

            var track = await spotify.Tracks.Get(trackId);

            var song = await _youtube.Search(context, $"{track.Name} by {track.Artists[0].Name}");

            _music.EnqueuePlaylist(song);

            await context.Channel.SendMessageAsync($"\ud83d\udc3a Add to playlist:\n\ud83c\udfb5 {song.Title}\n\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}");
        }

        public async Task AddPlaylistSongsAsync(SocketCommandContext context, string query, ulong channelId)
        {
            var match = Regex.Match(query, @"open\.spotify\.com/playlist/(?<playlistId>[a-zA-Z0-9]+)");
            var playlistId = match.Success ? match.Groups["playlistId"].Value : null;
            if (string.IsNullOrEmpty(playlistId))
            {
                await context.Channel.SendMessageAsync("\u2639\ufe0f I can't find song!.");
                return;
            }

            var response = await new OAuthClient(_spotifyConfig).RequestToken(_request);
            var spotify = new SpotifyClient(_spotifyConfig.WithToken(response.AccessToken));

            var spotifyPlaylist = await spotify.Playlists.Get(playlistId);

            if (spotifyPlaylist.Tracks is not { Total: > 0 }) return;

            var tasks = new List<Task>();

            await foreach (var track in spotify.Paginate(spotifyPlaylist.Tracks))
            {
                if (track.Track is not FullTrack fullTrack) continue;

                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(1);
                    _music.EnqueuePlaylist(new SongModel
                    {
                        Id = fullTrack.Id,
                        ChannelId = channelId,
                        Name = $"{fullTrack.Id}.mp3",
                        Platform = "Spotify",
                        Title = $"{fullTrack.Name} by {fullTrack.Artists[0].Name}",
                        Author = fullTrack.Artists[0].Name,
                        Duration = TimeSpan.FromMilliseconds(fullTrack.DurationMs),
                        CreatedAt = DateTime.Now
                    });
                }));
            }

            await Task.WhenAll(tasks);

            await context.Channel.SendMessageAsync($"\ud83d\udc3a Add Spotify playlist:\n\ud83c\udfb5 {spotifyPlaylist.Name}\n\ud83c\udfa4 {spotifyPlaylist.Owner?.DisplayName}\n {spotifyPlaylist.Tracks.Total} song added to playlist.");
        }
    }
}
