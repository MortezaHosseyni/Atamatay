using Discord.Commands;
using Discord;

namespace Atamatay.Utilities
{
    public class Message
    {
        public static async Task SendEmbedAsync(
            SocketCommandContext context,
            string title,
            string description,
            Color? color = null,
            string footerText = null,
            string footerIconUrl = null)
        {
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(color ?? Color.Blue)
                .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(footerText))
            {
                embed.WithFooter(footer =>
                {
                    footer.Text = footerText;
                    footer.IconUrl = footerIconUrl;
                });
            }

            await context.Channel.SendMessageAsync(embed: embed.Build());
        }
    }
}
