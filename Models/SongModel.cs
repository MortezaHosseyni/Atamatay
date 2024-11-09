namespace Atamatay.Models
{
    public class SongModel
    {
        public required string Title { get; set; }
        public required string Name { get; set; }
        public required ulong ChannelId { get; set; }
        public string? Author { get; set; }
        public string? Id { get; set; }
        public string? Url { get; set; }
        public TimeSpan? Duration { get; set; }
        public required string Platform { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
