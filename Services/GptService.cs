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
                            $"You are the Dungeon Master for an RPG/FRP game set in the world of '{worldName}'. Players: {playerDetails}. Your task is to guide the players through the adventure. First, choose a name for each player, define their abilities, select attributes, and pick starter items based on their race. As they progress, narrate the story, provide options for the players to interact with, and roll the dice when necessary to determine the outcomes of key actions. Wait for player input, and continue the adventure based on their choices."
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
