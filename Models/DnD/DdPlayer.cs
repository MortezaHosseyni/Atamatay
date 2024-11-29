namespace Atamatay.Models.DnD
{
    public class DdPlayer
    {
        public required ulong PlayerId { get; set; }
        public required string Username { get; set; }

        public required string Race { get; set; }
        public required string Gender { get; set; }

        public bool IsAccepted { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
