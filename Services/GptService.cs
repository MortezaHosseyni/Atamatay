using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Atamatay.Services
{
    public interface IGptService
    {
        Task<string> Post(string worldName, string playerDetails);
    }
    public class GptService() : IGptService
    {
        private const string Host = "https://api.openai.com/v1/chat/completions";

        public async Task<string> Post(string worldName, string playerDetails)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            var apiKey = config["GPTToken"];

            using var http = new HttpClient();

            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "You are a DM (Dungeon Master)." },
                    new { role = "user", content = $"You are the Dungeon Master for an RPG/FRP game set in the world of '{worldName}'. Players: {playerDetails}. Your task is to guide the players through the adventure. First, choose a name for each player, define their abilities, select attributes, and pick starter items based on their race. As they progress, narrate the story, provide options for the players to interact with, and roll the dice when necessary to determine the outcomes of key actions. Wait for player input, and continue the adventure based on their choices." }
                },
                temperature = 0.7
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
