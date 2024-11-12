using System.Collections.Concurrent;
using Discord.Audio;

namespace Atamatay.Models
{
    public class PlaylistModel
    {
        public ulong ChannelId { get; set; }
        public IAudioClient? AudioClient { get; set; }
        public CancellationTokenSource? CurrentSong { get; set; }
        public ConcurrentQueue<SongModel>? Songs { get; set; }

        public bool IsPlaying { get; set; }
        public bool SkipRequested { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
