namespace Atamatay.Models.DnD
{
    public class DdTimeline
    {
        public required string Role { get; set; }
        public required string Content { get; set; }

        public int Round { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
