namespace Atamatay.Models.DnD
{
    public class DdSession
    {
        public required ulong ChannelId { get; set; }

        public required string WorldName { get; set; }

        public required List<DdPlayer> Players { get; set; }
        public List<DdDialog>? Dialogs { get; set; }

        public int Round { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
