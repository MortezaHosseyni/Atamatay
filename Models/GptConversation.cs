namespace Atamatay.Models
{
    public class GptConversation
    {
        public string model { get; set; } = "gpt-4o-mini";

        public required List<GptMessage> messages { get; set; }

        public double temperature { get; set; } = 0.7;
    }

    public class GptMessage
    {
        public required string role { get; set; }
        public required string content { get; set; }
    }
}
