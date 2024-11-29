namespace Atamatay.Models.DnD
{
    public class DdDialog
    {
        public required ulong PlayerId { get; set; }
        public required ulong MessageId { get; set; }
        public required string Dialog { get; set; }
    }
}
