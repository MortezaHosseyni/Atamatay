using System.Text;
using System.Text.Json;
using Atamatay.Models;
using Atamatay.Models.DnD;
using Microsoft.Extensions.Configuration;

namespace Atamatay.Services
{
    public interface IGptService
    {
        Task<string> CreateWorld(string worldName, string playerDetails);
        Task<string> ContinueStory(List<DdTimeline> timelines);
    }
    public class GptService : IGptService
    {
        private const string Host = "https://api.openai.com/v1/chat/completions";

        public async Task<string> CreateWorld(string worldName, string playerDetails)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            var apiKey = config["GPTToken"];

            using var http = new HttpClient();

            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var requestBody = new GptConversation
            {
                messages =
                [
                    new GptMessage { role = "system", content = "You are a DM (Dungeon Master)." },
                    new GptMessage
                    {
                        role = "user",
                        content =
                            $"You are the Dungeon Master for an RPG/FRP adventure set in the world of **{worldName}**. This is a realm rich with lore, shaped by ancient conflicts, legendary heroes, and hidden secrets. Your role is to guide the players:\n{playerDetails}\nthrough an epic journey.  \r\n\r\n1. **World Building**: Begin by crafting a detailed backstory for **{{worldName}}**. Include its history, major events, key factions, and notable locations. Highlight the world’s unique features, dangers, and mysteries.  \r\n\r\n2. **Character Creation**: For each player:\r\n   - Assign a unique name and race.\r\n   - Develop a personal backstory that ties them to the world, including motivations and past events.\r\n   - Define their abilities and attributes based on their race/class.\r\n   - Provide one special ability that sets them apart.\r\n   - Identify a key weakness or flaw that could challenge them during the adventure.\r\n   - Equip them with appropriate starter items.  \r\n\r\n3. **Adventure Flow**: Narrate the story dynamically. Present engaging scenarios and challenges, allowing players to explore, strategize, and interact with the world. Offer choices at pivotal moments, and use dice rolls (Dungeon Master must roll a dice) to determine the outcomes of crucial actions.  \r\n\r\nAdapt the narrative based on player decisions and keep the experience immersive. Wait for input between scenes, then weave their choices into the unfolding tale."
                    }
                ],
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await http.PostAsync(Host, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    var contentField = jsonDoc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    return contentField;
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Details: {errorContent}");
                    return errorContent;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred:");
                Console.WriteLine(ex.Message);
                return ex.Message;
            }
        }

        public async Task<string> ContinueStory(List<DdTimeline> timelines)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            var apiKey = config["GPTToken"];

            using var http = new HttpClient();

            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var messages = timelines.Select(timeline => new GptMessage { role = timeline.Role, content = timeline.Content }).ToList();

            var requestBody = new GptConversation
            {
                messages = messages,
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await http.PostAsync(Host, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(responseContent);
                    var contentField = jsonDoc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    return contentField;
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Details: {errorContent}");
                    return errorContent;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred:");
                Console.WriteLine(ex.Message);
                return ex.Message;
            }
        }
    }
}
