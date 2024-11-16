using Discord.Commands;

namespace Atamatay.Services
{
    public class SoundcloudService(IMusicService music, IYoutubeService youtube)
    {
        private readonly IMusicService _music = music;
        private readonly IYoutubeService _youtube = youtube;

        public async Task AddSongAsync(SocketCommandContext context, string query)
        {
            var artistName = query.Split('/')[3];
            var songName = query.Split('/')[4];

            var song = await _youtube.Search(context, $"{songName} by {artistName}");

            _music.EnqueuePlaylist(song);
            await context.Channel.SendMessageAsync($"\ud83d\udc3a Add to playlist:\n\ud83c\udfb5 {song.Title}\n\ud83c\udfa4 {song.Author}\n\ud83d\udd54 {song.Duration}");
        }
    }
}
